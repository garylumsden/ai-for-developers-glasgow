using System.Text.Json;
using CouncilTools.Mcp;
using GovernanceCouncil.Agents.Debate;
using GovernanceCouncil.Core.Models;
using GovernanceCouncil.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static GovernanceCouncil.Web.Components.DebateChamber;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

string Reply(string detail) => JsonSerializer.Serialize(new { summary = "A point.", detail });

var originalDirectory = Directory.GetCurrentDirectory();
var originalRepositoryRoot = Environment.GetEnvironmentVariable("COUNCIL_REPOSITORY_ROOT");
var originalScenarioPath = Environment.GetEnvironmentVariable("COUNCIL_SCENARIO_PATH");
try
{
    Environment.SetEnvironmentVariable("COUNCIL_REPOSITORY_ROOT", null);
    Environment.SetEnvironmentVariable("COUNCIL_SCENARIO_PATH", null);
    var paths = RepositoryPaths.Find();
    Check(Path.GetFileName(paths.Council) == "act-2-council", "Act 2 must use the renamed directory.");
    Check(Directory.Exists(paths.Snapshots), "Cached tool snapshots must resolve under the renamed directory.");

    foreach (var directory in new[] { paths.Root, paths.Council, Path.Combine(paths.Council, "src") })
    {
        Directory.SetCurrentDirectory(directory);
        Check(RepositoryPaths.Find().Root == paths.Root, "Repository discovery must work from the root and Act 2 subdirectories.");
    }

    Environment.SetEnvironmentVariable("COUNCIL_REPOSITORY_ROOT", paths.Root);
    Check(RepositoryPaths.Find().Council == paths.Council, "An explicit repository root must resolve Act 2.");
    Environment.SetEnvironmentVariable("COUNCIL_REPOSITORY_ROOT", paths.Council);
    var rejected = false;
    try
    {
        RepositoryPaths.Find();
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }
    Check(rejected, "An Act 2 directory must not be accepted as the repository root.");

    Scenario.Initialise();
    Check(Scenario.IsConfigured, "The active scenario must load after the directory rename.");
    Check(Scenario.PromptsDirectory == Path.Combine(paths.Config, "prompts"), "Persona prompts must resolve under the renamed directory.");
}
finally
{
    Directory.SetCurrentDirectory(originalDirectory);
    Environment.SetEnvironmentVariable("COUNCIL_REPOSITORY_ROOT", originalRepositoryRoot);
    Environment.SetEnvironmentVariable("COUNCIL_SCENARIO_PATH", originalScenarioPath);
}

var longReply = "One sentence. A second sentence. A third sentence. A fourth sentence.";
var markdownReply = "# Conditions\n\nKeep the experiment reversible.\n\n- Assign an owner.\n- Measure the result.";
Check(ContributionParser.ParseSpoken(Reply(longReply)) == longReply, "Replies above three sentences must remain complete.");
Check(ContributionParser.ParseSpoken(Reply(markdownReply)) == markdownReply, "Paragraphs, headings, and lists must remain complete.");
Check(ContributionParser.ParseSpoken(Reply("A reply\nwith extra whitespace.")) == "A reply\nwith extra whitespace.", "Reply whitespace must not be collapsed.");
Check(ContributionParser.ParseSpoken(longReply) == longReply, "Plain-text replies must remain complete.");
Check(ContributionParser.ParseSpoken(markdownReply) == markdownReply, "Plain Markdown replies must remain complete.");
Check(ContributionParser.ParseSpoken($"```json\n{Reply(longReply)}\n```") == longReply, "Fenced reply JSON must expose its full detail.");
Check(ContributionParser.ParseSpoken("""{"summary":"A returned summary."}""") == "A returned summary.", "Summary-only replies must remain visible.");
Check(ContributionParser.ParseSpoken(Reply("")) == "A point.", "An empty detail must not discard a returned summary.");
Check(ContributionParser.ParseSpoken("""{"detail":42}""") == """{"detail":42}""", "Unexpected JSON fields must retain the returned text.");
Check(ContributionParser.ParseSpoken("""{"sentences":["One.","Two."]}""") == """{"sentences":["One.","Two."]}""", "Unexpected reply formats must retain the returned text.");
Check(ContributionParser.ParseSpoken("""{"speak":true,"reason":"A bid","urgency":3}""") == """{"speak":true,"reason":"A bid","urgency":3}""", "A returned reply must not be discarded because it resembles a bid.");
Check(ContributionParser.ParseSpoken("{") == "{", "Incomplete JSON must retain the returned text.");
Check(ContributionParser.ParseSpoken(null) == "", "A missing model response must remain empty.");
Check(ContributionParser.ParseSpoken(" \n\t ") == "", "Whitespace-only responses must remain empty.");

var calls = ToolCallCollector.Extract("member", [
    new FunctionCallContent("grounding", "SearchKnowledgeBase", new Dictionary<string, object?>()),
    new FunctionCallContent("mcp", "get_repo_activity", new Dictionary<string, object?>()),
    new FunctionResultContent("mcp", """{"source":"cached","result":"Repository evidence"}"""),
    new FunctionResultContent("grounding", """{"result":"Grounded evidence"}""")
]);
Check(calls.Count == 2, "Grounding and MCP calls must both be recorded.");
Check(calls[0].Tool == "get_repo_activity" && calls[0].Source == "cached", "Out-of-order results must match their call IDs.");
Check(calls[1].Tool == "SearchKnowledgeBase", "The grounding call must retain its tool name.");

var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, StaticRenderingJsRuntime>().BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
var html = await renderer.Dispatcher.InvokeAsync(async () =>
{
    var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(DebateChamber.DeliberationId)] = "chat-check",
        [nameof(DebateChamber.Seats)] = new SeatVm[]
        {
            new() { Key = "member", DisplayName = "Member", Role = "Reviewer", Initials = "M", Colour = "#456789", HandRaised = true, HandReason = "A brief reason.", PositionSummary = "Initial evidence.", Detail = "A long initial position remains available." }
        },
        [nameof(DebateChamber.Transcript)] = new TurnVm[]
        {
            new() { Id = Guid.NewGuid(), DisplayName = "Member", Role = "Reviewer", Initials = "M", Colour = "#456789", Text = "Earlier message.", ToolCalls = calls },
            new() { Id = Guid.NewGuid(), DisplayName = "Member", Role = "Reviewer", Initials = "M", Colour = "#456789", Text = ContributionParser.ParseSpoken(Reply(longReply)) },
            new() { Id = Guid.NewGuid(), DisplayName = "Member", Role = "Reviewer", Initials = "M", Colour = "#456789", Text = ContributionParser.ParseSpoken(Reply(markdownReply)) },
            new() { Id = Guid.NewGuid(), DisplayName = "Member", Role = "Reviewer", Initials = "M", Colour = "#456789", Text = ContributionParser.ParseSpoken("<script>alert('model output')</script>") },
            new() { Id = Guid.NewGuid(), DisplayName = "Next member", Role = "Reviewer", Initials = "N", Colour = "#654321", Speaking = true },
            new() { Id = Guid.NewGuid(), DisplayName = "Blocked member", Role = "Reviewer", Initials = "B", Colour = "#654321", Text = "Must not display this text.", Blocked = true }
        }
    });
    var component = await renderer.RenderComponentAsync<DebateChamber>(parameters);
    return component.ToHtmlString();
});
Check(html.Contains("Earlier message."), "Earlier messages must remain in the chat.");
Check(html.Contains(longReply), "The chat must display the fourth sentence, not a format-rejection notice.");
Check(html.Contains("Keep the experiment reversible.") && html.Contains("<li>Measure the result.</li>"), "The chat must display multi-paragraph Markdown replies.");
Check(!html.Contains("<script>"), "Permissive reply formats must not enable raw HTML.");
Check(html.Contains("Next member is typing"), "The known next member must show a typing indicator.");
Check(html.Contains("role=\"tooltip\">A brief reason."), "Raised hands must expose the supplied reason.");
Check(html.Contains("Initial evidence."), "Initial position summaries must appear above the chat.");
Check(html.Contains("Response tool activity") && html.Contains("get_repo_activity"), "Message bubbles must show actual tool evidence.");
Check(!html.Contains("Must not display this text."), "Blocked text must not be displayed.");
Check(!html.Contains("Previous response"), "The chat must not retain the single-response selector.");
Console.WriteLine($"Passed {checks} council chat and repository path checks.");

sealed class StaticRenderingJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException("Static rendering must not invoke JavaScript.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException("Static rendering must not invoke JavaScript.");
}
