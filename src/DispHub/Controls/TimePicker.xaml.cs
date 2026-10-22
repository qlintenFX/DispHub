// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;

namespace DispHub.Controls;

/// <summary>
/// A time of day picked like in Windows: hour, minutes and (for 12-hour clocks) AM or PM. Clicking a part shows all
/// its choices at once, so a time takes two clicks and no scrolling. Arrow keys step a focused part.
/// </summary>
public partial class TimePicker : UserControl
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time),
        typeof(TimeOnly),
        typeof(TimePicker),
        new FrameworkPropertyMetadata(default(TimeOnly), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TimePicker)d).ShowTime()));

    private readonly bool _twelveHour = TimeParts.UsesTwelveHours(CultureInfo.CurrentCulture);
    private TimePart _open;
    private DateTime _closedAt;

    public TimePicker()
    {
        InitializeComponent();
        if (!_twelveHour)
        {
            PeriodButton.Visibility = PeriodDivider.Visibility = Visibility.Collapsed;
            PeriodColumn.Width = PeriodDividerColumn.Width = new GridLength(0);
        }
        ChoicesPopup.Closed += (_, _) => _closedAt = DateTime.UtcNow;
        ShowTime();
    }

    public TimeOnly Time
    {
        get => (TimeOnly)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    private void ShowTime()
    {
        HourButton.Content = TimeParts.HourShown(Time, _twelveHour).ToString(_twelveHour ? "0" : "00", CultureInfo.CurrentCulture);
        MinuteButton.Content = Time.Minute.ToString("00", CultureInfo.CurrentCulture);
        DateTimeFormatInfo format = CultureInfo.CurrentCulture.DateTimeFormat;
        PeriodButton.Content = Time.Hour < 12 ? format.AMDesignator : format.PMDesignator;
    }

    private void OnHourClick(object sender, RoutedEventArgs e) => Open(TimePart.Hour, HourButton);

    private void OnMinuteClick(object sender, RoutedEventArgs e) => Open(TimePart.Minute, MinuteButton);

    private void OnPeriodClick(object sender, RoutedEventArgs e) => Time = TimeParts.Step(Time, TimePart.Period, 1);

    private void OnSegmentKeyDown(object sender, KeyEventArgs e)
    {
        TimePart part = sender == HourButton ? TimePart.Hour : sender == MinuteButton ? TimePart.Minute : TimePart.Period;
        int direction = e.Key switch { Key.Up => 1, Key.Down => -1, _ => 0 };
        if (direction == 0)
            return;
        Time = TimeParts.Step(Time, part, direction);
        e.Handled = true;
    }

    private void Open(TimePart part, Button anchor)
    {
        // The click that closed the list by landing on its own button shouldn't open it straight away again.
        if (_open == part && (DateTime.UtcNow - _closedAt).TotalMilliseconds < 300)
            return;
        _open = part;

        IReadOnlyList<int> values = part == TimePart.Hour ? TimeParts.Hours(_twelveHour) : TimeParts.Minutes;
        int current = part == TimePart.Hour ? TimeParts.HourShown(Time, _twelveHour) : TimeParts.NearestMinute(Time.Minute);
        ChoicesGrid.Columns = part == TimePart.Hour && !_twelveHour ? 6 : 4;
        ChoicesGrid.Children.Clear();
        foreach (int value in values)
        {
            var choice = new Wpf.Ui.Controls.Button
            {
                Content = value.ToString(part == TimePart.Minute || !_twelveHour ? "00" : "0", CultureInfo.CurrentCulture),
                Width = 46,
                Height = 34,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Appearance = value == current ? ControlAppearance.Primary : ControlAppearance.Transparent,
            };
            choice.Click += (_, _) =>
            {
                Time = part == TimePart.Hour ? TimeParts.WithHour(Time, value, _twelveHour) : new TimeOnly(Time.Hour, value);
                ChoicesPopup.IsOpen = false;
                anchor.Focus();
            };
            ChoicesGrid.Children.Add(choice);
        }
        ChoicesPopup.PlacementTarget = anchor;
        ChoicesPopup.IsOpen = true;
    }
}

public enum TimePart
{
    None,
    Hour,
    Minute,
    Period,
}

/// <summary>The arithmetic behind <see cref="TimePicker"/>, kept apart so it can be tested.</summary>
internal static class TimeParts
{
    public static IReadOnlyList<int> Minutes { get; } = [.. Enumerable.Range(0, 12).Select(i => i * 5)];

    /// <summary>True when the culture writes times with AM and PM ("h:mm tt") rather than 0–23 ("HH:mm").</summary>
    public static bool UsesTwelveHours(CultureInfo culture) =>
        culture.DateTimeFormat.ShortTimePattern.Contains('h', StringComparison.Ordinal);

    public static IReadOnlyList<int> Hours(bool twelveHour) =>
        twelveHour ? [.. Enumerable.Range(1, 12)] : [.. Enumerable.Range(0, 24)];

    /// <summary>The hour as the picker shows it: 12, 1 … 11 on a 12-hour clock.</summary>
    public static int HourShown(TimeOnly time, bool twelveHour) =>
        !twelveHour ? time.Hour : time.Hour % 12 == 0 ? 12 : time.Hour % 12;

    /// <summary>Sets the hour as shown, keeping AM or PM on a 12-hour clock.</summary>
    public static TimeOnly WithHour(TimeOnly time, int shown, bool twelveHour) =>
        new(twelveHour ? (shown % 12) + (time.Hour >= 12 ? 12 : 0) : shown, time.Minute);

    /// <summary>The 5-minute choice closest to <paramref name="minute"/>.</summary>
    public static int NearestMinute(int minute) => Math.Min((int)Math.Round(minute / 5.0) * 5, 55);

    /// <summary>One step up or down: an hour, five minutes, or between AM and PM. Wraps around midnight.</summary>
    public static TimeOnly Step(TimeOnly time, TimePart part, int direction) => part switch
    {
        TimePart.Hour => time.AddHours(direction),
        TimePart.Minute => new TimeOnly(time.Hour, 0).AddMinutes(Wrap((NearestMinute(time.Minute) / 5) + direction, 12) * 5),
        TimePart.Period => time.AddHours(12),
        _ => time,
    };

    private static int Wrap(int value, int count) => ((value % count) + count) % count;
}
