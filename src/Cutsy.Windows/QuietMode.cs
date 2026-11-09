using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cutsy.Windows;

internal enum ExclusionMode { Foreground, Running }
internal sealed record AppExclusion(string Executable, ExclusionMode Mode);

internal sealed class QuietMode(nint window) : IDisposable
{
    private readonly bool registered = Native.WTSRegisterSessionNotification(window, 0);
    private bool locked;
    private bool disconnected;
    private long? eligibleSince;

    internal bool Gaming { get; set; }
    internal bool Fullscreen { get; set; } = true;
    internal AppExclusion[] Exclusions { get; set; } = [];
    internal bool Active => Reason is not null;
    internal string? Reason { get; private set; }

    internal bool Evaluate(long now)
    {
        string? before = Reason;
        string? block = BlockReason();
        if (block is not null)
        {
            eligibleSince = null;
            Reason = block;
        }
        else if (Reason is not null)
        {
            eligibleSince ??= now;
            Reason = now - eligibleSince.Value >= 3000 ? null : "Resuming in a moment";
        }

        return (before is null) != (Reason is null);
    }

    private string? BlockReason()
    {
        int state = Native.SHQueryUserNotificationState(out int reported) == 0 ? reported : 0;
        if (locked || disconnected || state == 1)
            return "Windows session inactive";
        if (Gaming)
            return "Gaming mode";

        nint foreground = Native.GetForegroundWindow();
        string? excluded = ExcludedApplication(foreground);
        if (excluded is not null)
            return $"Paused for {excluded}";

        if (!Fullscreen)
            return null;
        if (state == 4)
            return "Presentation mode";
        if (state == 3)
            return "Exclusive fullscreen";
        if (IsMonitorFilling(foreground) || (state == 2 && !IsOrdinaryMaximized(foreground)))
            return "Fullscreen app";
        return null;
    }

    private static bool IsOrdinaryMaximized(nint hwnd) =>
        Native.IsZoomed(hwnd) && (Native.GetWindowLongPtr(hwnd, -16).ToInt64() & 0xC00000) == 0xC00000;

    private static bool IsMonitorFilling(nint hwnd)
    {
        if (hwnd == 0 || hwnd == Native.GetShellWindow() || WindowTracker.IsShellClass(hwnd) || IsOrdinaryMaximized(hwnd))
            return false;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId)
            return false;
        Rectangle bounds = WindowTracker.WindowBounds(hwnd);
        if (bounds.IsEmpty)
            return false;
        var monitor = Screen.FromRectangle(bounds).Bounds;
        int tolerance = (int)Math.Ceiling(3 * WindowTracker.MonitorScale(monitor.Location));
        return bounds.Left <= monitor.Left + tolerance && bounds.Top <= monitor.Top + tolerance &&
            bounds.Right >= monitor.Right - tolerance && bounds.Bottom >= monitor.Bottom - tolerance;
    }

    private string? ExcludedApplication(nint foreground)
    {
        if (Exclusions.Length == 0)
            return null;
        string? name = null;
        if (foreground != 0 && Native.GetWindowThreadProcessId(foreground, out uint pid) != 0)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName + ".exe";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Foreground process exited or Windows denied its details.
            }
        }

        foreach (var exclusion in Exclusions)
        {
            if (string.Equals(name, exclusion.Executable, StringComparison.OrdinalIgnoreCase))
                return exclusion.Executable;
            if (exclusion.Mode != ExclusionMode.Running)
                continue;
            try
            {
                Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exclusion.Executable));
                bool running = processes.Length != 0;
                foreach (var process in processes)
                    process.Dispose();
                if (running)
                    return exclusion.Executable;
            }
            catch (Win32Exception)
            {
                // Foreground and the shell notification signal still apply.
            }
        }
        return null;
    }

    internal void SessionChanged(int reason)
    {
        switch (reason)
        {
            case 1: // console connect
            case 3: // remote connect
                disconnected = false;
                break;
            case 2: // console disconnect
            case 4: // remote disconnect
            case 6: // logoff
                disconnected = true;
                break;
            case 7:
                locked = true;
                break;
            case 8:
                locked = false;
                break;
        }
    }

    public void Dispose()
    {
        if (registered)
            Native.WTSUnRegisterSessionNotification(window);
    }
}

internal static partial class Native
{
    internal const int WmSessionChange = 0x02B1;

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSRegisterSessionNotification(nint window, uint flags);

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSUnRegisterSessionNotification(nint window);

    [LibraryImport("shell32.dll")]
    internal static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsZoomed(nint window);
}
