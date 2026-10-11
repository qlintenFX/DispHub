using Windows.ApplicationModel;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DispHub.Platform;

/// <summary>Whether DispHub runs as an MSIX package, and whether that package came from the Microsoft Store.</summary>
public static class PackageInfo
{
    private static readonly Lazy<bool> Packaged = new(DetectPackaged);
    private static readonly Lazy<bool> StoreSigned = new(DetectStoreSigned);

    public static bool IsPackaged => Packaged.Value;

    /// <summary>True only for the build installed from the Microsoft Store (the one that sells Pro).</summary>
    public static bool IsStoreSigned => StoreSigned.Value;

    private static bool DetectPackaged()
    {
        uint length = 0;
        WIN32_ERROR result = PInvoke.GetCurrentPackageFullName(ref length, default);
        return result != WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE;
    }

    private static bool DetectStoreSigned()
    {
        if (!IsPackaged)
            return false;
        try
        {
            return Package.Current.SignatureKind == PackageSignatureKind.Store;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
