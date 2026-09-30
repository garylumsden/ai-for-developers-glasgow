using System.Diagnostics;
using System.Text;
using System.Text.Json;

sealed class RunResultsStore(string path, JsonSerializerOptions options)
{
    public List<RunResult> Read()
    {
        using var resultsLock = AcquireLock(LockPath());
        return ReadCore();
    }

    private List<RunResult> ReadCore()
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<List<RunResult>>(stream, options)
            ?? throw new InvalidOperationException($"Results file '{path}' must contain a JSON array.");
    }

    public void Record(RunResult result) => Update(results =>
    {
        var existing = results.FindIndex(item =>
            item.Label.Equals(result.Label, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            results[existing] = result;
        else
            results.Add(result);
    });

    public void RecordVote(string label, int vote) => Update(results =>
    {
        var index = results.FindIndex(item => item.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new InvalidOperationException($"No result has label '{label}'.");
        results[index] = results[index] with { QualityVote = vote };
    });

    private void Update(Action<List<RunResult>> update)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        using var resultsLock = AcquireLock(LockPath());
        var results = File.Exists(path) ? ReadCore() : [];
        update(results);

        var temporaryPath = Path.Combine(directory, $"results-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(results, options) + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private string LockPath() =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $"{Path.GetFileName(path)}.lock");

    private static FileStream AcquireLock(string lockPath)
    {
        // Retain the lock file so waiting processes continue to use the same lock.
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 11 or 32 or 33)
            {
                if (timer.Elapsed >= TimeSpan.FromSeconds(15))
                    throw new TimeoutException($"Could not lock results file '{lockPath}' within 15 seconds.", exception);
                Thread.Sleep(50);
            }
        }
    }
}
