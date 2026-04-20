namespace DeploymentService.Models;

public class FailureEvent
{
    public string PodName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Logs { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public FailureType Type { get; set; }
}

public enum FailureType
{
    PodCrash,
    OOMKilled,
    ImagePullError,
    CrashLoopBackOff,
    DeploymentFailed
}