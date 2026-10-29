# Third-party notices

DispHub is licensed under GPL-3.0-or-later. It uses the following components.

## Shipped with the app

| Component | Licence | Source |
| --- | --- | --- |
| WPF-UI, as FluentFlyout's fork (unchihugo.WPF-UI) | MIT | https://github.com/unchihugo/wpfui (from https://github.com/lepoco/wpfui) |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging | MIT | https://github.com/dotnet/runtime |
| .NET runtime (self-contained Store build) | MIT | https://github.com/dotnet/runtime |

### Screen effect techniques

DispHub's effect shader (`src/DispHub.Platform/Effects/Effects.hlsl`) and `FilmGrain.cs` follow published work:

| Effect | Based on | Licence |
| --- | --- | --- |
| Film grain | AV1 film grain synthesis as in [dav1d](https://code.videolan.org/videolan/dav1d) (grain generation and application) and [libaom](https://aomedia.googlesource.com/aom)'s film grain test vector 1 (filter coefficients, brightness curve) | BSD-2-Clause |
| CRT scanlines, shadow mask, screen curve | [Timothy Lottes' CRT shader](https://github.com/libretro/slang-shaders/blob/master/crt/shaders/crt-lottes.slang) | Public domain |
| VHS tape, head switching, tracking noise, snow, edge wave | The effect chain of [ntsc-rs](https://github.com/ntsc-rs/ntsc-rs) | MIT, Apache-2.0 or ISC |
| Colour vision help | Daltonisation by Fidaner, Lin and Ozguven | Published method |

### Preview photos

The profile previews (`src/DispHub/Assets/Previews`) are strips cropped from public-domain photos by U.S.
government employees, and the walkthrough's pictures (`src/DispHub/Assets/Onboarding`) set DispHub's own windows
on them. Thank you to the photographers.

| Picture | Photo | Credit |
| --- | --- | --- |
| Wildflowers | [Superbloom at Carrizo 2017](https://commons.wikimedia.org/wiki/File:Superbloom_at_Carrizo_2017.jpg) | Bob Wick, U.S. Bureau of Land Management |
| Coral reef | [Biscayne underwater](https://commons.wikimedia.org/wiki/File:Biscayne_underwater_NPS1.jpg) | U.S. National Park Service |
| Autumn forest | [Fall Trail Panorama](https://commons.wikimedia.org/wiki/File:Fall_Trail_Panorama_(50671767778).jpg) | Shenandoah National Park, NPS |
| Hot spring | [Grand Prismatic Spring Panorama](https://commons.wikimedia.org/wiki/File:Grand_Prismatic_Spring_Panorama_(37109293945).jpg) | Jacob W. Frank, Yellowstone National Park, NPS |

### Website pictures

The website's profiles page (`site/assets/img/show`) shows DispHub's profiles on these pictures, cropped to 16:9.
The cropped copies keep their licences.

| Picture | Source | Credit and licence |
| --- | --- | --- |
| evening-1 | [LibreOffice Writer, Lorem ipsum](https://commons.wikimedia.org/wiki/File:LibreOffice_Writer-Lorem_ipsum.png) | LoMit, [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) |
| evening-2 | [Thunderbird 115](https://commons.wikimedia.org/wiki/File:Thunderbird_115.png) | MZLA Technologies Corporation, [MPL 2.0](https://www.mozilla.org/MPL/2.0/) |
| evening-3 | [Platypus article on Vector 2022](https://commons.wikimedia.org/wiki/File:Platypus_article_on_Vector_2022.png) | Screenshot by AHollender (WMF), text by Wikipedia authors, [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/); cropped below the site's header |
| film-1 | [Tears of Steel frame 01 2a](https://commons.wikimedia.org/wiki/File:Tears_of_Steel_frame_01_2a.jpg) | Blender Foundation, [mango.blender.org](https://mango.blender.org/), [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| film-2 | [Tears of Steel frame 03 1a](https://commons.wikimedia.org/wiki/File:Tears_of_Steel_frame_03_1a.jpg) | Blender Foundation, [mango.blender.org](https://mango.blender.org/), [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| film-3 | [Tears of Steel frame 09 1a](https://commons.wikimedia.org/wiki/File:Tears_of_Steel_frame_09_1a.jpg) | Blender Foundation, [mango.blender.org](https://mango.blender.org/), [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| vision-1 | [Nuclear Dawn, Silo](https://commons.wikimedia.org/wiki/File:Nuclear_Dawn_-_Silo_Environment_02.png) | Interwave Studios, [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) |
| vision-2 | [Nuclear Dawn, Downtown](https://commons.wikimedia.org/wiki/File:Nuclear_Dawn_-_Downtown_Environment_03.png) | Interwave Studios, [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) |
| vision-3 | [Nuclear Dawn, Coast](https://commons.wikimedia.org/wiki/File:Nuclear_Dawn_-_Coast_Environment_02.jpg) | Interwave Studios, [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) |

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

## Store listing art, not shipped

The Microsoft Store screenshots (`store/screenshots`) and trailer use the preview photos above and:

- A screenshot of [Veloren](https://veloren.net), a free and open-source game whose art is under GPL-3.0:
  [Veloren 0.17 night flight](https://commons.wikimedia.org/wiki/File:Veloren-0.17-screenshot-nightfly.jpg) by
  Antimundo, released under CC0. It stands in for "a game" in the app-profile scene.
- In the trailer only, from Pixabay under the Pixabay Content License: the music "Calm Ambient Dreamscape" by
  MondaMusic and the sound effects "Minimalist Light Whoosh FX" by Chrysalyn, "Soft luxury air whoosh #8" by
  Sdanezis and "Subtle UI Button Click" by Turning_Pages.
