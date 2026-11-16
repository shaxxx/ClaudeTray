using ClaudeTray.Core;

namespace ClaudeTray.Tests;

public class ChildEnvironmentTests
{
    [Theory]
    [InlineData("ELECTRON_RUN_AS_NODE")]
    [InlineData("VSCODE_PID")]
    [InlineData("VSCODE_ESM_ENTRYPOINT")]
    [InlineData("CLAUDE_PID")]
    [InlineData("CLAUDECODE")]
    [InlineData("CLAUDE_CODE_ENTRYPOINT")]
    [InlineData("TERM_PROGRAM")]
    [InlineData("TERM_PROGRAM_VERSION")]
    [InlineData("vscode_cwd")]
    public void Removed(string name) => Assert.False(ChildEnvironment.IsInherited(name));

    [Theory]
    [InlineData("PATH")]
    [InlineData("USERPROFILE")]
    [InlineData("LOCALAPPDATA")]
    [InlineData("CLAUDE_TRAY_HOME")]
    [InlineData("CLAUDE_TRAY_DEBUG")]
    public void Kept(string name) => Assert.True(ChildEnvironment.IsInherited(name));

    [Fact]
    public void BuildBlock_FiltersSortsAndTerminates()
    {
        var vars = new Dictionary<string, string>
        {
            ["Path"] = @"C:\x", ["ELECTRON_RUN_AS_NODE"] = "1", ["ALLUSERSPROFILE"] = @"C:\ProgramData", ["CLAUDE_PID"] = "9",
        };
        Assert.Equal("ALLUSERSPROFILE=C:\\ProgramData\0Path=C:\\x\0\0", ChildEnvironment.BuildBlock(vars));
    }
}
