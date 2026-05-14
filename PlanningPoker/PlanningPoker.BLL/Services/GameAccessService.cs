using PlanningPoker.BLL.Constants;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;
using PlanningPoker.Domain.DTOs.Game;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для перевірки прав доступу та ролей учасників у межах ігрових сесій.
/// </summary>
public class GameAccessService(
    IParticipantRepository participantRepository,
    ICurrentUserContext currentUserContext) : IGameAccessService
{
    
    /// <summary>
    /// Повертає поточного активного учасника гри або викидає помилку доступу.
    /// Підтримує як авторизованих користувачів, так і guest participant.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="action">Дія, яку користувач намагається виконати.</param>
    /// <param name="resourceName">Назва ресурсу для повідомлення про помилку доступу.</param>
    /// <returns>Поточний активний учасник гри.</returns>
    /// <exception cref="ForbiddenException">Виникає, якщо поточний користувач не є учасником гри.</exception>
    public async Task<GameParticipant> GetRequiredParticipantAsync(
        Guid gameId,
        string action,
        string resourceName)
    {
        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();
        var participant = await participantRepository.GetCurrentParticipantAsync(
            gameId,
            currentIdentity.UserId,
            currentIdentity.GuestParticipantId);

        return participant ?? throw new ForbiddenException(action, resourceName);
    }

    /// <summary>
    /// Повертає поточного активного Master-учасника гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="action">Назва дії, яку користувач намагається виконати.</param>
    /// <param name="resourceName">Назва ресурсу для повідомлення про помилку доступу.</param>
    /// <returns>Сутність учасника з роллю Master.</returns>
    /// <exception cref="ForbiddenException">Виникає, якщо роль користувача відмінна від Master.</exception>
    public async Task<GameParticipant> GetRequiredMasterAsync(Guid gameId, string action, string resourceName)
    {
        var participant = await GetRequiredParticipantAsync(gameId, action, resourceName);

        return participant.Role != ParticipantRole.Master
            ? throw new ForbiddenException(action, AccessControlConstants.GameResource)
            : participant;
    }

    /// <summary>
    /// Перевіряє, чи дозволено Майстру гри ініціювати розкриття карт.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Учасник, який ініціює дію.</returns>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо політика розкриття вимагає Master, а користувач не має цієї ролі.
    /// </exception>
    public async Task<GameParticipant> EnsureCanRevealCardsAsync(Guid gameId)
    {
        var participant = await GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.RevealCardsAction,
            AccessControlConstants.GameResource);

        if (participant.Role == ParticipantRole.Spectator)
        {
            throw new ForbiddenException(
                AccessControlConstants.RevealCardsAction,
                AccessControlConstants.GameResource);
        }

        if (participant.Game.TimerEndsAt.HasValue)
        {
            bool isTimerFinished = participant.Game.TimerEndsAt <= DateTime.UtcNow;
            
            if (isTimerFinished && participant.Game.AutoRevealCards)
            {
                return participant;
            }
        }
        if (participant.Game.RevealPolicy == RevealPolicy.Everyone ||
            participant.Role == ParticipantRole.Master ||
            participant.CanRevealCards)
        {
            return participant;
        }

        throw new ForbiddenException(
            AccessControlConstants.RevealCardsAction,
            AccessControlConstants.GameResource);
    }

    /// <summary>
    /// Перевіряє, чи має учасник право голосувати. 
    /// Глядачі (Spectators) позбавлені права голосу.
    /// </summary>
    /// <param name="gameId">Унікальний ідентифікатор гри.</param>
    /// <returns>Учасник, чиє право на голосування підтверджено.</returns>
    /// <exception cref="ForbiddenException">Виникає, якщо учасник має роль Spectator.</exception>
    public async Task<GameParticipant> EnsureCanVoteAsync(Guid gameId)
    {
        var participant = await GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.VoteAction,
            AccessControlConstants.GameResource);

        return participant.Role == ParticipantRole.Spectator
            ? throw new ForbiddenException(
                AccessControlConstants.VoteAction,
                AccessControlConstants.GameResource)
            : participant;
    }

    /// <summary>
    /// Перевіряє, чи може користувач керувати списком задач (Issue) у грі.
    /// Якщо політика гри дозволяє це всім, управління доступне всім,
    /// окрім учасників із роллю Spectator.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="action">Дія, яку користувач намагається виконати.</param>
    /// <param name="resourceName">Назва ресурсу для повідомлення про помилку доступу.</param>
    /// <returns>Поточний учасник, якщо управління задачами дозволено.</returns>
    public async Task<GameParticipant> EnsureCanManageIssuesAsync(
        Guid gameId,
        string action,
        string resourceName)
    {
        var participant = await GetRequiredParticipantAsync(gameId, action, resourceName);

        if (participant.Role == ParticipantRole.Spectator)
        {
            throw new ForbiddenException(action, resourceName);
        }

        if (participant.Game.IssuesPolicy == IssuesPolicy.Everyone ||
            participant.CanManageIssues)
        {
            return participant;
        }

        throw new ForbiddenException(action, resourceName);
    }

    /// <summary>
    /// Повертає активного учасника конкретної гри за ідентифікатором.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="participantId">Ідентифікатор учасника.</param>
    /// <returns>Активного учасника гри.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо учасника не знайдено або він не належить до вказаної гри.
    /// </exception>
    public async Task<GameParticipant> GetRequiredActiveParticipantAsync(Guid gameId, Guid participantId)
    {
        var participant = await participantRepository.GetActiveByIdAsync(participantId);

        if (participant is null || participant.GameId != gameId)
        {
            throw new NotFoundException(nameof(GameParticipant), participantId);
        }

        return participant;
    }

    /// <summary>
    /// Повертає інших активних учасників гри, які не є Spectator.
    /// Якщо репозиторій не повернув список, використовує вже завантажених у гру учасників як fallback.
    /// </summary>
    /// <param name="game">Гра, для якої потрібно отримати список учасників.</param>
    /// <param name="excludedParticipantId">Ідентифікатор учасника, якого потрібно виключити зі списку.</param>
    /// <returns>Список активних non-spectator учасників без виключеного учасника.</returns>
    public async Task<IReadOnlyList<GameParticipant>> GetOtherActiveNonSpectatorParticipantsAsync(
        Game game,
        Guid excludedParticipantId)
    {
        var participants = (await participantRepository.GetGameParticipantsAsync(game.Id))?.ToList();
        if (participants is null || participants.Count == 0)
        {
            participants = game.Participants.ToList();
        }

        return participants
            .Where(participant =>
                participant.RemovedAt is null &&
                participant.Id != excludedParticipantId &&
                participant.Role != ParticipantRole.Spectator)
            .ToList();
    }

    /// <summary>
    /// Повертає активного учасника гри, який не має ролі Spectator.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="participantId">Ідентифікатор цільового учасника.</param>
    /// <param name="action">Дія, яку намагається виконати поточний користувач.</param>
    /// <param name="resourceName">Назва ресурсу для повідомлення про помилку доступу.</param>
    /// <returns>Активного учасника, якого можна використовувати як ціль дії.</returns>
    public async Task<GameParticipant> GetRequiredNonSpectatorParticipantAsync(
        Guid gameId,
        Guid participantId,
        string action,
        string resourceName)
    {
        var participant = await GetRequiredActiveParticipantAsync(gameId, participantId);

        return participant.Role == ParticipantRole.Spectator
            ? throw new ForbiddenException(action, resourceName)
            : participant;
    }

    public async Task<GameParticipant?> GetParticipantOrEnsureOwnerAsync(
        Game game,
        string action,
        string resourceName)
    {
        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();

        var participant = await participantRepository.GetCurrentParticipantAsync(
            game.Id,
            currentIdentity.UserId,
            currentIdentity.GuestParticipantId);

        if (participant is not null)
        {
            return participant;
        }

        var currentUserId = currentIdentity.UserId;
        if (currentUserId.HasValue && game.CreatedBy == currentUserId.Value)
        {
            return null;
        }

        throw new ForbiddenException(action, resourceName);
    }
    
    public async Task<GameParticipant?> EnsureCanUpdateGameAsync(Game game)
    {
        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();

        var participant = await participantRepository.GetCurrentParticipantAsync(
            game.Id,
            currentIdentity.UserId,
            currentIdentity.GuestParticipantId);

        if (participant is not null)
        {
            if (participant.Role == ParticipantRole.Master)
            {
                return participant;
            }

            throw new ForbiddenException(
                AccessControlConstants.UpdateAction,
                AccessControlConstants.GameResource);
        }

        if (currentIdentity.UserId.HasValue && game.CreatedBy == currentIdentity.UserId.Value)
        {
            return null;
        }

        throw new ForbiddenException(
            AccessControlConstants.UpdateAction,
            AccessControlConstants.GameResource);
    }

    public async Task EnsureCanDeleteGameAsync(Game game)
    {
        var currentIdentity = currentUserContext.GetCurrentParticipantIdentity();

        var participant = await participantRepository.GetCurrentParticipantAsync(
            game.Id,
            currentIdentity.UserId,
            currentIdentity.GuestParticipantId);

        if (participant is not null)
        {
            if (participant.Role == ParticipantRole.Master)
            {
                return;
            }

            throw new ForbiddenException(
                AccessControlConstants.DeleteAction,
                AccessControlConstants.GameResource);
        }

        if (currentIdentity.UserId.HasValue && game.CreatedBy == currentIdentity.UserId.Value)
        {
            return;
        }

        throw new ForbiddenException(
            AccessControlConstants.DeleteAction,
            AccessControlConstants.GameResource);
    }
}
