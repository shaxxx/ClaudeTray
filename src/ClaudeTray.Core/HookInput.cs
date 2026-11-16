using System.Text.Json.Serialization;

namespace ClaudeTray.Core;

/// <summary>Polja koja hook čita sa stdina. Ostala polja Claude Codea se ignoriraju.</summary>
public sealed record HookInput
{
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
    [JsonPropertyName("cwd")] public string? Cwd { get; init; }
    [JsonPropertyName("transcript_path")] public string? TranscriptPath { get; init; }
    [JsonPropertyName("hook_event_name")] public string? HookEventName { get; init; }
    [JsonPropertyName("tool_name")] public string? ToolName { get; init; }
    [JsonPropertyName("notification_type")] public string? NotificationType { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("session_title")] public string? SessionTitle { get; init; }
    [JsonPropertyName("prompt")] public string? Prompt { get; init; }
    /// <summary>Izvor za SessionStart: startup, resume, clear ili compact.</summary>
    [JsonPropertyName("source")] public string? Source { get; init; }
}
