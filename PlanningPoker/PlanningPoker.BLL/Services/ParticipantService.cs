using PlanningPoker.Domain.DTOs.Game;
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
    /// <exception cref="ForbiddenException">Виникає, якщо поточний користувач не є учасником гри</exception>
    public async Task<IEnumerable<GameParticipantDto>> GetGameParticipantsAsync(Guid gameId)
    {
        await gameAccessService.GetRequiredParticipantAsync(gameId, "view", "game");
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
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру з вказаним інвайт-кодом не знайдено.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач намагається приєднатися до неактивної гри без права повторно її активувати.
    /// </exception>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо обране display name уже використовується іншим активним учасником цієї гри.
    /// </exception>
    public async Task<JoinGameResponseDto> JoinGameByInviteCodeAsync(string inviteCode, JoinGameRequestDto joinGameRequestDto)
    {
        if (joinGameRequestDto.ParticipantRole == ParticipantRole.Master)
        {
            throw new InvalidOperationException("Master role cannot be selected when joining a game.");
        }

        var currentUser = await currentUserContext.GetUserOrDefaultAsync();
        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();
        var game = await gameRepository.GetByInviteCodeAsync(inviteCode)
                   ?? throw new NotFoundException(nameof(Game), nameof(Game.InviteCode), inviteCode);

        var activeParticipant = await participantRepository.GetCurrentParticipantAsync(
            game.Id,
            currentIdentity.UserId,
            currentIdentity.GuestParticipantId);
        var existingParticipant = activeParticipant ?? 
                                  await participantRepository.GetCurrentParticipantIncludingRemovedAsync(
                                      game.Id, 
                                      currentIdentity.UserId, 
                                      currentIdentity.GuestParticipantId);

        var requestedRole = ResolveRole(joinGameRequestDto.ParticipantRole, existingParticipant);

        if (!game.IsActive)
        {
            if (existingParticipant?.Role != ParticipantRole.Master)
            {
                throw new ForbiddenException("join", "game");
            }

            game.IsActive = true;
            gameRepository.Update(game);
        }

        var participant = activeParticipant;

        if (activeParticipant is not null)
        {
            var resolvedDisplayName = ResolveDisplayName(
                joinGameRequestDto.DisplayName,
                currentUser?.DisplayName,
                activeParticipant,
                activeParticipant.Id);

            if (!string.Equals(activeParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
            {
                await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
                activeParticipant.DisplayName = resolvedDisplayName;
            }

            activeParticipant.IsConnected = true;
            activeParticipant.JoinedAt = DateTime.UtcNow;

            if (activeParticipant.Role != ParticipantRole.Master &&
                activeParticipant.Role != requestedRole)
            {
                activeParticipant.Role = requestedRole; 
            }
        }
        else if (existingParticipant is not null)
        {
            var resolvedDisplayName = ResolveDisplayName(
                joinGameRequestDto.DisplayName,
                currentUser?.DisplayName,
                existingParticipant,
                existingParticipant.Id);

            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);

            if (!string.Equals(existingParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
            {
                existingParticipant.DisplayName = resolvedDisplayName;
            }

            existingParticipant.IsConnected = true;
            existingParticipant.JoinedAt = DateTime.UtcNow;
            existingParticipant.RemovedAt = null;

            if (existingParticipant.Role != ParticipantRole.Master &&
                existingParticipant.Role != requestedRole)
            {
                existingParticipant.Role = requestedRole;
            }

            participantRepository.Update(existingParticipant);
            participant = existingParticipant;
        }
        else
        {
            var participantId = Guid.NewGuid();
            var resolvedDisplayName = ResolveDisplayName(
                joinGameRequestDto.DisplayName,
                currentUser?.DisplayName,
                null,
                participantId);

            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
            participant = new GameParticipant
            {
                Id = participantId,
                GameId = game.Id,
                UserId = currentUser?.Id,
                DisplayName = resolvedDisplayName,
                Role = requestedRole,
                JoinedAt = DateTime.UtcNow,
                IsConnected = true
            };

            await participantRepository.AddAsync(participant);
        }

        await participantRepository.SaveChangesAsync();

        string? guestAccessToken = null;
        if (currentUser is null)
        {
            guestAccessToken = guestSessionService.GenerateGuestAccessToken(participant!.Id);
            await guestSessionService.CreateGuestSession(participant.Id, guestAccessToken);
        }

        var refreshedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;
        return new JoinGameResponseDto
        {
            Game = GameMapper.ToGameDto(refreshedGame),
            CurrentParticipantId = participant!.Id,
            GuestAccessToken = guestAccessToken
        };
    }

    /// <summary>
    /// Видаляє поточного користувача зі списку активних учасників гри.
    /// Якщо гру залишає master, що не є власником гри, права master повертаються до власника,
    /// а поточний користувач виходить з гри.
    /// Якщо гру залишає master власник гри, сесія завершується для всіх активних учасників,
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
        var participant = await gameAccessService.GetRequiredParticipantAsync(gameId, "leave", "game");
        
        if (participant.UserId == game.CreatedBy)
        {
            await CloseGameForAllParticipantsAsync(game);
            return;
        }

        if (participant.Role != ParticipantRole.Master)
        {
            participantRepository.RemoveGameParticipant(participant);
            await participantRepository.SaveChangesAsync();
            return;
        }

        var gameOwner = await participantRepository.GetCurrentParticipantIncludingRemovedAsync(gameId, game.CreatedBy, null);
        if (gameOwner is not null && gameOwner.Id != participant.Id)
        {
            if (gameOwner.Role != ParticipantRole.Master)
            {
                gameOwner.Role = ParticipantRole.Master;
                participantRepository.Update(gameOwner);
            }
            
            participant.Role = ParticipantRole.Player;
            participantRepository.Update(participant);
            participantRepository.RemoveGameParticipant(participant);
            await participantRepository.SaveChangesAsync();
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
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру або учасника не знайдено.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо поточний користувач не є master цієї гри.
    /// </exception>
    public async Task DeleteGameParticipantAsync(Guid gameId, Guid participantId)
    {
        await GetGameOrThrowAsync(gameId);
        var currentUserParticipant = await gameAccessService.GetRequiredMasterAsync(gameId, "delete", "game participant");

        if (currentUserParticipant.Id == participantId)
        {
            throw new InvalidOperationException("You cannot delete yourself.");
        }
        
        var participant = await gameAccessService.GetRequiredActiveParticipantAsync(gameId, participantId);

        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Оновлює display name поточного учасника в межах конкретної гри.
    /// Якщо нове display name не задане, використовується display name користувача з профілю.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="displayName">Нове display name учасника.
    /// Якщо значення порожнє або складається лише з пробілів, використовується display name користувача з профілю.
    /// </param>
    /// <returns>Оновлені дані учасника у вигляді <see cref="GameParticipantDto"/>.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру або поточного учасника в межах цієї гри не знайдено.
    /// </exception>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо нове display name уже використовується іншим активним учасником цієї гри.
    /// </exception>
    public async Task<GameParticipantDto> UpdateDisplayNameAsync(Guid gameId, string? displayName)
    {
        var currentUser = await currentUserContext.GetUserOrDefaultAsync();
        await GetGameOrThrowAsync(gameId);
        
        var currentUserParticipant =
            await gameAccessService.GetRequiredParticipantAsync(gameId, "update display name", "game");
        
        var resolvedDisplayName = ResolveUpdatedDisplayName(
            displayName,
            currentUser?.DisplayName,
            currentUserParticipant.DisplayName);

        if (!string.Equals(currentUserParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
        {
            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName!, gameId);
            currentUserParticipant.DisplayName = resolvedDisplayName;
        }

        await participantRepository.SaveChangesAsync();
        return ParticipantMapper.ToGameParticipantDto(currentUserParticipant);
    }
    
    /// <summary>
    /// Передає роль Master іншому активному учаснику в межах конкретної гри.
    /// Операцію може виконати лише поточний Master цієї гри.
    /// Після успішної передачі поточний Master отримує роль Player, а вибраний учасник стає новим Master.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="participantId">Ідентифікатор учасника, якому потрібно передати роль Master.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру або вказаного учасника не знайдено.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо поточний користувач не є Master цієї гри.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Виникає, якщо користувач намагається передати роль Master самому собі.
    /// </exception>
    public async Task TransferMasterAsync(Guid gameId, Guid participantId)
    {
        await GetGameOrThrowAsync(gameId);

        var currentUserParticipant = await gameAccessService.GetRequiredMasterAsync(gameId, "transfer master to", "participant");

        var participant =
            await gameAccessService.GetRequiredNonSpectatorParticipantAsync(
                gameId,
                participantId,
                "transfer master to",
                "spectator");

        if (currentUserParticipant.Id == participantId)
        {
            throw new InvalidOperationException("You cannot transfer master role to yourself.");
        }
        
        currentUserParticipant.Role = ParticipantRole.Player;
        participantRepository.Update(currentUserParticipant);
        
        participant.Role = ParticipantRole.Master;
        participantRepository.Update(participant);
        
        await participantRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Повертає гру за ідентифікатором або викидає виняток, якщо гру не знайдено.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Сутність гри.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру не знайдено.
    /// </exception>
    private async Task<Game> GetGameOrThrowAsync(Guid gameId)
    {
        return await gameRepository.GetByIdAsync(gameId)
               ?? throw new NotFoundException(nameof(Game), gameId);
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
    
    /// <summary>
    /// Завершує гру для всіх учасників, видаляючи їх із сесії та деактивуючи гру.
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
    /// Перемикає режим учасника між Player та Spectator.
    /// Master не може стати Spectator, оскільки він повинен керувати грою.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="isSpectator">True — увімкнути режим глядача, False — повернутися до ролі гравця.</param>
   public async Task SetSpectatorModeAsync(Guid gameId, bool isSpectator)
    {
        await GetGameOrThrowAsync(gameId);
        var currentUserParticipant =
            await gameAccessService.GetRequiredParticipantAsync(gameId, "change role", "game participant");

        if (!isSpectator)
        {
            var role = currentUserParticipant.Role == ParticipantRole.Spectator ? 
                ParticipantRole.Player : ParticipantRole.Spectator;
            
            currentUserParticipant.Role = role;
            participantRepository.Update(currentUserParticipant);
            await participantRepository.SaveChangesAsync();

            return;
        }

        if (currentUserParticipant.Role == ParticipantRole.Master)
        {
            throw new InvalidOperationException("You must transfer master rights before switching to another role.");
        }

        currentUserParticipant.Role = ParticipantRole.Spectator;

        participantRepository.Update(currentUserParticipant);
        await participantRepository.SaveChangesAsync();
    }
}