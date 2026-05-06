namespace PlanningPoker.BLL.DTOs.Issue.Export;

public class ExportIssuesRequestDto
{
    public string SummaryColumnName { get; set; } = "Summary";
    public string KeyColumnName { get; set; } = "Key";
    public string DescriptionColumnName { get; set; } = "Description";
    public string LinkColumnName { get; set; } = "Link";
    public string EstimateColumnName { get; set; } = "Estimate";
}