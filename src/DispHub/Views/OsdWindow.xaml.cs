// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DispHub.Platform.Shell;
using Wpf.Ui.Controls;

namespace DispHub.Views;

/// <summary>
/// The on-screen message shown when a profile changes from a hotkey or the tray, in the quick panel's style. It
/// never takes focus (so games keep it) and appears on the monitor the mouse is on.
/// </summary>
public partial class OsdWindow : FluentWindow
{
    private const double BottomMargin = 56;
    private const double EdgeMargin = 24;
    private readonly System.Windows.Threading.DispatcherTimer _hideTimer = new();

    public OsdWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowHelpers.MakeOverlay(new WindowInteropHelper(this).Handle);
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Fade(0, TimeSpan.FromMilliseconds(200), Hide);
        };
    }

    public void Flash(string title, string detail, SymbolRegular symbol, TimeSpan duration, Core.Model.OsdPosition position)
    {
        TitleText.Text = title;
        DetailText.Text = detail;
        DetailText.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
        Glyph.Symbol = symbol;

        // Show first (still fully transparent), then size and move it with real pixel coordinates. The size comes
        // from the content: SizeToContent would add a title bar this frameless window doesn't have.
        if (!IsVisible)
            Show();
        nint handle = new WindowInteropHelper(this).Handle;
        Root.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        System.Windows.Size size = Root.DesiredSize;

        Rectangle work = WindowHelpers.WorkAreaUnderCursor();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int width = (int)Math.Ceiling(size.Width * scale);
        int height = (int)Math.Ceiling(size.Height * scale);
        int edge = (int)(EdgeMargin * scale);
        int x = position switch
        {
            Core.Model.OsdPosition.TopLeft or Core.Model.OsdPosition.BottomLeft => work.Left + edge,
            Core.Model.OsdPosition.TopRight or Core.Model.OsdPosition.BottomRight => work.Right - width - edge,
            _ => work.Left + ((work.Width - width) / 2),
        };
        bool top = position is Core.Model.OsdPosition.TopLeft or Core.Model.OsdPosition.TopCenter or Core.Model.OsdPosition.TopRight;
        int y = top ? work.Top + edge : work.Bottom - height - (int)(BottomMargin * scale);
        WindowHelpers.ShowAt(handle, new Rectangle(x, y, width, height));

        // Slides in from the edge it sits on, like the quick panel; a new message while one shows just swaps the text.
        if (Opacity < 0.01)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(top ? -12 : 12, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        }
        Fade(1, TimeSpan.FromMilliseconds(140));
        _hideTimer.Stop();
        _hideTimer.Interval = duration;
        _hideTimer.Start();
    }

    private void Fade(double to, TimeSpan duration, Action? completed = null)
    {
        var animation = new DoubleAnimation(to, duration) { EasingFunction = new QuadraticEase() };
        if (completed is not null)
            animation.Completed += (_, _) => completed();
        BeginAnimation(OpacityProperty, animation);
    }
}
