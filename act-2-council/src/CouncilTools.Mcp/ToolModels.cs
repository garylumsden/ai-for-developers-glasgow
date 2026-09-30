namespace CouncilTools.Mcp;

public sealed record ToolEnvelope<T>(
    string Source,
    DateTimeOffset RetrievedAt,
    T Data,
    string? Warning = null);

public sealed record AzurePriceMeter(
    string MeterName,
    string ProductName,
    string SkuName,
    string Region,
    string Currency,
    decimal UnitPrice,
    string UnitOfMeasure);

public sealed record AzurePriceData(
    string Model,
    string Region,
    IReadOnlyList<AzurePriceMeter> Meters);

public sealed record CostValue(
    bool Calculable,
    decimal? PerTask,
    decimal? PerThousandTasks,
    decimal? ForTaskCount,
    string Currency,
    string? Reason);

public sealed record CompareCostData(
    string Model,
    int? InputTokens,
    int? OutputTokens,
    int TaskCount,
    CostValue Cloud,
    CostValue Local,
    IReadOnlyDictionary<string, decimal> LocalAssumptions);

public sealed record ModelRegisterEntry(
    string Model,
    string Provider,
    string Hosting,
    IReadOnlyList<string> ApprovedDataClassifications,
    string Status,
    string Conditions);

public sealed record ModelRegisterData(
    string Query,
    ModelRegisterEntry? Match,
    string Note);

public sealed record ProductLifecycleData(
    string Product,
    string Version,
    string? ReleaseDate,
    string? EndOfLife,
    string? Latest,
    string? LatestReleaseDate,
    bool? LongTermSupport);

public sealed record RepoCommit(string Sha, string Message, string? Date, string Url);

public sealed record RepoActivityData(
    string Repository,
    int Stars,
    int Forks,
    int OpenIssues,
    string? LatestRelease,
    string? LatestReleaseUrl,
    string? UpdatedAt,
    IReadOnlyList<RepoCommit> RecentCommits);

public sealed record LocalCostConfig(
    string Currency,
    decimal HardwarePurchasePrice,
    int AmortizedTasks,
    decimal WorkstationWatts,
    decimal ElectricityPricePerKwh);

public sealed record ToolDefaults(
    string AzureRegion,
    string GitHubRepository,
    string LifecycleProduct,
    string LifecycleVersion);

public sealed record ToolsConfig(LocalCostConfig LocalCost, ToolDefaults Defaults);

public sealed record RunRecord(
    string Runtime,
    string Model,
    double DurationSeconds,
    bool Succeeded,
    int? InputTokens,
    int? OutputTokens);
