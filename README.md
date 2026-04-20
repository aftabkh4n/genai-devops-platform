# genai-devops-platform

Your Kubernetes cluster just fixed itself. No, really.

This is a .NET system that watches your Kubernetes cluster, detects when something crashes, asks Claude AI what went wrong, and opens a GitHub PR with a suggested fix. All automatically. While you sleep.

## What actually happens

A pod crashes. Maybe the database is unreachable. Maybe someone pushed a broken image. Maybe the container ran out of memory.

The system detects it instantly. It pulls the last 100 lines of logs from that pod. It sends everything to Claude with the failure details. Claude reads the logs, figures out the root cause, rates the severity, and writes a step by step fix. The system creates a branch, commits the fix as a markdown file, and opens a PR on your GitHub repo.

You wake up, see a PR waiting, read the analysis, and decide whether to apply it. The hard part is already done.

Here is a real example. A pod crashed because it could not connect to PostgreSQL. Claude wrote this:

> "The application failed to establish a connection to the PostgreSQL database at postgres://db:5432. The pod crashed with exit code 1. Verify the PostgreSQL service named 'db' exists and is running: kubectl get svc db -n default. Check if the PostgreSQL pod is healthy: kubectl get pods -n default -l app=db."

And then it generated the Kubernetes Service YAML to fix the service discovery. In a PR. Automatically.

## How it works

There are four parts:

**KubernetesWatcher** runs as a background service and streams events from your cluster. When it sees a pod enter CrashLoopBackOff, OOMKilled, ImagePullError, or just exit with a non-zero code, it hands the failure to the orchestrator.

**ClaudeAnalyser** takes the failure details and pod logs, builds a prompt, and calls the Claude API. It asks for root cause, severity, a plain English fix, and the actual code or config change needed. It gets back structured JSON.

**GitHubService** creates a branch, commits the analysis as a markdown file, and opens a PR. The PR body has everything: what crashed, why it crashed, how to fix it, and what code to change.

**HealingOrchestrator** coordinates the three above. It also makes sure the same failure does not trigger multiple simultaneous healing attempts, which would open duplicate PRs.

## Stack

- .NET 10
- KubernetesClient
- Anthropic.SDK
- Octokit (GitHub API)
- Serilog

## Running locally

You need .NET 10, Docker Desktop with Kubernetes enabled, an Anthropic API key, and a GitHub personal access token with repo permissions.

Clone the repo:

```bash
git clone https://github.com/aftabkh4n/genai-devops-platform.git
cd genai-devops-platform/DeploymentService
```

Add your keys to appsettings.json:

```json
{
  "AppSettings": {
    "AnthropicApiKey": "sk-ant-...",
    "GitHubToken": "github_pat_...",
    "GitHubRepoOwner": "your-github-username",
    "GitHubRepoName": "your-repo-name",
    "KubernetesNamespace": "default"
  }
}
```

Run it:

```bash
dotnet run
```

You will see:

```
[INF] Kubernetes watcher started. Watching namespace: default
[INF] Now listening on: http://localhost:5271
```

Now deploy something that crashes on purpose to test it:

```bash
kubectl apply -f crash-test.yaml
```

Watch the console. Within a few seconds you will see the failure detected, Claude called, and a PR URL printed. Go check your GitHub repo.

## API endpoints

```
GET /health       Returns status and timestamp
GET /healings     Returns the history of all healing attempts
```

## Failure types detected

- CrashLoopBackOff
- OOMKilled (container ran out of memory)
- ImagePullBackOff and ErrImagePull
- Any container exit with a non-zero code after restarts

## Contributing

Pull requests welcome. If you find a failure type it misses, open an issue.

---

Built with .NET 10 and Claude AI. MIT licence.