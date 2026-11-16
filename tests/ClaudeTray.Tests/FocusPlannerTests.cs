using ClaudeTray.Core;
using ClaudeTray.Model;

namespace ClaudeTray.Tests;

/// <summary>Što klik na sesiju radi, prema entrypointu i TERM_PROGRAM-u (spec 7, dodatak A (f)).</summary>
public class FocusPlannerTests
{
    static SessionRecord Rec(string entrypoint, string termProgram) =>
        new() { SessionId = "s1", Entrypoint = entrypoint, TermProgram = termProgram, Pid = 42, PidStartedAt = 7 };

    [Theory]
    [InlineData("claude-vscode", "", true, FocusKind.VsCodeWindowAndUri)]
    [InlineData("claude-vscode", "vscode", true, FocusKind.VsCodeWindowAndUri)]
    [InlineData("cli", "vscode", true, FocusKind.VsCodeWindowOnly)]
    [InlineData("cli", "VSCode", true, FocusKind.VsCodeWindowOnly)]
    [InlineData("cli", "", true, FocusKind.TerminalWindow)]
    [InlineData("sdk-cli", "", true, FocusKind.TerminalWindow)]
    [InlineData("", "", true, FocusKind.TerminalWindow)]
    [InlineData("claude-vscode", "", false, FocusKind.None)]
    [InlineData("cli", "", false, FocusKind.None)]
    public void Plan_Cases(string entrypoint, string termProgram, bool alive, FocusKind expected) =>
        Assert.Equal(expected, FocusPlanner.Plan(Rec(entrypoint, termProgram), alive));
}
