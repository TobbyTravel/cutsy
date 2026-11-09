using System.Buffers.Binary;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Cutsy.Core;

namespace Cutsy.Windows;

internal sealed class PackContent
{
    private const string Prefix = "Cutsy.Pack/";
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Size> dimensions = new(StringComparer.Ordinal);
    private readonly Func<string, int, byte[]?> source;
    internal ValidatedPack Pack { get; }

    internal PackContent(Func<string, int, byte[]?> source, bool keepEncodedImages = true)
    {
        this.source = source;
        long encoded = 0;
        Pack = PackLoader.Load((path, limit) =>
        {
            if (files.TryGetValue(path, out var existing))
                return existing.Length <= limit ? existing : null;
            var data = source(path, limit);
            if (data is null || data.Length > limit)
                return null;
            encoded += data.Length;
            if (encoded > 64L * 1024 * 1024)
                throw new InvalidDataException("Encoded pack exceeds 64 MiB.");
            bool png = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && data.Length >= 24;
            if (png)
            {
                dimensions.TryAdd(path, new Size(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16)),
                    BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20))));
            }
            if (!png || keepEncodedImages)
                files.Add(path, data);
            return data;
        });

        if (!keepEncodedImages)
            return;

        foreach (string path in Pack.Sprites.Values.SelectMany(s => new[] { s.Path, s.MaskPath }).Distinct())
        {
            try
            {
                using var stream = new MemoryStream(files[path], writable: false);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
                if (image.Size != dimensions[path])
                    throw new InvalidDataException($"{path}: decoded size differs from the PNG header.");
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
            {
                throw new InvalidDataException($"{path}: Windows could not decode the PNG.", ex);
            }
        }
    }

    internal void Export(string directory)
    {
        foreach (string path in files.Keys.Concat(dimensions.Keys).Distinct(StringComparer.Ordinal))
        {
            string target = Path.Combine(directory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            System.IO.File.WriteAllBytes(target, File(path));
        }
    }

    internal Size ImageSize(string path) => dimensions[path];
    internal byte[] File(string path) => files.TryGetValue(path, out var bytes) ? bytes :
        source(path, PackLoader.MaxImageBytes) ?? throw new InvalidDataException($"Missing validated resource {path}.");

    internal static PackContent Embedded()
    {
        var assembly = typeof(PackContent).Assembly;
        var names = assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(n => n[Prefix.Length..].Replace('\\', '/'), StringComparer.Ordinal);
        return new PackContent((path, limit) =>
        {
            if (!names.TryGetValue(path, out var name))
                return null;
            using var stream = assembly.GetManifestResourceStream(name)!;
            if (stream.Length > limit)
                return null;
            var bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            return bytes;
        }, keepEncodedImages: false);
    }
}

internal sealed class SpriteRenderer(LayeredWindow body, LayeredWindow decorations) : IDisposable
{
    private const int CacheSize = 16;
    private readonly record struct Key(string Id, Size Size, Rectangle Visible);
    private sealed record Layers(Bitmap Body, Bitmap? Decoration) : IDisposable
    {
        public void Dispose()
        {
            Body.Dispose();
            Decoration?.Dispose();
        }
    }

    private readonly Dictionary<Key, Layers> cache = [];
    private readonly LinkedList<Key> recent = [];
    private RenderPose? last;
    private PackContent? content;

    internal void Use(PackContent pack)
    {
        Clear();
        content = pack;
    }

    internal void Draw(RenderPose pose)
    {
        if (last == pose)
            return;

        var (layers, origin) = Prepare(pose);
        body.Present(layers.Body, origin);
        if (layers.Decoration is { } effect)
            decorations.Present(effect, origin);
        else
            decorations.Hide();
        last = pose;
    }

    internal void Paint(Graphics graphics, RenderPose pose)
    {
        var (layers, origin) = Prepare(pose);
        graphics.DrawImageUnscaled(layers.Body, origin);
        if (layers.Decoration is { } effect)
            graphics.DrawImageUnscaled(effect, origin);
    }

    private (Layers Layers, Point Origin) Prepare(RenderPose pose)
    {
        var pack = content!;
        var sprite = pack.Pack.Sprites[pose.SpriteId];
        Size original = pack.ImageSize(sprite.Path);
        int w = original.Width;
        int h = original.Height;
        double scale = pose.CharacterHeight * sprite.ScaleCorrection / pack.Pack.Manifest.ReferenceHeightPx;
        var size = new Size(Math.Clamp((int)Math.Round(w * scale), 1, 1200),
            Math.Clamp((int)Math.Round(h * scale), 1, 1200));
        var anchor = sprite.Anchors.TryGetValue(pose.Anchor, out var point)
            ? point : new WorldPoint(sprite.ContentBounds.X + sprite.ContentBounds.Width / 2.0, sprite.ContentBounds.Bottom);
        var origin = new Point((int)Math.Round(pose.Contact.X - anchor.X * size.Width / w),
            (int)Math.Round(pose.Contact.Y - anchor.Y * size.Height / h));
        var visible = new Rectangle(Point.Empty, size);
        if (pose.Clip is { } clip)
            visible.Intersect(new Rectangle(clip.X - origin.X, clip.Y - origin.Y, clip.Width, clip.Height));

        var key = new Key(pose.SpriteId, size, visible);
        return (GetLayers(key, sprite), origin);
    }

    internal void Hide()
    {
        body.Hide();
        decorations.Hide();
        last = null;
    }

    private Layers GetLayers(Key key, SpriteSpec sprite)
    {
        if (cache.TryGetValue(key, out var existing))
        {
            recent.Remove(key);
            recent.AddLast(key);
            return existing;
        }

        using var image = Scale(content!.File(sprite.Path), key.Size, InterpolationMode.HighQualityBicubic);
        using var silhouette = Scale(content!.File(sprite.MaskPath), key.Size, InterpolationMode.NearestNeighbor);
        var baseLayer = new Bitmap(key.Size.Width, key.Size.Height, PixelFormat.Format32bppPArgb);
        var decoration = new Bitmap(key.Size.Width, key.Size.Height, PixelFormat.Format32bppPArgb);
        bool any;
        try
        {
            any = Split(image, silhouette, baseLayer, decoration, key.Visible);
        }
        catch
        {
            baseLayer.Dispose();
            decoration.Dispose();
            throw;
        }
        if (!any)
            decoration.Dispose();
        var layers = new Layers(baseLayer, any ? decoration : null);
        cache.Add(key, layers);
        recent.AddLast(key);
        if (cache.Count > CacheSize)
        {
            Key oldest = recent.First!.Value;
            cache[oldest].Dispose();
            cache.Remove(oldest);
            recent.RemoveFirst();
        }
        return layers;
    }

    private static Bitmap Scale(byte[] png, Size size, InterpolationMode interpolation)
    {
        using var bytes = new MemoryStream(png, writable: false);
        using var image = Image.FromStream(bytes, useEmbeddedColorManagement: false, validateImageData: true);
        var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = interpolation;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(image, new Rectangle(Point.Empty, size));
        return result;
    }

    private static unsafe bool Split(Bitmap art, Bitmap mask, Bitmap body, Bitmap decorations, Rectangle visible)
    {
        if (visible.IsEmpty)
            return false;
        var all = new Rectangle(Point.Empty, art.Size);
        const PixelFormat format = PixelFormat.Format32bppPArgb;
        var src = art.LockBits(all, ImageLockMode.ReadOnly, format);
        var hit = mask.LockBits(all, ImageLockMode.ReadOnly, format);
        var dst = body.LockBits(all, ImageLockMode.WriteOnly, format);
        var fx = decorations.LockBits(all, ImageLockMode.WriteOnly, format);
        bool anyEffect = false;
        try
        {
            for (int y = visible.Top; y < visible.Bottom; y++)
            {
                var row = (uint*)(src.Scan0 + y * src.Stride);
                var maskRow = (uint*)(hit.Scan0 + y * hit.Stride);
                var bodyRow = (uint*)(dst.Scan0 + y * dst.Stride);
                var fxRow = (uint*)(fx.Scan0 + y * fx.Stride);
                for (int x = visible.Left; x < visible.Right; x++)
                {
                    uint pixel = row[x];
                    if ((pixel >> 24) == 0)
                        continue;
                    if (((maskRow[x] >> 16) & 255) >= 128)
                        bodyRow[x] = pixel;
                    else
                    {
                        fxRow[x] = pixel;
                        anyEffect = true;
                    }
                }
            }
        }
        finally
        {
            art.UnlockBits(src);
            mask.UnlockBits(hit);
            body.UnlockBits(dst);
            decorations.UnlockBits(fx);
        }
        return anyEffect;
    }

    private void Clear()
    {
        last = null;
        foreach (var entry in cache.Values)
            entry.Dispose();
        cache.Clear();
        recent.Clear();
    }

    public void Dispose() => Clear();
}
