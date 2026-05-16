// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Services.Display;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DispHub.Tests.Services.Display;

[TestClass]
public class VibranceServiceFactoryTests
{
    [TestMethod]
    public void Create_ShouldReturnValidIVibranceService()
    {
        var service = VibranceServiceFactory.Create();

        Assert.IsNotNull(service);

        // Depending on the test environment (e.g. CI running on a VM without NVAPI),
        // we might get NullVibranceService or NvidiaVibranceService.
        // Both are valid implementations of IVibranceService.
        if (service is NullVibranceService nullService)
        {
            Assert.IsFalse(nullService.IsSupported);
        }
        else if (service is NvidiaVibranceService nvidiaService)
        {
            // It could be supported or unsupported, but it should be a valid instance
            Assert.IsNotNull(nvidiaService);
        }
        else
        {
            Assert.Fail("Unexpected service type returned by VibranceServiceFactory.");
        }
    }
}
