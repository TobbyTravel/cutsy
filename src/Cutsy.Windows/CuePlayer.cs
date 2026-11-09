using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Cutsy.Core;

namespace Cutsy.Windows;

internal sealed class CuePlayer : IDisposable
{
    private const uint MemoryAsyncNoDefault = 0x4 | 0x1 | 0x2;
    private GCHandle buffer;
    private long playingUntil;
    private long ambientUntil;
    private long clickUntil;

    internal double Volume { get; set; } = Preferences.DefaultVolume / 100.0;
    internal bool Muted { get; set; }

    internal bool Play(PackContent content, SoundRequest cue, bool preview = false)
    {
        long now = Environment.TickCount64;
        if (Muted || Volume <= 0 || now < playingUntil || (!preview && now < (cue.Interaction ? clickUntil : ambientUntil)) ||
            !content.Pack.Sounds.TryGetValue(cue.Id, out var spec))
            return false;

        Stop();
        var bytes = (byte[])content.File(spec.Path).Clone();
        var (rate, offset, length) = PackLoader.ReadPcm(bytes);
        double gain = Volume * spec.Gain;
        for (int i = offset; i < offset + length; i += 2)
        {
            var pcm = bytes.AsSpan(i, 2);
            short sample = BinaryPrimitives.ReadInt16LittleEndian(pcm);
            BinaryPrimitives.WriteInt16LittleEndian(pcm, (short)Math.Round(sample * gain));
        }

        buffer = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        if (!Native.PlaySound(buffer.AddrOfPinnedObject(), 0, MemoryAsyncNoDefault))
        {
            buffer.Free();
            return false;
        }

        playingUntil = now + (long)Math.Ceiling(length * 500.0 / rate);
        if (!preview)
        {
            if (cue.Interaction)
                clickUntil = now + 5000;
            else
                ambientUntil = now + 90000;
        }
        return true;
    }

    internal void Stop()
    {
        playingUntil = 0;
        if (!buffer.IsAllocated)
            return;
        Native.PlaySound(0, 0, 0);
        buffer.Free();
    }

    public void Dispose() => Stop();
}

internal static partial class Native
{
    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PlaySound(nint wave, nint module, uint flags);
}
