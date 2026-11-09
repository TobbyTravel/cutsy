using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Cutsy.Core;

public static partial class PackLoader
{
    public const int MaxJsonBytes = 2 * 1024 * 1024;
    public const int MaxImageBytes = 8 * 1024 * 1024;
    public const int MaxSoundBytes = 256 * 1024;
    public const int MaxDimension = 2048;
    public const long MaxDecodedBytes = 96L * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 20,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static ValidatedPack Load(Func<string, int, byte[]?> read)
    {
        var manifest = Parse<PackManifest>("pack.json", read);
        Require(manifest.SchemaVersion == 1, "pack.json schemaVersion", "expected 1");
        RequireId(manifest.Id, "pack.json id");
        Require(!string.IsNullOrWhiteSpace(manifest.DisplayName) && !string.IsNullOrWhiteSpace(manifest.Version),
            "pack.json", "displayName and version are required");
        Require(manifest.ReferenceHeightPx is > 0 and <= MaxDimension, "pack.json referenceHeightPx", "outside bounds");
        CheckPath(manifest.BehaviorFile, ".json", "pack.json behaviorFile");
        Require(!manifest.Sprites.IsDefaultOrEmpty && manifest.Sprites.Length <= 160, "pack.json sprites", "expected 1–160");
        Require(!manifest.Sounds.IsDefault && manifest.Sounds.Length <= 32, "pack.json sounds", "expected 0–32");

        var sprites = new Dictionary<string, SpriteSpec>(StringComparer.Ordinal);
        var images = new Dictionary<string, (int Width, int Height)>(StringComparer.Ordinal);
        long decodedBytes = 0;
        foreach (var sprite in manifest.Sprites)
        {
            Require(sprite is not null, "pack.json sprites", "null sprite");
            string at = $"pack.json sprites[{sprite!.Id}]";
            RequireId(sprite.Id, at);
            Require(sprites.TryAdd(sprite.Id, sprite), at, "duplicate ID");
            Require(!string.IsNullOrWhiteSpace(sprite.SourceAssetId), at, "sourceAssetId is required");
            CheckPath(sprite.Path, ".png", at);
            CheckPath(sprite.MaskPath, ".png", at);
            var size = ReadImage(sprite.Path);
            Require(ReadImage(sprite.MaskPath) == size, at, "mask dimensions must match sprite");
            var box = sprite.ContentBounds;
            Require(box.X >= 0 && box.Y >= 0 && box.Width > 0 && box.Height > 0 &&
                (long)box.X + box.Width <= size.Width && (long)box.Y + box.Height <= size.Height,
                at + ".contentBounds", "outside image");
            Require(Number(sprite.ScaleCorrection, .1, 4), at + ".scaleCorrection", "outside .1–4");
            Require(sprite.Anchors is { Count: > 0 and <= 12 }, at + ".anchors", "expected 1–12");
            foreach (var (name, point) in sprite.Anchors!)
            {
                RequireId(name, at + ".anchors");
                Require(Number(point.X, box.X, box.Right) && Number(point.Y, box.Y, box.Bottom),
                    at + ".anchors." + name, "outside content bounds");
            }
        }

        var warnings = ImmutableArray.CreateBuilder<string>();
        var soundIds = new HashSet<string>(StringComparer.Ordinal);
        var sounds = ImmutableDictionary.CreateBuilder<string, SoundSpec>(StringComparer.Ordinal);
        foreach (var sound in manifest.Sounds)
        {
            Require(sound is not null, "pack.json sounds", "null sound");
            string at = $"pack.json sounds[{sound!.Id}]";
            RequireId(sound.Id, at);
            Require(soundIds.Add(sound.Id), at, "duplicate ID");
            CheckPath(sound.Path, ".wav", at);
            Require(Number(sound.Gain, 0, 1), at + ".gain", "outside 0–1");
            try
            {
                ReadPcm(RequiredFile(read, sound.Path, MaxSoundBytes));
                sounds.Add(sound.Id, sound);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                warnings.Add($"{sound.Path} muted: {ex.Message}");
            }
        }

        var recipes = Parse<BehaviorFile>(manifest.BehaviorFile, read);
        string file = manifest.BehaviorFile;
        Require(recipes.SchemaVersion == 1, file + " schemaVersion", "expected 1");
        Require(!recipes.Behaviors.IsDefaultOrEmpty && recipes.Behaviors.Length <= 160, file, "expected 1–160 behaviors");
        var behaviors = new Dictionary<string, Behavior>(StringComparer.Ordinal);
        foreach (var behavior in recipes.Behaviors)
        {
            Require(behavior is not null, file, "null behavior");
            string at = $"{file} behaviors[{behavior!.Id}]";
            RequireId(behavior.Id, at);
            Require(behaviors.TryAdd(behavior.Id, behavior), at, "duplicate ID");
            Require(Number(behavior.SelectionWeight, 0, 1000) && behavior.CooldownMs is >= 0 and <= 3600000,
                at, "invalid selectionWeight or cooldownMs");
            Require(!behavior.RequiredCapabilities.IsDefault && behavior.RequiredCapabilities.Length <= 16, at,
                "requiredCapabilities must be an array with at most 16 items");
            foreach (string capability in behavior.RequiredCapabilities)
                RequireId(capability, at + ".requiredCapabilities");
            Require(behavior.Eligibility is not null && Number(behavior.Eligibility.MinimumWidth, 0, 10),
                at + ".eligibility", "invalid eligibility or minimumWidth");
            Require(!behavior.Steps.IsDefaultOrEmpty && behavior.Steps.Length <= 64, at + ".steps", "expected 1–64 steps");
            Require(!behavior.Next.IsDefault && behavior.Next.Length <= 32, at + ".next", "expected 0–32 transitions");
            bool timed = false;
            for (int i = 0; i < behavior.Steps.Length; i++)
            {
                var step = behavior.Steps[i];
                string field = $"{at}.steps[{i}]";
                Require(step is not null, field, "null step");
                Require(step!.DurationMs is >= 0 and <= 120000, field + ".durationMs", "outside 0–120000");
                Require(Number(step.Speed, -5, 5) && Number(step.Height, 0, 4), field, "unsafe speed or height");
                Require(step.SoundId is null || soundIds.Contains(step.SoundId), field + ".soundId", "unknown sound");
                Require(step.Type != StepType.PlaySound || step.SoundId is not null, field + ".soundId", "required");
                Require(step.Type != StepType.Attach || step.Target is not null, field + ".target", "required");
                Require(step.Type != StepType.PlayFrames || !step.Frames.IsDefaultOrEmpty, field + ".frames", "required");
                if (step.Anchor is not null)
                    RequireId(step.Anchor, field + ".anchor");
                if (step.SpriteId is not null)
                    CheckSprite(step.SpriteId, step.Anchor, field + ".spriteId");
                if (!step.Frames.IsDefaultOrEmpty)
                {
                    Require(step.Frames.Length <= 64, field + ".frames", "too many frames");
                    foreach (var frame in step.Frames)
                    {
                        Require(frame is not null && frame.DurationMs is > 0 and <= 120000,
                            field + ".frames", "frame duration must be 1–120000 ms");
                        CheckSprite(frame!.SpriteId, step.Anchor, field + ".frames");
                    }
                }
                timed |= step.DurationMs > 0 || !step.Frames.IsDefaultOrEmpty;
            }
            Require(timed, at, "at least one step must take time");
        }
        foreach (var behavior in recipes.Behaviors)
            foreach (var next in behavior.Next)
                Require(next is not null && next.BehaviorId is not null && behaviors.ContainsKey(next.BehaviorId) &&
                    Number(next.Weight, 0, 1000), $"{file} behaviors[{behavior.Id}].next", "invalid transition ID or weight");
        Require(manifest.DefaultBehaviorId is not null && behaviors.TryGetValue(manifest.DefaultBehaviorId, out var fallback) &&
            fallback.Family == BehaviorFamily.Idle && fallback.RequiredCapabilities.IsEmpty && fallback.Eligibility == new Eligibility() &&
            fallback.Steps.Any(s => s.SpriteId is not null || !s.Frames.IsDefaultOrEmpty),
            "pack.json defaultBehaviorId", "expected an unconditional idle with a sprite");

        return new ValidatedPack(manifest, recipes, sounds.ToImmutable(), warnings.ToImmutable());

        (int Width, int Height) ReadImage(string path)
        {
            if (images.TryGetValue(path, out var size))
                return size;
            try
            {
                size = CheckPng(RequiredFile(read, path, MaxImageBytes));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                throw new InvalidDataException($"{path}: {ex.Message}", ex);
            }
            decodedBytes += (long)size.Width * size.Height * 4;
            Require(decodedBytes <= MaxDecodedBytes, path, "total decoded image memory exceeds 96 MiB");
            images.Add(path, size);
            return size;
        }

        void CheckSprite(string id, string? anchor, string field)
        {
            Require(id is not null && sprites.ContainsKey(id), field, $"unknown sprite {id}");
            if (anchor is not null)
                Require(sprites[id!].Anchors.ContainsKey(anchor), field, $"sprite {id} has no {anchor} anchor");
        }
    }

    private static T Parse<T>(string path, Func<string, int, byte[]?> read) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(RequiredFile(read, path, MaxJsonBytes), Json)
                ?? throw new InvalidDataException($"{path}: expected a JSON object");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path} {ex.Path}: {ex.Message}", ex);
        }
    }

    private static byte[] RequiredFile(Func<string, int, byte[]?> read, string path, int limit)
    {
        byte[]? data = read(path, limit);
        Require(data is { Length: > 0 } && data.Length <= limit, path, $"missing, empty, or exceeds {limit} bytes");
        return data!;
    }

    private static void CheckPath(string path, string extension, string field) =>
        Require(path is { Length: > 0 and <= 180 } && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) &&
            path.Split('/').All(p => p is not ("" or "." or "..") && !p.EndsWith('.') &&
                PathComponent().IsMatch(p)), field, "expected a safe relative pack path");

    private static void RequireId(string id, string field) =>
        Require(id is { Length: > 0 and <= 96 } && IdPattern().IsMatch(id), field, "invalid identifier");

    private static bool Number(double value, double min, double max) => value >= min && value <= max;

    private static void Require(bool condition, string field, string message)
    {
        if (!condition)
            throw new InvalidDataException($"{field}: {message}");
    }

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]*\z")]
    private static partial Regex PathComponent();

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9_-]*\z")]
    private static partial Regex IdPattern();

    private static (int Width, int Height) CheckPng(byte[] png)
    {
        ReadOnlySpan<byte> bytes = png;
        ReadOnlySpan<byte> magic = [137, 80, 78, 71, 13, 10, 26, 10];
        Require(bytes.Length >= 33 && bytes[..8].SequenceEqual(magic) &&
            BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) == 13 && bytes.Slice(12, 4).SequenceEqual("IHDR"u8),
            "PNG", "invalid header");
        int width = BinaryPrimitives.ReadInt32BigEndian(bytes[16..]);
        int height = BinaryPrimitives.ReadInt32BigEndian(bytes[20..]);
        int channels = bytes[25] switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
        Require(width is > 0 and <= MaxDimension && height is > 0 and <= MaxDimension &&
            bytes[24] == 8 && channels > 0 && bytes[26] == 0 && bytes[27] == 0 && bytes[28] == 0,
            "PNG", "expected non-interlaced 8-bit grayscale, RGB, gray-alpha, or RGBA within 2048×2048");
        using var compressed = new MemoryStream();
        bool end = false;
        for (int offset = 8; offset <= bytes.Length - 12;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytes[offset..]);
            Require(length >= 0 && length <= bytes.Length - offset - 12, "PNG", "invalid chunk length");
            var chunk = bytes.Slice(offset + 4, length + 4);
            uint crc = BinaryPrimitives.ReadUInt32BigEndian(bytes[(offset + length + 8)..]);
            Require(Crc32(chunk) == crc, "PNG", "invalid chunk checksum");
            if (chunk[..4].SequenceEqual("IDAT"u8))
                compressed.Write(chunk[4..]);
            offset += length + 12;
            if (chunk[..4].SequenceEqual("IEND"u8))
            {
                end = length == 0 && offset == bytes.Length;
                break;
            }
        }
        Require(end && compressed.Length > 0, "PNG", "missing image data or terminator");
        compressed.Position = 0;
        using var decoded = new ZLibStream(compressed, CompressionMode.Decompress);
        var scanline = new byte[1 + width * channels];
        for (int y = 0; y < height; y++)
        {
            decoded.ReadExactly(scanline);
            Require(scanline[0] <= 4, "PNG", "invalid scanline filter");
        }
        Require(decoded.ReadByte() == -1, "PNG", "too much image data");
        return (width, height);
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data)
            crc = (crc >> 8) ^ CrcTable[(byte)(crc ^ value)];
        return ~crc;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(value =>
    {
        uint entry = (uint)value;
        for (int i = 0; i < 8; i++)
            entry = (entry >> 1) ^ ((entry & 1) == 0 ? 0u : 0xedb88320u);
        return entry;
    }).ToArray();

    public static (int SampleRate, int Offset, int Length) ReadPcm(ReadOnlySpan<byte> data)
    {
        Require(data.Length >= 44 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 8).SequenceEqual("WAVEfmt "u8) &&
            BinaryPrimitives.ReadInt32LittleEndian(data[16..]) == 16 &&
            BinaryPrimitives.ReadUInt16LittleEndian(data[20..]) == 1 &&
            BinaryPrimitives.ReadUInt16LittleEndian(data[22..]) == 1 &&
            BinaryPrimitives.ReadUInt16LittleEndian(data[32..]) == 2 &&
            BinaryPrimitives.ReadUInt16LittleEndian(data[34..]) == 16 &&
            data.Slice(36, 4).SequenceEqual("data"u8), "WAV", "expected canonical mono 16-bit PCM");
        int rate = BinaryPrimitives.ReadInt32LittleEndian(data[24..]);
        int length = BinaryPrimitives.ReadInt32LittleEndian(data[40..]);
        Require(rate is >= 8000 and <= 48000 && length > 0 && (length & 1) == 0 && length == data.Length - 44 &&
            length <= rate * 4 && BinaryPrimitives.ReadInt32LittleEndian(data[4..]) == data.Length - 8,
            "WAV", "invalid sample rate, data length, or cue longer than two seconds");
        return (rate, 44, length);
    }
}
