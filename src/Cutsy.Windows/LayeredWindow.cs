using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Cutsy.Windows;

internal sealed class LayeredWindow : NativeWindow, IDisposable
{
    private readonly bool interactive;
    private Bitmap? lastImage;
    private Point lastLocation;
    private bool visible;

    internal Action<Message>? MessageReceived { get; set; }

    internal LayeredWindow(string name, bool interactive)
    {
        this.interactive = interactive;
        CreateHandle(new CreateParams
        {
            Caption = name,
            Style = unchecked((int)0x80000000), // WS_POPUP
            ExStyle = Native.ExLayered | Native.ExToolWindow | Native.ExNoActivate | Native.ExTopmost |
                (interactive ? 0 : Native.ExTransparent)
        });
    }

    internal void Present(Bitmap bitmap, Point location)
    {
        if (!ReferenceEquals(lastImage, bitmap))
        {
            Upload(bitmap, location);
            lastImage = bitmap;
        }
        else if (location != lastLocation)
        {
            const uint moveOnly = 0x0001 | 0x0004 | 0x0010; // NOSIZE | NOZORDER | NOACTIVATE
            if (!Native.SetWindowPos(Handle, 0, location.X, location.Y, 0, 0, moveOnly))
                throw new Win32Exception();
        }
        lastLocation = location;
        if (!visible)
        {
            visible = true;
            Native.ShowWindow(Handle, 4); // SW_SHOWNOACTIVATE
        }
    }

    internal void Hide()
    {
        if (!visible)
            return;
        visible = false;
        Native.ShowWindow(Handle, 0);
    }

    private void Upload(Bitmap bitmap, Point location)
    {
        nint dc = Native.CreateCompatibleDC(0);
        if (dc == 0)
            throw new Win32Exception();

        nint dib = 0;
        nint old = 0;
        try
        {
            var info = new Native.BitmapInfo
            {
                Size = (uint)Marshal.SizeOf<Native.BitmapInfo>(),
                Width = bitmap.Width,
                Height = -bitmap.Height, // Top down BGRA
                Planes = 1,
                BitCount = 32
            };
            dib = Native.CreateDIBSection(dc, ref info, 0, out nint bits, 0, 0);
            if (dib == 0)
                throw new Win32Exception();
            old = Native.SelectObject(dc, dib);
            if (old == 0 || old == -1)
                throw new Win32Exception();

            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                CopyRows(data, bits, bitmap.Width * 4, bitmap.Height);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            var destination = new Native.XY(location.X, location.Y);
            var size = new Native.XY(bitmap.Width, bitmap.Height);
            var source = new Native.XY(0, 0);
            var blend = new Native.Blend { Opacity = 255, AlphaFormat = 1 };
            if (!Native.UpdateLayeredWindow(Handle, 0, ref destination, ref size, dc, ref source, 0, ref blend, 2))
                throw new Win32Exception();
        }
        finally
        {
            if (old != 0 && old != -1)
                Native.SelectObject(dc, old);
            if (dib != 0)
                Native.DeleteObject(dib);
            Native.DeleteDC(dc);
        }
    }

    private static unsafe void CopyRows(BitmapData data, nint target, int rowBytes, int height)
    {
        for (int y = 0; y < height; y++)
        {
            Buffer.MemoryCopy((void*)(data.Scan0 + y * data.Stride), (void*)(target + y * rowBytes),
                rowBytes, rowBytes);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WmMouseActivate)
        {
            m.Result = 3; // MA_NOACTIVATE: deliver the mouse event without activation.
            return;
        }

        if (interactive && m.Msg == Native.WmSetCursor)
        {
            Cursor.Current = Cursors.Arrow;
            m.Result = 1;
            return;
        }

        base.WndProc(ref m);
        MessageReceived?.Invoke(m);
    }

    public void Dispose() => DestroyHandle();
}
