namespace GovernanceCouncil.Agents.Runtime;

using System.Text.Json;
using System.Diagnostics;
using GovernanceCouncil.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;

internal sealed class McpToolProvider(
    ILoggerFactory loggerFactory,
    ILogger logger) : IAsyncDisposable
{
    private readonly Dictionary<string, McpClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyDictionary<string, McpClientTool>> _tools =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var server in Scenario.Current.McpServers)
        {
            if (server.Auth == "env-token"
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(server.TokenEnv!)))
            {
                if (server.Optional)
                {
                    logger.LogInformation(
                        "Optional MCP server {Server} skipped because {TokenEnv} is absent",
                        server.Id,
                        server.TokenEnv);
                    continue;
                }
                throw new InvalidOperationException(
                    $"MCP server '{server.Id}' requires environment variable '{server.TokenEnv}'.");
            }

            try
            {
                var http = new HttpClient(new CouncilMemberHeaderHandler())
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };
                if (server.Auth == "env-token")
                {
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue(
                            "Bearer",
                            Environment.GetEnvironmentVariable(server.TokenEnv!));
                }
                var transport = new HttpClientTransport(
                    new HttpClientTransportOptions
                    {
                        Endpoint = new Uri(server.Url),
                        Name = server.Id,
                        ConnectionTimeout = TimeSpan.FromSeconds(Scenario.Current.McpPolicy.TimeoutSeconds)
                    },
                    http,
                    loggerFactory,
                    ownsHttpClient: true);
                var client = await McpClient.CreateAsync(
                    transport,
                    loggerFactory: loggerFactory,
                    cancellationToken: cancellationToken);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Scenario.Current.McpPolicy.TimeoutSeconds));
                var listed = await client.ListToolsAsync(cancellationToken: timeout.Token);
                _clients.Add(server.Id, client);
                _tools.Add(server.Id, listed.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase));
                logger.LogInformation("MCP server {Server} connected with {Count} tools", server.Id, listed.Count);
            }
            catch (Exception exception) when (server.Optional && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Optional MCP server {Server} is unavailable and was skipped", server.Id);
            }
        }
    }

    public McpMemberTools GetToolsForMember(CouncilMember member)
    {
        if (member.Id.Equals("moderator", StringComparison.OrdinalIgnoreCase)
            || member.Tier == CouncilModels.ModelTier.Fast)
            return McpMemberTools.Empty;

        var persona = Scenario.Current.Council.Members
            .Concat([Scenario.Current.Council.Chair, Scenario.Current.Council.NexusAnalyst])
            .FirstOrDefault(candidate => candidate.Id.Equals(member.Id, StringComparison.OrdinalIgnoreCase));
        if (persona is null || persona.McpTools.Count == 0) return McpMemberTools.Empty;

        var budget = new McpCallBudget();
        var functions = new List<AITool>();
        foreach (var access in persona.McpTools)
        {
            if (!_tools.TryGetValue(access.Server, out var available))
                continue;
            foreach (var allowed in access.Allow)
            {
                if (!available.TryGetValue(allowed, out var tool))
                    throw new InvalidOperationException(
                        $"MCP server '{access.Server}' does not expose allowed tool '{allowed}' for '{member.Id}'.");
                functions.Add(new BudgetedMcpFunction(
                    tool,
                    budget,
                    TimeSpan.FromSeconds(Scenario.Current.McpPolicy.TimeoutSeconds),
                    SnapshotPath(access.Server, allowed),
                    access.Server,
                    member.Id,
                    logger));
            }
        }

        return functions.Count == 0 ? McpMemberTools.Empty : new McpMemberTools(functions, budget);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
            await client.DisposeAsync();
        _clients.Clear();
        _tools.Clear();
    }

    private static string? SnapshotPath(string server, string tool)
    {
        if (!server.Equals("council-tools", StringComparison.OrdinalIgnoreCase)) return null;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "act-2-council",
                "src",
                "CouncilTools.Mcp",
                "snapshots",
                $"{tool.Replace('_', '-')}.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}

internal sealed record McpMemberTools(IList<AITool> Tools, McpCallBudget? Budget)
{
    public static McpMemberTools Empty { get; } = new([], null);
}

internal sealed class McpCallBudget
{
    public int Remaining;
}

internal sealed class BudgetedMcpFunction(
    AIFunction inner,
    McpCallBudget budget,
    TimeSpan timeout,
    string? snapshotPath,
    string serverId,
    string memberId,
    ILogger logger) : DelegatingAIFunction(inner)
{
    private static readonly ActivitySource Activities = new("GovernanceCouncil.Mcp");

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Decrement(ref budget.Remaining) < 0)
            return "MCP tool budget reached for this turn. Use the results already gathered.";

        using var activity = Activities.StartActivity("mcp.call", ActivityKind.Client);
        activity?.SetTag("mcp.server", serverId);
        activity?.SetTag("mcp.tool.name", Name);
        activity?.SetTag("gen_ai.agent.name", memberId);
        var stopwatch = Stopwatch.StartNew();
        var previousMember = McpCallContext.Member.Value;
        McpCallContext.Member.Value = memberId;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var result = await InnerFunction.InvokeAsync(arguments, timeoutSource.Token);
            activity?.SetTag("mcp.result.source", FindSource(result) ?? "unknown");
            activity?.SetTag("mcp.success", true);
            return result;
        }
        catch (OperationCanceledException) when (
            timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var result = LoadSnapshot("MCP tool timed out.");
            activity?.SetTag("mcp.result.source", "cached");
            activity?.SetTag("mcp.success", snapshotPath is not null);
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or McpException)
        {
            logger.LogWarning(exception, "MCP tool {Tool} failed", Name);
            var result = LoadSnapshot("MCP server was unavailable.");
            activity?.SetTag("mcp.result.source", "cached");
            activity?.SetTag("mcp.success", snapshotPath is not null);
            return result;
        }
        finally
        {
            McpCallContext.Member.Value = previousMember;
            stopwatch.Stop();
            activity?.SetTag("mcp.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private object LoadSnapshot(string warning)
    {
        if (snapshotPath is null || !File.Exists(snapshotPath))
            return $"{warning} No cached snapshot is available.";

        using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
        var root = document.RootElement.Clone();
        return new
        {
            source = "cached",
            retrievedAt = DateTimeOffset.UtcNow,
            data = root.TryGetProperty("data", out var data) ? data : root,
            warning
        };
    }

    private static string? FindSource(object? value)
    {
        if (value is null) return null;
        try
        {
            var json = JsonSerializer.Serialize(value);
            using var document = JsonDocument.Parse(json);
            return FindSource(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindSource(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals("source", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
                var nested = FindSource(property.Value);
                if (nested is not null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindSource(item);
                if (nested is not null) return nested;
            }
        }
        return null;
    }
}

internal static class McpCallContext
{
    public static AsyncLocal<string?> Member { get; } = new();
}

internal sealed class CouncilMemberHeaderHandler : HttpClientHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (McpCallContext.Member.Value is { Length: > 0 } member)
        {
            request.Headers.Remove("X-Council-Member");
            request.Headers.TryAddWithoutValidation("X-Council-Member", member);
        }
        return base.SendAsync(request, cancellationToken);
    }
}

internal sealed class McpBudgetResetChatClient(
    IChatClient inner,
    McpCallBudget budget,
    int maximumCalls) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        budget.Remaining = maximumCalls;
        return base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        budget.Remaining = maximumCalls;
        return base.GetStreamingResponseAsync(messages, options, cancellationToken);
    }
}
