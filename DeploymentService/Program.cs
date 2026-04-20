using DeploymentService.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddSingleton<KubernetesWatcher>();
builder.Services.AddSingleton<ClaudeAnalyser>();
builder.Services.AddSingleton<GitHubService>();
builder.Services.AddSingleton<HealingOrchestrator>();
builder.Services.AddHostedService<KubernetesWatcher>();

var app = builder.Build();

app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });
app.MapGet("/healings", (HealingOrchestrator orchestrator) => orchestrator.GetHealingHistory());

app.Run();

public class AppSettings
{
    public string AnthropicApiKey { get; set; } = string.Empty;
    public string GitHubToken { get; set; } = string.Empty;
    public string GitHubRepoOwner { get; set; } = string.Empty;
    public string GitHubRepoName { get; set; } = string.Empty;
    public string KubernetesNamespace { get; set; } = "default";
    public int MaxReplicasOnScale { get; set; } = 5;
}