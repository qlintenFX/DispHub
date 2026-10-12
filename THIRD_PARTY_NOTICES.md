# Third-party notices

DispHub is licensed under GPL-3.0-or-later. It uses the following components.

## Shipped with the app

| Component | Licence | Source |
| --- | --- | --- |
| WPF-UI | MIT | https://github.com/lepoco/wpfui |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging | MIT | https://github.com/dotnet/runtime |
| .NET runtime (self-contained Store build) | MIT | https://github.com/dotnet/runtime |

### Preview photos

The profile previews (`src/DispHub/Assets/Previews`) are strips cropped from public-domain photos by U.S.
government employees. Thank you to the photographers.

| Picture | Photo | Credit |
| --- | --- | --- |
| Wildflowers | [Superbloom at Carrizo 2017](https://commons.wikimedia.org/wiki/File:Superbloom_at_Carrizo_2017.jpg) | Bob Wick, U.S. Bureau of Land Management |
| Coral reef | [Biscayne underwater](https://commons.wikimedia.org/wiki/File:Biscayne_underwater_NPS1.jpg) | U.S. National Park Service |
| Autumn forest | [Fall Trail Panorama](https://commons.wikimedia.org/wiki/File:Fall_Trail_Panorama_(50671767778).jpg) | Shenandoah National Park, NPS |
| Hot spring | [Grand Prismatic Spring Panorama](https://commons.wikimedia.org/wiki/File:Grand_Prismatic_Spring_Panorama_(37109293945).jpg) | Jacob W. Frank, Yellowstone National Park, NPS |

## Used at runtime, not shipped

- **NVIDIA NVAPI** (`nvapi64.dll`) is installed with NVIDIA's graphics driver. DispHub calls it to read and set
  digital vibrance. No NVIDIA code is included in DispHub.
- **AMD Display Library** (`atiadlxx.dll`) is installed with AMD Software. DispHub calls it to set saturation on
  AMD-driven displays. Its interface definitions come from AMD's MIT-licensed ADL SDK; no AMD code is included.
- **Intel Graphics Control Library** (`ControlLib.dll`) is installed with Intel's graphics driver. DispHub calls it
  to set saturation on Intel-driven displays. No Intel code is included in DispHub.

## Used only to build or test

| Component | Licence | Source |
| --- | --- | --- |
| Microsoft.Windows.CsWin32 | MIT | https://github.com/microsoft/CsWin32 |
| Microsoft.Windows.SDK.BuildTools (makeappx, makepri) | Microsoft Software License | https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools |
| xUnit.net v3 | Apache-2.0 | https://github.com/xunit/xunit |
| Microsoft.Extensions.TimeProvider.Testing | MIT | https://github.com/dotnet/extensions |

The colour-temperature conversion uses the published Kim et al. (2002) approximation of the Planckian locus.
