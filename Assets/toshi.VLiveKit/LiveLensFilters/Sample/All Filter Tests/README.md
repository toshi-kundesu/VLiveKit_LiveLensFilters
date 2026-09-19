# VLive Lens Filters Test Scenes

This sample contains test scenes for quickly checking the package effects in HDRP.

- `Scenes/All Filters Test.unity` cycles through every CreativeFx custom post process.
- `Scenes/Layer Bloom Test.unity` opens directly on the `LayerBloom` custom pass.
- `Prefabs/Volumes/` contains one ready-to-drop global volume prefab per filter, including `Genshin Bloom`, `Genshin Color Grading`, `Diffusion`, and `Screen Transform`, plus `Screen Wiggle`, `Layer Bloom`, and `Mask Offset Rim Light` helper prefabs.
- `Volume Profiles/` contains the profiles used by the CreativeFx volume prefabs.

The scenes use `VLiveKitLensFilterTestRig` to create a temporary preview stage, camera, global volume, and layer-bloom custom pass when the scene is opened or played. The generated objects are not saved into the scene file.

Before using the CreativeFx scene, make sure the VLiveKit Lens Filters custom post process types are present in HDRP Global Settings Custom Post Process Orders.

In the rig inspector, disable `Auto Cycle` and choose `Selected Preset` when you want to tune a single filter. `Layer Bloom` assigns Unity GameObject layer 30 to the preview model and all its children (or the fallback sphere). The floor, stage lamps, and material spheres remain outside the bloom target. The rig creates the `Before Post Process` Custom Pass automatically; a layer named `Character` is not required.

For Layer Bloom in another HDRP project, enable Custom Pass support and set Lit Shader Mode to Both on every HDRP Asset used by the project or quality levels. Keep `CustomPasses/LayerBloom/LayerBloom.shader` in Graphics Settings > Always Included Shaders so its runtime shader lookup also works in a player build. The VLiveKit template includes these settings.

`Light Wrap` in CreativeFx processes the whole camera image. `Layer Bloom` and `Mask Offset Rim Light` are the layer-targeted Custom Pass effects.
