// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Services.Display;

namespace DispHub.Tests.Services.Display;

public class VibranceServiceFactoryTests
{
    [Fact]
    public void Create_ShouldReturnValidIVibranceService()
    {
        var service = VibranceServiceFactory.Create();

        Assert.NotNull(service);

        // Depending on the test environment (e.g. CI running on a VM without NVAPI),
        // we might get NullVibranceService or NvidiaVibranceService.
        // Both are valid implementations of IVibranceService.
        if (service is NullVibranceService nullService)
        {
            Assert.False(nullService.IsSupported);
        }
        else if (service is NvidiaVibranceService nvidiaService)
        {
            // It could be supported or unsupported, but it should be a valid instance
            Assert.NotNull(nvidiaService);
        }
        else
        {
            Assert.Fail("Unexpected service type returned by VibranceServiceFactory.");
        }
    }
}
