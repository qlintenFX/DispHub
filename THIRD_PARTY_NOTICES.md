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

Home's glass panel (`src/DispHub.Core/Color/LiquidGlass.cs`) follows the glass shader of
[liquidglass](https://github.com/ybouane/liquidglass) by ybouane (MIT).

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
| Grand Canyon | [Powell Point Sunset](https://commons.wikimedia.org/wiki/File:Grand_Canyon_National_Park,_Powell_Point_Sunset_Feb.10,_2013_1516_-_Flickr_-_Grand_Canyon_NPS.jpg) | Grand Canyon National Park, NPS |
| Winter sunset | [Delicate Arch Sunset (8406472644)](https://commons.wikimedia.org/wiki/File:Delicate_Arch_Sunset_%288406472644%29.jpg) | Arches National Park, NPS |
| Red rock arch | [First Light at Delicate Arch (8709273042)](https://commons.wikimedia.org/wiki/File:First_Light_at_Delicate_Arch_%288709273042%29.jpg) | Arches National Park, NPS |
| Island fort | [Fort Jefferson at Dry Tortugas National Park](https://commons.wikimedia.org/wiki/File:Fort_Jefferson_at_Dry_Tortugas_National_Park,_Florida_%28e23e278c-e4cb-45d2-a77f-afe7eb556068%29.jpg) | U.S. National Park Service |
| Lake pebbles | [Rocks in Lake McDonald (30061292108)](https://commons.wikimedia.org/wiki/File:Rocks_in_Lake_McDonald_%2830061292108%29.jpg) | Glacier National Park, NPS |
| Snowy mountain | [Denali, seen from Wonder Lake](https://commons.wikimedia.org/wiki/File:Denali,_seen_from_Wonder_Lake,_around_2-30_am._%282701cb23-1071-4701-a4b6-09ffe990e291%29.jpg) | Mary Lewandowski, Denali National Park, NPS |
| Northern lights | [Northern lights over the Noatak River](https://commons.wikimedia.org/wiki/File:A_huge_spray_of_northern_lights_is_reflected_in_a_tiny_sliver_of_water_in_the_Noatak_River._This_image_was_captured_in_early_%2893522930-1dd8-b71c-07cc-3dd725d0fff8%29.jpg) | U.S. National Park Service |
| Milky Way | [Milky Way arching over Devils Tower](https://commons.wikimedia.org/wiki/File:Milky_Way_arching_over_Devils_Tower.jpg) | NPS Natural Resources |
| Starry dunes | [Milky Way over a Dune Ridge (53908885943)](https://commons.wikimedia.org/wiki/File:Milky_Way_over_a_Dune_Ridge_%2853908885943%29.jpg) | Great Sand Dunes National Park and Preserve, NPS |
| Desert cactus | [Teddy Bear Cholla and the Milky Way (53433935011)](https://commons.wikimedia.org/wiki/File:Teddy_Bear_Cholla_and_the_Milky_Way_%2853433935011%29.jpg) | Joshua Tree National Park, NPS |

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
