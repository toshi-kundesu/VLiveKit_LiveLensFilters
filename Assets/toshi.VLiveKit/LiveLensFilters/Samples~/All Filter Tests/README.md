# VLive Lens Filters Test Scenes

This sample contains test scenes for quickly checking the package effects in HDRP.

- `Scenes/All Filters Test.unity` cycles through every CreativeFx custom post process.
- `Scenes/Layer Bloom Test.unity` opens directly on the `LayerBloom` custom pass.
- `Prefabs/Volumes/` contains one ready-to-drop global volume prefab per filter, including `Genshin Bloom`, `Genshin Color Grading`, `Diffusion`, and `Screen Transform`, plus `Screen Wiggle`, `Layer Bloom`, and `Mask Offset Rim Light` helper prefabs.
- `Volume Profiles/` contains the profiles used by the CreativeFx volume prefabs.

The scenes use `VLiveKitLensFilterTestRig` to create a temporary preview stage, camera, global volume, and layer-bloom custom pass when the scene is opened or played. The generated objects are not saved into the scene file.

Before using the CreativeFx scene, make sure the VLiveKit Lens Filters custom post process types are present in HDRP Global Settings Custom Post Process Orders.

In the rig inspector, disable `Auto Cycle` and choose `Selected Preset` when you want to tune a single filter. `Layer Bloom` uses Unity layer 30 for its bloom source objects by default.
