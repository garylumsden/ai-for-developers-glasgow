namespace GovernanceCouncil.Agents.Debate;

using GovernanceCouncil.Core.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;

/// <summary>
/// Classifies WHY a model response was rejected, and a short safe preview of the raw output,
/// so discard logs explain themselves (empty vs bid-format vs unparseable vs no-detail) instead
/// of a generic "discarding". Used by both the Phase-1 assessment and Phase-2 speak paths.
/// </summary>
internal static class DiscardReason
{
    public static string Classify(string? raw, bool looksLikeBid, bool parsed, bool detailEmpty)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "empty response (no text)";
        if (looksLikeBid) return "bid-format JSON {speak,reason,urgency} where a contribution was expected";
        if (!parsed) return "unparseable / not the expected JSON object";
        if (detailEmpty) return "parsed but 'detail' was blank";
        return "unknown";
    }

    /// <summary>A single-line, length-capped preview of the raw output for the log.</summary>
    public static string Preview(string? raw, int max = 160)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "<empty>";
        var oneLine = raw.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return oneLine.Length > max ? oneLine[..max] + "…" : oneLine;
    }
}

/// <summary>Which phase a contribution belongs to, so the shared context can be grouped meaningfully.</summary>
internal enum ContributionKind { InitialPosition, Debate }

/// <summary>A single contribution in the clean debate transcript — narrative prose only, never JSON or tool messages.</summary>
internal sealed record CouncilContribution(string AgentName, string DisplayName, string Content, ContributionKind Kind = ContributionKind.Debate);

/// <summary>
/// Maps clean Foundry agent names (<c>gc-{id}</c>) to human display names used in the
/// transcript so the LLM (and the minutes) reads "CISO said…" rather than "gc-ciso said…".
/// </summary>
internal static class AgentNaming
{
    public static string DisplayName(string agentName) =>
        CouncilMembers.ByAgentName(agentName)?.Name ?? agentName;
}

/// <summary>
/// Extracts citations from a streamed response's content annotations. Citations from both
/// The Foundry IQ knowledge-base MCP tool (knowledge_base_retrieve) surfaces
/// as <see cref="CitationAnnotation"/> (Title + Url) on <see cref="AIContent.Annotations"/>.
/// Graceful: returns an empty list when no citations are present; accepts title-only citations
/// (some KB references expose a ref title without a resolvable URL).
/// </summary>
internal static class CitationCollector
{
    public static List<Citation> Extract(IEnumerable<AIContent> contents)
    {
        var citations = new List<Citation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var content in contents)
        {
            if (content.Annotations is null) continue;
            foreach (var annotation in content.Annotations)
            {
                if (annotation is not CitationAnnotation citation) continue;
                var url = citation.Url?.ToString();
                var title = citation.Title;
                var hasUrl = !string.IsNullOrWhiteSpace(url);
                var hasTitle = !string.IsNullOrWhiteSpace(title);
                if (!hasUrl && !hasTitle) continue;

                // De-dupe on URL when present, otherwise on title (KB ref-only citations).
                var key = hasUrl ? url! : title!;
                if (!seen.Add(key)) continue;

                citations.Add(new Citation(hasTitle ? title! : url!, hasUrl ? url! : string.Empty));
            }
        }

        return citations;
    }
}

internal static class ToolCallCollector
{
    public static List<ToolCallRecord> Extract(string agentName, IEnumerable<AIContent> contents)
    {
        var pending = new Dictionary<string, FunctionCallContent>(StringComparer.Ordinal);
        var records = new List<ToolCallRecord>();
        foreach (var content in contents)
        {
            if (content is FunctionCallContent call)
            {
                pending[call.CallId] = call;
                continue;
            }

            if (content is not FunctionResultContent result ||
                !pending.Remove(result.CallId, out var matched)) continue;
            var json = Serialize(result.Result);
            records.Add(new ToolCallRecord
            {
                Member = agentName,
                Tool = matched.Name,
                Source = FindSource(json) ?? "unknown",
                Summary = Summarise(json),
                Json = json,
                Success = result.Exception is null
            });
        }

        return records;
    }

    private static string Serialize(object? value)
    {
        if (value is string text)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(new { value = text }, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? FindSource(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return FindStringProperty(document.RootElement, "source");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
                var nested = FindStringProperty(property.Value, propertyName);
                if (nested is not null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindStringProperty(item, propertyName);
                if (nested is not null) return nested;
            }
        }
        return null;
    }

    private static string Summarise(string json)
    {
        var oneLine = json.Replace('\r', ' ').Replace('\n', ' ');
        oneLine = System.Text.RegularExpressions.Regex.Replace(oneLine, "\\s+", " ").Trim();
        return oneLine.Length <= 180 ? oneLine : oneLine[..180] + "…";
    }
}
