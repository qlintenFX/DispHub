// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Services.Display;

namespace DispHub.Tests.Services.Display;

public class NullVibranceServiceTests
{
    [Fact]
    public void Instance_ShouldReturnSingleton()
    {
        var instance1 = NullVibranceService.Instance;
        var instance2 = NullVibranceService.Instance;

        Assert.NotNull(instance1);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void IsSupported_ShouldBeFalse()
    {
        var service = NullVibranceService.Instance;
        Assert.False(service.IsSupported);
    }

    [Fact]
    public void Properties_ShouldReturnDefaultValues()
    {
        var service = NullVibranceService.Instance;
        Assert.Equal(0, service.MinValue);
        Assert.Equal(100, service.MaxValue);
        Assert.Equal(50, service.DefaultValue);
    }

    [Fact]
    public void Methods_ShouldPerformNoOp()
    {
        var service = NullVibranceService.Instance;

        // These shouldn't throw
        var result = service.ApplyVibrance(75);
        Assert.True(result);

        service.ResetVibrance();
        service.Dispose();
    }
}
