// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Services.Display;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DispHub.Tests.Services.Display;

[TestClass]
public class NullVibranceServiceTests
{
    [TestMethod]
    public void Instance_ShouldReturnSingleton()
    {
        var instance1 = NullVibranceService.Instance;
        var instance2 = NullVibranceService.Instance;

        Assert.IsNotNull(instance1);
        Assert.AreSame(instance1, instance2);
    }

    [TestMethod]
    public void IsSupported_ShouldBeFalse()
    {
        var service = NullVibranceService.Instance;
        Assert.IsFalse(service.IsSupported);
    }

    [TestMethod]
    public void Properties_ShouldReturnDefaultValues()
    {
        var service = NullVibranceService.Instance;
        Assert.AreEqual(0, service.MinValue);
        Assert.AreEqual(100, service.MaxValue);
        Assert.AreEqual(50, service.DefaultValue);
    }

    [TestMethod]
    public void Methods_ShouldPerformNoOp()
    {
        var service = NullVibranceService.Instance;
        
        // These shouldn't throw
        var result = service.ApplyVibrance(75);
        Assert.IsTrue(result);
        
        service.ResetVibrance();
        service.Dispose();
    }
}
