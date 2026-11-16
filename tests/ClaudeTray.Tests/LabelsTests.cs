using ClaudeTray.Core;

namespace ClaudeTray.Tests;

public class LabelsTests
{
    [Theory]
    [InlineData("Bash", "Pokreće naredbu")]
    [InlineData("PowerShell", "Pokreće naredbu")]
    [InlineData("Edit", "Uređuje")]
    [InlineData("MultiEdit", "Uređuje")]
    [InlineData("NotebookEdit", "Uređuje")]
    [InlineData("Write", "Piše")]
    [InlineData("Read", "Čita")]
    [InlineData("Grep", "Traži")]
    [InlineData("Glob", "Traži")]
    [InlineData("WebFetch", "Čita web")]
    [InlineData("WebSearch", "Pretražuje web")]
    [InlineData("Task", "Delegira")]
    [InlineData("Agent", "Delegira")]
    [InlineData("TodoWrite", "Planira")]
    [InlineData("TaskCreate", "Planira")]
    [InlineData("TaskUpdate", "Planira")]
    [InlineData("Skill", "Koristi skill")]
    [InlineData("mcp__x__y", "Koristi alat")]
    [InlineData("ToolSearch", "Koristi alat")]
    [InlineData(null, "Koristi alat")]
    public void ForTool_MapsSpecTable(string? tool, string expected) => Assert.Equal(expected, Labels.ForTool(tool));

    [Theory]
    [InlineData("rate_limit", "prekoračen limit")]
    [InlineData("overloaded", "preopterećeno")]
    [InlineData("authentication_failed", "prijava nije uspjela")]
    [InlineData("oauth_org_not_allowed", "prijava nije uspjela")]
    [InlineData("billing_error", "problem s računom")]
    [InlineData("account_on_hold", "problem s računom")]
    [InlineData("server_error", "greška servera")]
    [InlineData("max_output_tokens", "predug odgovor")]
    [InlineData("weird_new", "weird_new")]
    [InlineData(null, "nepoznata greška")]
    [InlineData("", "nepoznata greška")]
    public void ForError_MapsSpecTable(string? error, string expected) => Assert.Equal(expected, Labels.ForError(error));
}
