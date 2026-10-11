namespace DispHub.Platform;

/// <summary>Where DispHub keeps its files: %LocalAppData%\DispHub, or the package's LocalState when installed from the Store.</summary>
public static class AppPaths
{
    private static readonly Lazy<string> Data = new(ResolveDataDirectory);

    public static string DataDirectory => Data.Value;
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
    public static string JournalPath => Path.Combine(DataDirectory, "restore.json");
    public static string StatePath => Path.Combine(DataDirectory, "state.json");

    private static string ResolveDataDirectory()
    {
        string path = PackageInfo.IsPackaged
            ? global::Windows.Storage.ApplicationData.Current.LocalFolder.Path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DispHub");
        Directory.CreateDirectory(path);
        return path;
    }
}
