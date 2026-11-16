using System.Runtime.InteropServices;

namespace ClaudeTray.Install;

/// <summary>
/// ClaudeTray.exe je WinExe bez vlastite konzole; za --install i --uninstall ispis ide u konzolu roditelja (spec 10.2).
/// Console.Out se stvara tek pri prvom pristupu, nakon spajanja, pa piše u tu konzolu s njezinom kodnom stranicom.
/// </summary>
public static partial class ConsoleAttach
{
    const int ATTACH_PARENT_PROCESS = -1;

    public static void Attach() => AttachConsole(ATTACH_PARENT_PROCESS);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
