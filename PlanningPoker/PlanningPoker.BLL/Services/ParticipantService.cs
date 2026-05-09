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
    IGameAccessService gameAccessService,
    IGameRoomNotifier gameRoomNotifier) : IParticipantService
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
    /// Додає поточного користувача до активної гри за інвайт-кодом або відновлює його попередню участь.
    /// Для неактивних ігор повторний вхід виконується через окремий reconnect flow.
    /// </summary>
    /// <param name="inviteCode">Код запрошення до гри.</param>
    /// <param name="joinGameRequestDto">Дані для приєднання до гри.</param>
    /// <returns>Актуальний стан гри та дані поточного guest session у вигляді <see cref="JoinGameResponseDto"/>.</returns>
    /// <exception cref="NotFoundException">Виникає, якщо гру з вказаним інвайт-кодом не знайдено.</exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач намагається приєднатися до неактивної гри.
    /// </exception>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо обране display name уже використовується іншим активним учасником цієї гри.
    /// </exception>
    public async Task<JoinGameResponseDto> JoinGameByInviteCodeAsync(string inviteCode,
        JoinGameRequestDto joinGameRequestDto)
    {
        if (joinGameRequestDto.ParticipantRole == ParticipantRole.Master)
        {
            throw new InvalidOperationException("Master role cannot be selected when joining a game.");
        }

        var currentUser = await currentUserContext.GetUserOrDefaultAsync();
        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();
        var game = await GetGameByInviteCodeOrThrowAsync(inviteCode);

        var (activeParticipant, existingParticipant) = await GetParticipantStateAsync(
            game.Id,
            currentIdentity.UserId,
            currentIdentity.GuestParticipantId);
        
        EnsureGameCanBeJoinedByInviteCode(game);

        var requestedRole = ResolveRole(joinGameRequestDto.ParticipantRole, existingParticipant);
        var participant = await UpsertParticipantAsync(
            game.Id,
            activeParticipant,
            existingParticipant,
            joinGameRequestDto.DisplayName,
            currentUser,
            requestedRole,
            allowCreateNewParticipant: true);

        return await BuildJoinGameResponseAsync(game, participant, currentUser);
    }

    public async Task<JoinGameResponseDto> ReconnectToGameAsync(Guid gameId)
    {
        var currentUser = await currentUserContext.GetRequiredUserAsync();
        var game = await GetGameOrThrowAsync(gameId);
        var wasInactive = !game.IsActive;

        var (activeParticipant, existingParticipant) =
            await GetParticipantStateAsync(game.Id, currentUser.Id, null);

        EnsureGameCanBeReconnected(game, existingParticipant, currentUser.Id);

        if (existingParticipant is null)
        {
            throw new ForbiddenException(
                AccessControlConstants.JoinAction,
                AccessControlConstants.GameResource);
        }

        var participant = await UpsertParticipantAsync(
            game.Id,
            activeParticipant,
            existingParticipant,
            requestedDisplayName: null,
            currentUser,
            ResolveReconnectRole(game, existingParticipant, currentUser.Id, wasInactive),
            allowCreateNewParticipant: false);

        return await BuildJoinGameResponseAsync(game, participant, currentUser);
    }

    /// <summary>
    /// Видаляє поточного користувача зі списку активних учасників гри.
    /// Якщо виходить власник гри, вона закривається для всіх.
    /// Якщо це не master — просто виходить.
    /// Якщо виходить master не власник гри, права повертаються активному власнику гри,
    /// інакше передаємо їх іншому активному учаснику.
    /// Якщо активних учасників більше немає, права повертаються власнику, навіть якщо він відсутній,
    /// а гра закривається для всіх і позначається неактивною.
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

        if (participant.Role == ParticipantRole.Master)
        {
            await LeaveGameAsMasterAsync(game, participant);
            return;
        }

        await RemoveParticipantAsync(participant);
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

        var currentUserParticipant =
            await gameAccessService.GetRequiredMasterAsync(
                gameId,
                AccessControlConstants.DeleteAction,
                AccessControlConstants.GameParticipantResource);

        if (currentUserParticipant.Id == participantId)
        {
            throw new InvalidOperationException("You cannot delete yourself.");
        }

        var participant = await gameAccessService.GetRequiredActiveParticipantAsync(gameId, participantId);
        await RemoveParticipantAsync(participant, true);
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

        await gameRoomNotifier.NotifyMasterChangedAsync(
            gameId,
            ParticipantMapper.ToGameParticipantDto(currentParticipant));

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

        var newMaster =
            await gameAccessService.GetRequiredNonSpectatorParticipantAsync(
                gameId,
                participantId,
                AccessControlConstants.TransferMasterAction,
                AccessControlConstants.SpectatorResource);

        currentMaster.Role = ParticipantRole.Player;
        newMaster.Role = ParticipantRole.Master;

        participantRepository.Update(currentMaster);
        participantRepository.Update(newMaster);

        await participantRepository.SaveChangesAsync();
        await gameRoomNotifier.NotifyMasterChangedAsync(gameId, ParticipantMapper.ToGameParticipantDto(currentMaster));
        await gameRoomNotifier.NotifyMasterChangedAsync(gameId, ParticipantMapper.ToGameParticipantDto(newMaster));
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
                await gameRoomNotifier.NotifyMasterChangedAsync(
                    gameId,
                    ParticipantMapper.ToGameParticipantDto(currentParticipant));
            }

            return;
        }

        if (currentParticipant.Role == ParticipantRole.Spectator)
        {
            currentParticipant.Role = ParticipantRole.Player;
            participantRepository.Update(currentParticipant);
            await participantRepository.SaveChangesAsync();
            await gameRoomNotifier.NotifyMasterChangedAsync(
                gameId,
                ParticipantMapper.ToGameParticipantDto(currentParticipant));
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

    private async Task<(GameParticipant? ActiveParticipant, GameParticipant? ExistingParticipant)>
        GetParticipantStateAsync(
            Guid gameId,
            Guid? userId,
            Guid? guestParticipantId)
    {
        var activeParticipant =
            await participantRepository.GetCurrentParticipantAsync(gameId, userId, guestParticipantId);
        var existingParticipant = activeParticipant ??
                                  await participantRepository.GetCurrentParticipantIncludingRemovedAsync(
                                      gameId,
                                      userId,
                                      guestParticipantId);

        return (activeParticipant, existingParticipant);
    }

    private async Task<GameParticipant> UpsertParticipantAsync(
        Guid gameId,
        GameParticipant? activeParticipant,
        GameParticipant? existingParticipant,
        string? requestedDisplayName,
        User? currentUser,
        ParticipantRole requestedRole,
        bool allowCreateNewParticipant)
    {
        GameParticipant participant;

        if (activeParticipant is not null)
        {
            participant = await RejoinActiveParticipantAsync(
                activeParticipant,
                gameId,
                requestedDisplayName,
                currentUser,
                requestedRole);
        }
        else if (existingParticipant is not null)
        {
            participant = await RestoreParticipantAsync(
                existingParticipant,
                gameId,
                requestedDisplayName,
                currentUser,
                requestedRole);
        }
        else if (allowCreateNewParticipant)
        {
            participant = await CreateNewParticipantAsync(
                gameId,
                requestedDisplayName,
                currentUser,
                requestedRole);
        }
        else
        {
            throw new ForbiddenException(
                AccessControlConstants.JoinAction,
                AccessControlConstants.GameResource);
        }

        return participant;
    }

    private async Task<JoinGameResponseDto> BuildJoinGameResponseAsync(
        Game game,
        GameParticipant participant,
        User? currentUser)
    {
        await participantRepository.SaveChangesAsync();

        await gameRoomNotifier.NotifyParticipantJoinedAsync(
            game.Id,
            ParticipantMapper.ToGameParticipantDto(participant));

        var guestAccessToken = await CreateGuestTokenIfNeededAsync(currentUser, participant.Id);
        var refreshedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;

        return new JoinGameResponseDto
        {
            Game = GameMapper.ToGameDto(refreshedGame),
            CurrentParticipantId = participant.Id,
            GuestAccessToken = guestAccessToken
        };
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
    /// <param name="isKicked">Прапорець, що показує чи учасник вийшов, чи бу видалений</param>
    private async Task RemoveParticipantAsync(GameParticipant participant, bool isKicked = false)
    {
        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();

        if (isKicked)
        {
            await gameRoomNotifier.NotifyParticipantKickedAsync(participant.GameId, participant.Id);
        }
        else
        {
            await gameRoomNotifier.NotifyParticipantLeftAsync(participant.GameId, participant.Id);
        }
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

        foreach (var participantId in game.Participants.Select(p => p.Id))
        {
            await gameRoomNotifier.NotifyParticipantLeftAsync(game.Id, participantId);
        }
    }

    private async Task LeaveGameAsMasterAsync(Game game, GameParticipant currentParticipant)
    {
        var nextMaster = await GetNextMasterCandidateOrDefaultAsync(game, currentParticipant.Id);
        if (nextMaster is not null)
        {
            await TransferMasterBeforeLeaveAsync(nextMaster, currentParticipant);
            await RemoveParticipantAsync(currentParticipant);
            return;
        }

        await RestoreInactiveOwnerMasterRoleIfNeededAsync(game, currentParticipant);
        await CloseGameForAllParticipantsAsync(game);
    }

    private async Task<GameParticipant?> GetNextMasterCandidateOrDefaultAsync(Game game, Guid excludedParticipantId)
    {
        var activeNonSpectatorParticipants =
            await gameAccessService.GetOtherActiveNonSpectatorParticipantsAsync(game, excludedParticipantId);

        return activeNonSpectatorParticipants
            .OrderByDescending(participant => participant.UserId == game.CreatedBy)
            .ThenBy(participant => participant.JoinedAt)
            .FirstOrDefault();
    }

    private async Task RestoreInactiveOwnerMasterRoleIfNeededAsync(Game game, GameParticipant currentParticipant)
    {
        var ownerIncludingRemoved =
            await participantRepository.GetCurrentParticipantIncludingRemovedAsync(game.Id, game.CreatedBy, null);

        if (ownerIncludingRemoved is null ||
            ownerIncludingRemoved.Id == currentParticipant.Id ||
            ownerIncludingRemoved.RemovedAt is null ||
            ownerIncludingRemoved.Role == ParticipantRole.Spectator)
        {
            return;
        }

        ownerIncludingRemoved.Role = ParticipantRole.Master;
        currentParticipant.Role = ParticipantRole.Player;
        participantRepository.Update(ownerIncludingRemoved);
        participantRepository.Update(currentParticipant);
        await participantRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Передає роль Master іншому активному не-spectator учаснику
    /// перед виходом поточного master з гри.
    /// </summary>
    /// <param name="newMaster">Учасник, якому передається роль Master.</param>
    /// <param name="currentParticipant">Поточний master, який залишає гру.</param>
    private async Task TransferMasterBeforeLeaveAsync(
        GameParticipant newMaster,
        GameParticipant currentParticipant)
    {
        if (newMaster.Role != ParticipantRole.Master)
        {
            newMaster.Role = ParticipantRole.Master;
            participantRepository.Update(newMaster);
        }

        currentParticipant.Role = ParticipantRole.Player;
        participantRepository.Update(currentParticipant);

        await participantRepository.SaveChangesAsync();

        await gameRoomNotifier.NotifyMasterChangedAsync(
            currentParticipant.GameId,
            ParticipantMapper.ToGameParticipantDto(newMaster));
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
    /// Перевіряє, що приєднання за invite code дозволене лише для активної гри.
    /// </summary>
    /// <param name="game">Гра, до якої виконується приєднання.</param>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо гра неактивна.
    /// </exception>
    private static void EnsureGameCanBeJoinedByInviteCode(Game game)
    {
        if (!game.IsActive)
        {
            throw new ForbiddenException(
                AccessControlConstants.JoinAction,
                AccessControlConstants.GameResource);
        }
    }

    private void EnsureGameCanBeReconnected(
        Game game,
        GameParticipant? existingParticipant,
        Guid currentUserId)
    {
        if (game.IsActive)
        {
            if (existingParticipant is not null)
            {
                return;
            }

            throw new ForbiddenException(
                AccessControlConstants.JoinAction,
                AccessControlConstants.GameResource);
        }

        if (game.CreatedBy != currentUserId || existingParticipant is null)
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

        return !string.IsNullOrWhiteSpace(defaultDisplayName)
            ? defaultDisplayName.Trim()
            : BuildGuestDisplayName(fallbackParticipantId ?? Guid.NewGuid());
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

    private static ParticipantRole ResolveReconnectRole(
        Game game,
        GameParticipant existingParticipant,
        Guid currentUserId,
        bool wasInactive)
    {
        if (wasInactive && game.CreatedBy == currentUserId)
        {
            return ParticipantRole.Master;
        }

        return existingParticipant.Role;
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

        return !string.IsNullOrWhiteSpace(authenticatedUserDisplayName)
            ? authenticatedUserDisplayName.Trim()
            : currentParticipantDisplayName;
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
