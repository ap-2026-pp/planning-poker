using System.Text.Json.Serialization;
using PlanningPoker.BLL.Constants;
using PlanningPoker.BLL.DTOs.Plane;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для інтеграції з Plane API та отримання задач (issues) з проєктів.
/// </summary>
internal class PlaneService : IPlaneService
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IGameAccessService _gameAccessService;
    private readonly HttpClient _httpClient;
    private const int PageSize = 100;

    public PlaneService(IGameAccessService gameAccessService,
        HttpClient httpClient)
    {
        _gameAccessService = gameAccessService;
        _httpClient = httpClient;
    }

    private static readonly HashSet<string> AllowedGroups = new()
    {
        "backlog",
        "unstarted"
    };

    /// <summary>
    /// Отримує список задач (issues) з Plane для вказаного проєкту та воркспейсу.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <param name="workspaceSlug">Slug робочого простору Plane.</param>
    /// <param name="projectId">Ідентифікатор проєкту в Plane.</param>
    /// <param name="apiKey">API ключ для доступу до Plane API.</param>
    /// <returns>Список задач, відфільтрованих за дозволеними групами станів.</returns>
    public async Task<List<PlaneIssueDto>> GetIssuesAsync(Guid gameId, string workspaceSlug, string projectId, string apiKey)
    {
        await _gameAccessService.GetRequiredMasterAsync(
            gameId,
            AccessControlConstants.GetAction,
            AccessControlConstants.IssuesResource);

        var statesMap = await GetStatesMap(workspaceSlug, projectId, apiKey);
        var issues = new List<PlaneIssueDto>();
        string? cursor = null;

        do
        {
            var url = BuildIssuesUrl(workspaceSlug, projectId, PageSize, cursor);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(ApiKeyHeaderName, apiKey);

            using var response = await _httpClient.SendAsync(request);
            await EnsureSuccessfulPlaneResponseAsync(response);

            var rawJson = await response.Content.ReadAsStringAsync();

            var page = System.Text.Json.JsonSerializer.Deserialize<PlaneIssuesPage>(rawJson);

            if (page?.Results is null || page.Results.Count == 0)
                break;

            var filteredResults = page.Results
                .Where(i =>
                    i.State != null &&
                    statesMap.ContainsKey(i.State) &&
                    AllowedGroups.Contains(statesMap[i.State].Group.ToLower())
                )
                .Select(issue =>
                {
                    var state = statesMap[issue.State!];

                    return new PlaneIssueDto
                    {
                        Id = issue.Id,
                        SequenceId = issue.SequenceId,
                        Name = issue.Name,
                        DescriptionHtml = issue.DescriptionHtml,
                        Status = state.Name,
                        CreatedAt = issue.CreatedAt
                    };
                })
                .ToList();

            issues.AddRange(filteredResults);
            cursor = page.NextPageResults ? page.NextCursor : null;

        } while (!string.IsNullOrWhiteSpace(cursor));

        return issues.OrderBy(i => i.CreatedAt).ToList();
    }

    /// <summary>
    /// Отримує мапу станів задач для проєкту Plane.
    /// </summary>
    /// <param name="workspace">Slug воркспейсу Plane.</param>
    /// <param name="project">Ідентифікатор проєкту Plane.</param>
    /// <param name="apiKey">API ключ для доступу до Plane API.</param>
    /// <returns>Словник станів, де ключ — це ID стану.</returns>
    private async Task<Dictionary<string, PlaneStateDetail>> GetStatesMap(string workspace, string project, string apiKey)
    {
        var url = $"/api/v1/workspaces/{workspace}/projects/{project}/states/";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(ApiKeyHeaderName, apiKey);

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessfulPlaneResponseAsync(response);

        var rawJson = await response.Content.ReadAsStringAsync();

        var page = System.Text.Json.JsonSerializer.Deserialize<PlaneStatesPage>(rawJson);

        return page?.Results?.ToDictionary(s => s.Id, s => s) ?? new();
    }

    /// <summary>
    /// Формує URL для отримання списку задач з підтримкою пагінації.
    /// </summary>
    /// <param name="workspace">Slug воркспейсу Plane.</param>
    /// <param name="project">Ідентифікатор проєкту Plane.</param>
    /// <param name="pageSize">Кількість елементів на сторінку.</param>
    /// <param name="cursor">Курсор для наступної сторінки.</param>
    /// <returns>Згенерований URL для запиту до API.</returns>
    private static string BuildIssuesUrl(string workspace, string project, int pageSize, string? cursor)
    {
        var url =
            $"/api/v1/workspaces/{Uri.EscapeDataString(workspace)}/projects/{Uri.EscapeDataString(project)}/issues/?per_page={pageSize}&order_by=created_at";

        return string.IsNullOrWhiteSpace(cursor)
            ? url
            : $"{url}&cursor={Uri.EscapeDataString(cursor)}";
    }

    /// <summary>
    /// Перевіряє відповідь Plane API та кидає виняток у випадку помилки.
    /// </summary>
    /// <param name="response">HTTP відповідь від Plane API.</param>
    /// <returns>Асинхронна операція перевірки.</returns>
    /// <exception cref="Exception">Виникає, якщо API повернув помилку.</exception>
    private static async Task EnsureSuccessfulPlaneResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        throw new Exception($"Plane API error: {body}");
    }

    /// <summary>
    /// Відповідь API зі списком задач Plane.
    /// </summary>
    private sealed class PlaneIssuesPage
    {
        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; set; }

        [JsonPropertyName("next_page_results")]
        public bool NextPageResults { get; set; }

        [JsonPropertyName("results")]
        public List<PlaneIssueItem> Results { get; set; } = [];
    }

    /// <summary>
    /// Відповідь API зі списком станів задач Plane.
    /// </summary>
    private sealed class PlaneStatesPage
    {
        [JsonPropertyName("results")]
        public List<PlaneStateDetail> Results { get; set; } = [];
    }

    /// <summary>
    /// Модель задачі Plane.
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

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Деталі стану задачі Plane.
    /// </summary>
    private sealed class PlaneStateDetail
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("group")]
        public string Group { get; set; } = string.Empty;
    }
}