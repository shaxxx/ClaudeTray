using ClaudeTray.Core;
using ClaudeTray.Model;
using static ClaudeTray.Tests.AggregatorTests;

namespace ClaudeTray.Tests;

public class TooltipFormatterTests
{
    const long Now = 100_000_000;

    static string Format(params SessionView[] views) => TooltipFormatter.Format(Aggregator.Visible(views, Now), Now);

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(134_000, "2:14")]
    [InlineData(3_725_000, "1:02:05")]
    [InlineData(-5, "0:00")]
    public void Elapsed_Cases(long ms, string expected) => Assert.Equal(expected, TooltipFormatter.Elapsed(ms));

    [Fact]
    public void Format_SpecRows() => Assert.Equal(
        "web2023 – Pita te\nApi – Greška: prekoračen limit\nEvidencija – Uređuje 2:14",
        Format(View("1", SessionStatus.Working, project: @"C:\projekti\Evidencija", detail: "Uređuje", turnStart: Now - 134_000, age: 3000),
               View("2", SessionStatus.Waiting, project: @"C:\projekti\web\web2023", detail: "Pita te"),
               View("3", SessionStatus.Error, project: @"C:\projekti\Api", detail: "prekoračen limit"),
               View("4", SessionStatus.Done, project: @"C:\projekti\X")));

    [Fact]
    public void Format_AllRest_ShowsAll() => Assert.Equal(
        "A – Gotovo\nB – Miruje",
        Format(View("1", SessionStatus.Done, project: @"C:\A", age: 1000), View("2", SessionStatus.Idle, project: @"C:\B", age: 2000)));

    [Fact]
    public void Format_AcknowledgedErrorHidesRest() => Assert.Equal(
        "Api – Greška: preopterećeno",
        Format(View("1", SessionStatus.Error, ack: true, project: @"C:\Api", detail: "preopterećeno"), View("2", SessionStatus.Done, project: @"C:\A")));

    [Fact]
    public void Format_DuplicateNames()
    {
        string text = Format(
            View("1", SessionStatus.Working, project: @"C:\projekti\Evidencija", title: "napravi export u PDF za klijenta", detail: "Uređuje", turnStart: Now - 5000),
            View("2", SessionStatus.Waiting, project: @"C:\projekti\Evidencija", title: "popravi bug", detail: "Pita te"));
        Assert.Equal("Evidencija (popravi bug) – Pita te\nEvidencija (napravi export u PD…) – Uređuje 0:05", text);
    }

    [Fact]
    public void Format_TooLong_PlusMore()
    {
        var views = Enumerable.Range(0, 8)
            .Select(i => View(i.ToString(), SessionStatus.Working, project: @"C:\projekti\" + new string((char)('a' + i), 30),
                              detail: "Uređuje", turnStart: Now - 5000, age: i))
            .ToArray();
        string text = Format(views);
        var lines = text.Split('\n');

        Assert.True(text.Length <= TooltipFormatter.MaxLength, $"duljina {text.Length}");
        Assert.Matches(@"^\+\d+ još$", lines[^1]);
        int hidden = int.Parse(lines[^1][1..^4]);
        Assert.Equal(8, lines.Length - 1 + hidden);
        Assert.StartsWith(new string('a', 15) + "… – Uređuje", lines[0]);
    }

    [Fact]
    public void Format_PathWithSpaceAndCroatian()
    {
        Assert.Equal("Novi modul – Uređuje 0:05",
            Format(View("1", SessionStatus.Working, project: @"C:\projekti\Novi modul\", detail: "Uređuje", turnStart: Now - 5000)));
        Assert.Equal("Čakovec – Pita te", Format(View("1", SessionStatus.Waiting, project: @"C:\projekti\Čakovec", detail: "Pita te")));
    }

    [Fact]
    public void Format_NoProjectDir_UsesCwdThenQuestionMark()
    {
        var withCwd = View("1", SessionStatus.Waiting, project: "", detail: "Pita te");
        withCwd = withCwd with { Record = withCwd.Record with { Cwd = @"C:\projekti\Kasa" } };
        Assert.Equal("Kasa – Pita te", TooltipFormatter.Format([withCwd], Now));
        Assert.Equal("? – Pita te", Format(View("1", SessionStatus.Waiting, project: "", detail: "Pita te")));
    }

    [Fact]
    public void Format_Empty() => Assert.Equal("Claude: nema aktivnih sesija", TooltipFormatter.Format([], Now));

    [Fact]
    public void RowText_WithoutTitle() =>
        Assert.Equal("Evidencija – Gotovo",
            TooltipFormatter.RowText(View("1", SessionStatus.Done, project: @"C:\projekti\Evidencija", title: "x"), Now, withTitle: false));
}
