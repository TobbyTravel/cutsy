using System.Runtime.InteropServices;

namespace Cutsy.Windows;

internal static partial class Native
{
    internal const int ExLayered = 0x80000;
    internal const int ExNoActivate = 0x08000000;
    internal const int ExToolWindow = 0x80;
    internal const int ExTopmost = 0x8;
    internal const int ExTransparent = 0x20;
    internal const int WmMouseActivate = 0x21;
    internal const int WmMouseMove = 0x200;
    internal const int WmMouseLeave = 0x2a3;
    internal const int WmLeftDown = 0x201;
    internal const int WmLeftUp = 0x202;
    internal const int WmRightUp = 0x205;
    internal const int WmCaptureChanged = 0x215;
    internal const int WmCancelMode = 0x1f;
    internal const int WmDisplayChange = 0x7e;
    internal const int WmDpiChanged = 0x2e0;
    internal const int WmSettingChange = 0x1a;

    [StructLayout(LayoutKind.Sequential)]
    internal struct XY(int x, int y)
    {
        internal int X = x;
        internal int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Blend
    {
        internal byte Operation;
        internal byte Flags;
        internal byte Opacity;
        internal byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int XPelsPerMeter;
        internal int YPelsPerMeter;
        internal uint ColorsUsed;
        internal uint ColorsImportant;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UpdateLayeredWindow(nint hwnd, nint destinationDc, ref XY destination,
        ref XY size, nint sourceDc, ref XY source, uint colorKey, ref Blend blend, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll")]
    internal static partial nint SetCapture(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial nint GetCapture();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReleaseCapture();

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseTracking
    {
        internal uint Size;
        internal uint Flags;
        internal nint Window;
        internal uint HoverTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TrackMouseEvent(ref MouseTracking request);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(XY point, uint flags);

    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint window);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage,
        out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint SelectObject(nint dc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint dc);
}
