// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Services.Logging;

namespace DispHub.Services.Display;

public static class VibranceServiceFactory
{
    public static IVibranceService Create()
    {
        try
        {
            var nvidiaService = new NvidiaVibranceService();
            if (nvidiaService.IsSupported)
            {
                Logger.Log("Using NVIDIA vibrance service");
                return nvidiaService;
            }
            nvidiaService.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to create NVIDIA vibrance service", ex);
        }

        Logger.Log("Using null vibrance service (no hardware support detected)");
        return NullVibranceService.Instance;
    }
}
