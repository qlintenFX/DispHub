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

    public bool CanAddProfile(int currentCount) => currentCount < MaxProfiles;
}
