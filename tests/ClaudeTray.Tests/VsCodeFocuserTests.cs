namespace ClaudeTray.Tests;

/// <summary>Prepoznavanje prozora VS Codea, URI sesije i pokretanje <c>code</c> (spec 7, dodatak A (d)).</summary>
public class VsCodeFocuserTests
{
    [Theory]
    [InlineData("Program.cs - Evidencija - Visual Studio Code", @"C:\projekti\Evidencija", true)]
    [InlineData("Evidencija - Visual Studio Code", @"C:\projekti\Evidencija", true)]
    [InlineData("● Program.cs - Evidencija - Visual Studio Code", @"C:\projekti\Evidencija", true)]
    [InlineData("Program.cs - evidencija - Visual Studio Code", @"c:\projekti\Evidencija", true)]
    [InlineData("Program.cs - Evidencija2 - Visual Studio Code", @"C:\projekti\Evidencija", false)]
    [InlineData("Moj naslov", @"C:\projekti\Evidencija", false)]
    [InlineData("Evidencija - Bilješke", @"C:\projekti\Evidencija", false)]
    [InlineData("x - Novi modul - Visual Studio Code", @"C:\projekti\Novi modul\", true)]
    [InlineData("x - Čakovec - Visual Studio Code", @"C:\projekti\Čakovec", true)]
    [InlineData(@"C: - Visual Studio Code", @"C:\", false)]
    public void Titles_Match(string title, string projectDir, bool expected) =>
        Assert.Equal(expected, VsCodeTitles.Matches(title, projectDir));

    // Završni pregled: bez prozora čiji naslov odgovara, `code` bi otvorio novi prozor; CLI put bira prozor samo po imenu
    // mape, pa `code` ne smije dobiti mapu koju možda nijedan prozor nema otvorenu.
    [Theory]
    [InlineData(true, 0, true, VsCodeStep1.Nothing)]
    [InlineData(true, 1, true, VsCodeStep1.RunCode)]
    [InlineData(true, 2, true, VsCodeStep1.RunCode)]
    [InlineData(true, 1, false, VsCodeStep1.ActivateFirst)]
    [InlineData(true, 0, false, VsCodeStep1.Nothing)]
    [InlineData(false, 0, true, VsCodeStep1.Nothing)]
    [InlineData(false, 1, true, VsCodeStep1.ActivateFirst)]
    [InlineData(false, 2, true, VsCodeStep1.ActivateFirst)]
    public void Step1_NeverOpensNewWindow(bool withUri, int candidates, bool codeAvailable, VsCodeStep1 expected) =>
        Assert.Equal(expected, VsCodeSteps.Step1(withUri, candidates, codeAvailable));

    [Fact]
    public void Uri_ForSession()
    {
        Assert.Equal("vscode://anthropic.claude-code/open?session=abc-123", VsCodeUri.ForSession("abc-123"));
        Assert.Equal("vscode://anthropic.claude-code/open?session=a%20b%26c", VsCodeUri.ForSession("a b&c"));
    }

    [Fact]
    public void BuildArgs_Quoting()
    {
        Assert.Equal("/c \"\"C:\\VS Code\\bin\\code.cmd\" \"C:\\projekti\\Novi modul\"\"",
                     VsCodeLauncher.BuildArgs(@"C:\VS Code\bin\code.cmd", @"C:\projekti\Novi modul"));
        Assert.Contains("\"C:\\projekti\\Čakovec\"", VsCodeLauncher.BuildArgs(@"C:\VS Code\bin\code.cmd", @"C:\projekti\Čakovec"));
    }

    [Fact]
    public void BuildArgs_TrailingBackslashDoesNotEscapeQuote() =>
        Assert.EndsWith("\"C:\\projekti\\Novi modul\"\"", VsCodeLauncher.BuildArgs(@"C:\code.cmd", @"C:\projekti\Novi modul\"));

    [Fact]
    public void FindCodeCmd_ReturnsExistingCmdOrNull()
    {
        string? cmd = VsCodeLauncher.FindCodeCmd();
        if (cmd is null) return;
        Assert.True(File.Exists(cmd), cmd);
        Assert.Equal("code.cmd", Path.GetFileName(cmd), ignoreCase: true);
    }

    // CLI u terminalu VS Codea može biti pokrenut u podmapi; prozor nosi ime korijenske mape.
    [Theory]
    [InlineData(@"C:\projekti\Evidencija\src\Api", @"C:\projekti\Evidencija")]
    [InlineData(@"C:\projekti\Evidencija", @"C:\projekti\Evidencija")]
    [InlineData(@"C:\projekti\Evidencija\", @"C:\projekti\Evidencija")]
    [InlineData(@"C:\drugo\nesto", null)]
    public void ResolveDir_WalksUpToOpenFolder(string projectDir, string? expected)
    {
        string[] titles = ["Program.cs - Evidencija - Visual Studio Code", "Moj naslov"];
        Assert.Equal(expected, VsCodeTitles.ResolveDir(projectDir, titles));
    }
}
