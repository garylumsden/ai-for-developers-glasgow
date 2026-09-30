namespace CouncilTools.Mcp;

using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public sealed class CouncilToolDefinitions(CouncilToolService service)
{
    [McpServerTool(
        Name = "get_azure_model_price",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Get public Azure retail price meters for a model and Azure region.")]
    public Task<ToolEnvelope<AzurePriceData>> GetAzureModelPrice(
        [Description("Model or meter term, for example gpt-5.4.")] string model,
        [Description("Azure Retail Prices armRegionName. Defaults to uksouth.")] string region = "uksouth",
        CancellationToken cancellationToken = default) =>
        service.GetAzureModelPriceAsync(model, region, cancellationToken: cancellationToken);

    [McpServerTool(
        Name = "compare_run_costs",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Compare measured local run cost with Azure token cost. Unknown token counts remain unknown.")]
    public Task<ToolEnvelope<CompareCostData>> CompareRunCosts(
        [Description("Optional cloud model override.")] string? model = null,
        [Description("Optional observed input token count.")] int? inputTokens = null,
        [Description("Optional observed output token count.")] int? outputTokens = null,
        [Description("Number of tasks for the scaled cost.")] int taskCount = 1000,
        CancellationToken cancellationToken = default) =>
        service.CompareRunCostsAsync(model, inputTokens, outputTokens, taskCount, cancellationToken);

    [McpServerTool(
        Name = "check_model_register",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Check a fictional approved-model register used only for this demo.")]
    public Task<ToolEnvelope<ModelRegisterData>> CheckModelRegister(
        [Description("Model name.")] string model,
        [Description("Optional provider name.")] string? provider = null,
        CancellationToken cancellationToken = default) =>
        service.CheckModelRegisterAsync(model, provider, cancellationToken);

    [McpServerTool(
        Name = "get_product_lifecycle",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Get public support and end-of-life data from endoflife.date.")]
    public Task<ToolEnvelope<ProductLifecycleData>> GetProductLifecycle(
        [Description("endoflife.date product identifier, for example dotnet.")] string product,
        [Description("Product cycle or version, for example 10.")] string version,
        CancellationToken cancellationToken = default) =>
        service.GetProductLifecycleAsync(product, version, cancellationToken: cancellationToken);

    [McpServerTool(
        Name = "get_repo_activity",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Get recent public GitHub repository activity with a cached fallback.")]
    public Task<ToolEnvelope<RepoActivityData>> GetRepoActivity(
        [Description("Public repository in owner/name format.")] string repository = "garylumsden/copilocal",
        CancellationToken cancellationToken = default) =>
        service.GetRepoActivityAsync(repository, cancellationToken: cancellationToken);
}
