namespace CouncilTools.Mcp;

public static class EnvFileLoader
{
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var name = line[..separator].Trim();
            if (Environment.GetEnvironmentVariable(name) is not null) continue;
            Environment.SetEnvironmentVariable(name, line[(separator + 1)..].Trim());
        }
    }
}
