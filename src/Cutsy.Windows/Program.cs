using System.Runtime.InteropServices;

namespace Cutsy.Windows;

internal static class Program
{
    internal static readonly uint ShowSettingsMessage = Native.RegisterWindowMessage("TobbyTravel.Cutsy.ShowSettings.1");

    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(false, @"Local\TobbyTravel.Cutsy.Windows.v1");
        bool owns;
        try
        {
            owns = instance.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            owns = true;
        }
        if (!owns)
        {
            SignalExistingInstance();
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            using var cutsy = new MascotContext();
            Application.Run(cutsy);
        }
        finally
        {
            instance.ReleaseMutex();
        }
    }

    private static void SignalExistingInstance()
    {
        for (int i = 0; i < 40; i++)
        {
            nint window = Native.FindWindow(null, "Cutsy Mascot");
            if (window != 0)
            {
                Native.GetWindowThreadProcessId(window, out uint owner);
                Native.AllowSetForegroundWindow(owner);
                if (Native.PostMessage(window, ShowSettingsMessage, 0, 0))
                    return;
            }
            Thread.Sleep(50);
        }
    }
}

internal static partial class Native
{
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindow(string? className, string name);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string message);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AllowSetForegroundWindow(uint process);
}
