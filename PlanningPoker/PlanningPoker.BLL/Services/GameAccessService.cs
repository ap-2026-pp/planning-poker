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
    /// Перевіряє, чи є поточний користувач активним учасником гри.
    /// </summary>
    /// <param name="gameId">Унікальний ідентифікатор гри.</param>
    /// <returns>Об'єкт <see cref="GameParticipant"/>, якщо доступ дозволено.</returns>
    /// <exception cref="ForbiddenException">Виникає, якщо користувач не є учасником або був видалений з гри.</exception>
    public async Task<GameParticipant> GetRequiredParticipantAsync(Guid gameId)
    {
        var userId = currentUserContext.GetRequiredUserId();
        var participant = await participantRepository.GetByUserIdAndGameIdAsync(userId, gameId);

        if (participant is null || participant.RemovedAt is not null)
            throw new ForbiddenException("view", "game");

        return participant;
    }

    /// <summary>
    /// Перевіряє, чи володіє поточний користувач правами Майстра (Master) у вказаній грі.
    /// </summary>
    /// <param name="gameId">Унікальний ідентифікатор гри.</param>
    /// <returns>Сутність учасника з роллю Master.</returns>
    /// <exception cref="ForbiddenException">Виникає, якщо роль користувача відмінна від Master.</exception>
    public async Task<GameParticipant> GetRequiredMasterAsync(Guid gameId)
    {
        var participant = await GetRequiredParticipantAsync(gameId);

        if (participant.Role != ParticipantRole.Master)
            throw new ForbiddenException("manage", "game");

        return participant;
    }

    /// <summary>
    /// Перевіряє, чи дозволено Майстру гри ініціювати розкриття карт.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Учасник, який ініціює дію.</returns>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач не є Master.
    /// </exception>
    public async Task<GameParticipant> EnsureCanRevealCardsAsync(Guid gameId)
    {
        var participant = await GetRequiredParticipantAsync(gameId);

        if (participant.Role == ParticipantRole.Spectator)
        throw new ForbiddenException("reveal cards", "spectator cannot reveal cards");

        if (participant.Game.RevealPolicy == RevealPolicy.Everyone)
        {
            return participant;
        }

        if (participant.Role == ParticipantRole.Master || participant.CanRevealCards)
            return participant;

        throw new ForbiddenException("reveal cards", "game");
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
        var participant = await GetRequiredParticipantAsync(gameId);

        if (participant.Role == ParticipantRole.Spectator)
            throw new ForbiddenException("vote", "game");

        return participant;
    }

    /// <summary>
    /// Перевіряє, чи може користувач керувати списком задач (Issue) у грі.
    /// Доступно лише для користувачів з роллю Master.
    /// </summary>
    /// <param name="gameId">Унікальний ідентифікатор гри.</param>
    /// <returns>Майстер гри, який виконує управління.</returns>
    public async Task<GameParticipant> EnsureCanManageIssuesAsync(Guid gameId)
    {
        var participant = await GetRequiredParticipantAsync(gameId);
        var game = participant.Game;

        if (participant.Role == ParticipantRole.Spectator)
            throw new ForbiddenException("manage issues", "spectator cannot do this");

        if (game.IssuesPolicy == IssuesPolicy.Everyone)
        {
            return participant;
        }

        if (game.IssuesPolicy == IssuesPolicy.MasterOnly)
        {
            if (participant.Role == ParticipantRole.Master) return participant;
            throw new ForbiddenException("manage issues", "Only game master can manage issues in this mode");
        }

        if (participant.Role == ParticipantRole.Master || participant.CanManageIssues)
        {
            return participant;
        }

        throw new ForbiddenException("manage issues", "You don't have permission to manage issues");
    }
   
    public async Task UpdateBulkPermissionsAsync(Guid gameId, UpdateBulkPermissionsRequestDto dto)
    {
        await GetRequiredMasterAsync(gameId);

        var participants = await participantRepository.GetGameParticipantsAsync(gameId);

        if (participants == null)
            return;

        var permissionsById = dto.Participants
            .ToDictionary(x => x.ParticipantId);

        foreach (var participant in participants)
        {
            if (participant.Role is ParticipantRole.Master or ParticipantRole.Spectator)
                continue;

            if (permissionsById.TryGetValue(participant.Id, out var perm))
            {
                participant.CanRevealCards = perm.CanRevealCards;
                participant.CanManageIssues = perm.CanManageIssues;
            }
            else
            {
                participant.CanRevealCards = false;
                participant.CanManageIssues = false;
            }

            participantRepository.Update(participant);
        }

        await participantRepository.SaveChangesAsync();
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
}