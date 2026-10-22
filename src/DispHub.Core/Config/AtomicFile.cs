// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace DispHub.Core.Config;

/// <summary>Writes files so a crash or power cut never leaves a half-written file behind.</summary>
public static class AtomicFile
{
    /// <summary>Writes to a temp file, flushes it to disk, then swaps it in. The previous file becomes <paramref name="backupPath"/>.</summary>
    public static void WriteAllText(string path, string contents, string? backupPath = null)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string tempPath = path + ".tmp";

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(contents);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
        else
            File.Move(tempPath, path);
    }
}
