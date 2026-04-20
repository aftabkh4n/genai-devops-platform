using DeploymentService.Models;

namespace DeploymentService.Services;

public class HealingOrchestrator
{
    private readonly ILogger<HealingOrchestrator> _logger;
    private readonly ClaudeAnalyser _analyser;
    private readonly GitHubService _gitHub;
    private readonly List<HealingResult> _history = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<string> _processing = new();

    public HealingOrchestrator(
        ILogger<HealingOrchestrator> logger,
        ClaudeAnalyser analyser,
        GitHubService gitHub)
    {
        _logger = logger;
        _analyser = analyser;
        _gitHub = gitHub;
    }

    public async Task HandleFailureAsync(FailureEvent failure)
    {
        var key = $"{failure.PodName}-{failure.Reason}";

        await _lock.WaitAsync();
        try
        {
            if (_processing.Contains(key))
            {
                _logger.LogDebug("Already processing failure for {Pod}, skipping", failure.PodName);
                return;
            }
            _processing.Add(key);
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            _logger.LogInformation("Starting healing process for {Pod}", failure.PodName);

            var result = await _analyser.AnalyseFailureAsync(failure);

            _logger.LogInformation(
                "Analysis complete for {Pod}. Severity: {Severity}. Root cause: {RootCause}",
                result.PodName, result.Severity, result.RootCause);

            var prUrl = await _gitHub.OpenHealingPrAsync(result);
            result.PrOpened = prUrl != null;
            result.PrUrl = prUrl;

            await _lock.WaitAsync();
            try
            {
                _history.Insert(0, result);
                if (_history.Count > 100)
                    _history.RemoveAt(_history.Count - 1);
            }
            finally
            {
                _lock.Release();
            }

            _logger.LogInformation(
                "Healing complete for {Pod}. PR opened: {PrOpened} {PrUrl}",
                result.PodName, result.PrOpened, result.PrUrl ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Healing process failed for {Pod}", failure.PodName);
        }
        finally
        {
            await _lock.WaitAsync();
            try { _processing.Remove(key); }
            finally { _lock.Release(); }
        }
    }

    public IReadOnlyList<HealingResult> GetHealingHistory() => _history.AsReadOnly();
}