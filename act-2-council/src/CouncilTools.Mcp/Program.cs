using Azure.Monitor.OpenTelemetry.AspNetCore;
using CouncilTools.Mcp;
using OpenTelemetry.Trace;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
    return await SelfTestRunner.RunAsync();

if (args.Contains("--mcp-smoke", StringComparer.OrdinalIgnoreCase))
    return await McpSmokeRunner.RunAsync();

var repositoryPaths = RepositoryPaths.Find();
EnvFileLoader.Load(Path.Combine(
    repositoryPaths.Council,
    "src",
    "GovernanceCouncil.Web",
    ".env"));

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();
builder.Services.AddSingleton(repositoryPaths);
builder.Services.AddSingleton<SnapshotStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<CouncilToolService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ai-for-developers-glasgow-council-tools/1.0");
});
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPINSIGHTS_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddSource(CouncilToolService.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .UseAzureMonitor(options => options.ConnectionString = appInsightsConnectionString);
}

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "council-tools",
    telemetryConfigured = !string.IsNullOrWhiteSpace(appInsightsConnectionString),
    retrievedAt = DateTimeOffset.UtcNow
}));
app.MapMcp("/mcp");

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
await app.RunAsync(string.IsNullOrWhiteSpace(urls) ? "http://127.0.0.1:5199" : urls);
return 0;
