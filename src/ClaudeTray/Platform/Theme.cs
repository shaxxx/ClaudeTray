using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClaudeTray.Platform;

/// <summary>Boja trake, smanjeni pokret i veličina male ikone (spec 6.2).</summary>
public static partial class Theme
{
    const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
    const int SM_CXSMICON = 49;

    public static bool TaskbarIsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
    }

    public static bool AnimationsEnabled() =>
        !SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, out int enabled, 0) || enabled != 0;

    /// <summary>
    /// Mala ikona za DPI trake (Shell_TrayWnd). Sistemski DPI procesa se ne mijenja do odjave, pa ne bi pratio
    /// promjenu skaliranja.
    /// </summary>
    public static int SmallIconSize()
    {
        IntPtr taskbar = FindWindowW("Shell_TrayWnd", null);
        uint dpi = taskbar != IntPtr.Zero ? GetDpiForWindow(taskbar) : 0;
        if (dpi == 0) dpi = GetDpiForSystem();
        return GetSystemMetricsForDpi(SM_CXSMICON, dpi);
    }

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr FindWindowW(string className, string? windowName);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(IntPtr window);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint action, uint param, out int value, uint winIni);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();
}
