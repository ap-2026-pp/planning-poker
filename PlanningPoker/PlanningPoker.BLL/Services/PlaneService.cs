using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using PlanningPoker.BLL.DTOs.Issue;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Отримує задачі з Plane API
/// </summary>
internal class PlaneService : IPlaneService
{
    /// <summary>
    /// Назва HTTP-заголовка для передачі API-ключа Plane.
    /// </summary>
    private const string ApiKeyHeaderName = "X-API-Key";

    /// <summary>
    /// Максимальна кількість задач, що запитуються за один запит до Plane.
    /// </summary>
    private const int PageSize = 100;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PlaneService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Отримує всі задачі з указаного проєкту Plane з урахуванням пагінації
    /// </summary>
    public async Task<List<PlaneIssueDto>> GetIssuesAsync(ImportPlaneIssuesDto dto)
    {
        var apiKey = _configuration["Plane:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Plane API key is not configured.");

        if (string.IsNullOrWhiteSpace(dto.WorkspaceSlug))
            throw new ArgumentException("Workspace slug is required.", nameof(dto.WorkspaceSlug));

        if (string.IsNullOrWhiteSpace(dto.ProjectId))
            throw new ArgumentException("Project id is required.", nameof(dto.ProjectId));

        var issues = new List<PlaneIssueDto>();
        string? cursor = null;

        do
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                BuildUrl(dto.WorkspaceSlug, dto.ProjectId, cursor));

            request.Headers.Add(ApiKeyHeaderName, apiKey);

            using var response = await _httpClient.SendAsync(request);

            await EnsureSuccessfulPlaneResponseAsync(response);

            var page = await response.Content.ReadFromJsonAsync<PlaneIssuesPage>();

            if (page?.Results is null || page.Results.Count == 0)
                break;

            issues.AddRange(page.Results.Select(issue => new PlaneIssueDto
            {
                Id = issue.Id,
                SequenceId = issue.SequenceId,
                Name = issue.Name,
                DescriptionHtml = issue.DescriptionHtml
            }));

            cursor = page.NextPageResults ? page.NextCursor : null;

        } while (!string.IsNullOrWhiteSpace(cursor));

        return issues;
    }

    /// <summary>
    /// Формує відносний URL для запиту задач із Plane API
    /// </summary>
    private static string BuildUrl(string workspaceSlug, string projectId, string? cursor)
    {
        var url =
            $"/api/v1/workspaces/{Uri.EscapeDataString(workspaceSlug)}/projects/{Uri.EscapeDataString(projectId)}/work-items/?per_page={PageSize}";

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return url;
    }

    /// <summary>
    /// Перевіряє відповідь Plane API та формує зрозумілу помилку у разі невдалого запиту
    /// </summary>
    private static async Task EnsureSuccessfulPlaneResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync();

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                new InvalidOperationException("Plane API key is invalid."),

            HttpStatusCode.Forbidden =>
                new InvalidOperationException("Plane API access is forbidden."),

            HttpStatusCode.NotFound =>
                new InvalidOperationException("Plane workspace or project was not found."),

            (HttpStatusCode)429 =>
                new InvalidOperationException("Plane API rate limit exceeded."),

            _ =>
                new InvalidOperationException(
                    $"Plane API request failed with status {(int)response.StatusCode}: {responseBody}")
        };
    }

    /// <summary>
    /// Сторінка відповіді Plane API зі списком задач
    /// </summary>
    private sealed class PlaneIssuesPage
    {
        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; set; }

        [JsonPropertyName("next_page_results")]
        public bool NextPageResults { get; set; }

        [JsonPropertyName("results")]
        public List<PlaneIssueItem> Results { get; set; } = new();
    }

    /// <summary>
    /// Одна задача відповіді Plane API
    /// </summary>
    private sealed class PlaneIssueItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("sequence_id")]
        public int? SequenceId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description_html")]
        public string? DescriptionHtml { get; set; }
    }
}