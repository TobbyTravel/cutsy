using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using Cutsy.Core;

namespace Cutsy.Windows;

internal sealed class WindowTracker : IDisposable
{
    private readonly record struct WindowInfo(nint Handle, Rectangle Bounds, bool CanSupport, bool Caption);
    private readonly Native.WinEventCallback callback;
    private readonly List<nint> hooks = [];
    private List<WindowInfo> windows = [];
    private Screen[] screens = [];
    private long nextReconcile;
    private bool dirty = true;

    internal bool ForegroundChanged { get; set; } = true;

    internal WindowTracker()
    {
        callback = OnWindowEvent;
        Hook(3, 3);               // EVENT_SYSTEM_FOREGROUND
        Hook(10, 11);             // EVENT_SYSTEM_MOVESIZESTART/END
        Hook(0x8000, 0x800B);     // create through location-change
    }

    internal void Invalidate() => dirty = true;

    private void Hook(uint first, uint last)
    {
        nint hook = Native.SetWinEventHook(first, last, 0, callback, 0, 0, 2); // OUTOFCONTEXT | SKIPOWNPROCESS
        if (hook != 0)
            hooks.Add(hook);
    }

    private void OnWindowEvent(nint hook, uint eventId, nint window, int objectId, int childId, uint thread, uint time)
    {
        if (eventId == 3)
            ForegroundChanged = true;
        if (window != 0 && objectId == 0 && childId == 0)
            dirty = true;
    }

    internal bool Refresh(long now)
    {
        if (!dirty && now < nextReconcile)
            return false;
        dirty = false;
        nextReconcile = now + 2000;
        screens = Screen.AllScreens.OrderByDescending(s => s.Primary).ToArray();
        var observed = new List<WindowInfo>();
        nint shell = Native.GetShellWindow();
        Native.EnumWindows((hwnd, _) =>
        {
            if (hwnd == shell || !Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd))
                return true;
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId || IsShellClass(hwnd))
                return true;
            if (Native.DwmGetInt(hwnd, 14, out int cloaked, 4) == 0 && cloaked != 0)
                return true;
            Rectangle bounds = WindowBounds(hwnd);
            if (bounds.Width < 8 || bounds.Height < 8)
                return true;
            long style = Native.GetWindowLongPtr(hwnd, -16).ToInt64();
            long exStyle = Native.GetWindowLongPtr(hwnd, -20).ToInt64();
            bool caption = (style & 0xC00000) == 0xC00000;
            observed.Add(new WindowInfo(hwnd, bounds, (exStyle & (Native.ExToolWindow | Native.ExNoActivate)) == 0, caption));
            return true;
        }, 0);
        windows = observed;
        return true;
    }

    internal DesktopSnapshot Snapshot(int logicalHeight, bool quiet)
    {
        var monitors = ImmutableArray.CreateBuilder<MonitorArea>();
        var surfaces = ImmutableArray.CreateBuilder<Surface>();
        foreach (var screen in screens)
        {
            Rectangle work = screen.WorkingArea;
            double scale = MonitorScale(new Point(work.Left + work.Width / 2, work.Top + work.Height / 2));
            monitors.Add(new MonitorArea(new Bounds(work.X, work.Y, work.Width, work.Height), scale));
            surfaces.Add(new Surface("floor-" + screen.DeviceName, SurfaceKind.Floor,
                new WorldPoint(work.Left, work.Bottom), work.Left, work.Right, work.Bottom, work.Height));
            double height = logicalHeight * scale;
            int clearance = (int)Math.Ceiling(height);

            for (int i = 0; i < windows.Count; i++)
            {
                var window = windows[i];
                if (!window.CanSupport)
                    continue;
                Rectangle box = window.Bounds;
                string id = $"window-{window.Handle:X}";
                var origin = new WorldPoint(box.Left, box.Top);
                int start = Math.Max(work.Left, box.Left);
                int end = Math.Min(work.Right, box.Right);
                int top = Math.Max(work.Top, box.Top);
                int bottom = Math.Min(work.Bottom, box.Bottom);
                int minWidth = (int)Math.Ceiling(height * .76);

                if (box.Top <= work.Bottom && box.Top - work.Top >= clearance)
                {
                    var segments = Exposed(i, start, end, box.Top - clearance, box.Top + 2, vertical: false);
                    // Preserve the caption-button area as a fallback only if no other segment fits.
                    int captionStart = box.Right - (int)Math.Ceiling(150 * scale);
                    var preferred = segments.Select(s => (Start: s.Start, End: Math.Min(s.End, captionStart)))
                        .Where(s => s.End - s.Start >= minWidth).ToArray();
                    if (window.Caption && preferred.Length != 0)
                        segments = [.. preferred];
                    Add(segments, SurfaceKind.WindowTop, box.Top, box.Top - work.Top);
                }
                if (box.Bottom >= work.Top && work.Bottom - box.Bottom >= clearance)
                    Add(Exposed(i, start, end, box.Bottom - 2, box.Bottom + clearance, false),
                        SurfaceKind.WindowBottom, box.Bottom, work.Bottom - box.Bottom);
                if (box.Left <= work.Right && box.Left - work.Left >= clearance)
                    Add(Exposed(i, top, bottom, box.Left - clearance, box.Left + 2, true),
                        SurfaceKind.WindowLeft, box.Left, box.Left - work.Left);
                if (box.Right >= work.Left && work.Right - box.Right >= clearance)
                    Add(Exposed(i, top, bottom, box.Right - 2, box.Right + clearance, true),
                        SurfaceKind.WindowRight, box.Right, work.Right - box.Right);

                void Add(List<(int Start, int End)> segments, SurfaceKind kind, int line, int room)
                {
                    foreach (var (s, e) in segments)
                        if (e - s >= minWidth)
                            surfaces.Add(new Surface(id, kind, origin, s, e, line, room));
                }
            }
        }

        return new DesktopSnapshot(monitors.ToImmutable(), surfaces.ToImmutable(),
            ImmutableHashSet.Create("pointer", "windowEdges", "absolutePlacement"), quiet);
    }

    private List<(int Start, int End)> Exposed(int index, int start, int end,
        int crossStart, int crossEnd, bool vertical)
    {
        List<(int Start, int End)> segments = start < end ? [(start, end)] : [];
        for (int i = 0; i < index && segments.Count > 0; i++)
        {
            Rectangle box = windows[i].Bounds;
            int crossNear = vertical ? box.Left : box.Top;
            int crossFar = vertical ? box.Right : box.Bottom;
            if (crossFar <= crossStart || crossNear >= crossEnd)
                continue;
            int near = vertical ? box.Top : box.Left;
            int far = vertical ? box.Bottom : box.Right;
            for (int j = segments.Count - 1; j >= 0; j--)
            {
                var span = segments[j];
                if (far <= span.Start || near >= span.End)
                    continue;
                segments.RemoveAt(j);
                if (near > span.Start)
                    segments.Add((span.Start, near));
                if (far < span.End)
                    segments.Add((far, span.End));
            }
        }
        return segments;
    }

    internal static double MonitorScale(Point point)
    {
        var monitor = Native.MonitorFromPoint(new Native.XY(point.X, point.Y), 2);
        return Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96.0 : 1;
    }

    internal static Rectangle WindowBounds(nint hwnd)
    {
        if (Native.DwmGetRect(hwnd, 9, out var rect, Marshal.SizeOf<Native.RECT>()) != 0 &&
            !Native.GetWindowRect(hwnd, out rect))
            return Rectangle.Empty;
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    internal static bool IsShellClass(nint hwnd)
    {
        var name = new StringBuilder(128);
        Native.GetClassName(hwnd, name, name.Capacity);
        return name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    public void Dispose()
    {
        foreach (nint hook in hooks)
            Native.UnhookWinEvent(hook);
    }
}
