using System.Diagnostics;
using System.Runtime.InteropServices;
using Cutsy.Core;
using Timer = System.Windows.Forms.Timer;

namespace Cutsy.Windows;

internal sealed partial class MascotContext : ApplicationContext
{
    private readonly LayeredWindow body = new("Cutsy Mascot", interactive: true);
    private readonly LayeredWindow decorations = new("Cutsy Decorations", interactive: false);
    private readonly WindowTracker tracker = new();
    private readonly SpriteRenderer renderer;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly Timer simulation = new() { Interval = 33 };
    private readonly BehaviorEngine engine;
    private Form? settings;
    private Point down;
    private WorldPoint grabOffset;
    private bool pressed;
    private bool muted;
    private int logicalHeight = 160;
    private double dragThreshold;
    private long lastTick;

    internal MascotContext()
    {
        PackContent content = PackContent.Embedded();
        tracker.Refresh(Environment.TickCount64);
        engine = new BehaviorEngine(content.Pack, tracker.Snapshot(logicalHeight, quiet: false), Random.Shared.Next());
        renderer = new SpriteRenderer(body, decorations);
        renderer.Use(content);

        menu.Items.Add("Pause", null, (_, _) => TogglePause());
        menu.Items.Add("Stay here", null, (_, _) => engine.StayHere = !engine.StayHere);
        menu.Items.Add("Mute", null, (_, _) => muted = !muted);
        var sizes = new ToolStripMenuItem("Size");
        foreach (int size in new[] { 80, 120, 160, 240, 320 })
            sizes.DropDownItems.Add(new ToolStripMenuItem($"{size} px", null, (_, _) => ResizeMascot(size)));
        menu.Items.Add(sizes);
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Recover position", null, (_, _) => Recover());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) =>
        {
            ((ToolStripMenuItem)menu.Items[0]).Checked = engine.Paused;
            ((ToolStripMenuItem)menu.Items[1]).Checked = engine.StayHere;
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
        body.MessageReceived = HandleBodyMessage;
        renderer.Draw(engine.Pose);
        lastTick = Stopwatch.GetTimestamp();
        simulation.Tick += (_, _) => Tick();
        simulation.Start();
    }

    private void Tick()
    {
        long timestamp = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(lastTick, timestamp);
        lastTick = timestamp;

        if (tracker.Refresh(Environment.TickCount64))
            UpdateDesktop();

        engine.Advance(elapsed);
        renderer.Draw(engine.Pose);
    }

    private void UpdateDesktop() => engine.SetDesktop(tracker.Snapshot(logicalHeight, quiet: false));

    private void HandleBodyMessage(Message message)
    {
        switch (message.Msg)
        {
            case Native.WmLeftDown when !pressed:
                down = Cursor.Position;
                double scale = WindowTracker.MonitorScale(down);
                grabOffset = new WorldPoint((down.X - engine.Contact.X) / scale, (down.Y - engine.Contact.Y) / scale);
                dragThreshold = 4 * scale;
                pressed = true;
                var tracking = new Native.MouseTracking
                {
                    Size = (uint)Marshal.SizeOf<Native.MouseTracking>(),
                    Flags = 2, // TME_LEAVE
                    Window = body.Handle
                };
                Native.TrackMouseEvent(ref tracking);
                break;
            case Native.WmMouseMove when pressed:
                if ((message.WParam & 1) == 0)
                    CancelPointer();
                else if (engine.Dragging || BeyondThreshold(Cursor.Position))
                    MoveWithPointer(Cursor.Position);
                break;
            case Native.WmMouseLeave when pressed && !engine.Dragging:
                if (BeyondThreshold(Cursor.Position))
                    MoveWithPointer(Cursor.Position);
                else
                    CancelPointer();
                break;
            case Native.WmLeftUp when pressed:
                FinishPointer(Cursor.Position);
                break;
            case Native.WmCaptureChanged when engine.Dragging:
            case Native.WmCancelMode when pressed:
                CancelPointer();
                break;
            case Native.WmRightUp when !pressed:
                menu.Show(Cursor.Position);
                break;
            case Native.WmDisplayChange:
                CancelPointer();
                tracker.Invalidate();
                break;
            case Native.WmDpiChanged:
            case Native.WmSettingChange:
                tracker.Invalidate();
                break;
        }
    }

    private bool BeyondThreshold(Point pointer) =>
        new WorldPoint(down.X, down.Y).DistanceSquaredTo(new WorldPoint(pointer.X, pointer.Y))
            >= dragThreshold * dragThreshold;

    private WorldPoint DragContact(Point pointer)
    {
        double scale = WindowTracker.MonitorScale(pointer);
        return new WorldPoint(pointer.X - grabOffset.X * scale, pointer.Y - grabOffset.Y * scale);
    }

    private void MoveWithPointer(Point pointer)
    {
        var contact = DragContact(pointer);
        if (!engine.Dragging)
        {
            engine.BeginDrag(contact);
            Native.SetCapture(body.Handle);
        }
        engine.DragTo(contact);
        renderer.Draw(engine.Pose);
    }

    private void FinishPointer(Point pointer)
    {
        bool moved = engine.Dragging || BeyondThreshold(pointer);
        if (moved)
        {
            if (!engine.Dragging)
                engine.BeginDrag(DragContact(pointer));
            engine.Drop(DragContact(pointer));
        }
        else
        {
            engine.Click();
        }
        CancelPointer();
    }

    private void CancelPointer()
    {
        pressed = false;
        engine.CancelDrag();
        if (Native.GetCapture() == body.Handle)
            Native.ReleaseCapture();
    }

    private void Recover()
    {
        CancelPointer();
        engine.Paused = false;
        engine.Recover();
        renderer.Draw(engine.Pose);
    }

    private void ResizeMascot(int size)
    {
        CancelPointer();
        logicalHeight = size;
        engine.LogicalHeight = size;
        UpdateDesktop();
        renderer.Draw(engine.Pose);
    }

    private void TogglePause()
    {
        CancelPointer();
        engine.Paused = !engine.Paused;
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
            simulation.Stop();
            CancelPointer();
            renderer.Dispose();
            tracker.Dispose();
            body.Dispose();
            decorations.Dispose();
            simulation.Dispose();
            tray.Visible = false;
            tray.Dispose();
            menu.Dispose();
            settings?.Dispose();
        }
        base.Dispose(disposing);
    }
}
