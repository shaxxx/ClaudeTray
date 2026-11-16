using System.Text.Json.Serialization;

namespace ClaudeTray.Core;

/// <summary>Stanje sesije kako ga zapisuje hook (spec 5.3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))]
public enum SessionStatus
{
    [JsonStringEnumMemberName("idle")] Idle,
    [JsonStringEnumMemberName("working")] Working,
    [JsonStringEnumMemberName("waiting")] Waiting,
    [JsonStringEnumMemberName("done")] Done,
    [JsonStringEnumMemberName("error")] Error,
}

/// <summary>Sadržaj datoteke <c>state\&lt;session_id&gt;.json</c> (spec 5.3).</summary>
public sealed record SessionRecord
{
    public int V { get; init; } = 1;
    public string SessionId { get; init; } = "";
    public SessionStatus State { get; init; }
    public string Detail { get; init; } = "";
    public string Tool { get; init; } = "";
    public string Cwd { get; init; } = "";
    public string ProjectDir { get; init; } = "";
    public string Entrypoint { get; init; } = "";
    public string TermProgram { get; init; } = "";
    public int Pid { get; init; }
    public long PidStartedAt { get; init; }
    public string Transcript { get; init; } = "";
    public string Title { get; init; } = "";
    public bool Started { get; init; }
    public long TurnStartedAt { get; init; }
    public long UpdatedAt { get; init; }
}
