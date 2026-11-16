using System.Drawing;
using System.Runtime.InteropServices;

namespace ClaudeTray.Icons;

/// <summary>Sve sličice za jednu veličinu i boju trake. HICON-i se oslobađaju u Dispose.</summary>
public sealed partial class IconSet : IDisposable
{
    /// <summary>
    /// Kao u simulaciji: 9° svakih 83 ms, uz pulsiranje zraka. 40 sličica je puni krug (40 · 9° = 360°) i točno
    /// tri pulsa, pa se petlja zatvara bez trzaja.
    /// </summary>
    public const int WorkingFrameCount = 40;
    public const float WorkingStepDeg = 9f;
    const int PulsesPerLoop = 3;

    /// <summary>Kao u simulaciji: duge zrake 0,9 + 0,1·sin, kratke 0,9 + 0,1·cos.</summary>
    public static (float Long, float Short) PulseScales(int frame)
    {
        double phase = frame * 2 * Math.PI * PulsesPerLoop / WorkingFrameCount;
        return ((float)(0.9 + 0.1 * Math.Sin(phase)), (float)(0.9 + 0.1 * Math.Cos(phase)));
    }

    readonly List<IntPtr> _handles = [];

    public Icon Rest { get; }
    public Icon Waiting { get; }
    public Icon Error { get; }
    public IReadOnlyList<Icon> Working { get; }

    IconSet(int sizePx, bool lightTaskbar)
    {
        Rest = Make(sizePx, lightTaskbar ? Palette.RestOnLight : Palette.RestOnDark, 0, null);
        Waiting = Make(sizePx, Palette.Orange, 0, Palette.Yellow);
        Error = Make(sizePx, Palette.Red, 0, null);
        Working = Enumerable.Range(0, WorkingFrameCount)
                            .Select(i =>
                            {
                                var (longScale, shortScale) = PulseScales(i);
                                return Make(sizePx, Palette.Orange, i * WorkingStepDeg, null, longScale, shortScale);
                            })
                            .ToList();
    }

    public static IconSet Create(int sizePx, bool lightTaskbar) => new(sizePx, lightTaskbar);

    Icon Make(int size, Color fill, float angle, Color? dot, float longScale = 1f, float shortScale = 1f)
    {
        using var bmp = StarRenderer.Render(size, fill, angle, dot, longScale, shortScale);
        IntPtr h = bmp.GetHicon();
        _handles.Add(h);
        return Icon.FromHandle(h);
    }

    public void Dispose()
    {
        foreach (var h in _handles) DestroyIcon(h);
        _handles.Clear();
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr icon);
}
