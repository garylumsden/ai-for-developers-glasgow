namespace CouncilTools.Mcp;

using ModelContextProtocol.Client;
using Microsoft.Extensions.Logging.Abstractions;

public static class McpSmokeRunner
{
    public static async Task<int> RunAsync()
    {
        var endpoint = Environment.GetEnvironmentVariable("COUNCIL_TOOLS_MCP_URL")
            ?? "http://127.0.0.1:5199/mcp";
        var http = new HttpClient();
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Council-Member", "smoke-member");
        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint),
                Name = "council-tools-smoke"
            },
            http,
            NullLoggerFactory.Instance,
            ownsHttpClient: true);
        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        var names = tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray();
        var expected = new[]
        {
            "check_model_register",
            "compare_run_costs",
            "get_azure_model_price",
            "get_product_lifecycle",
            "get_repo_activity"
        };
        if (!names.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException($"Unexpected MCP tool list: {string.Join(", ", names)}");

        var result = await client.CallToolAsync(
            "check_model_register",
            new Dictionary<string, object?>
            {
                ["model"] = "ibm/granite-4-h-tiny",
                ["provider"] = "lmstudio"
            });
        if (result.IsError == true)
            throw new InvalidOperationException("MCP tool call returned an error.");

        Console.WriteLine($"PASS: MCP endpoint listed and invoked {tools.Count} tools.");
        return 0;
    }
}
