using System.Drawing;

namespace ClaudeTray.Icons;

/// <summary>Boje ikone (spec 6.2). Početne vrijednosti; potvrđuju se pregledom kontrasta (--render-icons).</summary>
public static class Palette
{
    public static readonly Color Orange = ColorTranslator.FromHtml("#D97757");
    public static readonly Color Yellow = ColorTranslator.FromHtml("#F2C744");
    public static readonly Color Red = ColorTranslator.FromHtml("#D13438");
    public static readonly Color RestOnDark = ColorTranslator.FromHtml("#FFFFFF");
    public static readonly Color RestOnLight = ColorTranslator.FromHtml("#000000");

    /// <summary>Približne boje trake u Windowsu 11, samo za list za pregled.</summary>
    public static readonly Color TaskbarDark = ColorTranslator.FromHtml("#1C1C1C");
    public static readonly Color TaskbarLight = ColorTranslator.FromHtml("#F3F3F3");
}
