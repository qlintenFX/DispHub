using DispHub.Core.Model;

namespace DispHub.Core.Effects;

/// <summary>Finds looks by id (built-in or saved) and decides which one the screen shows.</summary>
public static class LookLibrary
{
    /// <summary>A profile setting that turns effects off while the profile is on.</summary>
    public const string None = "none";

    /// <summary>A built-in look by id, or a saved one by <see cref="SavedLook.Key"/>.</summary>
    public static Look? Find(EffectsConfig effects, string? id)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (string.IsNullOrEmpty(id))
            return null;
        return Looks.Find(id)?.Look ?? effects.SavedLooks.FirstOrDefault(s => s.Key == id)?.Look;
    }

    /// <summary>The built-in look an id comes from: itself, or what a saved look was made from.</summary>
    public static LookPreset? Origin(EffectsConfig effects, string? id)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return Looks.Find(id) ?? Looks.Find(effects.SavedLooks.FirstOrDefault(s => s.Key == id)?.BasedOn);
    }

    /// <summary>
    /// What the screen shows: the active profile's look when it picks one (unless profiles are off); otherwise
    /// the Effects page. Returns the look and the id it came from. DispHub's master switch is the caller's job.
    /// </summary>
    public static (Look? Look, string? Id) Effective(EffectsConfig effects, Profile? activeProfile, bool profilesOff)
    {
        ArgumentNullException.ThrowIfNull(effects);
        string? chosen = profilesOff ? null : activeProfile?.Effect;
        if (chosen == None)
            return (null, null);
        if (chosen is not null && Find(effects, chosen) is { } look)
            return (look, chosen);
        return effects.Enabled ? (effects.ShownLook, effects.Preset) : (null, null);
    }
}
