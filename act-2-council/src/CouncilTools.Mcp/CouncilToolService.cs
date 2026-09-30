namespace CouncilTools.Mcp;

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

public sealed class CouncilToolService(
    HttpClient http,
    RepositoryPaths paths,
    SnapshotStore snapshots,
    ILogger<CouncilToolService> logger,
    IHttpContextAccessor? httpContextAccessor = null)
{
    public const string ActivitySourceName = "CouncilTools.Mcp";
    private static readonly ActivitySource Activities = new(ActivitySourceName);
    private static readonly Regex SafeTerm = new("^[A-Za-z0-9._/-]{1,120}$", RegexOptions.Compiled);
    private static readonly Regex SafeRepo = new(
        "^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9_.-]{1,100}$",
        RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ToolEnvelope<AzurePriceData>> GetAzureModelPriceAsync(
        string model,
        string region = "uksouth",
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ValidateTerm(model, nameof(model));
        ValidateTerm(region, nameof(region));
        const string snapshotName = "get-azure-model-price";
        if (!forceRefresh && Fresh<AzurePriceData>(
                snapshotName,
                TimeSpan.FromHours(24),
                data => data.Model.Equals(model, StringComparison.OrdinalIgnoreCase)
                    && data.Region.Equals(region, StringComparison.OrdinalIgnoreCase)) is { } fresh)
            return fresh with { Source = "cached", Warning = "Using a fresh cached retail-price response." };

        using var activity = Start("get_azure_model_price");
        try
        {
            var searchTerm = model.Replace("gpt-", "", StringComparison.OrdinalIgnoreCase);
            var filter = Uri.EscapeDataString(
                $"armRegionName eq '{region}' and contains(meterName, '{searchTerm}') and priceType eq 'Consumption'");
            using var response = await http.GetAsync(
                $"https://prices.azure.com/api/retail/prices?$filter={filter}",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var meters = document.RootElement.GetProperty("Items").EnumerateArray()
                .Select(item => new AzurePriceMeter(
                    GetString(item, "meterName"),
                    GetString(item, "productName"),
                    GetString(item, "skuName"),
                    GetString(item, "armRegionName"),
                    GetString(item, "currencyCode"),
                    item.TryGetProperty("unitPrice", out var price) ? price.GetDecimal() : 0,
                    GetString(item, "unitOfMeasure")))
                .Where(meter => meter.UnitPrice > 0)
                .Take(50)
                .ToArray();
            if (meters.Length == 0)
                throw new InvalidOperationException($"No Azure retail price meters matched {model} in {region}.");

            var envelope = new ToolEnvelope<AzurePriceData>(
                "live",
                DateTimeOffset.UtcNow,
                new AzurePriceData(model, region, meters));
            await snapshots.SaveAsync(snapshotName, envelope, cancellationToken);
            Complete(activity, envelope.Source, true);
            return envelope;
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            var cached = RequireSnapshot<AzurePriceData>(
                snapshotName,
                exception.Message,
                data => data.Model.Equals(model, StringComparison.OrdinalIgnoreCase)
                    && data.Region.Equals(region, StringComparison.OrdinalIgnoreCase),
                $"{model} in {region}");
            Complete(activity, cached.Source, true);
            return cached;
        }
    }

    public async Task<ToolEnvelope<CompareCostData>> CompareRunCostsAsync(
        string? model = null,
        int? inputTokens = null,
        int? outputTokens = null,
        int taskCount = 1000,
        CancellationToken cancellationToken = default)
    {
        if (taskCount is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(taskCount), "taskCount must be between 1 and 1,000,000.");
        if (model is not null) ValidateTerm(model, nameof(model));
        if (inputTokens is < 0 || outputTokens is < 0)
            throw new ArgumentOutOfRangeException(nameof(inputTokens), "Token counts cannot be negative.");

        using var activity = Start("compare_run_costs");
        var config = LoadConfig();
        var runs = LoadRuns();
        var cloudRun = runs.LastOrDefault(run => run.Runtime == "cloud" && run.Succeeded);
        var localRuns = runs.Where(run => run.Runtime == "local" && run.Succeeded).TakeLast(3).ToArray();
        var selectedModel = model ?? cloudRun?.Model ?? "gpt-5.4";
        var selectedInput = inputTokens ?? cloudRun?.InputTokens;
        var selectedOutput = outputTokens ?? cloudRun?.OutputTokens;

        var localAssumptions = new Dictionary<string, decimal>
        {
            ["hardwarePurchasePrice"] = config.LocalCost.HardwarePurchasePrice,
            ["amortizedTasks"] = config.LocalCost.AmortizedTasks,
            ["workstationWatts"] = config.LocalCost.WorkstationWatts,
            ["electricityPricePerKwh"] = config.LocalCost.ElectricityPricePerKwh
        };

        var local = CalculateLocalCost(config.LocalCost, localRuns);
        CostValue cloud;
        var source = "live";
        if (selectedInput is null || selectedOutput is null)
        {
            cloud = new CostValue(false, null, null, null, "USD",
                "Input or output tokens are unknown. The cloud cost cannot be calculated.");
        }
        else
        {
            var prices = await GetAzureModelPriceAsync(selectedModel, config.Defaults.AzureRegion,
                cancellationToken: cancellationToken);
            source = prices.Source;
            cloud = CalculateCloudCost(prices.Data.Meters, selectedInput.Value, selectedOutput.Value);
        }

        var data = new CompareCostData(
            selectedModel,
            selectedInput,
            selectedOutput,
            taskCount,
            Scale(cloud, taskCount),
            Scale(local, taskCount),
            localAssumptions);
        var envelope = new ToolEnvelope<CompareCostData>(source, DateTimeOffset.UtcNow, data);
        await snapshots.SaveAsync("compare-run-costs", envelope, cancellationToken);
        Complete(activity, envelope.Source, true);
        return envelope;
    }

    public async Task<ToolEnvelope<ModelRegisterData>> CheckModelRegisterAsync(
        string model,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTerm(model, nameof(model));
        if (provider is not null) ValidateTerm(provider, nameof(provider));
        using var activity = Start("check_model_register");
        var entries = JsonSerializer.Deserialize<List<ModelRegisterEntry>>(
            await File.ReadAllTextAsync(Path.Combine(paths.Config, "model-register.json"), cancellationToken),
            JsonOptions) ?? [];
        var match = entries.FirstOrDefault(entry =>
            entry.Model.Equals(model, StringComparison.OrdinalIgnoreCase)
            && (provider is null || entry.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase)));
        var data = new ModelRegisterData(
            provider is null ? model : $"{provider}/{model}",
            match,
            "This fictional register is demo data. MCP is a protocol, not a hosted service.");
        var envelope = new ToolEnvelope<ModelRegisterData>("live", DateTimeOffset.UtcNow, data);
        await snapshots.SaveAsync("check-model-register", envelope, cancellationToken);
        Complete(activity, envelope.Source, true);
        return envelope;
    }

    public async Task<ToolEnvelope<ProductLifecycleData>> GetProductLifecycleAsync(
        string product,
        string version,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ValidateTerm(product, nameof(product));
        ValidateTerm(version, nameof(version));
        const string snapshotName = "get-product-lifecycle";
        if (!forceRefresh && Fresh<ProductLifecycleData>(
                snapshotName,
                TimeSpan.FromHours(24),
                data => data.Product.Equals(product, StringComparison.OrdinalIgnoreCase)
                    && data.Version.Equals(version, StringComparison.OrdinalIgnoreCase)) is { } fresh)
            return fresh with { Source = "cached", Warning = "Using a fresh cached lifecycle response." };

        using var activity = Start("get_product_lifecycle");
        try
        {
            using var response = await http.GetAsync($"https://endoflife.date/api/{product}.json", cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var release = document.RootElement.EnumerateArray().FirstOrDefault(item =>
                GetString(item, "cycle").Equals(version, StringComparison.OrdinalIgnoreCase)
                || GetString(item, "cycle").StartsWith($"{version}.", StringComparison.OrdinalIgnoreCase));
            if (release.ValueKind == JsonValueKind.Undefined)
                throw new InvalidOperationException($"No lifecycle entry matched {product} {version}.");
            var data = new ProductLifecycleData(
                product,
                version,
                GetNullableString(release, "releaseDate"),
                GetFlexibleString(release, "eol"),
                GetNullableString(release, "latest"),
                GetNullableString(release, "latestReleaseDate"),
                GetNullableBoolean(release, "lts"));
            var envelope = new ToolEnvelope<ProductLifecycleData>("live", DateTimeOffset.UtcNow, data);
            await snapshots.SaveAsync(snapshotName, envelope, cancellationToken);
            Complete(activity, envelope.Source, true);
            return envelope;
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            var cached = RequireSnapshot<ProductLifecycleData>(
                snapshotName,
                exception.Message,
                data => data.Product.Equals(product, StringComparison.OrdinalIgnoreCase)
                    && data.Version.Equals(version, StringComparison.OrdinalIgnoreCase),
                $"{product} {version}");
            Complete(activity, cached.Source, true);
            return cached;
        }
    }

    public async Task<ToolEnvelope<RepoActivityData>> GetRepoActivityAsync(
        string repository = "garylumsden/copilocal",
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!SafeRepo.IsMatch(repository) || repository.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("repository must use the owner/name format.", nameof(repository));
        const string snapshotName = "get-repo-activity";
        if (!forceRefresh && Fresh<RepoActivityData>(
                snapshotName,
                TimeSpan.FromMinutes(15),
                data => data.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)) is { } fresh)
            return fresh with { Source = "cached", Warning = "Using a fresh cached GitHub response." };

        using var activity = Start("get_repo_activity");
        try
        {
            using var repoResponse = await http.GetAsync($"https://api.github.com/repos/{repository}", cancellationToken);
            repoResponse.EnsureSuccessStatusCode();
            using var repoDocument = JsonDocument.Parse(await repoResponse.Content.ReadAsStringAsync(cancellationToken));
            using var commitsResponse = await http.GetAsync(
                $"https://api.github.com/repos/{repository}/commits?per_page=5",
                cancellationToken);
            commitsResponse.EnsureSuccessStatusCode();
            using var commitsDocument = JsonDocument.Parse(await commitsResponse.Content.ReadAsStringAsync(cancellationToken));

            string? releaseName = null;
            string? releaseUrl = null;
            using var releaseResponse = await http.GetAsync(
                $"https://api.github.com/repos/{repository}/releases/latest",
                cancellationToken);
            if (releaseResponse.IsSuccessStatusCode)
            {
                using var releaseDocument = JsonDocument.Parse(
                    await releaseResponse.Content.ReadAsStringAsync(cancellationToken));
                releaseName = GetNullableString(releaseDocument.RootElement, "tag_name");
                releaseUrl = GetNullableString(releaseDocument.RootElement, "html_url");
            }
            else if (releaseResponse.StatusCode != HttpStatusCode.NotFound)
            {
                releaseResponse.EnsureSuccessStatusCode();
            }

            var commits = commitsDocument.RootElement.EnumerateArray().Select(item =>
            {
                var commit = item.GetProperty("commit");
                var author = commit.TryGetProperty("author", out var authorElement) ? authorElement : default;
                return new RepoCommit(
                    GetString(item, "sha")[..Math.Min(7, GetString(item, "sha").Length)],
                    GetString(commit, "message").Split('\n')[0],
                    author.ValueKind == JsonValueKind.Object ? GetNullableString(author, "date") : null,
                    GetString(item, "html_url"));
            }).ToArray();

            var root = repoDocument.RootElement;
            var data = new RepoActivityData(
                repository,
                root.GetProperty("stargazers_count").GetInt32(),
                root.GetProperty("forks_count").GetInt32(),
                root.GetProperty("open_issues_count").GetInt32(),
                releaseName,
                releaseUrl,
                GetNullableString(root, "updated_at"),
                commits);
            var envelope = new ToolEnvelope<RepoActivityData>("live", DateTimeOffset.UtcNow, data);
            await snapshots.SaveAsync(snapshotName, envelope, cancellationToken);
            Complete(activity, envelope.Source, true);
            return envelope;
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            var cached = RequireSnapshot<RepoActivityData>(
                snapshotName,
                exception.Message,
                data => data.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase),
                repository);
            Complete(activity, cached.Source, true);
            return cached;
        }
    }

    private ToolsConfig LoadConfig() =>
        JsonSerializer.Deserialize<ToolsConfig>(
            File.ReadAllText(Path.Combine(paths.Config, "tools.json")),
            JsonOptions) ?? throw new InvalidOperationException("tools.json is invalid.");

    private IReadOnlyList<RunRecord> LoadRuns()
    {
        var path = File.Exists(paths.Act1Results) ? paths.Act1Results : paths.Act1SampleResults;
        return JsonSerializer.Deserialize<List<RunRecord>>(File.ReadAllText(path), JsonOptions) ?? [];
    }

    private ToolEnvelope<T>? Fresh<T>(string name, TimeSpan maximumAge, Func<T, bool> matches)
    {
        var snapshot = snapshots.Load<T>(name);
        return snapshot is not null
            && matches(snapshot.Data)
            && DateTimeOffset.UtcNow - snapshot.RetrievedAt <= maximumAge
            ? snapshot
            : null;
    }

    private ToolEnvelope<T> RequireSnapshot<T>(
        string name,
        string reason,
        Func<T, bool> matches,
        string requested)
    {
        var snapshot = snapshots.Load<T>(name)
            ?? throw new InvalidOperationException($"The live source failed and snapshot '{name}' is absent.");
        if (!matches(snapshot.Data))
            throw new InvalidOperationException(
                $"The live source failed and snapshot '{name}' does not match requested arguments '{requested}'.");
        logger.LogWarning("Using cached snapshot {Snapshot}: {Reason}", name, reason);
        return snapshot with
        {
            Source = "cached",
            RetrievedAt = DateTimeOffset.UtcNow,
            Warning = $"Live source failed: {reason}"
        };
    }

    private static CostValue CalculateCloudCost(
        IReadOnlyList<AzurePriceMeter> meters,
        int inputTokens,
        int outputTokens)
    {
        var filtered = meters.Where(meter =>
            !ContainsAny(meter.MeterName, "batch", "cached", " cd ", "longco", "mini", "nano", "pro", " pp "))
            .ToArray();
        var input = filtered.FirstOrDefault(meter => ContainsAny(meter.MeterName, " inp ", "input"));
        var output = filtered.FirstOrDefault(meter => ContainsAny(meter.MeterName, " opt ", " out ", "output"));
        if (input is null || output is null)
            return new CostValue(false, null, null, null, "USD",
                "Matching base input and output price meters were not found.");
        var perTask = inputTokens * PricePerToken(input) + outputTokens * PricePerToken(output);
        return new CostValue(true, decimal.Round(perTask, 6), decimal.Round(perTask * 1000, 4), null,
            input.Currency, null);
    }

    private static CostValue CalculateLocalCost(LocalCostConfig config, IReadOnlyList<RunRecord> localRuns)
    {
        if (localRuns.Count == 0)
            return new CostValue(false, null, null, null, config.Currency, "No successful local duration is available.");
        var averageSeconds = (decimal)localRuns.Average(run => run.DurationSeconds);
        var electricity = config.WorkstationWatts / 1000m * averageSeconds / 3600m
                          * config.ElectricityPricePerKwh;
        var hardware = config.HardwarePurchasePrice / config.AmortizedTasks;
        var perTask = electricity + hardware;
        return new CostValue(true, decimal.Round(perTask, 6), decimal.Round(perTask * 1000, 4), null,
            config.Currency, null);
    }

    private static CostValue Scale(CostValue value, int taskCount) =>
        value with
        {
            PerThousandTasks = value.PerTask is null
                ? null
                : decimal.Round(value.PerTask.Value * 1000, 4),
            ForTaskCount = value.PerTask is null
                ? null
                : decimal.Round(value.PerTask.Value * taskCount, 4)
        };

    private static decimal PricePerToken(AzurePriceMeter meter) =>
        meter.UnitOfMeasure.Contains("1M", StringComparison.OrdinalIgnoreCase)
            ? meter.UnitPrice / 1_000_000m
            : meter.UnitOfMeasure.Contains("1K", StringComparison.OrdinalIgnoreCase)
                ? meter.UnitPrice / 1_000m
                : meter.UnitPrice;

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static void ValidateTerm(string value, string name)
    {
        if (!SafeTerm.IsMatch(value))
            throw new ArgumentException($"{name} contains unsupported characters.", name);
    }

    private static bool IsUpstreamFailure(Exception exception) =>
        exception is HttpRequestException
            or TaskCanceledException
            or JsonException
            or InvalidOperationException;

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string? GetNullableString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetFlexibleString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool? GetNullableBoolean(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private Activity? Start(string tool)
    {
        var activity = Activities.StartActivity("mcp.tool", ActivityKind.Internal);
        activity?.SetTag("mcp.server", "council-tools");
        activity?.SetTag("mcp.tool.name", tool);
        var member = httpContextAccessor?.HttpContext?.Request.Headers["X-Council-Member"].ToString();
        if (!string.IsNullOrWhiteSpace(member))
            activity?.SetTag("gen_ai.agent.name", member);
        return activity;
    }

    private static void Complete(Activity? activity, string source, bool success)
    {
        activity?.SetTag("mcp.result.source", source);
        activity?.SetTag("mcp.success", success);
    }
}
