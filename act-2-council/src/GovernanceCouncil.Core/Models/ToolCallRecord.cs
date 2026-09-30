namespace GovernanceCouncil.Core.Models;

using System.Text.Json.Serialization;

public sealed record ToolCallRecord
{
    [JsonPropertyName("member")]
    public required string Member { get; init; }

    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("json")]
    public required string Json { get; init; }

    [JsonPropertyName("success")]
    public required bool Success { get; init; }
}
