using DispHub.Core.Color;

namespace DispHub.Core.Model;

public static class DefaultConfig
{
    public static AppConfig Create()
    {
        var standard = new Profile { Name = "Standard", Color = ColorSettings.Neutral, Hotkey = "Ctrl+Alt+1" };
        var config = new AppConfig
        {
            Profiles =
            [
                standard,
                new Profile
                {
                    Name = "Gaming",
                    Color = new ColorSettings(Brightness: 5, Contrast: 5, Gamma: 1.15, Vibrance: 70),
                    Hotkey = "Ctrl+Alt+2",
                },
                new Profile
                {
                    Name = "Night",
                    Color = new ColorSettings(Brightness: -10, TemperatureK: 3600),
                    Hotkey = "Ctrl+Alt+3",
                },
            ],
            ActiveProfileId = standard.Id,
        };
        config.Hotkeys.TogglePause = "Ctrl+Alt+0";
        return config;
    }
}
