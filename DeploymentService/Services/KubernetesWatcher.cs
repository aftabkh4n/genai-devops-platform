using k8s;
using k8s.Models;
using DeploymentService.Models;
using Microsoft.Extensions.Options;

namespace DeploymentService.Services;

public class KubernetesWatcher : BackgroundService
{
    private readonly ILogger<KubernetesWatcher> _logger;
    private readonly HealingOrchestrator _orchestrator;
    private readonly AppSettings _settings;
    private readonly IKubernetes _client;

    public KubernetesWatcher(
        ILogger<KubernetesWatcher> logger,
        HealingOrchestrator orchestrator,
        IOptions<AppSettings> settings)
    {
        _logger = logger;
        _orchestrator = orchestrator;
        _settings = settings.Value;

        var config = KubernetesClientConfiguration.IsInCluster()
            ? KubernetesClientConfiguration.InClusterConfig()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile();

        _client = new Kubernetes(config);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kubernetes watcher started. Watching namespace: {Namespace}", 
            _settings.KubernetesNamespace);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WatchPodsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Watcher disconnected, reconnecting in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task WatchPodsAsync(CancellationToken stoppingToken)
    {
        var response = _client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
            _settings.KubernetesNamespace,
            watch: true,
            cancellationToken: stoppingToken);

        await foreach (var (type, pod) in response.WatchAsync<V1Pod, V1PodList>(
            onError: e => _logger.LogError(e, "Watch error"),
            cancellationToken: stoppingToken))
        {
            if (type == WatchEventType.Modified)
            {
                await CheckPodHealthAsync(pod);
            }
        }
    }

    private async Task CheckPodHealthAsync(V1Pod pod)
    {
        var containerStatuses = pod.Status?.ContainerStatuses;
        if (containerStatuses == null) return;

        foreach (var status in containerStatuses)
        {
            var failure = DetectFailure(pod, status);
            if (failure != null)
            {
                _logger.LogWarning("Failure detected in pod {Pod}: {Reason}", 
                    pod.Name(), failure.Reason);

                failure.Logs = await GetPodLogsAsync(pod.Name(), pod.Namespace());
                await _orchestrator.HandleFailureAsync(failure);
            }
        }
    }

    private FailureEvent? DetectFailure(V1Pod pod, V1ContainerStatus status)
    {
        var waiting = status.State?.Waiting;
        var terminated = status.State?.Terminated;

        if (waiting?.Reason == "CrashLoopBackOff")
            return CreateFailure(pod, FailureType.CrashLoopBackOff, waiting.Reason, waiting.Message ?? "");

        if (waiting?.Reason == "ImagePullBackOff" || waiting?.Reason == "ErrImagePull")
            return CreateFailure(pod, FailureType.ImagePullError, waiting.Reason, waiting.Message ?? "");

        if (terminated?.Reason == "OOMKilled")
            return CreateFailure(pod, FailureType.OOMKilled, "OOMKilled", "Container exceeded memory limit");

        if (terminated?.ExitCode > 0 && status.RestartCount > 0)
            return CreateFailure(pod, FailureType.PodCrash, "CrashExit",
                $"Container exited with code {terminated.ExitCode}");

        return null;
    }

    private FailureEvent CreateFailure(V1Pod pod, FailureType type, string reason, string message)
    {
        return new FailureEvent
        {
            PodName = pod.Name(),
            Namespace = pod.Namespace(),
            DeploymentName = pod.GetLabel("app") ?? pod.Name(),
            Reason = reason,
            Message = message,
            Type = type,
            DetectedAt = DateTime.UtcNow
        };
    }

    private async Task<string> GetPodLogsAsync(string podName, string namespaceName)
    {
        try
        {
            var stream = await _client.CoreV1.ReadNamespacedPodLogAsync(
                podName, namespaceName, tailLines: 100);

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch logs for pod {Pod}", podName);
            return "Logs unavailable";
        }
    }
}