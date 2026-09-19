# VLive Lens Filters Test Scenes

This sample contains test scenes for quickly checking the package effects in HDRP.

- `Scenes/All Filters Test.unity` cycles through the CreativeFx filters and character Custom Pass examples.
- `Scenes/Layer Bloom Test.unity` opens directly on the `LayerBloom` custom pass.
- `Scenes/Layer Light Wrap Test.unity` opens on `Light Wrap (Character)` with Auto Cycle disabled and broad amber/blue background panels.
- `Prefabs/Volumes/` contains one ready-to-drop global volume prefab per filter, including `Genshin Bloom`, `Genshin Color Grading`, `Diffusion`, and `Screen Transform`, plus `Screen Wiggle`, `Layer Bloom`, `Layer Light Wrap`, and `Mask Offset Rim Light` helper prefabs.
- `Volume Profiles/` contains the profiles used by the CreativeFx volume prefabs.

The scenes use `VLiveKitLensFilterTestRig` to create a temporary preview stage, camera, global volume, and character Custom Pass when the scene is opened or played. The generated objects are not saved into the scene file.

Before using the CreativeFx scene, make sure the VLiveKit Lens Filters custom post process types are present in HDRP Global Settings Custom Post Process Orders.

In the rig inspector, disable `Auto Cycle` and choose `Selected Preset` when you want to tune a single filter. `Layer Bloom` assigns Unity GameObject layer 30 to the preview model and all its children (or the fallback sphere). The floor, stage lamps, and material spheres remain outside the bloom target. The rig creates the `Before Post Process` Custom Pass automatically; a layer named `Character` is not required.

For Layer Bloom in another HDRP project, enable Custom Pass support and set Lit Shader Mode to Both on every HDRP Asset used by the project or quality levels. Keep `CustomPasses/LayerBloom/LayerBloom.shader` in Graphics Settings > Always Included Shaders so its runtime shader lookup also works in a player build. The VLiveKit template includes these settings.

`Light Wrap (Character)` uses the `LayerLightWrap` Custom Pass to bring surrounding background colors into the visible edge of the selected character layer. Only this preset displays the temporary amber and blue panels behind the preview model; switching filters restores the dark stage.

To use it elsewhere, add `Prefabs/Volumes/Layer Light Wrap.prefab`, enable HDRP Custom Pass support, and set `Target Layer` to the character's Unity layer. The prefab uses `Before Post Process`, Width `32`, Softness `0.6`, Intensity `0.8`, Background Exposure `0`, Saturation `1`, white Tint, and `Use Camera Depth` enabled. The sample assigns layer 30 to the model and leaves the floor, panels, and material spheres outside the target layer. Tune Width and Intensity while viewing a bright background behind the character.

The original CreativeFx `Light Wrap` Volume Profile and prefab remain available for existing setups; that effect processes the whole camera image. `Layer Bloom` and `Mask Offset Rim Light` also remain available as separate character Custom Pass effects.

Alternatively, enable Use Explicit Renderers and populate Target Renderers to target characters without changing their Unity layers. An empty explicit list affects nothing. LiveToon's character setup uses this mode, sharing one managed pass per scene and preserving later tuning. Its initial intensity is `0.35`; its volume priority is `10`, before the default character bloom and rim passes at `0`. Use Camera Depth is initially disabled for LiveToon's expanded outline silhouette; enable it when foreground scene geometry should occlude the wrap.

Light Wrap samples the visible scene immediately outside the character silhouette; it does not render a separate background plate behind fully hidden objects. The full-resolution mask keeps the effect inside the character, while the background blur runs at half resolution. Debug View can show the target mask, the wrap contribution, or the sampled background.

Mask Source defaults to Material Alpha, preserving alpha-tested cutouts and the original shader's vertex deformation. Use Solid Geometry for an opaque material that does not write alpha (that mode ignores cutouts and shader deformation). For Material Alpha, use Lit Shader Mode Both in the active HDRP Assets. The Light Wrap shader is packaged in Resources and is retained in player builds.
