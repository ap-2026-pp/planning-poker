using PlanningPoker.Domain.DTOs.Timer;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Exceptions;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для керування таймерами в ігрових кімнатах.
/// </summary>
public class TimerService : ITimerService
{
    private readonly ITimerRepository _timerRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IGameAccessService _accessService;
    private readonly IIssueRepository _issueRepository;

    public TimerService(
        ITimerRepository timerRepository,
        IGameRepository gameRepository,
        IGameAccessService accessService,
        IIssueRepository issueRepository)
    {
        _timerRepository = timerRepository;
        _gameRepository = gameRepository;
        _accessService = accessService;
        _issueRepository = issueRepository;
    }
    
    /// <summary>
    /// Запускає таймер гри та за потреби вмикає автоматичне відкриття карт після завершення таймера.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Інформація про запущений таймер.</returns>
    /// <exception cref="InvalidOperationException">Виникає, якщо не вибрано активну задачу для обговорення.</exception>
    /// <exception cref="NotFoundException">Виникає, якщо гра не знайдена.</exception>
    public async Task<TimerDto> StartTimerAsync(Guid gameId)
    {
        await _accessService.GetRequiredMasterAsync(gameId, "Start", "Timer");

        var activeIssue = await _issueRepository.GetActiveIssueByGameIdAsync(gameId);

        if (activeIssue is null)
        {
            throw new InvalidOperationException(
                "Cannot start the timer: please select an active issue for discussion first.");
        }

        var game = await _gameRepository.GetByIdAsync(gameId)
                   ?? throw new NotFoundException(nameof(Game), gameId);

        var timer = await _timerRepository.GetByIdAsync(gameId);

        if (timer is null)
        {
            timer = new GameTimer { GameId = gameId };
            await _timerRepository.AddAsync(timer);
        }

        timer.StartedAt = DateTime.UtcNow;
        timer.EndsAt = DateTime.UtcNow.AddMinutes(game.DefaultTimerMinutes);
        timer.AutoReset = game.AutoResetTimer;

        await _timerRepository.SaveChangesAsync();

        return TimerMapper.ToDto(timer);
    }
    
    /// <summary>
    /// Зупиняє активний таймер гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    public async Task StopTimerAsync(Guid gameId)
    {
        await _accessService.GetRequiredMasterAsync(gameId, "Stop", "Timer");

        var timer = await _timerRepository.GetByIdAsync(gameId);

        if (timer is not null)
        {
            _timerRepository.Delete(timer);
            await _timerRepository.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Отримує інформацію про активний таймер гри.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>DTO активного таймера або null, якщо таймер не запущено.</returns>
    public async Task<TimerDto?> GetActiveTimerAsync(Guid gameId)
    {
        var timer = await _timerRepository.GetByIdAsync(gameId);

        return timer is not null ? TimerMapper.ToDto(timer) : null;
    }
}