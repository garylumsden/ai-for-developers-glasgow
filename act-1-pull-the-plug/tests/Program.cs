using System.Diagnostics;
using System.Text.Json;

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

if (args is ["--record-worker", var recordPath, var recordLabel])
{
    new RunResultsStore(recordPath, options).Record(NewResult(recordLabel));
    return 0;
}
if (args is ["--vote-worker", var votePath, var voteLabel, var voteText])
{
    new RunResultsStore(votePath, options).RecordVote(voteLabel, int.Parse(voteText));
    return 0;
}

var testDirectory = Path.Combine(
    Path.GetTempPath(),
    $"act1-results-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(testDirectory);
try
{
    var resultsPath = Path.Combine(testDirectory, "results.json");
    var store = new RunResultsStore(resultsPath, options);
    store.Record(NewResult("local"));
    store.Record(NewResult("cloud") with { Runtime = "cloud" });
    store.RecordVote("local", 11);
    store.RecordVote("cloud", 17);
    var initial = store.Read();
    Require(initial.Count == 2, "Both runtime results must be preserved.");
    Require(initial.Single(result => result.Label == "local").QualityVote == 11, "The local vote must be preserved.");
    Require(initial.Single(result => result.Label == "cloud").QualityVote == 17, "The cloud vote must be preserved.");
    Console.WriteLine("PASS: Both runtime results and votes are preserved.");

    store.Record(NewResult("LOCAL") with { DurationSeconds = 30 });
    var retried = store.Read();
    Require(retried.Count == 2 && retried.Single(result => result.Label == "LOCAL").DurationSeconds == 30,
        "A retried label must replace its old record without duplicating it.");
    Require(retried.Single(result => result.Label == "LOCAL").QualityVote is null,
        "A retried label must not retain the old run's vote.");
    Require(retried.Single(result => result.Label == "cloud").QualityVote == 17,
        "A retried label must preserve other results' votes.");
    Console.WriteLine("PASS: Retrying a label replaces only that record.");

    var recordWorkers = Enumerable.Range(0, 12)
        .Select(index => StartWorker("--record-worker", resultsPath, $"parallel-{index}"))
        .ToArray();
    await VerifyWorkersAsync(recordWorkers, store);
    Require(store.Read().Count == 14, "Concurrent writers lost a result.");
    Console.WriteLine("PASS: Twelve concurrent result writers retain every record.");

    var voteWorkers = Enumerable.Range(0, 12)
        .Select(index => StartWorker("--vote-worker", resultsPath, $"parallel-{index}", (index + 1).ToString()))
        .ToArray();
    await VerifyWorkersAsync(voteWorkers, store);
    var voted = store.Read();
    for (var index = 0; index < 12; index++)
        Require(voted.Single(result => result.Label == $"parallel-{index}").QualityVote == index + 1,
            $"Concurrent writers lost vote {index + 1}.");
    Console.WriteLine("PASS: Twelve concurrent vote writers retain every vote.");

    var mixedWorkers = Enumerable.Range(0, 6)
        .Select(index => StartWorker("--record-worker", resultsPath, $"mixed-{index}"))
        .Concat(Enumerable.Range(0, 6)
            .Select(index => StartWorker("--vote-worker", resultsPath, $"parallel-{index}", (index + 20).ToString())))
        .ToArray();
    await VerifyWorkersAsync(mixedWorkers, store);
    var mixed = store.Read();
    Require(mixed.Count == 20, "Mixed result and vote updates lost a result.");
    for (var index = 0; index < 6; index++)
        Require(mixed.Single(result => result.Label == $"parallel-{index}").QualityVote == index + 20,
            $"Mixed result and vote updates lost vote {index + 20}.");
    Console.WriteLine("PASS: Concurrent result, vote, and read operations retain every update.");

    File.WriteAllText(resultsPath, "null");
    var rejected = false;
    try
    {
        store.Record(NewResult("must-not-overwrite"));
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }
    Require(rejected && File.ReadAllText(resultsPath) == "null",
        "Invalid results must be rejected rather than overwritten.");
    Console.WriteLine("PASS: Invalid result data is preserved and rejected explicitly.");

    Require(!Directory.GetFiles(testDirectory, "*.tmp").Any(), "Temporary result files were not cleaned.");
    return 0;
}
finally
{
    Directory.Delete(testDirectory, recursive: true);
}

static RunResult NewResult(string label) =>
    new(label, "local", "lmstudio", "model", DateTimeOffset.UtcNow, 10, false, true, null, null, null, null);

static Process StartWorker(params string[] arguments)
{
    var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    startInfo.ArgumentList.Add(typeof(RunResultsStore).Assembly.Location);
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start a result-store worker.");
}

static async Task VerifyWorkersAsync(Process[] workers, RunResultsStore store)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
        var completion = Task.WhenAll(workers.Select(process => process.WaitForExitAsync(timeout.Token)));
        while (!completion.IsCompleted)
        {
            Require(store.Read().Count >= 2, "A reader observed incomplete JSON during a write.");
            await Task.Delay(5, timeout.Token);
        }
        await completion;
        foreach (var process in workers)
            Require(process.ExitCode == 0, $"Result-store worker exited with code {process.ExitCode}.");
    }
    finally
    {
        foreach (var process in workers)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            process.Dispose();
        }
    }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
