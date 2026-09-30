namespace CouncilTools.Mcp;

using Microsoft.Extensions.Logging.Abstractions;

public static class SelfTestRunner
{
    public static async Task<int> RunAsync()
    {
        var paths = RepositoryPaths.Find();
        var snapshots = new SnapshotStore(paths);
        using var liveHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        liveHttp.DefaultRequestHeaders.UserAgent.ParseAdd("ai-for-developers-glasgow-self-test/1.0");
        var live = new CouncilToolService(liveHttp, paths, snapshots, NullLogger<CouncilToolService>.Instance);

        var price = await live.GetAzureModelPriceAsync("gpt-5.4", forceRefresh: true);
        Require(price.Source == "live" && price.Data.Meters.Count > 0, "Live Azure price test failed.");
        var lifecycle = await live.GetProductLifecycleAsync("dotnet", "10", forceRefresh: true);
        Require(lifecycle.Source == "live", "Live lifecycle test failed.");
        var repo = await live.GetRepoActivityAsync("garylumsden/copilocal", forceRefresh: true);
        Require(repo.Source == "live" && repo.Data.RecentCommits.Count > 0, "Live GitHub test failed.");
        var register = await live.CheckModelRegisterAsync("ibm/granite-4-h-tiny", "lmstudio");
        Require(register.Data.Match is not null, "Model register test failed.");
        var comparison = await live.CompareRunCostsAsync();
        Require(comparison.Data.Local.Calculable, "Local comparison test failed.");

        var invalidRejected = false;
        try
        {
            await live.GetRepoActivityAsync("../private");
        }
        catch (ArgumentException)
        {
            invalidRejected = true;
        }
        Require(invalidRejected, "Invalid repository input was not rejected.");

        using var offlineHttp = new HttpClient(new FailingHandler()) { Timeout = TimeSpan.FromSeconds(1) };
        var offline = new CouncilToolService(
            offlineHttp,
            paths,
            snapshots,
            NullLogger<CouncilToolService>.Instance);
        Require((await offline.GetAzureModelPriceAsync("gpt-5.4", forceRefresh: true)).Source == "cached",
            "Azure price cached fallback failed.");
        Require((await offline.GetProductLifecycleAsync("dotnet", "10", forceRefresh: true)).Source == "cached",
            "Lifecycle cached fallback failed.");
        Require((await offline.GetRepoActivityAsync("garylumsden/copilocal", forceRefresh: true)).Source == "cached",
            "GitHub cached fallback failed.");
        var mismatchedCacheRejected = false;
        try
        {
            await offline.GetRepoActivityAsync("microsoft/vscode", forceRefresh: true);
        }
        catch (InvalidOperationException)
        {
            mismatchedCacheRejected = true;
        }
        Require(mismatchedCacheRejected, "A cached repository response was reused for the wrong arguments.");

        Console.WriteLine("PASS: council-tools live, cached, invalid-input, register, and comparison checks.");
        return 0;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated upstream outage.");
    }
}
