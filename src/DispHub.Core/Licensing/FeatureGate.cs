namespace DispHub.Core.Licensing;

/// <summary>
/// What the free tier includes. Builds from source and sideloaded builds are always Pro; only the Microsoft
/// Store build checks the add-on. All limits live here so pricing decisions stay in one place.
/// </summary>
public sealed record FeatureGate(bool IsPro)
{
    public const int FreeProfileLimit = 5;

    public int MaxProfiles => IsPro ? int.MaxValue : FreeProfileLimit;
    public bool AppRules => IsPro;
    public bool Schedules => IsPro;
    public bool PerMonitorProfiles => IsPro;

    /// <summary>Saving looks under a name.</summary>
    public bool SavedLooks => IsPro;

    /// <summary>
    /// Whether a look can be shown on screen. Everyone can preview every look in the app; free shows the comfort and
    /// accessibility looks (and changes made to them) on screen, Pro shows all.
    /// </summary>
    public bool CanShowLook(Effects.LookPreset? origin) =>
        IsPro || origin?.Category is Effects.LookCategory.Comfort or Effects.LookCategory.Access;

    public bool CanAddProfile(int currentCount) => currentCount < MaxProfiles;
}
