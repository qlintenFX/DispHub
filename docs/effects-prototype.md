# Screen effects (prototype)

Film, camera and retro looks for the whole screen, in two layers. Lives on `proto/screen-effects`; not shipped.

## What works

| Layer | How | Cost | In recordings |
| --- | --- | --- | --- |
| Colour grade: saturation, contrast, fade, sepia, tint | Windows' full-screen colour matrix (`MagSetFullscreenColorEffect`), 5x5 | none | yes |
| Texture: grain, vignette, scanlines, flicker, letterbox | Click-through DirectComposition window per monitor, one HLSL pixel shader | ~3 % of one core at 24 fps grain | optional |

Measured on a 165 Hz 2560x1600 laptop (Intel UHD): grain at 24 fps costs about 3 % of one CPU core, clicks pass
through, and the overlay steps aside when a full-screen app is in front (it would otherwise stop the game using
independent flip and add a frame of latency).

## Not possible this way

Effects that read the picture (bloom, blur, chromatic aberration, lens distortion, CRT curvature, VHS colour bleed)
need the screen captured and redrawn, like ShaderGlass or Magpie: a frame of latency and real GPU load. Left out.

## Open before shipping

- **Safety of the colour matrix.** During testing, after a process that set the matrix was killed without resetting
  it, new WPF windows stopped drawing with hardware acceleration until the desktop was restarted. Windows did remove
  the colour effect itself. Reproduce after a clean sign-in; if confirmed, run the matrix from a tiny helper process
  that resets it on exit, or use the graphics driver (vibrance plus gamma ramps) for monochrome and sepia instead.
- Memory: the overlay's swap chains add about 30 MB per 2560x1600 monitor while effects are on.
- Should effects be part of a profile (so a hotkey switches look and colours together) and a Pro feature?
