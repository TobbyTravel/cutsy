using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Cutsy.Core;
using Timer = System.Windows.Forms.Timer;

namespace Cutsy.Windows;

internal sealed class SettingsForm : Form
{
    private readonly MascotContext owner;
    private readonly NumericUpDown height = new() { Minimum = 80, Maximum = 320, Width = 80 };
    private readonly NumericUpDown volume = new() { Minimum = 0, Maximum = 100, Width = 80 };
    private readonly ComboBox activity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly CheckBox mute = Check("Mute");
    private readonly CheckBox stay = Check("Stay here");
    private readonly CheckBox fullscreen = Check("Hide in fullscreen");
    private readonly CheckBox gaming = Check("Gaming mode");
    private readonly CheckBox startup = Check("Start with Windows");
    private readonly TextBox exclusions = new() { Width = 440, Height = 78, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(8), AutoEllipsis = true };
    private readonly ComboBox recipe = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 235 };
    private readonly ComboBox cue = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 235 };
    private readonly PreviewPanel previewPanel = new() { Width = 456, Height = 238, BackColor = Color.FromArgb(242, 235, 245) };
    private readonly Timer previewTimer = new() { Interval = 33 };
    private BehaviorEngine? preview;
    private long previewStart;
    private long lastTick;

    internal SettingsForm(MascotContext owner)
    {
        this.owner = owner;
        Text = "Cutsy Settings";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(540, 530);
        MinimumSize = new Size(495, 460);
        AutoScaleMode = AutoScaleMode.Dpi;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var settings = new TabPage("Preferences");
        var pack = new TabPage("Pack and preview");
        tabs.TabPages.AddRange([settings, pack]);
        Controls.Add(tabs);
        Controls.Add(status);

        var preferences = Column(settings);
        activity.Items.AddRange(Enum.GetNames<ActivityLevel>());
        preferences.Controls.Add(Row(new Label { Text = "Height (px)", AutoSize = true }, height,
            new Label { Text = "Volume (%)", AutoSize = true }, volume));
        preferences.Controls.Add(Row(new Label { Text = "Activity", AutoSize = true }, activity));
        preferences.Controls.Add(Row(mute, stay, fullscreen));
        preferences.Controls.Add(Row(gaming, startup));
        preferences.Controls.Add(new Label
        {
            Text = "App exclusions. One per line, for example foreground game.exe or running recorder.exe.",
            AutoSize = true, MaximumSize = new Size(440, 0)
        });
        preferences.Controls.Add(exclusions);
        preferences.Controls.Add(Row(Button("Apply", Apply), Button("Restore defaults", Restore),
            Button("Recover position", owner.RecoverFromSettings)));
        preferences.Controls.Add(Row(Button("Diagnostics", ShowDiagnostics)));

        var packLayout = Column(pack);
        packLayout.Controls.Add(new Label { Text = owner.PackLocation, AutoSize = true, MaximumSize = new Size(440, 0) });
        packLayout.Controls.Add(Row(Button("Open editable pack", () => Perform(owner.OpenPackFolder)),
            Button("Reload pack", Reload)));
        packLayout.Controls.Add(Row(recipe, Button("Preview behavior", StartPreview)));
        packLayout.Controls.Add(previewPanel);
        packLayout.Controls.Add(Row(cue, Button("Preview sound", () =>
        {
            if (cue.SelectedItem is string selected)
                status.Text = owner.PreviewCue(selected);
        })));

        previewPanel.Paint += PaintPreview;
        previewTimer.Tick += (_, _) => AdvancePreview();
        LoadValues();
        RefreshPack();
        RefreshStatus();
    }

    private static CheckBox Check(string text) => new() { Text = text, AutoSize = true };

    private static FlowLayoutPanel Column(Control parent)
    {
        var column = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(10)
        };
        parent.Controls.Add(column);
        return column;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        foreach (var control in controls)
            row.Controls.Add(control);
        return row;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action();
        return button;
    }

    private void LoadValues()
    {
        Preferences preferences = owner.CurrentPreferences;
        height.Value = preferences.Height;
        volume.Value = preferences.Volume;
        activity.SelectedIndex = (int)preferences.Activity;
        mute.Checked = preferences.Muted;
        stay.Checked = preferences.StayHere;
        fullscreen.Checked = preferences.HideInFullscreen;
        gaming.Checked = preferences.Gaming;
        exclusions.Lines = preferences.Exclusions.Select(e => $"{e.Mode.ToString().ToLowerInvariant()} {e.Executable}").ToArray();
        Perform(() => startup.Checked = SettingsStore.StartsWithWindows());
    }

    private void Apply() => Perform(() =>
    {
        var rules = new List<AppExclusion>();
        foreach (string line in exclusions.Lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            string[] parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !Enum.TryParse<ExclusionMode>(parts[0], true, out var mode) ||
                !Enum.IsDefined(mode) || !SettingsStore.ValidExecutable(parts[1].Trim()))
                throw new InvalidDataException($"Invalid exclusion: {line}. Use foreground name.exe or running name.exe.");
            rules.Add(new AppExclusion(parts[1].Trim(), mode));
        }
        if (rules.Count > 64)
            throw new InvalidDataException("Use at most 64 exclusion rules.");
        if (startup.Checked != SettingsStore.StartsWithWindows())
            SettingsStore.SetStartup(startup.Checked);
        owner.ApplyPreferences(new Preferences
        {
            Height = (int)height.Value, Volume = (int)volume.Value, Activity = (ActivityLevel)activity.SelectedIndex,
            Muted = mute.Checked, StayHere = stay.Checked, HideInFullscreen = fullscreen.Checked,
            Gaming = gaming.Checked, Exclusions = [.. rules]
        });
    });

    private void Restore() => Perform(() =>
    {
        if (SettingsStore.StartsWithWindows())
            SettingsStore.SetStartup(false);
        owner.ApplyPreferences(new Preferences());
        LoadValues();
    });

    private void Reload()
    {
        StopPreview();
        owner.ReloadPack();
        RefreshPack();
        RefreshStatus();
    }

    private void RefreshPack()
    {
        Fill(recipe, owner.ActivePack.Behaviors.Keys.Order(StringComparer.Ordinal));
        Fill(cue, owner.ActivePack.Sounds.Keys.Order(StringComparer.Ordinal));
    }

    private static void Fill(ComboBox selector, IEnumerable<string> options)
    {
        string? previous = selector.SelectedItem as string;
        selector.Items.Clear();
        selector.Items.AddRange(options.ToArray());
        if (previous is not null && selector.Items.Contains(previous))
            selector.SelectedItem = previous;
        else if (selector.Items.Count > 0)
            selector.SelectedIndex = 0;
    }

    internal void RefreshStatus() => status.Text = owner.Status;

    private void Perform(Action action)
    {
        try
        {
            action();
            RefreshStatus();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            SecurityException or Win32Exception)
        {
            status.Text = ex.Message;
        }
    }

    private void ShowDiagnostics()
    {
        using var dialog = new Form
        {
            Text = "Cutsy diagnostics", StartPosition = FormStartPosition.CenterParent,
            Size = new Size(560, 330)
        };
        dialog.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Text = owner.Diagnostics
        });
        dialog.ShowDialog(this);
    }

    private (int Width, int Floor, int Left, int Right, int Top, int Bottom) PreviewGeometry()
    {
        int width = previewPanel.ClientSize.Width;
        int floor = previewPanel.ClientSize.Height - 12;
        return (width, floor, width * 3 / 10, width * 7 / 10, floor * 56 / 100, floor * 82 / 100);
    }

    private void StartPreview()
    {
        if (recipe.SelectedItem is not string selected)
            return;
        var (width, floor, left, right, top, bottom) = PreviewGeometry();
        var origin = new WorldPoint(left, top);
        var desktop = new DesktopSnapshot([new MonitorArea(new Bounds(0, 0, width, floor), 1)],
        [
            new Surface("preview-floor", SurfaceKind.Floor, new WorldPoint(0, floor), 0, width, floor, floor),
            new Surface("preview-window", SurfaceKind.WindowTop, origin, left, right, top, top),
            new Surface("preview-window", SurfaceKind.WindowBottom, origin, left, right, bottom, 110),
            new Surface("preview-window", SurfaceKind.WindowLeft, origin, 46, floor - 25, left, left),
            new Surface("preview-window", SurfaceKind.WindowRight, origin, 46, floor - 25, right, width - right),
        ], System.Collections.Immutable.ImmutableHashSet.Create("pointer", "windowEdges", "absolutePlacement"));
        preview = new BehaviorEngine(owner.ActivePack, desktop, seed: 31) { LogicalHeight = 104 };
        preview.Recover();
        if (!preview.Preview(selected))
        {
            StopPreview();
            status.Text = "This recipe requires an unavailable capability.";
            return;
        }
        previewStart = lastTick = Stopwatch.GetTimestamp();
        previewTimer.Start();
        previewPanel.Invalidate();
    }

    private void AdvancePreview()
    {
        long now = Stopwatch.GetTimestamp();
        preview!.Advance(Stopwatch.GetElapsedTime(lastTick, now));
        lastTick = now;
        previewPanel.Invalidate();
        if (Stopwatch.GetElapsedTime(previewStart, now) >= TimeSpan.FromSeconds(20))
            previewTimer.Stop();
    }

    private void PaintPreview(object? sender, PaintEventArgs e)
    {
        var (width, floor, left, right, top, bottom) = PreviewGeometry();
        using var fill = new SolidBrush(Color.FromArgb(222, 207, 231));
        e.Graphics.FillRectangle(fill, left, top, right - left, bottom - top);
        e.Graphics.DrawLine(Pens.MediumPurple, 0, floor, width, floor);
        if (preview is not null)
            owner.PaintPreview(e.Graphics, preview.Pose);
    }

    private void StopPreview()
    {
        previewTimer.Stop();
        preview = null;
        previewPanel.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            previewTimer.Dispose();
        base.Dispose(disposing);
    }

    private sealed class PreviewPanel : Panel
    {
        internal PreviewPanel() => DoubleBuffered = true;
    }
}
