using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Cutsy.Core;
using Timer = System.Windows.Forms.Timer;

namespace Cutsy.Windows;

internal sealed partial class MascotContext : ApplicationContext
{
    private readonly LayeredWindow body = new("Cutsy Mascot", interactive: true);
    private readonly LayeredWindow effect = new("Cutsy Reaction", interactive: false);
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly Timer reactionTimer = new() { Interval = 550 };
    private readonly Timer displayTimer = new() { Interval = 50 };
    private readonly Bitmap stand;
    private Bitmap? image;
    private Bitmap? heart;
    private Form? settings;
    private Point location;
    private Point down;
    private PointF grabFraction;
    private bool pressed;
    private bool dragging;
    private bool paused;
    private bool stayHere;
    private bool muted;
    private int logicalHeight = 160;
    private double dragThreshold;

    internal MascotContext()
    {
        using var stream = typeof(MascotContext).Assembly.GetManifestResourceStream("Cutsy.Proof.Stand.png")!;
        using var decoded = new Bitmap(stream);
        stand = new Bitmap(decoded);

        menu.Items.Add("Pause", null, (_, _) => TogglePause());
        menu.Items.Add("Stay here", null, (_, _) => stayHere = !stayHere);
        menu.Items.Add("Mute", null, (_, _) => muted = !muted);
        var sizes = new ToolStripMenuItem("Size");
        foreach (int size in new[] { 80, 120, 160, 240, 320 })
        {
            var item = new ToolStripMenuItem($"{size} px", null, (_, _) => ResizeMascot(size));
            sizes.DropDownItems.Add(item);
        }
        menu.Items.Add(sizes);
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Recover position", null, (_, _) => Recover());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) =>
        {
            ((ToolStripMenuItem)menu.Items[0]).Checked = paused;
            ((ToolStripMenuItem)menu.Items[1]).Checked = stayHere;
            ((ToolStripMenuItem)menu.Items[2]).Checked = muted;
            foreach (ToolStripMenuItem item in sizes.DropDownItems)
                item.Checked = item.Text == $"{logicalHeight} px";
        };

        tray = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "Cutsy",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => ShowSettings();
        reactionTimer.Tick += (_, _) => StopReaction();
        displayTimer.Tick += (_, _) =>
        {
            displayTimer.Stop();
            if (!pressed)
                PlaceOnFloor(new Point(location.X + (image?.Width ?? 0) / 2, location.Y));
        };
        body.MessageReceived = HandleBodyMessage;
        Recover();
    }

    private void HandleBodyMessage(Message message)
    {
        switch (message.Msg)
        {
            case Native.WmLeftDown when !pressed && image is not null:
                StopReaction();
                down = Cursor.Position;
                grabFraction = new PointF((down.X - location.X) / (float)image.Width,
                    (down.Y - location.Y) / (float)image.Height);
                dragThreshold = 4 * MonitorScale(down);
                pressed = true;
                var tracking = new Native.MouseTracking
                {
                    Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MouseTracking>(),
                    Flags = 2, // TME_LEAVE
                    Window = body.Handle
                };
                Native.TrackMouseEvent(ref tracking);
                break;
            case Native.WmMouseMove when pressed:
                if ((message.WParam & 1) == 0)
                    CancelPointer();
                else if (dragging || BeyondThreshold(Cursor.Position))
                    MoveWithPointer(Cursor.Position);
                break;
            case Native.WmMouseLeave when pressed && !dragging:
                if (BeyondThreshold(Cursor.Position))
                    MoveWithPointer(Cursor.Position);
                else
                    CancelPointer();
                break;
            case Native.WmLeftUp when pressed:
                FinishPointer(Cursor.Position);
                break;
            case Native.WmCaptureChanged when dragging:
            case Native.WmCancelMode when pressed:
                bool wasDragging = dragging;
                CancelPointer();
                if (wasDragging)
                    PlaceOnFloor(Cursor.Position);
                break;
            case Native.WmRightUp when !pressed:
                menu.Show(Cursor.Position);
                break;
            case Native.WmDisplayChange:
                CancelPointer();
                displayTimer.Start();
                break;
            case Native.WmDpiChanged:
            case Native.WmSettingChange:
                displayTimer.Start();
                break;
        }
    }

    private bool BeyondThreshold(Point pointer) =>
        new WorldPoint(down.X, down.Y).DistanceSquaredTo(new WorldPoint(pointer.X, pointer.Y))
            >= dragThreshold * dragThreshold;

    private void MoveWithPointer(Point pointer)
    {
        if (!dragging)
        {
            dragging = true;
            Native.SetCapture(body.Handle);
        }
        UpdateImage(pointer);
        var offset = new Point((int)Math.Round(image!.Width * grabFraction.X),
            (int)Math.Round(image.Height * grabFraction.Y));
        Present(new Point(pointer.X - offset.X, pointer.Y - offset.Y));
    }

    private void FinishPointer(Point pointer)
    {
        if (dragging || BeyondThreshold(pointer))
        {
            MoveWithPointer(pointer);
            CancelPointer();
            PlaceOnFloor(pointer);
            return;
        }

        CancelPointer();
        React();
    }

    private void CancelPointer()
    {
        pressed = dragging = false;
        if (Native.GetCapture() == body.Handle)
            Native.ReleaseCapture();
    }

    private static double MonitorScale(Point point)
    {
        var monitor = Native.MonitorFromPoint(new Native.XY(point.X, point.Y), 2);
        return Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96.0 : 1;
    }

    private void UpdateImage(Point target)
    {
        int height = (int)Math.Round(logicalHeight * MonitorScale(target));
        if (image?.Height == height)
            return;

        int width = Math.Max(1, (int)Math.Round(height * stand.Width / (double)stand.Height));
        image?.Dispose();
        image = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(stand, new Rectangle(0, 0, width, height));
    }

    private void Present(Point position)
    {
        location = position;
        if (!paused)
            body.Present(image!, location);
    }

    private void PlaceOnFloor(Point target)
    {
        var work = Screen.FromPoint(target).WorkingArea;
        UpdateImage(target);
        int width = image!.Width;
        Present(new Point(Math.Clamp(location.X, work.Left, Math.Max(work.Left, work.Right - width)),
            Math.Max(work.Top, work.Bottom - image.Height)));
    }

    private void Recover()
    {
        CancelPointer();
        StopReaction();
        paused = false;
        var work = Screen.PrimaryScreen!.WorkingArea;
        UpdateImage(new Point(work.Left + work.Width / 2, work.Top + work.Height / 2));
        location = new Point(work.Right - image!.Width - 64, work.Bottom - image.Height);
        PlaceOnFloor(location);
    }

    private void ResizeMascot(int size)
    {
        CancelPointer();
        StopReaction();
        logicalHeight = size;
        PlaceOnFloor(new Point(location.X + image!.Width / 2, location.Y));
    }

    private void TogglePause()
    {
        CancelPointer();
        StopReaction();
        paused = !paused;
        if (paused)
            body.Hide();
        else
            Present(location);
    }

    private void React()
    {
        int size = Math.Max(12, image!.Height / 5);
        if (heart?.Width != size)
        {
            heart?.Dispose();
            heart = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(heart);
            graphics.ScaleTransform(size / 32f, size / 32f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = new GraphicsPath();
            shape.AddBezier(16, 28, 4, 20, 0, 16, 2, 9);
            shape.AddBezier(2, 9, 4, 2, 12, 2, 16, 8);
            shape.AddBezier(16, 8, 20, 2, 28, 2, 30, 9);
            shape.AddBezier(30, 9, 32, 16, 28, 20, 16, 28);
            graphics.FillPath(Brushes.HotPink, shape);
        }
        effect.Present(heart, new Point(location.X + image.Width - size / 5, location.Y + size / 2));
        reactionTimer.Stop();
        reactionTimer.Start();
    }

    private void StopReaction()
    {
        reactionTimer.Stop();
        effect.Hide();
    }

    private void ShowSettings()
    {
        if (settings is { IsDisposed: false })
        {
            settings.Activate();
            return;
        }

        settings = new Form
        {
            Text = "Cutsy Settings",
            AutoScaleMode = AutoScaleMode.Dpi,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(340, 118)
        };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        layout.Controls.Add(new Label { Text = "Mascot height", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        var size = new NumericUpDown { Minimum = 80, Maximum = 320, Value = logicalHeight, Width = 80 };
        size.ValueChanged += (_, _) => ResizeMascot((int)size.Value);
        layout.Controls.Add(size);
        var recover = new Button { Text = "Recover position", AutoSize = true };
        recover.Click += (_, _) => Recover();
        layout.SetFlowBreak(size, true);
        layout.Controls.Add(recover);
        settings.Controls.Add(layout);
        settings.Show();
        settings.Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPointer();
            body.Dispose();
            effect.Dispose();
            reactionTimer.Dispose();
            displayTimer.Dispose();
            tray.Visible = false;
            tray.Dispose();
            menu.Dispose();
            settings?.Dispose();
            heart?.Dispose();
            image?.Dispose();
            stand.Dispose();
        }
        base.Dispose(disposing);
    }
}
