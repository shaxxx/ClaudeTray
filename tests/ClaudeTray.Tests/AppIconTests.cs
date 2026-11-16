using System.Drawing;
using ClaudeTray.Icons;

namespace ClaudeTray.Tests;

/// <summary>Ikona aplikacije (ClaudeTray.exe): varijanta C, svijetla zvjezdica na narančastoj zaobljenoj pločici (16. 11.).</summary>
public class AppIconTests
{
    static void AssertColor(Color expected, Color actual, string where) =>
        Assert.True(Math.Abs(expected.R - actual.R) <= 10 && Math.Abs(expected.G - actual.G) <= 10 && Math.Abs(expected.B - actual.B) <= 10
                    && actual.A >= 200, $"{where}: očekivano {expected}, dobiveno {actual}");

    [Fact]
    public void Render32_TileOrange_RoundedCornerTransparent()
    {
        using var bmp = AppIconRenderer.Render(32);
        Assert.Equal(new Size(32, 32), bmp.Size);
        Assert.True(bmp.GetPixel(0, 0).A < 40, $"kut: {bmp.GetPixel(0, 0)}");
        AssertColor(Palette.Orange, bmp.GetPixel(16, 3), "pločica iznad zvjezdice");
    }

    [Fact]
    public void Render48_RayIsIvory()
    {
        using var bmp = AppIconRenderer.Render(48);
        AssertColor(AppIconRenderer.StarColor, bmp.GetPixel(34, 24), "desna zraka");
    }

    [Theory]
    [InlineData(256)]
    [InlineData(128)]
    public void LargeSizes_CenterHasNoHole(int size)
    {
        using var bmp = AppIconRenderer.Render(size);
        AssertColor(AppIconRenderer.StarColor, bmp.GetPixel(size / 2, size / 2), "središte");
    }

    [Fact]
    public void IcoWriter_WritesDirectoryForAllSizes()
    {
        using var ms = new MemoryStream();
        IcoWriter.Write(ms, AppIconRenderer.Sizes.Select(s => (s, AppIconRenderer.Render(s))).ToList());
        AssertIco(ms.ToArray());
    }

    [Fact]
    public void CommittedIco_HasAllSizesAndLoads()
    {
        string path = Path.Combine(Repo.Root, "src", "ClaudeTray", "ClaudeTray.ico");
        AssertIco(File.ReadAllBytes(path));
        using var icon = new Icon(path, 32, 32);
        Assert.Equal(32, icon.Width);
    }

    [Fact]
    public void AppIcon_IsEmbeddedInPublishedExe()
    {
        using var icon = Icon.ExtractAssociatedIcon(Repo.Publish("ClaudeTray.exe"))!;
        using var bmp = icon.ToBitmap();
        AssertColor(Palette.Orange, bmp.GetPixel(bmp.Width / 2, bmp.Height / 10 + 1), "pločica u ikoni exe-a");
    }

    static void AssertIco(byte[] bytes)
    {
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));   // rezervirano
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));   // vrsta: ikona
        int count = BitConverter.ToUInt16(bytes, 4);
        var widths = Enumerable.Range(0, count).Select(i => bytes[6 + 16 * i] == 0 ? 256 : bytes[6 + 16 * i]).ToList();
        Assert.Equal(AppIconRenderer.Sizes, widths);
        for (int i = 0; i < count; i++)
        {
            int size = BitConverter.ToInt32(bytes, 6 + 16 * i + 8), offset = BitConverter.ToInt32(bytes, 6 + 16 * i + 12);
            Assert.True(offset + size <= bytes.Length);
            Assert.Equal(0x89, bytes[offset]);   // PNG potpis
        }
    }
}
