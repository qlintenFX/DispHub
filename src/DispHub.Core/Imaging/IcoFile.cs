// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace DispHub.Core.Imaging;

/// <summary>Reads the frame directory of an .ico file, to hand Windows the frame drawn for an exact size.</summary>
public static class IcoFile
{
    public readonly record struct Frame(int Size, int Offset, int Length);

    public static IReadOnlyList<Frame> ReadFrames(ReadOnlySpan<byte> ico)
    {
        if (ico.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(ico[2..]) != 1)
            return [];
        int count = BinaryPrimitives.ReadUInt16LittleEndian(ico[4..]);
        var frames = new List<Frame>(count);
        for (int i = 0; i < count; i++)
        {
            int entry = 6 + (i * 16);
            if (entry + 16 > ico.Length)
                break;
            int size = ico[entry] == 0 ? 256 : ico[entry];
            int length = BinaryPrimitives.ReadInt32LittleEndian(ico[(entry + 8)..]);
            int offset = BinaryPrimitives.ReadInt32LittleEndian(ico[(entry + 12)..]);
            if (offset > 0 && length > 0 && (long)offset + length <= ico.Length)
                frames.Add(new Frame(size, offset, length));
        }
        return frames;
    }

    /// <summary>The smallest frame at least <paramref name="size"/> pixels, else the largest; null if there are none.</summary>
    public static Frame? Pick(IReadOnlyList<Frame> frames, int size)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            return null;
        Frame? fitting = frames.Where(f => f.Size >= size).OrderBy(f => f.Size).Cast<Frame?>().FirstOrDefault();
        return fitting ?? frames.MaxBy(f => f.Size);
    }
}
