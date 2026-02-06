using KeyedColors.Services.Logging;

namespace KeyedColors.Services.Display;

/// <summary>
/// Selects the best available vibrance implementation for the current system.
/// </summary>
public static class VibranceServiceFactory
{
    public static IVibranceService Create()
    {
        // Try NVIDIA first
        try
        {
            var nvidiaService = new KeyedColors.Services.Display.NvidiaVibranceService();
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

        // Fallback to no-op implementation
        Logger.Log("Using null vibrance service (no hardware vibrance support detected)");
        return NullVibranceService.Instance;
    }
}
