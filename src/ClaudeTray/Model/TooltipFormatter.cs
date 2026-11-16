using ClaudeTray.Core;

namespace ClaudeTray.Model;

/// <summary>Tekst tooltipa, najviše 127 znakova, jedan red po sesiji (spec 6.3).</summary>
public static class TooltipFormatter
{
    public const int MaxLength = 127;
    public const string Empty = "Claude: nema aktivnih sesija";

    const int TitleMax = 20, ShortNameMax = 16, ShortTitleMax = 12;

    public static string Format(IReadOnlyList<SessionView> visible, long nowMs)
    {
        if (visible.Count == 0) return Empty;
        var rows = visible.Any(v => v.Rank > 1) ? visible.Where(v => v.Rank > 1).ToList() : visible.ToList();
        var duplicateNames = rows.GroupBy(Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

        List<string> Build(int nameMax, int titleMax) =>
            rows.Select(v => RowText(v, nowMs, duplicateNames.Contains(Name(v)), nameMax, titleMax)).ToList();

        string full = string.Join('\n', Build(int.MaxValue, TitleMax));
        if (full.Length <= MaxLength) return full;

        var shortRows = Build(ShortNameMax, ShortTitleMax);
        string shortened = string.Join('\n', shortRows);
        if (shortened.Length <= MaxLength) return shortened;

        for (int hidden = 1; hidden <= shortRows.Count; hidden++)
        {
            string text = string.Join('\n', shortRows.Take(shortRows.Count - hidden).Append($"+{hidden} još"));
            if (text.Length <= MaxLength) return text;
        }
        return $"+{shortRows.Count} još";
    }

    public static string RowText(SessionView v, long nowMs, bool withTitle) => RowText(v, nowMs, withTitle, int.MaxValue, TitleMax);

    static string RowText(SessionView v, long nowMs, bool withTitle, int nameMax, int titleMax)
    {
        string name = Trunc(Name(v), nameMax);
        if (withTitle && v.Record.Title.Length > 0) name += $" ({Trunc(v.Record.Title, titleMax)})";
        return $"{name} – {Label(v, nowMs)}";
    }

    static string Label(SessionView v, long nowMs) => v.Effective switch
    {
        SessionStatus.Working => v.Record.TurnStartedAt > 0 ? $"{v.Record.Detail} {Elapsed(nowMs - v.Record.TurnStartedAt)}" : v.Record.Detail,
        SessionStatus.Waiting => v.Record.Detail,
        SessionStatus.Error => "Greška: " + v.Record.Detail,
        SessionStatus.Done => Labels.Done,
        _ => Labels.Idle,
    };

    public static string Elapsed(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    static string Name(SessionView v)
    {
        string dir = v.Record.ProjectDir.Length > 0 ? v.Record.ProjectDir : v.Record.Cwd;
        string trimmed = dir.TrimEnd('\\', '/');
        if (trimmed.Length == 0) return "?";
        string name = Path.GetFileName(trimmed);
        return name.Length > 0 ? name : trimmed;
    }

    static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
