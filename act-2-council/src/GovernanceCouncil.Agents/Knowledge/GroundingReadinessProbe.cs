namespace GovernanceCouncil.Agents.Knowledge;

using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using GovernanceCouncil.Core.Models;
using Microsoft.Extensions.Logging;

public sealed class GroundingReadinessProbe(
    HttpClient http,
    TokenCredential credential,
    string? searchEndpoint,
    string knowledgeBaseName,
    string? webIqApiKey,
    ILogger<GroundingReadinessProbe> logger)
{
    private const string SearchAudience = "https://search.azure.com/.default";
    private const string KbApiVersion = "2026-05-01-preview";
    private static readonly string WebIqRestUrl =
        Environment.GetEnvironmentVariable("WEBIQ_REST_URL")?.Trim() is { Length: > 0 } value
            ? value
            : "https://api.microsoft.ai/v3/search/web";

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (Scenario.Current.GroundingDomains.Count == 0)
        {
            logger.LogInformation("Grounding readiness: ungrounded. No authoritative domains are configured");
            return;
        }

        logger.LogInformation("Grounding readiness: configured. Provider={Provider}",
            Grounding.DisplayName(Grounding.Active));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            switch (Grounding.Active)
            {
                case Grounding.Provider.WebIq:
                    await ProbeWebIqAsync(timeout.Token);
                    break;
                case Grounding.Provider.FoundryIq:
                    await ProbeFoundryIqAsync(timeout.Token);
                    break;
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning("Grounding readiness: degraded. Provider probe timed out after 20 seconds");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Grounding readiness: degraded. Provider request failed");
        }
        catch (AuthenticationFailedException exception)
        {
            logger.LogWarning(exception, "Grounding readiness: degraded. Azure authentication failed");
        }
    }

    private async Task ProbeWebIqAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webIqApiKey))
        {
            logger.LogWarning("Grounding readiness: degraded. Web IQ is selected but WEBIQ_API_KEY is absent");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, WebIqRestUrl);
        request.Headers.TryAddWithoutValidation("x-apikey", webIqApiKey);
        request.Content = JsonContent.Create(new
        {
            query = "Microsoft developer guidance",
            includeDomains = Scenario.Current.GroundingDomains,
            contentFormat = "passage",
            maxResults = 1,
            maxLength = 300
        });
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Grounding readiness: degraded. Web IQ returned {Status}", response.StatusCode);
            return;
        }

        logger.LogInformation("Grounding readiness: usable. Web IQ capability probe passed");
    }

    private async Task ProbeFoundryIqAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchEndpoint))
        {
            logger.LogWarning("Grounding readiness: degraded. Foundry IQ is selected but SEARCH_SERVICE_ENDPOINT is absent");
            return;
        }

        var token = await credential.GetTokenAsync(new TokenRequestContext([SearchAudience]), cancellationToken);
        var endpoint = searchEndpoint.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{endpoint}/knowledgebases/{knowledgeBaseName}/retrieve?api-version={KbApiVersion}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = JsonContent.Create(new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "What does the fictional sample policy require before generated changes are merged?"
                        }
                    }
                }
            }
        });
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Grounding readiness: degraded. Foundry IQ returned {Status}: {Body}",
                response.StatusCode, Trim(body));
            return;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        logger.LogInformation("Grounding readiness: usable. Foundry IQ capability probe passed");
    }

    private static string Trim(string value) => value.Length <= 200 ? value : value[..200];
}
