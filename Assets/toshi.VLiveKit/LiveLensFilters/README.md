# VLive Lens Filters

HDRP / Visual Compositor 向けの、ライブ映像用ポストプロセスとレンズフィルター表現をまとめた Unity package です。

## Package

- Package name: `com.toshi.vlivekit.lensfilters`
- Version: `0.1.2`
- Unity: 2022.3
- Repository: https://github.com/toshi-kundesu/VLiveKit_LiveLensFilters
- Package root: `Assets/toshi.VLiveKit/LiveLensFilters`

## Effects

CreativeFx は `Post-processing/VLiveKit` の Volume Component として追加できます。

- Film / lens: `Halation`, `Film Grain`, `Light Leak`, `Lens Vignette`, `Lens Distortion`, `Chromatic Aberration Plus`
- Optical light: `Anamorphic Flare`, `Star Filter`, `Light Sweep`, `Light Rays`, `Light Wrap`
- Glass / atmosphere: `Prism`, `Dream Blur`, `Water Droplets`, `Depth Fog Overlay`
- Color / anime: `Bleach Bypass`, `Three Strip Color`, `Color Quantize`, `Anime Speed Lines`, `Cinema Scope`
- Glitch / live VJ: `Analog Damage`, `RGB Glitch`, `Block Tear Glitch`, `Scan Roll Glitch`, `Pixel Sort`, `Zoom Blur`

Scene View and Preview cameras are bypassed so the editor view stays usable while the effects remain active for Game cameras.

## Install

Add this package to `Packages/manifest.json`.

```json
{
  "dependencies": {
    "com.toshi.vlivekit.lensfilters": "0.1.2"
  }
}
```

For local development in the VLiveKit sandbox, the package is installed as a submodule under `Packages/VLiveKit_LiveLensFilters` and referenced with a local `file:` dependency.

## Dependencies

- Unity Visual Compositor 0.30.7-preview
- Kino post-processing assets are included under their original license.

## License

Original package code and assets follow this repository's `LICENSE`. Third-party assets keep their own licenses and notices.
