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

        if (participant is null || participant.RemovedAt.HasValue)
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
    /// Перевіряє можливість розкриття карт у грі. 
    /// Наразі ця дія дозволена лише для Майстра гри.
    /// </summary>
    /// <param name="gameId">Унікальний ідентифікатор гри.</param>
    /// <returns>Учасник, який ініціював розкриття.</returns>
    public async Task<GameParticipant> EnsureCanRevealCardsAsync(Guid gameId)
    {
        return await GetRequiredMasterAsync(gameId);
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
        return await GetRequiredMasterAsync(gameId);
    }

    /// <summary>
    /// Передає повноваження Майстра гри іншому учаснику.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри, у якій відбувається передача прав.</param>
    /// <param name="newMasterParticipantId">Ідентифікатор учасника, який стане новим Майстром.</param>
    /// <returns>Асинхронна операція.</returns>
    /// <exception cref="NotFoundException">Виникає, якщо нового учасника не знайдено в базі або він не належить до цієї гри.</exception>
    /// <exception cref="ForbiddenException">Виникає, якщо дію намагається виконати не поточний Майстер.</exception>
    public async Task TransferMasterRoleAsync(Guid gameId, Guid newMasterParticipantId)
    {
        var currentMaster = await GetRequiredMasterAsync(gameId);
        var newMaster = await participantRepository.GetByIdAsync(newMasterParticipantId);

        if (newMaster == null || newMaster.GameId != gameId || newMaster.RemovedAt.HasValue)
            throw new NotFoundException("Participant", newMasterParticipantId);

        currentMaster.Role = ParticipantRole.Player;
        newMaster.Role = ParticipantRole.Master;

        await participantRepository.SaveChangesAsync();
    }
} 