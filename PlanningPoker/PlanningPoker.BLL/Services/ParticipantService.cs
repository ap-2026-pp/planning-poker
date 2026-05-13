using PlanningPoker.BLL.Constants;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.DTOs.Participant;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Керує участю в грі: приєднанням, виходом, оновленням display name,
/// передачею ролі Master та видаленням учасників.
/// </summary>
public class ParticipantService(
    IParticipantRepository participantRepository,
    IGameRepository gameRepository,
    ICurrentUserContext currentUserContext,
    IGuestSessionService guestSessionService,
    IGameAccessService gameAccessService) : IParticipantService
{

    /// <summary>
    /// Повертає список активних учасників гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Колекцію учасників гри у вигляді <see cref="GameParticipantDto"/>.</returns>
    /// <exception cref="ForbiddenException">Виникає, якщо поточний користувач не є учасником гри.</exception>
    public async Task<IEnumerable<GameParticipantDto>> GetGameParticipantsAsync(Guid gameId)
    {
        await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.ViewAction,
            AccessControlConstants.GameResource);
        var participants = await participantRepository.GetGameParticipantsAsync(gameId);
        return (participants ?? []).Select(ParticipantMapper.ToGameParticipantDto);
    }

    /// <summary>
    /// Додає поточного користувача до гри за інвайт-кодом або відновлює його попередню участь.
    /// Якщо користувач уже є активним учасником гри, оновлює його display name та статус підключення.
    /// Якщо користувач раніше був видалений з гри, відновлює його участь.
    /// Якщо гра неактивна, реактивувати її може лише master, який уже був учасником цієї гри.
    /// </summary>
    /// <param name="inviteCode">Код запрошення до гри.</param>
    /// <param name="joinGameRequestDto">Дані для приєднання до гри.</param>
    /// <returns>Актуальний стан гри та дані поточного guest session у вигляді <see cref="JoinGameResponseDto"/>.</returns>
    /// <exception cref="NotFoundException">Виникає, якщо гру з вказаним інвайт-кодом не знайдено.</exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач намагається приєднатися до неактивної гри без права повторно її активувати.
    /// </exception>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо обране display name уже використовується іншим активним учасником цієї гри.
    /// </exception>
    public async Task<JoinGameResponseDto> JoinGameByInviteCodeAsync(
    string inviteCode,
    JoinGameRequestDto joinGameRequestDto)
    {
        var currentUser = await currentUserContext.GetUserOrDefaultAsync();

        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();

        var game = await GetGameByInviteCodeOrThrowAsync(inviteCode);

        var activeParticipant = await participantRepository.GetCurrentParticipantAsync(
            game.Id,
            currentIdentity.UserId,
            currentIdentity.GuestParticipantId);

        var existingParticipant = activeParticipant ??
                                await participantRepository.GetCurrentParticipantIncludingRemovedAsync(
                                    game.Id,
                                    currentIdentity.UserId,
                                    currentIdentity.GuestParticipantId);

        if (activeParticipant == null)
        {
            var currentCount = game.Participants.Count(p => p.RemovedAt == null);

            if (currentCount >= 20)
            {
                throw new ForbiddenException(
                    "join",
                    "Game is full now, count mustn't over 20 participants!");
            }
        }

        if (joinGameRequestDto.ParticipantRole == ParticipantRole.Master)
        {
            throw new InvalidOperationException(
                "Master role cannot be selected when joining a game.");
        }

        if (!game.IsActive)
        {
            throw new ForbiddenException(
                "join",
                "Game is inactive.");
        }

        EnsureGameCanBeJoinedAsync(game, existingParticipant);

        var requestedRole = ResolveRole(
            joinGameRequestDto.ParticipantRole,
            existingParticipant);

        var requestedDisplayName = string.IsNullOrWhiteSpace(joinGameRequestDto.DisplayName)
            ? currentUser?.DisplayName ?? "Guest"
            : joinGameRequestDto.DisplayName.Trim();

        GameParticipant participant;

        if (activeParticipant is not null)
        {
            participant = await RejoinActiveParticipantAsync(
                activeParticipant,
                game.Id,
                requestedDisplayName,
                currentUser,
                requestedRole);
        }
        else if (existingParticipant is not null)
        {
            participant = await RestoreParticipantAsync(
                existingParticipant,
                game.Id,
                requestedDisplayName,
                currentUser,
                requestedRole);
        }
        else
        {
            participant = await CreateNewParticipantAsync(
                game.Id,
                requestedDisplayName,
                currentUser,
                requestedRole);
        }

        await participantRepository.SaveChangesAsync();

        var guestAccessToken = await CreateGuestTokenIfNeededAsync(
            currentUser,
            participant.Id);

        var refreshedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;

        return new JoinGameResponseDto
        {
            Game = GameMapper.ToGameDto(refreshedGame),
            CurrentParticipantId = participant.Id,
            GuestAccessToken = guestAccessToken
        };
    }

    /// <summary>
    /// Видаляє поточного користувача зі списку активних учасників гри.
    /// Якщо гру залишає master, що не є власником гри, права master повертаються до власника,
    /// а поточний користувач виходить з гри.
    /// Якщо гру залишає master-власник гри, сесія завершується для всіх активних учасників,
    /// а сама гра позначається як неактивна.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру або поточного учасника в межах цієї гри не знайдено.
    /// </exception>
    public async Task LeaveGameAsync(Guid gameId)
    {
        var game = await GetGameOrThrowAsync(gameId);
        var participant = await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.LeaveAction,
            AccessControlConstants.GameResource);

        if (participant.UserId == game.CreatedBy)
        {
            await CloseGameForAllParticipantsAsync(game);
            return;
        }

        if (participant.Role != ParticipantRole.Master)
        {
            await RemoveParticipantAsync(participant);
            return;
        }

        var gameOwner =
            await participantRepository.GetCurrentParticipantIncludingRemovedAsync(gameId, game.CreatedBy, null);

        if (gameOwner is not null && gameOwner.Id != participant.Id)
        {
            ReturnMasterRoleToOwner(gameOwner, participant);
            await RemoveParticipantAsync(participant);
            return;
        }

        await CloseGameForAllParticipantsAsync(game);
    }

    /// <summary>
    /// Видаляє активного учасника з гри. Операція доступна лише Master у межах цієї гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="participantId">Ідентифікатор учасника, якого потрібно видалити.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="NotFoundException">Виникає, якщо гру або учасника не знайдено.</exception>
    /// <exception cref="ForbiddenException">Виникає, якщо поточний користувач не є master цієї гри.</exception>
    public async Task DeleteGameParticipantAsync(Guid gameId, Guid participantId)
    {
        await GetGameOrThrowAsync(gameId);

        var currentMaster =
            await gameAccessService.GetRequiredMasterAsync(
                gameId,
                AccessControlConstants.DeleteAction,
                AccessControlConstants.GameParticipantResource);

        if (participantId == currentMaster.Id)
        {
            throw new InvalidOperationException("You cannot delete yourself.");
        }

        var participant = await participantRepository.GetActiveByIdAsync(participantId);
        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }

        if (participant.Role == ParticipantRole.Master)
        {
            throw new InvalidOperationException("Master cannot be removed. Use LeaveGame instead.");
        }

        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
        await RemoveParticipantAsync(participant);
    }

    /// <summary>
    /// Оновлює display name поточного учасника в межах конкретної гри.
    /// Якщо нове display name не задане, використовується display name користувача з профілю.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="displayName">
    /// Нове display name учасника.
    /// Якщо значення порожнє або складається лише з пробілів, використовується display name користувача з профілю.
    /// </param>
    /// <returns>Оновлені дані учасника у вигляді <see cref="GameParticipantDto"/>.</returns>
    /// <exception cref="NotFoundException">Виникає, якщо гру або поточного учасника в межах цієї гри не знайдено.</exception>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо нове display name уже використовується іншим активним учасником цієї гри.
    /// </exception>
    public async Task<GameParticipantDto> UpdateDisplayNameAsync(Guid gameId, string? displayName)
    {
        var currentUser = await currentUserContext.GetUserOrDefaultAsync();
        await GetGameOrThrowAsync(gameId);

        var currentParticipant =
            await gameAccessService.GetRequiredParticipantAsync(
                gameId,
                AccessControlConstants.UpdateDisplayNameAction,
                AccessControlConstants.GameResource);

        var resolvedDisplayName = ResolveUpdatedDisplayName(
            displayName,
            currentUser?.DisplayName,
            currentParticipant.DisplayName);

        if (string.Equals(currentParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
        {
            return ParticipantMapper.ToGameParticipantDto(currentParticipant);
        }

        await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName!, gameId);
        currentParticipant.DisplayName = resolvedDisplayName;
        await participantRepository.SaveChangesAsync();

        return ParticipantMapper.ToGameParticipantDto(currentParticipant);
    }

    /// <summary>
    /// Передає роль Master іншому активному учаснику в межах конкретної гри.
    /// Операцію може виконати лише поточний Master цієї гри.
    /// Після успішної передачі поточний Master отримує роль Player, а вибраний учасник стає новим Master.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="participantId">Ідентифікатор учасника, якому потрібно передати роль Master.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="NotFoundException">Виникає, якщо гру або вказаного учасника не знайдено.</exception>
    /// <exception cref="ForbiddenException">Виникає, якщо поточний користувач не є Master цієї гри.</exception>
    /// <exception cref="InvalidOperationException">
    /// Виникає, якщо користувач намагається передати роль Master самому собі.
    /// </exception>
    public async Task TransferMasterAsync(Guid gameId, Guid participantId)
    {
        await GetGameOrThrowAsync(gameId);

        var currentMaster =
            await gameAccessService.GetRequiredMasterAsync(
                gameId,
                AccessControlConstants.TransferMasterAction,
                AccessControlConstants.GameParticipantResource);

        if (currentMaster.Id == participantId)
        {
            throw new InvalidOperationException("You cannot transfer master role to yourself.");
        }

        var newMaster = await participantRepository.GetActiveByIdAsync(participantId);

        if (newMaster == null || newMaster.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }

        if (newMaster.Role == ParticipantRole.Spectator)
        {
            throw new InvalidOperationException("A spectator cannot be a master. Please select a player or ask them to switch mode.");
        }

        currentMaster.Role = ParticipantRole.Player;
        newMaster.Role = ParticipantRole.Master;

        newMaster.CanRevealCards = true;
        newMaster.CanManageIssues = true;
        currentMaster.CanRevealCards = false;
        currentMaster.CanManageIssues = false;

        participantRepository.Update(currentMaster);
        participantRepository.Update(newMaster);

        await participantRepository.SaveChangesAsync();
    }
    /// <summary>
    /// Перемикає режим учасника між Player та Spectator.
    /// Master не може стати Spectator, оскільки він повинен керувати грою.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="isSpectator">
    /// True — увімкнути режим глядача.
    /// False — повернути учасника до ролі Player.
    /// </param>
    public async Task SetSpectatorModeAsync(Guid gameId, bool isSpectator)
    {
        await GetGameOrThrowAsync(gameId);

        var currentParticipant =
            await gameAccessService.GetRequiredParticipantAsync(
                gameId,
                AccessControlConstants.ChangeRoleAction,
                AccessControlConstants.GameParticipantResource);

        if (isSpectator)
        {
            if (currentParticipant.Role == ParticipantRole.Master)
            {
                throw new InvalidOperationException(
                    "You must transfer master rights before switching to another role.");
            }

            if (currentParticipant.Role != ParticipantRole.Spectator)
            {
                currentParticipant.Role = ParticipantRole.Spectator;
                participantRepository.Update(currentParticipant);
                await participantRepository.SaveChangesAsync();
            }

            return;
        }

        if (currentParticipant.Role == ParticipantRole.Spectator)
        {
            currentParticipant.Role = ParticipantRole.Player;
            participantRepository.Update(currentParticipant);
            await participantRepository.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Повторно підключає активного учасника до гри: оновлює його display name, статус підключення, час входу
    /// та за потреби змінює роль.
    /// </summary>
    /// <param name="participant">Активний учасник, який уже присутній у грі.</param>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="requestedDisplayName">Display name, переданий у запиті на приєднання.</param>
    /// <param name="currentUser">Поточний авторизований користувач, якщо він є.</param>
    /// <param name="requestedRole">Роль, яку запитано під час входу.</param>
    /// <returns>Оновлену сутність <see cref="GameParticipant"/>.</returns>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо новий display name уже використовується іншим учасником гри.
    /// </exception>
    private async Task<GameParticipant> RejoinActiveParticipantAsync(
        GameParticipant participant,
        Guid gameId,
        string? requestedDisplayName,
        User? currentUser,
        ParticipantRole requestedRole)
    {
        var resolvedDisplayName = ResolveDisplayName(
            requestedDisplayName,
            currentUser?.DisplayName,
            participant,
            participant.Id);

        if (!string.Equals(participant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
        {
            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, gameId);
            participant.DisplayName = resolvedDisplayName;
        }

        participant.IsConnected = true;
        participant.JoinedAt = DateTime.UtcNow;
        UpdateParticipantRoleIfAllowed(participant, requestedRole);

        return participant;
    }

    /// <summary>
    /// Відновлює раніше видаленого учасника гри: повертає його до активного стану, оновлює display name,
    /// час входу, статус підключення та за потреби роль.
    /// </summary>
    /// <param name="participant">Учасник, який раніше був видалений з гри.</param>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="requestedDisplayName">Display name, переданий у запиті на приєднання.</param>
    /// <param name="currentUser">Поточний авторизований користувач, якщо він є.</param>
    /// <param name="requestedRole">Роль, яку запитано під час входу.</param>
    /// <returns>Оновлену сутність <see cref="GameParticipant"/>.</returns>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо новий display name уже використовується іншим учасником гри.
    /// </exception>
    private async Task<GameParticipant> RestoreParticipantAsync(
        GameParticipant participant,
        Guid gameId,
        string? requestedDisplayName,
        User? currentUser,
        ParticipantRole requestedRole)
    {
        var resolvedDisplayName = ResolveDisplayName(
            requestedDisplayName,
            currentUser?.DisplayName,
            participant,
            participant.Id);

        await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, gameId);

        if (!string.Equals(participant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
        {
            participant.DisplayName = resolvedDisplayName;
        }

        participant.IsConnected = true;
        participant.JoinedAt = DateTime.UtcNow;
        participant.RemovedAt = null;

        UpdateParticipantRoleIfAllowed(participant, requestedRole);
        participantRepository.Update(participant);

        return participant;
    }

    /// <summary>
    /// Створює нового учасника гри з обраним display name та роллю.
    /// Для гостя за потреби автоматично генерується fallback display name.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="requestedDisplayName">Display name, переданий у запиті на приєднання.</param>
    /// <param name="currentUser">Поточний авторизований користувач, якщо він є.</param>
    /// <param name="requestedRole">Роль, яку потрібно призначити новому учаснику.</param>
    /// <returns>Новостворену сутність <see cref="GameParticipant"/>.</returns>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо обране display name уже використовується іншим учасником гри.
    /// </exception>
    private async Task<GameParticipant> CreateNewParticipantAsync(
        Guid gameId,
        string? requestedDisplayName,
        User? currentUser,
        ParticipantRole requestedRole)
    {
        var participantId = Guid.NewGuid();
        var resolvedDisplayName = ResolveDisplayName(
            requestedDisplayName,
            currentUser?.DisplayName,
            null,
            participantId);

        await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, gameId);

        var participant = new GameParticipant
        {
            Id = participantId,
            GameId = gameId,
            UserId = currentUser?.Id,
            DisplayName = resolvedDisplayName,
            Role = requestedRole,
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        await participantRepository.AddAsync(participant);
        return participant;
    }

    /// <summary>
    /// Створює guest access token для неавторизованого учасника та зберігає відповідну guest session.
    /// Для авторизованого користувача токен не створюється.
    /// </summary>
    /// <param name="currentUser">Поточний авторизований користувач, якщо він є.</param>
    /// <param name="participantId">Ідентифікатор учасника, для якого створюється guest session.</param>
    /// <returns>Guest access token або <see langword="null"/>, якщо користувач авторизований.</returns>
    private async Task<string?> CreateGuestTokenIfNeededAsync(User? currentUser, Guid participantId)
    {
        if (currentUser is not null)
        {
            return null;
        }

        await guestSessionService.RevokeGuestSessionsAsync(participantId);

        var guestAccessToken = guestSessionService.GenerateGuestAccessToken(participantId);
        await guestSessionService.CreateGuestSession(participantId, guestAccessToken);

        return guestAccessToken;
    }

    /// <summary>
    /// Видаляє учасника з гри та зберігає зміни.
    /// </summary>
    /// <param name="participant">Учасник, якого потрібно видалити з гри.</param>
    private async Task RemoveParticipantAsync(GameParticipant participant)
    {
        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Завершує гру для всіх учасників, позначає її як неактивну та зберігає зміни.
    /// </summary>
    /// <param name="game">Гра, яку потрібно завершити.</param>
    private async Task CloseGameForAllParticipantsAsync(Game game)
    {
        foreach (var currentParticipant in game.Participants)
        {
            participantRepository.RemoveGameParticipant(currentParticipant);
        }

        game.IsActive = false;
        gameRepository.Update(game);
        await participantRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Повертає роль Master власнику гри, а поточному master-учаснику призначає роль Player.
    /// </summary>
    /// <param name="gameOwner">Учасник-власник гри, якому повертається роль Master.</param>
    /// <param name="currentParticipant">Поточний учасник з роллю Master, який передає права.</param>
    private void ReturnMasterRoleToOwner(GameParticipant gameOwner, GameParticipant currentParticipant)
    {
        if (gameOwner.Role != ParticipantRole.Master)
        {
            gameOwner.Role = ParticipantRole.Master;
            participantRepository.Update(gameOwner);
        }

        currentParticipant.Role = ParticipantRole.Player;
        participantRepository.Update(currentParticipant);
    }

    /// <summary>
    /// Повертає гру за ідентифікатором або кидає виняток, якщо гру не знайдено.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Сутність гри.</returns>
    /// <exception cref="NotFoundException">Виникає, якщо гру не знайдено.</exception>
    private async Task<Game> GetGameOrThrowAsync(Guid gameId)
    {
        return await gameRepository.GetByIdAsync(gameId)
               ?? throw new NotFoundException(nameof(Game), gameId);
    }

    /// <summary>
    /// Повертає гру за інвайт-кодом або кидає виняток, якщо гру не знайдено.
    /// </summary>
    /// <param name="inviteCode">Інвайт-код гри.</param>
    /// <returns>Сутність гри.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру з вказаним інвайт-кодом не знайдено.
    /// </exception>
    private async Task<Game> GetGameByInviteCodeOrThrowAsync(string inviteCode)
    {
        return await gameRepository.GetByInviteCodeAsync(inviteCode)
               ?? throw new NotFoundException(nameof(Game), nameof(Game.InviteCode), inviteCode);
    }

    /// <summary>
    /// Перевіряє, чи може користувач приєднатися до гри.
    /// Якщо гра неактивна, повторно активувати її дозволено лише учаснику з роллю Master,
    /// який уже був пов’язаний із цією грою.
    /// </summary>
    /// <param name="game">Гра, до якої виконується приєднання.</param>
    /// <param name="existingParticipant">Існуючий учасник гри, якщо він уже був пов’язаний із нею.</param>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач не має права повторно активувати неактивну гру.
    /// </exception>
    private void EnsureGameCanBeJoinedAsync(Game game, GameParticipant? existingParticipant)
    {
        if (game.IsActive)
        {
            return;
        }

        if (existingParticipant?.Role != ParticipantRole.Master)
        {
            throw new ForbiddenException(
                AccessControlConstants.JoinAction,
                AccessControlConstants.GameResource);
        }

        game.IsActive = true;
        gameRepository.Update(game);
    }

    /// <summary>
    /// Перевіряє, чи вказане display name вільне в межах конкретної гри.
    /// </summary>
    /// <param name="displayName">Display name, яке потрібно перевірити.</param>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо таке display name уже використовується в цій грі.
    /// </exception>
    private async Task EnsureDisplayNameIsAvailableAsync(string displayName, Guid gameId)
    {
        var exists = await participantRepository.ExistsByDisplayNameAsync(displayName, gameId);
        if (exists)
        {
            throw new ResourceAlreadyExistsException(nameof(GameParticipant), displayName);
        }
    }

    /// <summary>
    /// Оновлює роль учасника, якщо це дозволено.
    /// Роль Master не змінюється автоматично під час повторного входу або відновлення участі.
    /// </summary>
    /// <param name="participant">Учасник, роль якого потрібно оновити.</param>
    /// <param name="requestedRole">Роль, запитана під час приєднання до гри.</param>
    private static void UpdateParticipantRoleIfAllowed(GameParticipant participant, ParticipantRole requestedRole)
    {
        if (participant.Role != ParticipantRole.Master && participant.Role != requestedRole)
        {
            participant.Role = requestedRole;
        }
    }

    /// <summary>
    /// Визначає display name учасника під час входу в гру.
    /// Пріоритет такий:
    /// 1. display name із запиту;
    /// 2. поточний display name існуючого учасника;
    /// 3. display name авторизованого користувача;
    /// 4. автоматично згенерований псевдонім гостя.
    /// </summary>
    /// <param name="requestedDisplayName">Display name, переданий у запиті.</param>
    /// <param name="defaultDisplayName">Display name авторизованого користувача, якщо він є.</param>
    /// <param name="existingParticipant">Існуючий учасник гри, якщо він уже є.</param>
    /// <param name="fallbackParticipantId">Ідентифікатор учасника для побудови guest display name.</param>
    /// <returns>Фінальне display name, яке слід використати.</returns>
    private static string ResolveDisplayName(
        string? requestedDisplayName,
        string? defaultDisplayName,
        GameParticipant? existingParticipant,
        Guid? fallbackParticipantId = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedDisplayName))
        {
            return requestedDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(existingParticipant?.DisplayName))
        {
            return existingParticipant.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(defaultDisplayName))
        {
            return defaultDisplayName.Trim();
        }

        return BuildGuestDisplayName(fallbackParticipantId ?? Guid.NewGuid());
    }

    /// <summary>
    /// Визначає роль учасника під час входу в гру.
    /// Якщо нова роль не передана, зберігає попередню або використовує Player за замовчуванням.
    /// </summary>
    /// <param name="requestedRole">Роль, запитана під час входу.</param>
    /// <param name="existingParticipant">Існуючий учасник, якщо він уже був у грі.</param>
    /// <returns>Роль, яку слід призначити учаснику.</returns>
    private static ParticipantRole ResolveRole(ParticipantRole? requestedRole, GameParticipant? existingParticipant)
    {
        if (requestedRole.HasValue)
        {
            return requestedRole.Value;
        }

        return existingParticipant?.Role ?? ParticipantRole.Player;
    }

    /// <summary>
    /// Визначає новий display name для поточного учасника під час оновлення профілю в грі.
    /// Пріоритет:
    /// 1. display name із запиту;
    /// 2. display name авторизованого користувача;
    /// 3. поточний display name учасника.
    /// </summary>
    /// <param name="requestedDisplayName">Display name, переданий у запиті.</param>
    /// <param name="authenticatedUserDisplayName">Display name авторизованого користувача, якщо він є.</param>
    /// <param name="currentParticipantDisplayName">Поточний display name учасника в грі.</param>
    /// <returns>Фінальне display name, яке слід зберегти.</returns>
    private static string? ResolveUpdatedDisplayName(
        string? requestedDisplayName,
        string? authenticatedUserDisplayName,
        string? currentParticipantDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(requestedDisplayName))
        {
            return requestedDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(authenticatedUserDisplayName))
        {
            return authenticatedUserDisplayName.Trim();
        }

        return currentParticipantDisplayName;
    }

    /// <summary>
    /// Формує display name для guest participant, якщо користувач не передав власне ім'я.
    /// </summary>
    /// <param name="participantId">Ідентифікатор учасника, що використовується як основа псевдоніма.</param>
    /// <returns>Автоматично згенерований display name гостя.</returns>
    private static string BuildGuestDisplayName(Guid participantId)
    {
        return $"Guest-{participantId.ToString("N")[..6]}";
    }
}