namespace GovernanceCouncil.Core.Models;

public static class ScenarioConfigValidator
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> KnownTools =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["council-tools"] =
            [
                "get_azure_model_price",
                "compare_run_costs",
                "check_model_register",
                "get_product_lifecycle",
                "get_repo_activity"
            ]
        };

    public static void Validate(ScenarioConfig scenario)
    {
        if (scenario.McpPolicy.MaxCallsPerTurn is < 1 or > 10)
            throw new InvalidOperationException("mcpPolicy.maxCallsPerTurn must be between 1 and 10.");
        if (scenario.McpPolicy.TimeoutSeconds is < 1 or > 60)
            throw new InvalidOperationException("mcpPolicy.timeoutSeconds must be between 1 and 60.");

        var servers = new Dictionary<string, McpServerConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var server in scenario.McpServers)
        {
            if (string.IsNullOrWhiteSpace(server.Id))
                throw new InvalidOperationException("Every MCP server requires an id.");
            if (!servers.TryAdd(server.Id, server))
                throw new InvalidOperationException($"MCP server id '{server.Id}' is duplicated.");
            if (!server.Transport.Equals("http", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"MCP server '{server.Id}' uses unsupported transport '{server.Transport}'.");
            if (!Uri.TryCreate(server.Url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https"))
                throw new InvalidOperationException($"MCP server '{server.Id}' requires an absolute HTTP or HTTPS url.");
            if (server.Auth.Equals("entra", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"MCP server '{server.Id}' uses entra auth, which is not implemented. Use none or env-token.");
            if (server.Auth is not ("none" or "env-token"))
                throw new InvalidOperationException($"MCP server '{server.Id}' has unsupported auth '{server.Auth}'.");
            if (server.Auth == "env-token" && string.IsNullOrWhiteSpace(server.TokenEnv))
                throw new InvalidOperationException($"MCP server '{server.Id}' requires tokenEnv for env-token auth.");
            if (server.Auth != "none" && uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException(
                    $"Authenticated MCP server '{server.Id}' must use HTTPS.");
            if (uri.Scheme == Uri.UriSchemeHttp
                && (!uri.IsLoopback || server.Auth != "none"))
                throw new InvalidOperationException(
                    $"Plain HTTP MCP server '{server.Id}' is allowed only on loopback with auth 'none'.");
        }

        foreach (var persona in AllPersonas(scenario))
        {
            if (persona.Id.Equals("moderator", StringComparison.OrdinalIgnoreCase)
                && persona.McpTools.Count > 0)
                throw new InvalidOperationException("The Moderator cannot receive MCP tools.");

            var duplicateServers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var access in persona.McpTools)
            {
                if (!servers.ContainsKey(access.Server))
                    throw new InvalidOperationException(
                        $"Persona '{persona.Id}' references unknown MCP server '{access.Server}'.");
                if (!duplicateServers.Add(access.Server))
                    throw new InvalidOperationException(
                        $"Persona '{persona.Id}' repeats MCP server '{access.Server}'.");
                if (access.Allow.Count == 0)
                    throw new InvalidOperationException(
                        $"Persona '{persona.Id}' requires a non-empty allow-list for '{access.Server}'.");
                if (access.Allow.Any(tool => tool.Contains('*')))
                    throw new InvalidOperationException(
                        $"Persona '{persona.Id}' cannot use wildcard MCP tool allow-lists.");
                if (access.Allow.Count != access.Allow.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                    throw new InvalidOperationException(
                        $"Persona '{persona.Id}' repeats a tool in the '{access.Server}' allow-list.");
                if (KnownTools.TryGetValue(access.Server, out var known))
                {
                    var unknown = access.Allow.Where(tool => !known.Contains(tool)).ToArray();
                    if (unknown.Length > 0)
                        throw new InvalidOperationException(
                            $"Persona '{persona.Id}' references unknown tools on '{access.Server}': " +
                            string.Join(", ", unknown));
                }
            }
        }
    }

    private static IEnumerable<PersonaConfig> AllPersonas(ScenarioConfig scenario)
    {
        yield return scenario.Council.Chair;
        yield return scenario.Council.Moderator;
        yield return scenario.Council.NexusAnalyst;
        foreach (var member in scenario.Council.Members) yield return member;
    }
}
