using System.Drawing;
using ClaudeTray.Icons;
using ClaudeTray.Platform;

namespace ClaudeTray.Tests;

/// <summary>
/// Zvjezdica kao u simulaciji: 8 zraka naizmjenične duljine (R i 0,62·R), debljine 12 % veličine, zaobljenih krajeva.
/// Na 16 px: središte 8, debljina 1,92, R = 6,84; zraka 0 ide desno, 2 dolje, 6 gore; neparne su dijagonalne i kratke.
/// </summary>
public class StarRendererTests
{
    static void AssertColor(Color expected, Color actual, string where)
    {
        Assert.True(Math.Abs(expected.R - actual.R) <= 8 && Math.Abs(expected.G - actual.G) <= 8 && Math.Abs(expected.B - actual.B) <= 8
                    && actual.A >= 200, $"{where}: očekivano {expected}, dobiveno {actual}");
    }

    [Fact]
    public void Render16_RayIsFill_CornerTransparent()
    {
        using var bmp = StarRenderer.Render(16, Palette.Orange, 0, null);
        Assert.Equal(new Size(16, 16), bmp.Size);
        AssertColor(Palette.Orange, bmp.GetPixel(8, 4), "gornja zraka");
        Assert.Equal(0, bmp.GetPixel(0, 0).A);
    }

    [Fact]
    public void Render16_DotIsYellow()
    {
        using var bmp = StarRenderer.Render(16, Palette.Orange, 0, Palette.Yellow);
        AssertColor(Palette.Yellow, bmp.GetPixel(12, 12), "točka");
        AssertColor(Palette.Orange, bmp.GetPixel(8, 4), "gornja zraka");
    }

    [Fact]
    public void Render16_DotHasTransparentRing()
    {
        // Piksel (9,9) leži na kratkoj dijagonalnoj zraci prema točki: bez točke je pun, s točkom je u prozirnom prstenu.
        using var plain = StarRenderer.Render(16, Palette.Orange, 0, null);
        using var withDot = StarRenderer.Render(16, Palette.Orange, 0, Palette.Yellow);
        Assert.True(plain.GetPixel(9, 9).A > 200, $"bez točke: {plain.GetPixel(9, 9)}");
        Assert.True(withDot.GetPixel(9, 9).A < 80, $"s točkom: {withDot.GetPixel(9, 9)}");
    }

    [Theory]
    [InlineData(16, 1.12f)]
    [InlineData(24, 1.68f)]
    [InlineData(32, 2.24f)]
    [InlineData(10, 1f)]
    public void DotRing_ScalesWithSize(int size, float ring) => Assert.Equal(ring, StarRenderer.DotRingPx(size), 3);

    [Fact]
    public void Render32_ErrorRayRed()
    {
        using var bmp = StarRenderer.Render(32, Palette.Red, 0, null);
        AssertColor(Palette.Red, bmp.GetPixel(24, 15), "desna zraka");
    }

    [Fact]
    public void Rotation_ChangesPixels()
    {
        using var a = StarRenderer.Render(32, Palette.Orange, 0, null);
        using var b = StarRenderer.Render(32, Palette.Orange, 22.5f, null);
        Assert.True(Differs(a, b));
    }

    [Fact]
    public void Pulse_ShortensLongRays()
    {
        // Vrh desne duge zrake na 32 px: puna duljina doseže x ≈ 31,8; na 80 % samo ≈ 29.
        using var full = StarRenderer.Render(32, Palette.Orange, 0, null);
        using var pulsed = StarRenderer.Render(32, Palette.Orange, 0, null, longScale: 0.8f, shortScale: 1f);
        Assert.True(full.GetPixel(29, 15).A > 200, $"puna: {full.GetPixel(29, 15)}");
        Assert.True(pulsed.GetPixel(29, 15).A < 100, $"pulsirana: {pulsed.GetPixel(29, 15)}");
    }

    [Fact]
    public void PulseScales_LikeMockup_ThreeCyclesPerLoop()
    {
        Assert.Equal((0.9f, 1.0f), IconSet.PulseScales(0));
        Assert.Equal(IconSet.PulseScales(0), IconSet.PulseScales(IconSet.WorkingFrameCount));
        var all = Enumerable.Range(0, IconSet.WorkingFrameCount).Select(IconSet.PulseScales).ToList();
        Assert.InRange(all.Max(p => p.Long), 0.99f, 1.0f);
        Assert.InRange(all.Min(p => p.Long), 0.8f, 0.81f);
        int upward = Enumerable.Range(0, all.Count).Count(f => all[(f + all.Count - 1) % all.Count].Long < 0.9f && all[f].Long >= 0.9f);
        Assert.Equal(3, upward);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    public void IconSet_Has40WorkingFramesOf9Degrees(int size)
    {
        // Kao u simulaciji: 9° svakih 83 ms; 40 sličica je puni krug i točno tri pulsa, pa petlja ne trza.
        Assert.Equal(9f, IconSet.WorkingStepDeg);
        using var set = IconSet.Create(size, lightTaskbar: false);
        Assert.Equal(40, set.Working.Count);
        Assert.Equal(size, set.Rest.Width);
        Assert.Equal(size, set.Working[39].Height);
    }

    [Fact]
    public void Export_WritesAllFrames()
    {
        using var t = new TempDir();
        IconExporter.Export(t.Path);
        var files = Directory.GetFiles(t.Path, "*.png");
        Assert.Equal(4 * 2 * (3 + 40), files.Length);
        Assert.Contains(Path.Combine(t.Path, "dark-24-working-03.png"), files);
        Assert.Contains(Path.Combine(t.Path, "light-16-rest.png"), files);
    }

    [Fact]
    public void ExportPreview_WritesSheet()
    {
        using var t = new TempDir();
        IconExporter.ExportPreview(t.Path);
        Assert.True(File.Exists(Path.Combine(t.Path, "pregled.png")));
    }

    [Fact]
    public void Theme_SmallIconSizeInRange() => Assert.InRange(Theme.SmallIconSize(), 16, 64);

    static bool Differs(Bitmap a, Bitmap b)
    {
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                if (a.GetPixel(x, y) != b.GetPixel(x, y)) return true;
        return false;
    }
}
