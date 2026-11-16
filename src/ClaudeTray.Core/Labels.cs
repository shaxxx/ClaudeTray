namespace ClaudeTray.Core;

/// <summary>Natpisi na hrvatskom (spec 5.2).</summary>
public static class Labels
{
    public const string Thinking = "Razmišlja…";
    public const string Asking = "Pita te";
    public const string Permission = "Traži dozvolu";
    public const string Done = "Gotovo";
    public const string Idle = "Miruje";
    public const string OtherTool = "Koristi alat";
    public const string UnknownError = "nepoznata greška";

    public static string ForTool(string? toolName) => toolName switch
    {
        "Bash" or "PowerShell" => "Pokreće naredbu",
        "Edit" or "MultiEdit" or "NotebookEdit" => "Uređuje",
        "Write" => "Piše",
        "Read" => "Čita",
        "Grep" or "Glob" => "Traži",
        "WebFetch" => "Čita web",
        "WebSearch" => "Pretražuje web",
        "Task" or "Agent" => "Delegira",
        "TodoWrite" or "TaskCreate" or "TaskUpdate" => "Planira",
        "Skill" => "Koristi skill",
        _ => OtherTool,
    };

    public static string ForError(string? error) => error switch
    {
        null or "" => UnknownError,
        "rate_limit" => "prekoračen limit",
        "overloaded" => "preopterećeno",
        "authentication_failed" or "oauth_org_not_allowed" => "prijava nije uspjela",
        "billing_error" or "account_on_hold" => "problem s računom",
        "server_error" => "greška servera",
        "max_output_tokens" => "predug odgovor",
        _ => error,
    };
}
