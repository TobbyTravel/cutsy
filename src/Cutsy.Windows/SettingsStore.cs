using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Cutsy.Windows;

internal enum ActivityLevel { Restful, Calm, Lively }

internal sealed record Preferences
{
    public int Version { get; init; } = 1;
    public int Height { get; init; } = 160;
    public ActivityLevel Activity { get; init; } = ActivityLevel.Calm;
    public int Volume { get; init; } = 10;
    public bool Muted { get; init; }
    public bool StayHere { get; init; }
    public bool HideInFullscreen { get; init; } = true;
    public bool Gaming { get; init; }
    public AppExclusion[] Exclusions { get; init; } = [];
}

internal sealed class SettingsStore
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 10,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    private readonly Queue<string> diagnostics = new();
    internal bool Portable { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"));
    internal string Root { get; }
    internal string PackRoot => Path.Combine(Root, "pack");
    internal string? Warning { get; private set; }
    internal string Diagnostics => string.Join(Environment.NewLine, diagnostics);

    internal SettingsStore()
    {
        Root = Portable ? AppContext.BaseDirectory :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cutsy");
    }

    internal Preferences Load()
    {
        try
        {
            using var stream = File.OpenRead(Path.Combine(Root, "settings.json"));
            if (stream.Length > 64 * 1024)
                throw new InvalidDataException("Settings exceed 64 KiB.");
            var value = JsonSerializer.Deserialize<Preferences>(stream, Json);
            if (value is null || value.Version != 1 || value.Exclusions is null || value.Exclusions.Length > 64 ||
                value.Exclusions.Any(e => e is null || !ValidExecutable(e.Executable)))
                throw new InvalidDataException("Settings version or exclusions are invalid.");
            return value with { Height = Math.Clamp(value.Height, 80, 320), Volume = Math.Clamp(value.Volume, 0, 100) };
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
        {
            Warning = $"Could not load settings. Defaults are active. {ex.Message}";
            Note(Warning);
        }
        return new Preferences();
    }

    internal void Save(Preferences preferences)
    {
        string temp = Path.Combine(Root, "settings.json.tmp");
        try
        {
            Directory.CreateDirectory(Root);
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, preferences, Json);
            File.Move(temp, Path.Combine(Root, "settings.json"), overwrite: true);
            Warning = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warning = Portable
                ? "This portable folder is read-only or unavailable. Changes apply for this session."
                : "Settings could not be saved. Changes apply for this session.";
            Note($"{Warning} {ex.Message}");
        }
    }

    internal void Note(string detail)
    {
        if (diagnostics.Count == 40)
            diagnostics.Dequeue();
        diagnostics.Enqueue($"{DateTimeOffset.Now:HH:mm:ss} {detail[..Math.Min(detail.Length, 512)]}");
    }

    internal static bool ValidExecutable(string? value) => value is { Length: > 4 and <= 128 } &&
        value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && value == value.Trim();

    internal static bool StartsWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("Cutsy") as string == StartupCommand;
    }

    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue("Cutsy", StartupCommand, RegistryValueKind.String);
        else
            key.DeleteValue("Cutsy", throwOnMissingValue: false);
    }

    private static string StartupCommand => $"\"{Environment.ProcessPath}\"";

    internal void OpenPackFolder(PackContent embedded)
    {
        if (!Directory.Exists(PackRoot))
        {
            Directory.CreateDirectory(Root);
            string staging = Path.Combine(Root, $".pack-{Guid.NewGuid():N}");
            Directory.CreateDirectory(staging);
            try
            {
                embedded.Export(staging);
                Directory.Move(staging, PackRoot);
            }
            finally
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
        }
        Process.Start(new ProcessStartInfo(PackRoot) { UseShellExecute = true });
    }
}
