using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never
};

var repositoryRoot = FindRepositoryRoot();
var actRoot = Path.Combine(repositoryRoot, "act-1-pull-the-plug");
var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
var commandArgs = args.Skip(1).ToArray();

return command switch
{
    "prepare" => Prepare(),
    "check" => Check(GetOption(commandArgs, "--workspace") ?? Path.Combine(actRoot, "runs", "local")),
    "run-local" => await RunModelAsync(local: true),
    "run-cloud" => await RunModelAsync(local: false),
    "open-local" => OpenExistingOutput("local"),
    "open-cloud" => OpenExistingOutput("cloud"),
    "record-vote" => RecordVote(),
    "show-results" => ShowResults(),
    "bridge" => BridgeResults(),
    "clear-dossier" => ClearDossier(),
    _ => Help()
};

int Prepare()
{
    var task = LoadTask();
    ValidatePromptSync(task.Prompt);
    var resolvedPick = ResolveCopilocalPick(task.Local.Provider, task.Local.Model);
    VerifyCopilocalPick(resolvedPick, task.Local.Provider, task.Local.Model);

    foreach (var runtime in new[] { "local", "cloud" })
    {
        var workspace = Path.Combine(actRoot, "runs", runtime);
        DeleteGeneratedDirectory(workspace);
        CopyDirectory(Path.Combine(actRoot, "starter"), workspace);
    }

    void ValidatePromptSync(string prompt)
    {
        var taskMarkdown = File.ReadAllText(Path.Combine(actRoot, "TASK.md"));
        var match = Regex.Match(taskMarkdown, @"(?s)## Exact prompt\s+```text\r?\n(.*?)\r?\n```");
        if (!match.Success || !string.Equals(
                match.Groups[1].Value.Replace("\r\n", "\n"),
                prompt.Replace("\r\n", "\n"),
                StringComparison.Ordinal))
            throw new InvalidOperationException("TASK.md and task.json contain different prompts.");
    }

    Console.WriteLine("Prepared isolated local and cloud workspaces.");
    Console.WriteLine($"Copilocal pick {resolvedPick}: {task.Local.Provider}/{task.Local.Model}");
    return 0;
}

int Check(string workspace)
{
    workspace = Path.GetFullPath(workspace);
    var htmlPath = Path.Combine(workspace, "index.html");
    if (!File.Exists(htmlPath))
    {
        Console.Error.WriteLine("FAIL: index.html is missing.");
        return 1;
    }

    var html = File.ReadAllText(htmlPath);
    if (string.IsNullOrWhiteSpace(html))
    {
        Console.Error.WriteLine("FAIL: index.html is empty.");
        return 1;
    }

    Console.WriteLine($"Output available: {htmlPath}");
    Console.WriteLine("Content and design quality are for the audience to judge.");
    return 0;
}

async Task<int> RunModelAsync(bool local)
{
    var task = LoadTask();
    var runtime = local ? "local" : "cloud";
    var workspace = Path.Combine(actRoot, "runs", runtime);
    if (!Directory.Exists(workspace))
        throw new DirectoryNotFoundException($"Run prepare first. Missing: {workspace}");

    if (local)
    {
        await EnsureLocalProviderAsync(task.Local);
        await VerifyExternalProbeFailsAsync();
    }

    var label = GetOption(commandArgs, "--label") ?? $"{runtime}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
    var liveDir = Path.Combine(actRoot, "results", "live");
    Directory.CreateDirectory(liveDir);
    var usagePath = Path.Combine(liveDir, $"{label}-usage.json");
    var outputFormat = commandArgs.Contains("--json", StringComparer.OrdinalIgnoreCase) ? "json" : "text";

    var executable = local ? "copilocal" : "copilot";
    var copilocalPick = local
        ? ResolveCopilocalPick(task.Local.Provider, task.Local.Model)
        : 0;
    var processArgs = (local
        ? new[]
        {
            "--pick", copilocalPick.ToString(), "--offline", "--",
            "--prompt", task.Prompt,
            "--allow-all", "--no-ask-user", "--no-auto-update", "--disable-builtin-mcps",
            "--no-custom-instructions", "--output-format", outputFormat,
            "--usage-output-file", usagePath, "-C", workspace
        }
        : new[]
        {
            "--prompt", task.Prompt, "--model", task.Cloud.Model,
            "--allow-all", "--no-ask-user", "--no-auto-update", "--disable-builtin-mcps",
            "--no-custom-instructions", "--output-format", outputFormat,
            "--usage-output-file", usagePath, "-C", workspace
        }).Concat(DisabledMcpArguments()).ToArray();

    var environment = local
        ? new Dictionary<string, string?>
        {
            ["HTTP_PROXY"] = "http://127.0.0.1:9",
            ["HTTPS_PROXY"] = "http://127.0.0.1:9",
            ["ALL_PROXY"] = "http://127.0.0.1:9",
            ["NO_PROXY"] = "127.0.0.1,localhost"
        }
        : null;

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine($" {runtime.ToUpperInvariant()} RUN: {label}");
    Console.WriteLine($" MODEL: {(local ? task.Local.Model : task.Cloud.Model)}");
    Console.WriteLine($" TIME BOX: {task.TimeBoxSeconds} seconds");
    Console.WriteLine("========================================");

    var startedAt = DateTimeOffset.UtcNow;
    var processResult = RunTimed(executable, processArgs, workspace, task.TimeBoxSeconds, environment);
    var succeeded = !processResult.TimedOut && processResult.ExitCode == 0 && Check(workspace) == 0;
    var (inputTokens, outputTokens) = ReadTokenUsage(usagePath);
    var notes = "Completion means successful execution and non-empty output, not content or design quality.";
    if (local)
        notes += " Copilocal offline mode with an unreachable child-process proxy and a local-provider exception.";

    var result = new RunResult(
        label,
        runtime,
        local ? task.Local.Provider : "github-copilot",
        local ? task.Local.Model : task.Cloud.Model,
        startedAt,
        Math.Round(processResult.Duration.TotalSeconds, 2),
        processResult.TimedOut,
        succeeded,
        inputTokens,
        outputTokens,
        null,
        notes);
    AppendResult(result);

    Console.WriteLine();
    Console.WriteLine(succeeded ? "RUN COMPLETED" : "RUN FAILED");
    if (processResult.TimedOut)
        Console.Error.WriteLine($"The run exceeded the {task.TimeBoxSeconds}-second time box.");
    else if (processResult.ExitCode != 0)
        Console.Error.WriteLine($"Copilot exited with code {processResult.ExitCode}.");
    Console.WriteLine($"Duration: {result.DurationSeconds:0.00} seconds");
    if (succeeded && !commandArgs.Contains("--no-open", StringComparer.OrdinalIgnoreCase))
        OpenOutput(Path.Combine(workspace, "index.html"));
    return succeeded ? 0 : 1;
}

int OpenExistingOutput(string runtime)
{
    var workspace = Path.Combine(actRoot, "runs", runtime);
    if (Check(workspace) != 0) return 1;
    return OpenOutput(Path.Combine(workspace, "index.html")) ? 0 : 1;
}

static bool OpenOutput(string path)
{
    var outputUri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
    try
    {
        using var process = Process.Start(new ProcessStartInfo(outputUri) { UseShellExecute = true });
        Console.WriteLine($"Opening output: {path}");
        return true;
    }
    catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
    {
        Console.Error.WriteLine($"Could not open the output automatically: {exception.Message}");
        Console.Error.WriteLine($"Open this file manually: {path}");
        return false;
    }
}

int RecordVote()
{
    var label = GetRequiredOption(commandArgs, "--label");
    if (!int.TryParse(GetRequiredOption(commandArgs, "--vote"), out var vote) || vote < 0)
        throw new ArgumentException("--vote must be zero or a positive integer.");

    new RunResultsStore(ResultsPath(), options).RecordVote(label, vote);
    Console.WriteLine($"Recorded vote {vote} for {label}.");
    return 0;
}

int ShowResults()
{
    var sourceSelection = ResolveResultsSource();
    var results = LoadResultsFrom(sourceSelection.Path);
    var source = sourceSelection.IsSample ? "REHEARSAL SAMPLE - NOT LIVE" : "LIVE RESULTS";
    Console.WriteLine(source);
    Console.WriteLine(new string('=', source.Length));
    Console.WriteLine($"{"LABEL",-22} {"RUNTIME",-7} {"MODEL",-28} {"SECONDS",8} {"COMPLETED",9} {"VOTE",6}");
    foreach (var result in results.TakeLast(10))
        Console.WriteLine($"{result.Label,-22} {result.Runtime,-7} {Trim(result.Model, 28),-28} " +
                          $"{result.DurationSeconds,8:0.00} {(result.Succeeded ? "YES" : "NO"),9} " +
                          $"{(result.QualityVote?.ToString() ?? "-"),6}");
    Console.WriteLine("COMPLETED records execution and non-empty output. VOTE records audience quality.");
    return 0;
}

int BridgeResults()
{
    var taskPath = Path.Combine(actRoot, "TASK.md");
    var dossierPath = Path.Combine(repositoryRoot, "act-2-council", "data", "policies",
        "run-coding-agents-on-local-models.md");
    var sourceSelection = ResolveResultsSource();
    var useLive = !sourceSelection.IsSample;
    var results = LoadResultsFrom(sourceSelection.Path);
    if (sourceSelection.IsSample)
        Console.WriteLine("WARNING: Writing rehearsal sample data instead of live results.");

    var task = LoadTask();
    var taskSummary =
        $"- Task: create a fictional Glasgow Subway service board as self-contained HTML.{Environment.NewLine}" +
        $"- Models: local `{task.Local.Model}` through Copilocal; cloud `{task.Cloud.Model}`.";
    var table = new StringBuilder()
        .AppendLine("| Runtime | Model | Duration | Completed | Input tokens | Output tokens | Audience vote |")
        .AppendLine("|---|---|---:|:---:|---:|---:|---:|");
    foreach (var result in results)
    {
        table.AppendLine($"| {result.Runtime} | `{result.Model}` | {result.DurationSeconds:0.00} s | " +
                         $"{(result.Succeeded ? "Yes" : "No")} | {Value(result.InputTokens)} | " +
                         $"{Value(result.OutputTokens)} | {Value(result.QualityVote)} |");
    }
    table.AppendLine()
        .AppendLine("Completion records execution and non-empty output. The audience judges content and design quality.");

    var text = File.ReadAllText(dossierPath);
    text = ReplaceMarkerContent(text, "<!-- ACT1_TASK -->", "<!-- ACT1_RESULTS -->", taskSummary);
    var resultsEnd = NextHeadingOrEnd(text, text.IndexOf("<!-- ACT1_RESULTS -->", StringComparison.Ordinal));
    text = ReplaceRangeAfterMarker(text, "<!-- ACT1_RESULTS -->", resultsEnd,
        $"{Environment.NewLine}{(useLive ? "" : "> **Rehearsal sample data. Replace before the live debate.**" + Environment.NewLine + Environment.NewLine)}{table.ToString().TrimEnd()}{Environment.NewLine}");
    File.WriteAllText(dossierPath, text);
    Console.WriteLine($"Updated {Path.GetRelativePath(repositoryRoot, dossierPath)}.");
    return 0;
}

int ClearDossier()
{
    var dossierPath = Path.Combine(repositoryRoot, "act-2-council", "data", "policies",
        "run-coding-agents-on-local-models.md");
    var text = File.ReadAllText(dossierPath);
    text = ReplaceMarkerContent(text, "<!-- ACT1_TASK -->", "<!-- ACT1_RESULTS -->", "");
    var markerIndex = text.IndexOf("<!-- ACT1_RESULTS -->", StringComparison.Ordinal);
    text = ReplaceRangeAfterMarker(
        text,
        "<!-- ACT1_RESULTS -->",
        NextHeadingOrEnd(text, markerIndex),
        Environment.NewLine);
    File.WriteAllText(dossierPath, text);
    Console.WriteLine("Cleared Act 1 dossier evidence while preserving both markers.");
    return 0;
}

TaskConfig LoadTask() =>
    JsonSerializer.Deserialize<TaskConfig>(File.ReadAllText(Path.Combine(actRoot, "task.json")), options)
    ?? throw new InvalidOperationException("task.json is invalid.");

List<RunResult> LoadResultsFrom(string path) =>
    new RunResultsStore(path, options).Read();

void AppendResult(RunResult result) => new RunResultsStore(ResultsPath(), options).Record(result);

string ResultsPath() => Path.Combine(actRoot, "results", "results.json");

(string Path, bool IsSample) ResolveResultsSource()
{
    var samplePath = Path.Combine(actRoot, "results", "results.sample.json");
    if (commandArgs.Any(value => value.Equals("--sample", StringComparison.OrdinalIgnoreCase)))
        return (samplePath, true);

    if (GetOption(commandArgs, "--results") is { } requested)
    {
        var resultsDirectory = Path.GetFullPath(Path.Combine(actRoot, "results"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.IsPathRooted(requested)
            ? requested
            : Path.Combine(actRoot, requested));
        if (!full.StartsWith(resultsDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("--results must identify a file under act-1-pull-the-plug/results.");
        if (!File.Exists(full)) throw new FileNotFoundException("The selected results file is missing.", full);
        return (full, Path.GetFileName(full).Contains("sample", StringComparison.OrdinalIgnoreCase));
    }

    return File.Exists(ResultsPath()) ? (ResultsPath(), false) : (samplePath, true);
}

int ResolveCopilocalPick(string provider, string model)
{
    var result = RunCaptured("copilocal", ["--pick", int.MaxValue.ToString(), "--dry-run"], repositoryRoot);
    var expectedProvider = provider.Equals("lmstudio", StringComparison.OrdinalIgnoreCase)
        ? "LM Studio"
        : provider;
    var match = Regex.Matches(result.Output, @"^\s*(\d+)\.\s+(.+?)\s+/\s+(.+?)\s*$",
            RegexOptions.Multiline)
        .Select(candidate => new
        {
            Pick = int.Parse(candidate.Groups[1].Value),
            Provider = candidate.Groups[2].Value.Trim(),
            Model = candidate.Groups[3].Value.Trim()
        })
        .SingleOrDefault(candidate =>
            candidate.Provider.Equals(expectedProvider, StringComparison.OrdinalIgnoreCase)
            && candidate.Model.Equals(model, StringComparison.OrdinalIgnoreCase));
    return match?.Pick ?? throw new InvalidOperationException(
        $"Copilocal did not discover {expectedProvider}/{model}.");
}

void VerifyCopilocalPick(int pick, string provider, string model)
{
    var result = RunCaptured(
        "copilocal",
        ["--pick", pick.ToString(), "--offline", "--dry-run"],
        repositoryRoot);
    var expectedProvider = provider.Equals("lmstudio", StringComparison.OrdinalIgnoreCase)
        ? "LM Studio"
        : provider;
    if (result.ExitCode != 0
        || !result.Output.Contains($"[{expectedProvider}]", StringComparison.OrdinalIgnoreCase)
        || !result.Output.Contains($"COPILOT_MODEL={model}", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            $"Copilocal pick {pick} did not resolve to {expectedProvider}/{model}.");
}

IEnumerable<string> DisabledMcpArguments()
{
    var result = RunCaptured("copilot", ["mcp", "list", "--json"], repositoryRoot);
    if (result.ExitCode != 0) yield break;

    using var document = JsonDocument.Parse(result.Output);
    if (!document.RootElement.TryGetProperty("mcpServers", out var servers)
        || servers.ValueKind != JsonValueKind.Object) yield break;

    foreach (var server in servers.EnumerateObject())
    {
        if (server.Name.Equals("github-mcp-server", StringComparison.OrdinalIgnoreCase)) continue;
        if (!server.Value.TryGetProperty("enabled", out var enabled)
            || enabled.ValueKind != JsonValueKind.True) continue;
        yield return "--disable-mcp-server";
        yield return server.Name;
    }
}

async Task EnsureLocalProviderAsync(LocalConfig local)
{
    if (!local.Provider.Equals("lmstudio", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            $"Automatic provider startup is not implemented for '{local.Provider}'.");

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    if (!await IsLmStudioReadyAsync(client))
    {
        Console.WriteLine("Starting the LM Studio local server...");
        var start = RunCaptured(
            "lms",
            ["server", "start", "-p", "1234", "--bind", "127.0.0.1"],
            repositoryRoot);
        if (start.ExitCode != 0)
            throw new InvalidOperationException(
                $"LM Studio server startup failed:{Environment.NewLine}{start.Output}");
        await WaitForLmStudioAsync(client, TimeSpan.FromSeconds(30));
    }

    var loaded = await GetLoadedLmStudioModelAsync(client, local.Model);
    if (loaded is null || loaded.ContextLength < local.ContextLength)
    {
        if (loaded is not null)
        {
            var unload = RunCaptured("lms", ["unload", loaded.Identifier], repositoryRoot);
            if (unload.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Could not unload the existing LM Studio allocation:{Environment.NewLine}{unload.Output}");
        }

        Console.WriteLine(
            $"Loading {local.Model} with {local.ContextLength:N0} context tokens...");
        var load = RunCaptured(
            "lms",
            [
                "load", local.Model,
                "--context-length", local.ContextLength.ToString(),
                "--ttl", local.TtlSeconds.ToString(),
                "--yes"
            ],
            repositoryRoot);
        if (load.ExitCode != 0)
            throw new InvalidOperationException(
                $"LM Studio model load failed:{Environment.NewLine}{load.Output}");
        loaded = await WaitForLoadedModelAsync(
            client,
            local.Model,
            local.ContextLength,
            TimeSpan.FromSeconds(60));
    }

    Console.WriteLine(
        $"Local provider check: PASS ({local.Model}, {loaded.ContextLength:N0} context tokens)");
}

static async Task<bool> IsLmStudioReadyAsync(HttpClient client)
{
    try
    {
        using var response = await client.GetAsync("http://127.0.0.1:1234/api/v1/models");
        return response.IsSuccessStatusCode;
    }
    catch (HttpRequestException)
    {
        return false;
    }
    catch (TaskCanceledException)
    {
        return false;
    }
}

static async Task WaitForLmStudioAsync(HttpClient client, TimeSpan timeout)
{
    var timer = Stopwatch.StartNew();
    while (timer.Elapsed < timeout)
    {
        if (await IsLmStudioReadyAsync(client)) return;
        await Task.Delay(500);
    }
    throw new TimeoutException($"LM Studio did not become ready within {timeout.TotalSeconds:0} seconds.");
}

static async Task<LoadedModel?> GetLoadedLmStudioModelAsync(HttpClient client, string model)
{
    using var document = JsonDocument.Parse(
        await client.GetStringAsync("http://127.0.0.1:1234/api/v1/models"));
    if (!document.RootElement.TryGetProperty("models", out var models)
        || models.ValueKind != JsonValueKind.Array)
        return null;

    foreach (var item in models.EnumerateArray())
    {
        if (!item.TryGetProperty("key", out var key)
            || !string.Equals(key.GetString(), model, StringComparison.OrdinalIgnoreCase)
            || !item.TryGetProperty("loaded_instances", out var instances)
            || instances.ValueKind != JsonValueKind.Array)
            continue;

        foreach (var instance in instances.EnumerateArray())
        {
            var identifier = instance.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
            var contextLength = instance.TryGetProperty("config", out var config)
                && config.TryGetProperty("context_length", out var context)
                && context.TryGetInt32(out var value)
                    ? value
                    : 0;
            if (!string.IsNullOrWhiteSpace(identifier))
                return new LoadedModel(identifier, contextLength);
        }
    }
    return null;
}

static async Task<LoadedModel> WaitForLoadedModelAsync(
    HttpClient client,
    string model,
    int minimumContextLength,
    TimeSpan timeout)
{
    var timer = Stopwatch.StartNew();
    while (timer.Elapsed < timeout)
    {
        var loaded = await GetLoadedLmStudioModelAsync(client, model);
        if (loaded is not null && loaded.ContextLength >= minimumContextLength)
            return loaded;
        await Task.Delay(500);
    }
    throw new TimeoutException(
        $"{model} did not load with at least {minimumContextLength:N0} context tokens.");
}

async Task VerifyExternalProbeFailsAsync()
{
    using var handler = new HttpClientHandler
    {
        Proxy = new WebProxy("http://127.0.0.1:9"),
        UseProxy = true
    };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    try
    {
        await client.GetAsync("https://github.com/");
        throw new InvalidOperationException("External connectivity probe unexpectedly succeeded.");
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
    {
        Console.WriteLine("External connectivity probe: BLOCKED");
    }
}

TimedResult RunTimed(
    string executable,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    int timeoutSeconds,
    IReadOnlyDictionary<string, string?>? environment)
{
    var startInfo = new ProcessStartInfo(executable)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false
    };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    if (environment is not null)
        foreach (var variable in environment) startInfo.Environment[variable.Key] = variable.Value;

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Could not start {executable}.");
    var stopwatch = Stopwatch.StartNew();
    var nextTimer = 10;
    var timedOut = false;
    while (!process.WaitForExit(1000))
    {
        if (stopwatch.Elapsed.TotalSeconds >= nextTimer)
        {
            Console.WriteLine($"[TIMER] {stopwatch.Elapsed:mm\\:ss} / {TimeSpan.FromSeconds(timeoutSeconds):mm\\:ss}");
            nextTimer += 10;
        }
        if (stopwatch.Elapsed.TotalSeconds < timeoutSeconds) continue;
        timedOut = true;
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        break;
    }
    stopwatch.Stop();
    return new TimedResult(process.ExitCode, stopwatch.Elapsed, timedOut);
}

(int? Input, int? Output) ReadTokenUsage(string path)
{
    if (!File.Exists(path)) return (null, null);
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return (FindInt(document.RootElement, "inputTokens", "input_tokens"),
        FindInt(document.RootElement, "outputTokens", "output_tokens"));
}

static int? FindInt(JsonElement element, params string[] names)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                && property.Value.TryGetInt32(out var value)) return value;
            var nested = FindInt(property.Value, names);
            if (nested.HasValue) return nested;
        }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in element.EnumerateArray())
        {
            var nested = FindInt(item, names);
            if (nested.HasValue) return nested;
        }
    }
    return null;
}

static CapturedResult RunCaptured(string executable, IReadOnlyList<string> arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo(executable)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Could not start {executable}.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    Task.WaitAll(stdout, stderr);
    return new CapturedResult(process.ExitCode, stdout.Result + Environment.NewLine + stderr.Result);
}

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var file in Directory.GetFiles(source))
        File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
    foreach (var directory in Directory.GetDirectories(source))
        CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
}

static void DeleteGeneratedDirectory(string path)
{
    var full = Path.GetFullPath(path);
    if (!full.Contains($"{Path.DirectorySeparatorChar}act-1-pull-the-plug{Path.DirectorySeparatorChar}runs{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Refusing to delete outside the generated runs folder: {full}");
    if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
}

static string FindRepositoryRoot()
{
    foreach (var origin in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(origin);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "act-1-pull-the-plug", "task.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("Could not locate the repository root.");
}

static string? GetOption(string[] values, string name)
{
    var index = Array.FindIndex(values, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}

static string GetRequiredOption(string[] values, string name) =>
    GetOption(values, name) ?? throw new ArgumentException($"Missing required option {name}.");

static string Trim(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";
static string Value(int? value) => value?.ToString() ?? "unknown";

static string ReplaceMarkerContent(string text, string marker, string nextMarker, string content)
{
    var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
    var nextIndex = text.IndexOf(nextMarker, markerIndex + marker.Length, StringComparison.Ordinal);
    if (markerIndex < 0 || nextIndex < 0) throw new InvalidOperationException("Dossier markers are missing.");
    var start = markerIndex + marker.Length;
    return text[..start] + Environment.NewLine + content.TrimEnd() + Environment.NewLine + text[nextIndex..];
}

static int NextHeadingOrEnd(string text, int markerIndex)
{
    var lfHeading = text.IndexOf("\n#", markerIndex, StringComparison.Ordinal);
    var crlfHeading = text.IndexOf("\r\n#", markerIndex, StringComparison.Ordinal);
    if (lfHeading < 0) return crlfHeading < 0 ? text.Length : crlfHeading;
    if (crlfHeading < 0) return lfHeading;
    return Math.Min(lfHeading, crlfHeading);
}

static string ReplaceRangeAfterMarker(string text, string marker, int end, string replacement)
{
    var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
    if (markerIndex < 0) throw new InvalidOperationException($"Marker is missing: {marker}");
    var start = markerIndex + marker.Length;
    return text[..start] + replacement + text[end..];
}

static int Help()
{
    Console.WriteLine("Act1Harness commands: prepare, check, run-local, run-cloud, open-local, open-cloud, record-vote, show-results, bridge, clear-dossier");
    Console.WriteLine("Run options: --label <label>, --no-open, --json");
    return 1;
}

record TaskConfig(
    string Prompt,
    int TimeBoxSeconds,
    int TargetSeconds,
    LocalConfig Local,
    CloudConfig Cloud,
    CheckConfig Check);
record LocalConfig(
    string Provider,
    string Model,
    int ContextLength = 131072,
    int TtlSeconds = 7200);
record CloudConfig(string Model);
record CheckConfig(string Windows, string Posix);
record TimedResult(int ExitCode, TimeSpan Duration, bool TimedOut);
record CapturedResult(int ExitCode, string Output);
record LoadedModel(string Identifier, int ContextLength);
