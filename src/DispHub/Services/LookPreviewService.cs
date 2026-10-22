// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DispHub.Core.Effects;
using DispHub.Platform.Effects;
using Microsoft.Extensions.Logging;

namespace DispHub.Services;

/// <summary>
/// Renders looks onto the preview photos for the Effects page: a large picture for the editor and small ones for
/// the gallery. Uses the GPU off screen, on worker threads; created when the page opens and released when the
/// window closes, so DispHub stays light in the background.
/// </summary>
public sealed class LookPreviewService(ILogger<LookPreviewService> logger) : IDisposable
{
    public const int LargeWidth = 960;
    public const int LargeHeight = 600;
    public const int ThumbWidth = 320;
    public const int ThumbHeight = 200;

    // Held only by worker threads, never by the UI thread, so waiting on it can't deadlock.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private EffectPreview? _large;
    private EffectPreview? _thumbs;
    private string? _picture;

    /// <summary>The photo without any effect, at the large size.</summary>
    public BitmapSource? Original { get; private set; }

    /// <summary>Loads one of <see cref="Controls.ColorPreview.Pictures"/> as the picture looks are drawn on.</summary>
    public async Task<bool> SetPictureAsync(string picture)
    {
        BitmapSource? original = await Locked(() =>
        {
            if (picture == _picture && _large is not null)
                return null;
            (BitmapSource photo, byte[] large, byte[] thumb) = Load(picture);
            _large ??= new EffectPreview();
            _thumbs ??= new EffectPreview();
            _large.SetPicture(large, LargeWidth, LargeHeight);
            _thumbs.SetPicture(thumb, ThumbWidth, ThumbHeight);
            _picture = picture;
            return photo;
        }).ConfigureAwait(true);
        if (original is not null)
            Original = original;
        return Original is not null;
    }

    /// <summary>Draws <paramref name="look"/> on the large picture into <paramref name="target"/>.</summary>
    public async Task<bool> RenderLargeAsync(Look look, double seconds, WriteableBitmap target)
    {
        ArgumentNullException.ThrowIfNull(target);
        byte[] pixels = new byte[LargeWidth * LargeHeight * 4];
        bool done = await Locked(() =>
        {
            _large?.Render(look, pixels, seconds);
            return _large is not null;
        }).ConfigureAwait(true);
        if (done)
            target.WritePixels(new Int32Rect(0, 0, LargeWidth, LargeHeight), pixels, LargeWidth * 4, 0);
        return done;
    }

    /// <summary>A small picture of <paramref name="look"/> for the gallery.</summary>
    public Task<BitmapSource?> RenderThumbAsync(Look look) => Locked(() =>
    {
        if (_thumbs is null)
            return null;
        byte[] pixels = new byte[ThumbWidth * ThumbHeight * 4];
        _thumbs.Render(look, pixels);
        BitmapSource bitmap = BitmapSource.Create(ThumbWidth, ThumbHeight, 96, 96, PixelFormats.Bgra32, null, pixels, ThumbWidth * 4);
        bitmap.Freeze();
        return bitmap;
    });

    /// <summary>Releases the GPU resources; the next <see cref="SetPictureAsync"/> creates them again.</summary>
    public void Release()
    {
        Original = null;
        _ = Locked(() =>
        {
            ReleaseRenderers();
            return true;
        });
    }

    public void Dispose()
    {
        // At exit: wait briefly for a render in progress, then let the GPU objects go.
        if (_gate.Wait(TimeSpan.FromSeconds(1)))
        {
            ReleaseRenderers();
            _gate.Release();
        }
    }

    private void ReleaseRenderers()
    {
        _large?.Dispose();
        _thumbs?.Dispose();
        (_large, _thumbs, _picture) = (null, null, null);
    }

    private Task<T?> Locked<T>(Func<T?> work) => Task.Run(async () =>
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return work();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or System.IO.IOException or NotSupportedException or ObjectDisposedException)
        {
            logger.LogWarning(ex, "Couldn't render an effect preview");
            return default;
        }
        finally
        {
            _gate.Release();
        }
    });

    private static (BitmapSource Photo, byte[] Large, byte[] Thumb) Load(string picture)
    {
        var decoder = BitmapDecoder.Create(new Uri($"pack://application:,,,/Assets/Previews/{picture}-wide.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapSource photo = decoder.Frames[0];
        BitmapSource large = Bgra(photo, LargeWidth, LargeHeight);
        BitmapSource thumb = Bgra(photo, ThumbWidth, ThumbHeight);
        byte[] largePixels = new byte[LargeWidth * LargeHeight * 4];
        large.CopyPixels(largePixels, LargeWidth * 4, 0);
        byte[] thumbPixels = new byte[ThumbWidth * ThumbHeight * 4];
        thumb.CopyPixels(thumbPixels, ThumbWidth * 4, 0);
        return (large, largePixels, thumbPixels);
    }

    private static WriteableBitmap Bgra(BitmapSource photo, int width, int height)
    {
        var scaled = new TransformedBitmap(photo, new ScaleTransform(width / (double)photo.PixelWidth, height / (double)photo.PixelHeight));
        // A frozen copy, so it can be used on the UI thread.
        var copy = new WriteableBitmap(new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0));
        copy.Freeze();
        return copy;
    }
}
