namespace CouncilTools.Mcp;

using System.Text.Json;

public sealed class SnapshotStore(RepositoryPaths paths)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ToolEnvelope<T>? Load<T>(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ToolEnvelope<T>>(File.ReadAllText(path), Options);
    }

    public async Task SaveAsync<T>(string name, ToolEnvelope<T> value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.Snapshots);
        await File.WriteAllTextAsync(
            PathFor(name),
            JsonSerializer.Serialize(value, Options) + Environment.NewLine,
            cancellationToken);
    }

    private string PathFor(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9-]+$"))
            throw new ArgumentException("Snapshot name is invalid.", nameof(name));
        return Path.Combine(paths.Snapshots, $"{name}.json");
    }
}
