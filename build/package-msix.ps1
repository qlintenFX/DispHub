<#
.SYNOPSIS
  Builds the MSIX bundle for the Microsoft Store without Visual Studio.

.DESCRIPTION
  Publishes DispHub self-contained for each architecture, stamps packaging/AppxManifest.xml with the Store identity
  from packaging/identity.json (the dev identity with -Register) and the languages in src/DispHub/Localization, indexes resources with makepri, packs each architecture with makeappx and bundles them.
  The Store signs the package on upload, so the output is unsigned.

  -Register skips packing and registers the x64 layout for local testing. It needs Developer Mode
  (Settings > System > For developers).

.EXAMPLE
  ./build/package-msix.ps1
  ./build/package-msix.ps1 -Architectures x64 -Register
#>
param(
    [string]$Configuration = "Release",
    [string[]]$Architectures = @("x64", "arm64"),
    [string]$Version,
    [switch]$Register
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root "artifacts"
$identities = Get-Content (Join-Path $root "packaging/identity.json") -Raw | ConvertFrom-Json
# The Store's identity for the upload; a separate one for local test installs.
$identity = if ($Register) { $identities.dev } else { $identities.store }

if (-not $Version) {
    $props = [xml](Get-Content (Join-Path $root "Directory.Build.props"))
    $Version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
# MSIX versions have four parts and the Store requires the last one to be 0.
$packageVersion = (($Version -split "[.-]")[0..2] -join ".") + ".0"

# Every language DispHub ships, English first as the default. Windows only offers a packaged app the languages its
# manifest declares (and the Store lists only those), so a missing one would leave its speakers in English.
$languages = @("en-US") + @(Get-ChildItem (Join-Path $root "src/DispHub/Localization") -Filter "Strings.*.resx" |
    ForEach-Object { $_.BaseName.Substring("Strings.".Length) } | Sort-Object)
$resources = ($languages | ForEach-Object { "<Resource Language=`"$_`" />" }) -join "`r`n    "

# Locate makeappx and makepri from the Windows SDK build tools NuGet package.
dotnet restore (Join-Path $PSScriptRoot "tools/Tools.csproj") | Out-Null
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME ".nuget/packages" }
$toolsRoot = Get-ChildItem (Join-Path $nuget "microsoft.windows.sdk.buildtools") -Directory | Sort-Object Name | Select-Object -Last 1
$bin = Get-ChildItem (Join-Path $toolsRoot.FullName "bin") -Directory | Where-Object { Test-Path (Join-Path $_.FullName "x64/makeappx.exe") } | Select-Object -Last 1
$makeappx = Join-Path $bin.FullName "x64/makeappx.exe"
$makepri = Join-Path $bin.FullName "x64/makepri.exe"

New-Item -ItemType Directory -Force $artifacts | Out-Null
$packages = @()

foreach ($arch in $Architectures) {
    $layout = Join-Path $artifacts "layout-$arch"
    if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }

    Write-Host "Publishing $arch..."
    dotnet publish (Join-Path $root "src/DispHub/DispHub.csproj") -c $Configuration -r "win-$arch" --self-contained `
        -p:PublishReadyToRun=true -p:DebugType=none -p:Version=$Version -o $layout
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $arch" }

    $manifest = (Get-Content (Join-Path $root "packaging/AppxManifest.xml") -Raw).
        Replace('$IdentityName$', $identity.name).
        Replace('$Publisher$', $identity.publisher).
        Replace('$PublisherDisplayName$', $identity.publisherDisplayName).
        Replace('$Version$', $packageVersion).
        Replace('$Architecture$', $arch).
        Replace('$Resources$', $resources)
    Set-Content (Join-Path $layout "AppxManifest.xml") $manifest -Encoding utf8
    Copy-Item (Join-Path $root "packaging/Assets") (Join-Path $layout "Assets") -Recurse -Force

    # Index only the manifest and assets so the runtime DLLs stay out of resources.pri.
    $staging = Join-Path $artifacts "pri-$arch"
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    New-Item -ItemType Directory $staging | Out-Null
    Copy-Item (Join-Path $layout "AppxManifest.xml") $staging
    Copy-Item (Join-Path $layout "Assets") (Join-Path $staging "Assets") -Recurse
    $priConfig = Join-Path $artifacts "priconfig.xml"
    & $makepri createconfig /cf $priConfig /dq ($languages -join "_") /pv 10.0.0 /o | Out-Null
    & $makepri new /pr $staging /cf $priConfig /mn (Join-Path $staging "AppxManifest.xml") /of (Join-Path $layout "resources.pri") /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri failed for $arch" }

    if ($Register) {
        Write-Host "Registering $layout (Developer Mode required)..."
        Add-AppxPackage -Register (Join-Path $layout "AppxManifest.xml") -ForceApplicationShutdown
        return
    }

    $package = Join-Path $artifacts "DispHub_${packageVersion}_$arch.msix"
    & $makeappx pack /d $layout /p $package /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $arch" }
    $packages += $package
}

# Bundle every architecture into one upload for Partner Center.
$bundleDir = Join-Path $artifacts "bundle"
if (Test-Path $bundleDir) { Remove-Item -Recurse -Force $bundleDir }
New-Item -ItemType Directory $bundleDir | Out-Null
$packages | ForEach-Object { Copy-Item $_ $bundleDir }
$bundle = Join-Path $artifacts "DispHub_$packageVersion.msixbundle"
& $makeappx bundle /d $bundleDir /p $bundle /bv $packageVersion /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed" }

Write-Host "Done: $bundle"
