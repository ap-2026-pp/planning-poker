using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;

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
        

        if ( participant.Game.RevealPolicy == RevealPolicy.Everyone)
        {
            return participant;
        }

        if (participant.Role != ParticipantRole.Master)
            throw new ForbiddenException("reveal cards", "game");

        return participant;
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
        
        if(game.IssuesPolicy == IssuesPolicy.MasterOnly)
        {
            if(participant.Role != ParticipantRole.Master)
                throw new ForbiddenException("manage issues", "game");
        }
        return participant;
    }

    /// <summary>
    /// Передає роль Master іншому учаснику гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="newMasterParticipantId">Ідентифікатор нового майстра.</param>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо нового учасника не знайдено або він не належить до гри.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач не має права передавати роль.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Виникає при спробі передати роль самому собі.
    /// </exception>
   public async Task TransferMasterRoleAsync(Guid gameId, Guid newMasterParticipantId)
    {
        var currentMaster = await GetRequiredMasterAsync(gameId);
        var newMaster = await participantRepository.GetByIdAsync(newMasterParticipantId);

        if (newMaster is null || newMaster.GameId != gameId || newMaster.RemovedAt.HasValue)
            throw new NotFoundException(nameof(GameParticipant), newMasterParticipantId);

        if (currentMaster.Id == newMaster.Id)
            throw new InvalidOperationException("Cannot transfer master to yourself!");
        
        if (newMaster.Role == ParticipantRole.Spectator)
        {
            throw new InvalidOperationException("Spectator cannot be promoted to Master.");
        }

        currentMaster.Role = ParticipantRole.Player;
        newMaster.Role = ParticipantRole.Master;

        participantRepository.Update(currentMaster);
        participantRepository.Update(newMaster);

        await participantRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Перемикає учасника між режимами Player і Spectator.
    /// Spectator не бере участі в голосуванні.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="isSpectator">true — зробити глядачем, false — повернути в Player.</param>
    public async Task SetSpectatorModeAsync(Guid gameId, bool isSpectator)
    {
        var participant = await GetRequiredParticipantAsync(gameId);
        var newRole = isSpectator ? ParticipantRole.Spectator : ParticipantRole.Player;

        if (participant.Role == newRole)
        {
            return;
        }

        if (isSpectator)
        {
            if (participant.Role == ParticipantRole.Master)
                throw new ForbiddenException("become spectator", "game");

            participant.Role = ParticipantRole.Spectator;
        }
        else
        {
            if (participant.Role == ParticipantRole.Spectator)
                participant.Role = ParticipantRole.Player;
        }

        participantRepository.Update(participant);
        await participantRepository.SaveChangesAsync();
    }
} 