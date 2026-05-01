namespace PlanningPoker.BLL.DTOs.Issue.Export;

public class ExportIssuesFileDto
{
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
}