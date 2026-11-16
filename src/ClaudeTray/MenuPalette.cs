using System.Drawing;

namespace ClaudeTray;

/// <summary>
/// Boje izbornika u stilu Windowsa 11, za svijetlu i tamnu traku. Izbornik ih ne uzima iz WinFormsa, jer WinForms
/// tamni način bira samo pri pokretanju i ne prati kasniju promjenu teme (ručna provjera, 16. 11.).
/// </summary>
public sealed record MenuPalette(Color Background, Color Text, Color Hover, Color Border, Color Separator)
{
    public static readonly MenuPalette Light = new(
        Background: Color.FromArgb(0xF9, 0xF9, 0xF9), Text: Color.FromArgb(0x1A, 0x1A, 0x1A),
        Hover: Color.FromArgb(0xE8, 0xE8, 0xE8), Border: Color.FromArgb(0xDB, 0xDB, 0xDB), Separator: Color.FromArgb(0xE0, 0xE0, 0xE0));

    public static readonly MenuPalette Dark = new(
        Background: Color.FromArgb(0x2C, 0x2C, 0x2C), Text: Color.White,
        Hover: Color.FromArgb(0x3D, 0x3D, 0x3D), Border: Color.FromArgb(0x40, 0x40, 0x40), Separator: Color.FromArgb(0x48, 0x48, 0x48));

    public static MenuPalette For(bool light) => light ? Light : Dark;
}

/// <summary>Tablica boja za <see cref="ToolStripProfessionalRenderer"/>: ravna pozadina, bez gradijenata i rubova stavki.</summary>
public sealed class MenuColorTable(MenuPalette p) : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => p.Background;
    public override Color ImageMarginGradientBegin => p.Background;
    public override Color ImageMarginGradientMiddle => p.Background;
    public override Color ImageMarginGradientEnd => p.Background;
    public override Color MenuItemSelected => p.Hover;
    public override Color MenuItemSelectedGradientBegin => p.Hover;
    public override Color MenuItemSelectedGradientEnd => p.Hover;
    public override Color MenuItemPressedGradientBegin => p.Hover;
    public override Color MenuItemPressedGradientMiddle => p.Hover;
    public override Color MenuItemPressedGradientEnd => p.Hover;
    public override Color MenuItemBorder => p.Hover;
    public override Color MenuBorder => p.Border;
    public override Color SeparatorDark => p.Separator;
    public override Color SeparatorLight => p.Background;
}
