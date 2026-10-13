namespace DispHub.Core.Licensing;

/// <summary>
/// What the free tier includes. Builds from source and sideloaded builds are always Pro; only the Microsoft
/// Store build checks the add-on. All limits live here so pricing decisions stay in one place.
/// </summary>
public sealed record FeatureGate(bool IsPro)
{
    public const int FreeProfileLimit = 3;

    public int MaxProfiles => IsPro ? int.MaxValue : FreeProfileLimit;
    public bool AppRules => IsPro;
    public bool Schedules => IsPro;
    public bool PerMonitorProfiles => IsPro;

    /// <summary>Showing screen effects. Everyone can try every look in the app's preview.</summary>
    public bool ScreenEffects => IsPro;

    /// <summary>Saving looks under a name.</summary>
    public bool SavedLooks => IsPro;

    public bool CanAddProfile(int currentCount) => currentCount < MaxProfiles;
}
