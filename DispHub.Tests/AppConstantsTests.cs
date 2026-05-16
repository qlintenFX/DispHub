using DispHub.Constants;

namespace DispHub.Tests;

public class AppConstantsTests
{
    [Fact]
    public void GammaRange_IsValid()
    {
        Assert.True(AppConstants.GammaMin < AppConstants.GammaMax);
        Assert.True(AppConstants.GammaDefault >= AppConstants.GammaMin);
        Assert.True(AppConstants.GammaDefault <= AppConstants.GammaMax);
        Assert.True(AppConstants.GammaStep > 0);
    }

    [Fact]
    public void ContrastRange_IsValid()
    {
        Assert.True(AppConstants.ContrastMin < AppConstants.ContrastMax);
        Assert.True(AppConstants.ContrastDefault >= AppConstants.ContrastMin);
        Assert.True(AppConstants.ContrastDefault <= AppConstants.ContrastMax);
        Assert.True(AppConstants.ContrastStep > 0);
    }

    [Fact]
    public void VibranceRange_IsValid()
    {
        Assert.True(AppConstants.VibranceMin < AppConstants.VibranceMax);
        Assert.True(AppConstants.VibranceDefault >= AppConstants.VibranceMin);
        Assert.True(AppConstants.VibranceDefault <= AppConstants.VibranceMax);
        Assert.True(AppConstants.VibranceStep > 0);
    }

    [Fact]
    public void ColorTempRange_IsValid()
    {
        Assert.True(AppConstants.ColorTempMin < AppConstants.ColorTempMax);
        Assert.True(AppConstants.ColorTempDefault >= AppConstants.ColorTempMin);
        Assert.True(AppConstants.ColorTempDefault <= AppConstants.ColorTempMax);
        Assert.True(AppConstants.ColorTempNeutralValue >= AppConstants.ColorTempMin);
        Assert.True(AppConstants.ColorTempNeutralValue <= AppConstants.ColorTempMax);
        Assert.True(AppConstants.ColorTempStep > 0);
    }

    [Fact]
    public void KelvinRange_IsValid()
    {
        Assert.True(AppConstants.ColorTempMinKelvin < AppConstants.ColorTempNeutralKelvin);
        Assert.True(AppConstants.ColorTempNeutralKelvin < AppConstants.ColorTempMaxKelvin);
    }

    [Fact]
    public void NvidiaVibranceRange_IsValid()
    {
        Assert.True(AppConstants.NvidiaVibranceMin < AppConstants.NvidiaVibranceMax);
        Assert.True(AppConstants.NvidiaVibranceDefault >= AppConstants.NvidiaVibranceMin);
        Assert.True(AppConstants.NvidiaVibranceDefault <= AppConstants.NvidiaVibranceMax);
    }

    [Fact]
    public void GammaRampConstants_ArePositive()
    {
        Assert.True(AppConstants.GammaRampSize > 0);
        Assert.True(AppConstants.GammaRampMaxValue > 0);
    }

    [Fact]
    public void AppDataPath_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(AppConstants.AppDataPath));
    }

    [Fact]
    public void AppDataPath_ContainsAppDataFolder()
    {
        Assert.Contains(AppConstants.AppDataFolder, AppConstants.AppDataPath, StringComparison.Ordinal);
    }

    [Fact]
    public void AppMetadata_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.AppName));
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.Version));
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.ProfilesFileName));
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.SettingsFileName));
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.LogFileName));
    }

    [Fact]
    public void HotkeyModifiers_ArePowersOfTwo()
    {
        Assert.Equal(0x0001u, AppConstants.MOD_ALT);
        Assert.Equal(0x0002u, AppConstants.MOD_CONTROL);
        Assert.Equal(0x0004u, AppConstants.MOD_SHIFT);
        Assert.Equal(0x0008u, AppConstants.MOD_WIN);
    }

    [Fact]
    public void GitHubUrls_AreWellFormed()
    {
        Assert.True(Uri.IsWellFormedUriString(AppConstants.GitHubUrl, UriKind.Absolute));
        Assert.True(Uri.IsWellFormedUriString(AppConstants.GitHubIssuesUrl, UriKind.Absolute));
    }

    [Fact]
    public void StartupRegistryKey_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.StartupRegistryKey));
    }
}
