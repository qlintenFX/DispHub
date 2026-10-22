using System.Globalization;
using DispHub.Controls;

namespace DispHub.Tests.App;

public sealed class TimePickerTests
{
    [Theory]
    [InlineData("en-US", true)]
    [InlineData("en-GB", false)]
    [InlineData("nl-BE", false)]
    public void ClockFollowsTheCulture(string culture, bool twelveHour) =>
        Assert.Equal(twelveHour, TimeParts.UsesTwelveHours(new CultureInfo(culture)));

    [Theory]
    [InlineData(0, 12)]
    [InlineData(9, 9)]
    [InlineData(12, 12)]
    [InlineData(21, 9)]
    public void TwelveHourClock_ShowsOneToTwelve(int hour, int shown) =>
        Assert.Equal(shown, TimeParts.HourShown(new TimeOnly(hour, 0), twelveHour: true));

    [Fact]
    public void PickingAnHour_KeepsAmOrPm()
    {
        Assert.Equal(new TimeOnly(21, 30), TimeParts.WithHour(new TimeOnly(19, 30), 9, twelveHour: true));
        Assert.Equal(new TimeOnly(0, 30), TimeParts.WithHour(new TimeOnly(7, 30), 12, twelveHour: true));
        Assert.Equal(new TimeOnly(12, 0), TimeParts.WithHour(new TimeOnly(13, 0), 12, twelveHour: true));
        Assert.Equal(new TimeOnly(9, 0), TimeParts.WithHour(new TimeOnly(21, 0), 9, twelveHour: false));
    }

    [Fact]
    public void ArrowKeys_StepAndWrap()
    {
        Assert.Equal(new TimeOnly(0, 0), TimeParts.Step(new TimeOnly(23, 0), TimePart.Hour, 1));
        Assert.Equal(new TimeOnly(21, 0), TimeParts.Step(new TimeOnly(21, 55), TimePart.Minute, 1)); // minutes wrap within the hour
        Assert.Equal(new TimeOnly(21, 10), TimeParts.Step(new TimeOnly(21, 7), TimePart.Minute, 1)); // from the nearest step
        Assert.Equal(new TimeOnly(9, 15), TimeParts.Step(new TimeOnly(21, 15), TimePart.Period, 1));
    }

    [Fact]
    public void Choices_HaveNothingToScroll()
    {
        Assert.Equal(12, TimeParts.Hours(twelveHour: true).Count);
        Assert.Equal(24, TimeParts.Hours(twelveHour: false).Count);
        Assert.Equal(12, TimeParts.Minutes.Count);
        Assert.Equal(55, TimeParts.NearestMinute(58));
    }
}
