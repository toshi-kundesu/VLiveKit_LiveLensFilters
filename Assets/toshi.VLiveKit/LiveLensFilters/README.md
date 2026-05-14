# VLive Lens Filters

HDRP / Visual Compositor 向けの、ライブ映像用ポストプロセスとレンズフィルター表現をまとめた Unity package です。

## Package

- Package name: `com.toshi.vlivekit.lensfilters`
- Version: `0.1.6`
- Unity: 6000.3
- Repository: https://github.com/toshi-kundesu/VLiveKit_LiveLensFilters
- Package root: `Assets/toshi.VLiveKit/LiveLensFilters`

## Effects

CreativeFx は `Post-processing/toshi/LensFilters` の Volume Component として追加できます。

- Film / lens: `Halation`, `Film Grain`, `Light Leak`, `Lens Vignette`, `Lens Distortion`, `Chromatic Aberration Plus`, `VLiveDOF`
- Optical light: `Anamorphic Flare`, `Star Filter`, `Shaped Bokeh Filter`, `Light Sweep`, `Light Rays`, `Light Wrap`
- Stylized bloom / diffusion: `Genshin Bloom`, `Genshin Color Grading`, `Diffusion`
- Glass / atmosphere: `Prism`, `Dream Blur`, `Water Droplets`, `Depth Fog Overlay`
- Color / anime: `Bleach Bypass`, `Three Strip Color`, `Color Quantize`, `Anime Speed Lines`, `Cinema Scope`
- Screen motion: `Screen Transform`, `Zoom Blur`
- Glitch / live VJ: `Analog Damage`, `RGB Glitch`, `Block Tear Glitch`, `Scan Roll Glitch`, `Pixel Sort`

Scene View and Preview cameras are bypassed so the editor view stays usable while the effects remain active for Game cameras.

`VLiveDOF` is a depth-aware creative depth of field effect. Set `Focus Distance` and `Focus Range` for the sharp plane, then use `Blur Radius`, `Bokeh Threshold`, `Bokeh Intensity`, and `Bokeh Tint` to make bright out-of-focus lights draw intentional round bokeh discs.

`Shaped Bokeh Filter` turns bright highlights into patterned bokeh. Use `Pattern` for built-in Forest, Star, Heart, and Circle masks, or assign a high-contrast alpha texture to `Pattern Texture` for custom shapes such as text or icons.

`Screen Transform` applies a post-process UV transform to the camera image. Use `Offset`, `Zoom`, `Rotation`, and `Pivot` directly for keyframed screen motion, or add `ScreenTransformWiggle` to a Volume object to drive those values with smooth Perlin-noise motion similar to After Effects wiggle. Keep `Zoom` slightly above `1` when using offset wiggle so the clamped screen edges stay outside the frame.

## Layer Bloom Custom Pass

`LayerBloom` is an HDRP Custom Pass for adding bloom only from objects on a selected Unity layer.

1. Add a `Custom Pass Volume` to the scene and add `LayerBloom`.
2. Set `Target Layer` to the layer that should emit the bloom.
3. Place the volume at an injection point after the target objects are rendered, such as `Before Post Process` or `After Post Process`.
4. Tune `Threshold`, `Source Boost`, `Blur Radius`, `Blur Iterations`, `Intensity`, and `Tint`.

Set `Color Mode` to `Source Color` when the bloom should keep the target objects' rendered color. Use `Tint Color` for a single-color glow driven by the source brightness, or `Source Color Tinted` for the older multiplied-tint behavior.

`Use Camera Depth` keeps the bloom source limited to visible parts of the selected layer. Disable it when you want hidden or always-on layer silhouettes to contribute to the bloom. `Show Bloom Only` is useful while tuning the mask and blur.

## Samples

- `Empty Scene` is a blank starting point for a Lens Filters setup.
- `All Filter Tests` includes `All Filters Test.unity`, which cycles through every CreativeFx custom post process, and `Layer Bloom Test.unity`, which opens directly on the `LayerBloom` custom pass.
- `All Filter Tests/Prefabs/Volumes` includes ready-to-drop global volume prefabs for every CreativeFx effect, a `Screen Wiggle Volume` helper prefab, plus LayerBloom and MaskOffsetRimLight custom pass volume prefabs.

For local package development, the same scenes are mirrored under `Sample/` so they stay visible in the Unity Project window. Published packages use the importable `Samples~/` copies listed in `package.json`; `Sample/` is excluded from npm packages.

## Install

Add this package to `Packages/manifest.json`.

```json
{
  "dependencies": {
    "com.toshi.vlivekit.lensfilters": "0.1.6"
  }
}
```

For local development in the VLiveKit sandbox, the package is installed as a submodule under `Packages/VLiveKit_LiveLensFilters` and referenced with a local `file:` dependency.

## Dependencies

- Kino 2.1.12 (`jp.keijiro.kino.post-processing`) is installed as a package dependency from the `jp.keijiro` scoped registry.
- Cinema (`jp.supertask.cinema.post-processing`) is installed from Git in the project `Packages/manifest.json`, because Unity does not allow Git URL dependencies inside a package `package.json`.

Unity Visual Compositor 0.30.7-preview is not installed by default because it targets an older Unity/HDRP stack and can fail shader compilation in Unity 6000.3/HDRP 17. The legacy `StepNode` custom node is still included as optional source and only compiles when `VLIVEKIT_LIVELENSFILTERS_ENABLE_VISUAL_COMPOSITOR` is defined in a project that installs Visual Compositor explicitly.

To use the registry dependency outside the VLiveKit installer flow, add the Keijiro scoped registry to the project manifest:

```json
{
  "scopedRegistries": [
    {
      "name": "Keijiro",
      "url": "https://registry.npmjs.com",
      "scopes": [ "jp.keijiro" ]
    }
  ]
}
```

Cinema can be added to the project manifest with:

```json
{
  "dependencies": {
    "jp.supertask.cinema.post-processing": "https://github.com/supertask/Cinema.git?path=/Packages/jp.supertask.cinema.post-processing"
  }
}
```

## License

Original package code and assets follow this repository's `LICENSE`. Third-party assets keep their own licenses and notices.
