using System.Buffers.Binary;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Cutsy.Core;

namespace Cutsy.Windows;

internal sealed class PackContent
{
    private const string Prefix = "Cutsy.Pack/";
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    internal ValidatedPack Pack { get; }

    internal PackContent(Func<string, int, byte[]?> source)
    {
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
            files.Add(path, data);
            return data;
        });
    }

    internal byte[] File(string path) => files[path];

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
        });
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

        var pack = content!;
        var sprite = pack.Pack.Sprites[pose.SpriteId];
        byte[] art = pack.File(sprite.Path);
        int w = BinaryPrimitives.ReadInt32BigEndian(art.AsSpan(16));
        int h = BinaryPrimitives.ReadInt32BigEndian(art.AsSpan(20));
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
        var layers = GetLayers(key, art, pack.File(sprite.MaskPath));
        body.Present(layers.Body, origin);
        if (layers.Decoration is { } effect)
            decorations.Present(effect, origin);
        else
            decorations.Hide();
        last = pose;
    }

    internal void Hide()
    {
        body.Hide();
        decorations.Hide();
        last = null;
    }

    private Layers GetLayers(Key key, byte[] art, byte[] mask)
    {
        if (cache.TryGetValue(key, out var existing))
        {
            recent.Remove(key);
            recent.AddLast(key);
            return existing;
        }

        using var image = Scale(art, key.Size, InterpolationMode.HighQualityBicubic);
        using var silhouette = Scale(mask, key.Size, InterpolationMode.NearestNeighbor);
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
