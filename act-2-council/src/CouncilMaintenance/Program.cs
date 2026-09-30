using System.Text.Json;
using Azure.Identity;
using GovernanceCouncil.Core.Models;
using Microsoft.Azure.Cosmos;

var repositoryRoot = FindRepositoryRoot();
LoadEnvironment(Path.Combine(repositoryRoot, "act-2-council", "src", "GovernanceCouncil.Web", ".env"));

var endpoint = Require("COSMOS_DB_ENDPOINT");
var databaseName = Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE_NAME") ?? "governance-council";
var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
var credentialOptions = new DefaultAzureCredentialOptions();
if (!string.IsNullOrWhiteSpace(tenantId)) credentialOptions.TenantId = tenantId;

var configPath = Path.Combine(repositoryRoot, "act-2-council", "config", "rehearsal-data.json");
var config = JsonSerializer.Deserialize<RehearsalConfig>(
    File.ReadAllText(configPath),
    new JsonSerializerOptions(JsonSerializerDefaults.Web))
    ?? throw new InvalidOperationException("rehearsal-data.json is invalid.");
var titles = config.DossierTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);
var configuredIds = config.DossierIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
var apply = args.Contains("--yes", StringComparer.OrdinalIgnoreCase);

using var client = new CosmosClient(
    endpoint,
    new DefaultAzureCredential(credentialOptions),
    new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    });
var database = client.GetDatabase(databaseName);
var dossierContainer = database.GetContainer("dossiers");
var deliberationContainer = database.GetContainer("deliberations");
var assessmentContainer = database.GetContainer("assessments");
var nexusContainer = database.GetContainer("nexuses");

var dossiers = await ReadAllAsync<Dossier>(dossierContainer);
var targetDossierIds = dossiers
    .Where(dossier => titles.Contains(dossier.Title) || configuredIds.Contains(dossier.DossierId))
    .Select(dossier => dossier.DossierId)
    .Concat(configuredIds)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
var deliberations = (await ReadAllAsync<Deliberation>(deliberationContainer))
    .Where(item => targetDossierIds.Contains(item.DossierId))
    .ToArray();
var assessments = (await ReadAllAsync<Assessment>(assessmentContainer))
    .Where(item => targetDossierIds.Contains(item.DossierId))
    .ToArray();
var assessmentIds = assessments.Select(item => item.AssessmentId).ToHashSet(StringComparer.OrdinalIgnoreCase);
var nexuses = (await ReadAllAsync<Nexus>(nexusContainer))
    .Where(item => assessmentIds.Contains(item.SourceAssessmentId)
        || assessmentIds.Contains(item.TargetAssessmentId))
    .ToArray();

Console.WriteLine("Targeted rehearsal cleanup:");
Console.WriteLine($"  Dossier IDs retained: {string.Join(", ", targetDossierIds.Order())}");
Console.WriteLine($"  Deliberations to delete: {deliberations.Length}");
Console.WriteLine($"  Assessments to delete: {assessments.Length}");
Console.WriteLine($"  Nexuses to delete: {nexuses.Length}");
if (!apply)
{
    Console.WriteLine("Dry run only. Pass --yes after reviewing the exact counts.");
    return 0;
}

foreach (var nexus in nexuses)
    await nexusContainer.DeleteItemAsync<Nexus>(nexus.Id, new PartitionKey(nexus.SourceAssessmentId));
foreach (var assessment in assessments)
    await assessmentContainer.DeleteItemAsync<Assessment>(
        assessment.Id,
        new PartitionKey(assessment.AssessmentId));
foreach (var deliberation in deliberations)
    await deliberationContainer.DeleteItemAsync<Deliberation>(
        deliberation.Id,
        new PartitionKey(deliberation.DeliberationId));

Console.WriteLine("Targeted rehearsal records deleted. Containers, indexes, dossiers, and blobs were preserved.");
return 0;

static async Task<List<T>> ReadAllAsync<T>(Container container)
{
    var items = new List<T>();
    using var iterator = container.GetItemQueryIterator<T>("SELECT * FROM c");
    while (iterator.HasMoreResults)
        items.AddRange(await iterator.ReadNextAsync());
    return items;
}

static string Require(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Required setting '{name}' is missing.");

static void LoadEnvironment(string path)
{
    if (!File.Exists(path)) throw new FileNotFoundException("The generated council .env file is missing.", path);
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

static string FindRepositoryRoot()
{
    foreach (var origin in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(origin);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "act-2-council", "config", "scenario.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("Could not locate the repository root.");
}

sealed record RehearsalConfig(IReadOnlyList<string> DossierTitles, IReadOnlyList<string> DossierIds);
