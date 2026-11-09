using System.Diagnostics;
using System.Runtime.InteropServices;
using Cutsy.Core;
using Cutsy.PackIO;
using Timer = System.Windows.Forms.Timer;

namespace Cutsy.Windows;

internal sealed partial class MascotContext : ApplicationContext
{
    private readonly LayeredWindow body = new("Cutsy Mascot", interactive: true);
    private readonly LayeredWindow decorations = new("Cutsy Decorations", interactive: false);
    private readonly WindowTracker tracker = new();
    private readonly QuietMode quiet;
    private long nextQuietCheck;
    private readonly SpriteRenderer renderer;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly Timer simulation = new() { Interval = 33 };
    private readonly BehaviorEngine engine;
    private readonly CuePlayer audio = new();
    private PackContent content;
    private readonly SettingsStore storage = new();
    private Preferences preferences;
    private string? packNotice;
    private SettingsForm? settings;
    private Point down;
    private WorldPoint grabOffset;
    private bool pressed;
    private int logicalHeight = 160;
    private double dragThreshold;
    private long lastTick;

    internal MascotContext()
    {
        preferences = storage.Load();
        logicalHeight = preferences.Height;
        quiet = new QuietMode(body.Handle)
        {
            Fullscreen = preferences.HideInFullscreen,
            Gaming = preferences.Gaming,
            Exclusions = preferences.Exclusions
        };
        quiet.Evaluate(Environment.TickCount64);
        content = LoadInitialPack();
        tracker.Refresh(Environment.TickCount64);
        engine = new BehaviorEngine(content.Pack, tracker.Snapshot(logicalHeight, quiet.Active), Random.Shared.Next());
        engine.LogicalHeight = logicalHeight;
        engine.Activity = ActivityWeight(preferences.Activity);
        engine.Recover();
        engine.StayHere = preferences.StayHere;
        audio.Muted = preferences.Muted;
        audio.Volume = preferences.Volume / 100.0;
        renderer = new SpriteRenderer(body, decorations);
        renderer.Use(content);

        menu.Items.Add("Pause", null, (_, _) => TogglePause());
        menu.Items.Add("Stay here", null, (_, _) => ApplyPreferences(preferences with { StayHere = !engine.StayHere }));
        menu.Items.Add("Mute", null, (_, _) => ApplyPreferences(preferences with { Muted = !audio.Muted }));
        var sizes = new ToolStripMenuItem("Size");
        foreach (int size in new[] { 80, 120, 160, 240, 320 })
            sizes.DropDownItems.Add(new ToolStripMenuItem($"{size} px", null, (_, _) => ResizeMascot(size)));
        menu.Items.Add(sizes);
        var gaming = new ToolStripMenuItem("Gaming mode", null, (_, _) =>
        {
            ApplyPreferences(preferences with { Gaming = !quiet.Gaming });
        });
        menu.Items.Add(gaming);
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Recover position", null, (_, _) => Recover());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) =>
        {
            ((ToolStripMenuItem)menu.Items[0]).Checked = engine.Paused;
            ((ToolStripMenuItem)menu.Items[1]).Checked = engine.StayHere;
            ((ToolStripMenuItem)menu.Items[2]).Checked = audio.Muted;
            gaming.Checked = quiet.Gaming;
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
        Draw();
        lastTick = Stopwatch.GetTimestamp();
        simulation.Tick += (_, _) => Tick();
        simulation.Start();
    }

    private void Tick()
    {
        long timestamp = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(lastTick, timestamp);
        lastTick = timestamp;

        CheckQuiet(force: tracker.ForegroundChanged);
        tracker.ForegroundChanged = false;
        if (tracker.Refresh(Environment.TickCount64))
            UpdateDesktop();

        if (!pressed || engine.Dragging)
            engine.Advance(elapsed);
        if (engine.TakeSound() is { } cue && !quiet.Active && !engine.Paused)
            audio.Play(content, cue);
        Draw();
    }

    private void Draw()
    {
        if (!quiet.Active)
            renderer.Draw(engine.Pose);
    }

    private void CheckQuiet(bool force)
    {
        long now = Environment.TickCount64;
        if (!force && now < nextQuietCheck)
            return;
        nextQuietCheck = now + 500;
        string? previousReason = quiet.Reason;
        bool changed = quiet.Evaluate(now);
        string status = quiet.Reason is { } reason ? $"Cutsy · {reason}" : "Cutsy";
        if (tray.Text != status)
            tray.Text = status[..Math.Min(status.Length, 63)];
        if (previousReason != quiet.Reason)
            settings?.RefreshStatus();
        if (!changed)
            return;
        if (quiet.Active)
        {
            CancelPointer();
            renderer.Hide();
            audio.Stop();
        }
        UpdateDesktop();
        Draw();
    }

    private void UpdateDesktop() => engine.SetDesktop(tracker.Snapshot(logicalHeight, quiet.Active));

    private void HandleBodyMessage(Message message)
    {
        if (message.Msg == Program.ShowSettingsMessage)
        {
            ShowSettings();
            return;
        }
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
            case Native.WmSessionChange:
                quiet.SessionChanged((int)message.WParam);
                CheckQuiet(force: true);
                break;
            case Native.WmSettingChange:
                CheckQuiet(force: true);
                tracker.Invalidate();
                break;
            case Native.WmDpiChanged:
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
        Draw();
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
        Draw();
    }

    private void ResizeMascot(int size) => ApplyPreferences(preferences with { Height = size });

    private void TogglePause()
    {
        CancelPointer();
        engine.Paused = !engine.Paused;
        if (engine.Paused)
            audio.Stop();
    }

    private void ShowSettings()
    {
        if (settings is { IsDisposed: false })
        {
            if (settings.WindowState == FormWindowState.Minimized)
                settings.WindowState = FormWindowState.Normal;
            settings.Activate();
            return;
        }

        settings = new SettingsForm(this);
        settings.FormClosed += (_, _) => settings = null;
        settings.Show();
        settings.Activate();
    }

    internal Preferences CurrentPreferences => preferences;
    internal ValidatedPack ActivePack => content.Pack;
    internal string PackLocation => $"Pack folder: {storage.PackRoot}";
    internal string Status => string.Join("  ", new[]
    {
        storage.Warning,
        packNotice,
        quiet.Reason,
    }.Where(value => !string.IsNullOrEmpty(value)).DefaultIfEmpty(storage.Portable ? "Portable mode" : "Ready"));
    internal string Diagnostics => $"{Status}{Environment.NewLine}Behavior: {engine.BehaviorId}{Environment.NewLine}" +
        $"Support: {engine.SupportKind?.ToString() ?? "airborne"}{Environment.NewLine}" +
        $"{content.Pack.Sprites.Count} sprites, {content.Pack.Behaviors.Count} recipes, {content.Pack.Sounds.Count} sounds." +
        $"{Environment.NewLine}{storage.Diagnostics}";

    internal void ApplyPreferences(Preferences updated)
    {
        CancelPointer();
        preferences = updated;
        logicalHeight = updated.Height;
        engine.LogicalHeight = logicalHeight;
        engine.Activity = ActivityWeight(updated.Activity);
        engine.StayHere = updated.StayHere;
        audio.Muted = updated.Muted;
        double volume = updated.Volume / 100.0;
        if (audio.Muted || audio.Volume != volume)
            audio.Stop();
        audio.Volume = volume;
        quiet.Gaming = updated.Gaming;
        quiet.Fullscreen = updated.HideInFullscreen;
        quiet.Exclusions = updated.Exclusions;
        CheckQuiet(force: true);
        UpdateDesktop();
        storage.Save(updated);
        Draw();
        if (storage.Warning is not null && settings is null)
            tray.ShowBalloonTip(4000, "Cutsy", storage.Warning, ToolTipIcon.Warning);
    }

    private static double ActivityWeight(ActivityLevel level) => level switch
    {
        ActivityLevel.Restful => .32,
        ActivityLevel.Lively => 1.85,
        _ => 1
    };

    internal void OpenPackFolder() => storage.OpenPackFolder(
        Directory.Exists(storage.PackRoot) ? content : PackContent.Embedded());

    internal void RecoverFromSettings() => Recover();

    internal void PaintPreview(Graphics graphics, RenderPose pose) => renderer.Paint(graphics, pose);

    internal string PreviewCue(string id)
    {
        if (quiet.Active)
            return "Sound is paused while quiet mode is active.";
        if (audio.Muted || audio.Volume == 0)
            return "Unmute Cutsy and set a volume above zero to preview sounds.";
        return audio.Play(content, new SoundRequest(id, Interaction: true), preview: true)
            ? $"Playing {id}."
            : "The sound could not play. Wait for the current cue or check the output device.";
    }

    private PackContent LoadInitialPack()
    {
        try
        {
            if (Directory.Exists(storage.PackRoot))
            {
                var external = LoadExternalPack();
                RecordPackNotice(external, "Editable pack loaded.");
                return external;
            }
        }
        catch (Exception ex) when (IsPackError(ex))
        {
            packNotice = $"Using the embedded pack. {ex.Message}";
            storage.Note(packNotice);
        }
        return PackContent.Embedded();
    }

    private PackContent LoadExternalPack() => new(new PackDirectory(storage.PackRoot).Read);

    internal void ReloadPack()
    {
        PackContent candidate;
        bool external = Directory.Exists(storage.PackRoot);
        try
        {
            candidate = external ? LoadExternalPack() : PackContent.Embedded();
        }
        catch (Exception ex) when (IsPackError(ex))
        {
            packNotice = $"Kept the current pack. {ex.Message}";
            storage.Note(packNotice);
            return;
        }
        CancelPointer();
        audio.Stop();
        content = candidate;
        renderer.Use(candidate);
        engine.ReplacePack(candidate.Pack);
        RecordPackNotice(candidate, external ? "Editable pack reloaded." : "Embedded pack reloaded.");
        Draw();
    }

    private static bool IsPackError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException;

    private void RecordPackNotice(PackContent pack, string message)
    {
        packNotice = pack.Pack.Warnings.IsEmpty ? message : $"{message} {pack.Pack.Warnings[0]}";
        storage.Note(message);
        foreach (string warning in pack.Pack.Warnings)
            storage.Note(warning);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            simulation.Stop();
            CancelPointer();
            renderer.Dispose();
            audio.Dispose();
            tracker.Dispose();
            quiet.Dispose();
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
