namespace DeploymentService.Models;

public class HealingResult
{
    public string PodName { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public string CodeFix { get; set; } = string.Empty;
    public bool PrOpened { get; set; }
    public string? PrUrl { get; set; }
    public DateTime HealedAt { get; set; } = DateTime.UtcNow;
    public FailureEvent OriginalFailure { get; set; } = new();
}