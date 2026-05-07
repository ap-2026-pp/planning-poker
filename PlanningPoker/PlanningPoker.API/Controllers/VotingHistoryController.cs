using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs.VotingHistory;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/games/{gameId:guid}/history")]
public class VotingHistoryController : ControllerBase
{
    private readonly IVotingHistoryService _serviceHistory;

    public VotingHistoryController(IVotingHistoryService serviceHistory)
    {
        _serviceHistory = serviceHistory;
    }

    /// <summary>
    /// Повертає історію завершених голосувань для гри
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetVotingHistory([FromRoute] Guid gameId,
        [FromQuery] VotingHistoryQueryDto query)
    {
        var history = await _serviceHistory.GetHistoryAsync(gameId, query);

        return Ok(history);
    }

    /// <summary>
    /// Повертає деталі одного завершеного голосування
    /// </summary>
    [HttpGet("{entryId:guid}")]
    public async Task<IActionResult> GetHistoryDetails([FromRoute] Guid gameId,
        [FromRoute] Guid entryId)
    {
        var result = await _serviceHistory.GetHistoryDetailsAsync(gameId, entryId);

        return Ok(result);
    }

    /// <summary>
    /// Експорт історії голосувань у CSV формат
    /// </summary>
    [HttpPost("export-csv")]
    public async Task<IActionResult> ExportHistoryCsv([FromRoute] Guid gameId,
        [FromBody] ExportVotingHistoryDto dto)
    {
        var fileBytes = await _serviceHistory.ExportHistoryCsvAsync(gameId, dto);

        return File(
            fileBytes,
            "text/csv",
            $"voting_history_{gameId}.csv");
    }
}