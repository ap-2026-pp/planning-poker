using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class ParticipantService(
    IParticipantRepository participantRepository,
    IGameRepository gameRepository,
    ICurrentUserContext currentUserContext) : IParticipantService
{
    
    /// <summary>
    /// Повертає список активних учасників гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Колекцію учасників гри у вигляді <see cref="GameParticipantDto"/>.</returns>
    public async Task<IEnumerable<GameParticipantDto>> GetGameParticipantsAsync(Guid gameId)
    {
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
    /// <param name="displayName">Бажане display name учасника в межах гри.
    /// Якщо значення не задане або порожнє, використовується display name користувача з профілю.
    /// </param>
    /// <returns>Актуальний стан гри після приєднання у вигляді <see cref="GameDto"/>.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру з вказаним інвайт-кодом не знайдено.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач намагається приєднатися до неактивної гри без права повторно її активувати.
    /// </exception>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо обране display name уже використовується іншим активним учасником цієї гри.
    /// </exception>
    public async Task<GameDto> JoinGameByInviteCodeAsync(string inviteCode, string? displayName)
    {
        var currentUser = await currentUserContext.GetRequiredUserAsync();
        var userId = currentUser.Id;
        var game = await gameRepository.GetByInviteCodeAsync(inviteCode)
                   ?? throw new NotFoundException(nameof(Game), nameof(Game.InviteCode), inviteCode);
        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? currentUser.DisplayName
            : displayName.Trim();

        var activeParticipant = await participantRepository.GetByUserIdAndGameIdAsync(userId, game.Id);
        var existingParticipant = activeParticipant ??
                                  await participantRepository.GetByUserIdAndGameIdIncludingRemovedAsync(userId, game.Id);

        if (!game.IsActive)
        {
            if (existingParticipant?.Role != ParticipantRole.Master)
            {
                throw new ForbiddenException("join", "game");
            }

            game.IsActive = true;
            gameRepository.Update(game);
        }

        if (activeParticipant is not null)
        {
            if (!string.Equals(activeParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
            {
                await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
                activeParticipant.DisplayName = resolvedDisplayName;
            }

            activeParticipant.IsConnected = true;
            activeParticipant.JoinedAt = DateTime.UtcNow;
            await participantRepository.SaveChangesAsync();

            var updatedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;
            return GameMapper.ToGameDto(updatedGame);
        }

        if (existingParticipant is not null)
        {
            if (!string.Equals(existingParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
            {
                await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
                existingParticipant.DisplayName = resolvedDisplayName;
            }

            existingParticipant.IsConnected = true;
            existingParticipant.JoinedAt = DateTime.UtcNow;
            existingParticipant.RemovedAt = null;
            participantRepository.Update(existingParticipant);
        }
        else
        {
            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, game.Id);
            await participantRepository.AddAsync(new GameParticipant
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                UserId = userId,
                DisplayName = resolvedDisplayName,
                Role = ParticipantRole.Player,
                JoinedAt = DateTime.UtcNow,
                IsConnected = true
            });
        }

        await participantRepository.SaveChangesAsync();
        var refreshedGame = await gameRepository.GetByIdAsync(game.Id) ?? game;
        return GameMapper.ToGameDto(refreshedGame);
    }

    /// <summary>
    /// Видаляє поточного користувача зі списку активних учасників гри.
    /// Якщо гру залишає master, сесія завершується для всіх активних учасників, а сама гра позначається як неактивна.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру або поточного учасника в межах цієї гри не знайдено.
    /// </exception>
    public async Task LeaveGameAsync(Guid gameId)
    {
        var userId = currentUserContext.GetRequiredUserId();
        var game = await GetGameOrThrowAsync(gameId);
        
        var participant = game.Participants.SingleOrDefault(currentParticipant => currentParticipant.UserId == userId);
        if (participant is null)
        {
            throw new NotFoundException("Active game participant was not found.");
        }

        if (participant.Role == ParticipantRole.Master)
        {
            foreach (var currentParticipant in game.Participants)
            {
                participantRepository.RemoveGameParticipant(currentParticipant);
            }

            game.IsActive = false;
            gameRepository.Update(game);
            await participantRepository.SaveChangesAsync();
            return;
        }

        participantRepository.RemoveGameParticipant(participant);
        await participantRepository.SaveChangesAsync();
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
        var userId = currentUserContext.GetRequiredUserId();
        await GetGameOrThrowAsync(gameId);

        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(userId, gameId);
        if (currentUserParticipant is null || currentUserParticipant.Role != ParticipantRole.Master)
        {
            throw new ForbiddenException("delete", "participant");
        }

        if (currentUserParticipant.Id == participantId)
        {
            throw new InvalidOperationException("You cannot delete yourself.");
        }
        
        var participant = await participantRepository.GetActiveByIdAsync(participantId);
        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }

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
        var currentUser = await currentUserContext.GetRequiredUserAsync();
        var userId = currentUser.Id;
        await GetGameOrThrowAsync(gameId);
        
        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(userId, gameId)
                                     ?? throw new NotFoundException("User participant was not found in the game.");
        
        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? currentUser.DisplayName : displayName.Trim();

        if (!string.Equals(currentUserParticipant.DisplayName, resolvedDisplayName, StringComparison.Ordinal))
        {
            await EnsureDisplayNameIsAvailableAsync(resolvedDisplayName, gameId);
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
        var currentUserId = currentUserContext.GetRequiredUserId();
        await GetGameOrThrowAsync(gameId);

        var currentUserParticipant = await participantRepository.GetByUserIdAndGameIdAsync(currentUserId, gameId);
        if (currentUserParticipant is null || currentUserParticipant.Role != ParticipantRole.Master)
        {
            throw new ForbiddenException("transfer master to", "participant");
        }
        
        var participant = await participantRepository.GetActiveByIdAsync(participantId);
        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }

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
            throw new ResourceAlreadyExistsException(nameof(GameParticipant), displayName); // TODO change exception
        }
    }
}
