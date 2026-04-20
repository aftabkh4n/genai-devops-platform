using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using DeploymentService.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Anthropic.SDK.Common;

namespace DeploymentService.Services;

public class ClaudeAnalyser
{
    private readonly ILogger<ClaudeAnalyser> _logger;
    private readonly AnthropicClient _client;

    public ClaudeAnalyser(
        ILogger<ClaudeAnalyser> logger,
        IOptions<AppSettings> settings)
    {
        _logger = logger;
        _client = new AnthropicClient(new APIAuthentication(settings.Value.AnthropicApiKey));
    }

    public async Task<HealingResult> AnalyseFailureAsync(FailureEvent failure)
    {
        _logger.LogInformation("Sending failure to Claude for analysis: {Pod}", failure.PodName);

        var prompt = BuildPrompt(failure);

        try
        {
            var response = await _client.Messages.GetClaudeMessageAsync(
                new MessageParameters
                {
                    Model = "claude-haiku-4-5-20251001",
                    MaxTokens = 2048,
                    Messages = new List<Message>
                    {
                        new Message
                        {
                            Role = RoleType.User,
                            Content = new List<ContentBase>
                            {
                                new TextContent { Text = prompt }
                            }
                        }
                    }
                });

            var content = response.Content
                .OfType<TextContent>()
                .FirstOrDefault()?.Text ?? string.Empty;

            return ParseResponse(content, failure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude analysis failed for pod {Pod}", failure.PodName);
            return new HealingResult
            {
                PodName = failure.PodName,
                RootCause = "Analysis unavailable",
                Severity = "Unknown",
                SuggestedFix = "Manual investigation required",
                OriginalFailure = failure
            };
        }
    }

    private string BuildPrompt(FailureEvent failure)
    {
        return "You are a Kubernetes expert and .NET engineer analysing a production failure.\n\n" +
               "Analyse this Kubernetes pod failure and respond ONLY with valid JSON.\n\n" +
               "FAILURE DETAILS:\n" +
               $"Pod: {failure.PodName}\n" +
               $"Namespace: {failure.Namespace}\n" +
               $"Deployment: {failure.DeploymentName}\n" +
               $"Failure Type: {failure.Type}\n" +
               $"Reason: {failure.Reason}\n" +
               $"Message: {failure.Message}\n\n" +
               "POD LOGS (last 100 lines):\n" +
               $"{failure.Logs}\n\n" +
               "Respond with this exact JSON structure:\n" +
               "{\n" +
               "    \"rootCause\": \"Clear explanation of what caused the failure\",\n" +
               "    \"severity\": \"Critical|High|Medium|Low\",\n" +
               "    \"suggestedFix\": \"Step by step fix in plain English\",\n" +
               "    \"codeFix\": \"If applicable, the actual code or config change needed. Otherwise empty string.\",\n" +
               "    \"fixType\": \"config|code|resources|image|none\"\n" +
               "}\n\n" +
               "Be specific. Reference actual log lines where relevant.";
    }

    private HealingResult ParseResponse(string content, FailureEvent failure)
    {
        try
        {
            var clean = content
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var json = JsonSerializer.Deserialize<JsonElement>(clean);

            return new HealingResult
            {
                PodName = failure.PodName,
                RootCause = json.GetProperty("rootCause").GetString() ?? "",
                Severity = json.GetProperty("severity").GetString() ?? "",
                SuggestedFix = json.GetProperty("suggestedFix").GetString() ?? "",
                CodeFix = json.GetProperty("codeFix").GetString() ?? "",
                OriginalFailure = failure
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response");
            return new HealingResult
            {
                PodName = failure.PodName,
                RootCause = content,
                Severity = "Unknown",
                SuggestedFix = "See root cause for details",
                OriginalFailure = failure
            };
        }
    }
}