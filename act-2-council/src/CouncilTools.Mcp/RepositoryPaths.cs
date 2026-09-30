namespace CouncilTools.Mcp;

public sealed record RepositoryPaths(string Root)
{
    public string Council => Path.Combine(Root, "act-2-council");
    public string Config => Path.Combine(Council, "config");
    public string Snapshots => Path.Combine(Council, "src", "CouncilTools.Mcp", "snapshots");
    public string Act1Results => Path.Combine(Root, "act-1-pull-the-plug", "results", "results.json");
    public string Act1SampleResults => Path.Combine(Root, "act-1-pull-the-plug", "results", "results.sample.json");

    public static RepositoryPaths Find()
    {
        var configured = Environment.GetEnvironmentVariable("COUNCIL_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var full = Path.GetFullPath(configured);
            Validate(full);
            return new RepositoryPaths(full);
        }

        foreach (var origin in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(origin);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "act-1-pull-the-plug", "task.json"))
                    && Directory.Exists(Path.Combine(directory.FullName, "act-2-council", "config")))
                    return new RepositoryPaths(directory.FullName);
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static void Validate(string path)
    {
        if (!File.Exists(Path.Combine(path, "act-1-pull-the-plug", "task.json"))
            || !Directory.Exists(Path.Combine(path, "act-2-council", "config")))
            throw new InvalidOperationException("COUNCIL_REPOSITORY_ROOT does not identify this repository.");
    }
}
