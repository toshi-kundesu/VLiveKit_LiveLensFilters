using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using VLiveKit.LiveLensFilters.PostProcessing;

[ExecuteAlways]
public sealed class VLiveKitLensFilterTestRig : MonoBehaviour
{
    public LensFilterTestPreset selectedPreset = LensFilterTestPreset.Halation;
    public bool autoCycle = true;
    [Min(1f)] public float cycleSeconds = 3f;
    [Range(0, 31)] public int layerBloomLayer = 30;
    public bool showBloomOnly;

    const string GeneratedRootName = "Generated Preview Rig";
    const int PresetCount = (int)LensFilterTestPreset.LayerBloom + 1;

    Transform generatedRoot;
    Volume volume;
    CustomPassVolume customPassVolume;
    TextMesh label;
    VolumeProfile runtimeProfile;
    LensFilterTestPreset appliedPreset = (LensFilterTestPreset)(-1);

    void OnEnable()
    {
        EnsureRig();
        ApplyPreset(GetActivePreset(), true);
    }

    void OnDisable()
    {
        DestroyRuntimeObject(runtimeProfile);
        runtimeProfile = null;
    }

    void OnValidate()
    {
        cycleSeconds = Mathf.Max(1f, cycleSeconds);
        layerBloomLayer = Mathf.Clamp(layerBloomLayer, 0, 31);
        appliedPreset = (LensFilterTestPreset)(-1);
    }

    void Update()
    {
        EnsureRig();
        ApplyPreset(GetActivePreset(), false);
    }

    LensFilterTestPreset GetActivePreset()
    {
        if (!autoCycle)
            return selectedPreset;

        var index = Mathf.FloorToInt(Time.realtimeSinceStartup / Mathf.Max(1f, cycleSeconds));
        return (LensFilterTestPreset)(index % PresetCount);
    }

    void EnsureRig()
    {
        generatedRoot = transform.Find(GeneratedRootName);
        if (generatedRoot == null)
        {
            var rootObject = new GameObject(GeneratedRootName);
            rootObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(transform, false);
            BuildPreviewObjects();
        }

        if (volume == null)
            volume = GetOrCreateComponent<Volume>("Global Volume");

        if (customPassVolume == null)
            customPassVolume = GetOrCreateComponent<CustomPassVolume>("Layer Bloom Custom Pass");
    }

    T GetOrCreateComponent<T>(string objectName) where T : Component
    {
        var child = generatedRoot.Find(objectName);
        if (child == null)
        {
            var gameObject = new GameObject(objectName);
            gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            child = gameObject.transform;
            child.SetParent(generatedRoot, false);
        }

        var component = child.GetComponent<T>();
        if (component == null)
            component = child.gameObject.AddComponent<T>();

        return component;
    }

    void BuildPreviewObjects()
    {
        var cameraObject = new GameObject("Preview Camera");
        cameraObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        cameraObject.transform.SetParent(generatedRoot, false);
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2.1f, -8f), Quaternion.Euler(12f, 0f, 0f));
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 80f;
        camera.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
        cameraData.volumeLayerMask = 1;
        cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var keyLightObject = new GameObject("Key Light");
        keyLightObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        keyLightObject.transform.SetParent(generatedRoot, false);
        keyLightObject.transform.SetPositionAndRotation(new Vector3(-3.5f, 4.5f, -2.5f), Quaternion.Euler(50f, -30f, 0f));
        var keyLight = keyLightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.color = new Color(1f, 0.93f, 0.82f);
        keyLight.intensity = 2.2f;

        CreatePrimitive("Floor", PrimitiveType.Cube, new Vector3(0f, -0.55f, 1.8f), new Vector3(8f, 0.08f, 7f), CreateMaterial("Floor Matte", new Color(0.09f, 0.1f, 0.12f), Color.black, 0f), 0);
        CreatePrimitive("Backdrop", PrimitiveType.Cube, new Vector3(0f, 1.65f, 5f), new Vector3(8f, 4f, 0.08f), CreateMaterial("Backdrop Matte", new Color(0.05f, 0.06f, 0.08f), Color.black, 0f), 0);
        CreatePrimitive("Warm Emissive Sphere", PrimitiveType.Sphere, new Vector3(-2.2f, 0.4f, 1.5f), Vector3.one * 0.8f, CreateMaterial("Warm Emissive", new Color(1f, 0.38f, 0.12f), new Color(4f, 1.1f, 0.25f), 1f), 0);
        CreatePrimitive("Cool Emissive Sphere", PrimitiveType.Sphere, new Vector3(2.2f, 0.4f, 1.5f), Vector3.one * 0.8f, CreateMaterial("Cool Emissive", new Color(0.1f, 0.55f, 1f), new Color(0.2f, 1.8f, 4f), 1f), 0);
        CreatePrimitive("Layer Bloom Source A", PrimitiveType.Cube, new Vector3(-0.7f, 0.55f, 0.2f), new Vector3(0.7f, 0.7f, 0.7f), CreateMaterial("Layer Bloom Pink", new Color(1f, 0.18f, 0.75f), new Color(5f, 0.25f, 3.5f), 1f), layerBloomLayer);
        CreatePrimitive("Layer Bloom Source B", PrimitiveType.Cube, new Vector3(0.7f, 0.55f, 0.2f), new Vector3(0.7f, 0.7f, 0.7f), CreateMaterial("Layer Bloom Cyan", new Color(0.1f, 0.95f, 1f), new Color(0.2f, 4.5f, 5f), 1f), layerBloomLayer);

        var labelObject = new GameObject("Preset Label");
        labelObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        labelObject.transform.SetParent(generatedRoot, false);
        labelObject.transform.SetPositionAndRotation(new Vector3(-3.5f, 2.8f, 1.2f), Quaternion.Euler(0f, 0f, 0f));
        label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.UpperLeft;
        label.alignment = TextAlignment.Left;
        label.characterSize = 0.16f;
        label.fontSize = 48;
        label.color = new Color(0.92f, 0.94f, 0.98f, 1f);
    }

    void CreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, int layer)
    {
        var gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = objectName;
        gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        gameObject.layer = Mathf.Clamp(layer, 0, 31);
        gameObject.transform.SetParent(generatedRoot, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    static Material CreateMaterial(string name, Color baseColor, Color emission, float emissionIntensity)
    {
        var shader = Shader.Find("HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        SetColor(material, "_BaseColor", baseColor);
        SetColor(material, "_Color", baseColor);

        if (emissionIntensity > 0f)
        {
            material.EnableKeyword("_EMISSION");
            SetColor(material, "_EmissiveColor", emission * emissionIntensity);
            SetColor(material, "_EmissionColor", emission * emissionIntensity);
        }

        return material;
    }

    static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, value);
    }

    void ApplyPreset(LensFilterTestPreset preset, bool force)
    {
        if (!force && preset == appliedPreset && runtimeProfile != null)
        {
            UpdateLabel(preset);
            return;
        }

        appliedPreset = preset;
        DestroyRuntimeObject(runtimeProfile);
        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "Runtime " + GetDisplayName(preset);
        runtimeProfile.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.sharedProfile = runtimeProfile;

        customPassVolume.enabled = preset == LensFilterTestPreset.LayerBloom;
        if (preset == LensFilterTestPreset.LayerBloom)
            ApplyLayerBloom();
        else
            ApplyCreativeFx(preset);

        UpdateLabel(preset);
    }

    void ApplyLayerBloom()
    {
        customPassVolume.isGlobal = true;
        customPassVolume.priority = 0f;
        customPassVolume.fadeRadius = 0f;
        customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        customPassVolume.customPasses.Clear();

        customPassVolume.customPasses.Add(new LayerBloom
        {
            enabled = true,
            targetMode = LayerBloom.BloomTargetMode.Layer,
            targetLayer = 1 << layerBloomLayer,
            useCameraDepth = true,
            threshold = 0.04f,
            softKnee = 0.65f,
            sourceBoost = 2.0f,
            downsample = 2,
            blurIterations = 5,
            blurRadius = 2.2f,
            intensity = 1.3f,
            colorMode = LayerBloom.BloomColorMode.SourceColor,
            compositeMode = LayerBloom.BloomCompositeMode.Screen,
            tint = Color.white,
            normalizeSourceBrightness = true,
            normalizedSourceBrightness = 1f,
            normalizationFloor = 0.03f,
            showBloomOnly = showBloomOnly
        });
    }

    void ApplyCreativeFx(LensFilterTestPreset preset)
    {
        customPassVolume.customPasses.Clear();

        switch (preset)
        {
            case LensFilterTestPreset.AnalogDamage:
                var analogDamage = AddEffect<AnalogDamage>();
                Override(analogDamage.noise, 0.55f);
                Override(analogDamage.scanlines, 0.55f);
                break;
            case LensFilterTestPreset.AnamorphicFlare:
                var anamorphicFlare = AddEffect<AnamorphicFlare>();
                Override(anamorphicFlare.threshold, 0.45f);
                Override(anamorphicFlare.length, 0.85f);
                Override(anamorphicFlare.tint, new Color(0.35f, 0.6f, 1f, 1f));
                break;
            case LensFilterTestPreset.AnimeSpeedLines:
                var animeSpeedLines = AddEffect<AnimeSpeedLines>();
                Override(animeSpeedLines.density, 0.72f);
                Override(animeSpeedLines.innerRadius, 0.2f);
                Override(animeSpeedLines.outerRadius, 0.95f);
                break;
            case LensFilterTestPreset.BleachBypass:
                var bleachBypass = AddEffect<BleachBypass>();
                Override(bleachBypass.contrast, 0.8f);
                break;
            case LensFilterTestPreset.BlockTearGlitch:
                var blockTearGlitch = AddEffect<BlockTearGlitch>();
                Override(blockTearGlitch.probability, 0.35f);
                Override(blockTearGlitch.displacement, 0.75f);
                Override(blockTearGlitch.blockSize, 0.45f);
                Override(blockTearGlitch.quantizeSteps, 9);
                break;
            case LensFilterTestPreset.ChromaticAberrationPlus:
                var chromaticAberrationPlus = AddEffect<ChromaticAberrationPlus>();
                Override(chromaticAberrationPlus.amount, 0.72f);
                Override(chromaticAberrationPlus.edgeBias, 1.1f);
                break;
            case LensFilterTestPreset.CinemaScope:
                var cinemaScope = AddEffect<CinemaScope>();
                Override(cinemaScope.aspect, 0.58f);
                Override(cinemaScope.extraCrop, 0.08f);
                Override(cinemaScope.softness, 0.1f);
                Override(cinemaScope.centerGradeAmount, 0.18f);
                break;
            case LensFilterTestPreset.ColorQuantize:
                var colorQuantize = AddEffect<ColorQuantize>();
                Override(colorQuantize.steps, 5);
                Override(colorQuantize.dither, 0.35f);
                break;
            case LensFilterTestPreset.DepthFogOverlay:
                var depthFogOverlay = AddEffect<DepthFogOverlay>();
                Override(depthFogOverlay.fogColor, new Color(0.45f, 0.66f, 0.82f, 1f));
                Override(depthFogOverlay.near, 2.5f);
                Override(depthFogOverlay.far, 8f);
                break;
            case LensFilterTestPreset.Diffusion:
                var diffusionEffect = AddCustomPostProcess<diffusion>();
                Override(diffusionEffect.sourceMode, diffusion.SourceMode.HighlightsOnly);
                Override(diffusionEffect.blendMode, diffusion.BlendMode.Screen);
                Override(diffusionEffect.useTint, true);
                Override(diffusionEffect.tint, new Color(0.7f, 0.82f, 1f, 1f));
                Override(diffusionEffect.threshold, 0.42f);
                Override(diffusionEffect.blurRadius, 4f);
                Override(diffusionEffect.intensity, 0.72f);
                Override(diffusionEffect.bloomIntensity, 1.25f);
                break;
            case LensFilterTestPreset.DreamBlur:
                var dreamBlur = AddEffect<DreamBlur>();
                Override(dreamBlur.radius, 5.5f);
                Override(dreamBlur.lift, 0.22f);
                break;
            case LensFilterTestPreset.FilmGrain:
                var filmGrain = AddEffect<VLiveKit.LiveLensFilters.PostProcessing.FilmGrain>();
                Override(filmGrain.amount, 0.34f);
                break;
            case LensFilterTestPreset.GenshinBloom:
                var genshinBloom = AddCustomPostProcess<GenshinBloom>();
                Override(genshinBloom.threshold, 0.55f);
                Override(genshinBloom.blurRadius, 3.5f);
                Override(genshinBloom.intensity, 0.75f);
                Override(genshinBloom.bloomIntensity, 1.4f);
                Override(genshinBloom.bloomColor, new Color(0.62f, 0.72f, 1f, 1f));
                break;
            case LensFilterTestPreset.GenshinColorGrading:
                var genshinColorGrading = AddCustomPostProcess<GenshinColorGrading>();
                Override(genshinColorGrading.intensity, 0.65f);
                Override(genshinColorGrading.exposure, 1.08f);
                Override(genshinColorGrading.contrast, 1.16f);
                Override(genshinColorGrading.saturation, 1.18f);
                Override(genshinColorGrading.tint, new Color(0.74f, 0.82f, 1f, 1f));
                break;
            case LensFilterTestPreset.Halation:
                var halation = AddEffect<Halation>();
                Override(halation.threshold, 0.22f);
                Override(halation.radius, 9.5f);
                Override(halation.tint, new Color(1f, 0.32f, 0.16f, 1f));
                break;
            case LensFilterTestPreset.LensDistortion:
                var lensDistortion = AddEffect<LensDistortionFx>();
                Override(lensDistortion.amount, 0.58f);
                Override(lensDistortion.chromatic, 0.45f);
                break;
            case LensFilterTestPreset.LensVignette:
                var lensVignette = AddEffect<LensVignette>();
                Override(lensVignette.roundness, 0.75f);
                Override(lensVignette.softness, 0.42f);
                break;
            case LensFilterTestPreset.LightLeak:
                var lightLeak = AddEffect<LightLeak>();
                Override(lightLeak.drift, 0.45f);
                Override(lightLeak.softness, 0.7f);
                Override(lightLeak.burn, 0.72f);
                break;
            case LensFilterTestPreset.LightRays:
                var lightRays = AddEffect<LightRays>();
                Override(lightRays.threshold, 0.38f);
                Override(lightRays.decay, 0.7f);
                Override(lightRays.length, 0.82f);
                Override(lightRays.samples, 14);
                Override(lightRays.center, new Vector2(0.5f, 0.35f));
                break;
            case LensFilterTestPreset.LightSweep:
                var lightSweep = AddEffect<LightSweep>();
                Override(lightSweep.position, 0.52f);
                Override(lightSweep.angle, 0.38f);
                Override(lightSweep.width, 0.24f);
                Override(lightSweep.threshold, 0.18f);
                break;
            case LensFilterTestPreset.LightWrap:
                var lightWrap = AddEffect<LightWrap>();
                Override(lightWrap.threshold, 0.3f);
                Override(lightWrap.radius, 7.5f);
                break;
            case LensFilterTestPreset.PixelSort:
                var pixelSort = AddEffect<PixelSort>();
                Override(pixelSort.threshold, 0.32f);
                Override(pixelSort.length, 0.72f);
                break;
            case LensFilterTestPreset.Prism:
                var prism = AddEffect<Prism>();
                Override(prism.refraction, 0.68f);
                Override(prism.facets, 0.52f);
                break;
            case LensFilterTestPreset.RGBGlitch:
                var rgbGlitch = AddEffect<RGBGlitch>();
                Override(rgbGlitch.probability, 0.32f);
                Override(rgbGlitch.displacement, 0.65f);
                Override(rgbGlitch.bandDensity, 0.55f);
                break;
            case LensFilterTestPreset.ScanRollGlitch:
                var scanRollGlitch = AddEffect<ScanRollGlitch>();
                Override(scanRollGlitch.speed, 0.62f);
                Override(scanRollGlitch.frequency, 0.5f);
                break;
            case LensFilterTestPreset.ScreenTransform:
                var screenTransform = AddEffect<ScreenTransform>();
                Override(screenTransform.offset, new Vector2(0.035f, -0.02f));
                Override(screenTransform.zoom, 1.06f);
                Override(screenTransform.rotation, 1.2f);
                break;
            case LensFilterTestPreset.ShapedBokehFilter:
                var shapedBokehFilter = AddEffect<ShapedBokehFilter>();
                Override(shapedBokehFilter.threshold, 0.28f);
                Override(shapedBokehFilter.size, 0.55f);
                Override(shapedBokehFilter.bokehIntensity, 1.5f);
                Override(shapedBokehFilter.softness, 0.16f);
                Override(shapedBokehFilter.samples, 9);
                Override(shapedBokehFilter.pattern, ShapedBokehPattern.Forest);
                break;
            case LensFilterTestPreset.StarFilter:
                var starFilter = AddEffect<StarFilter>();
                Override(starFilter.threshold, 0.38f);
                Override(starFilter.length, 0.82f);
                break;
            case LensFilterTestPreset.ThreeStripColor:
                var threeStripColor = AddEffect<ThreeStripColor>();
                Override(threeStripColor.density, 0.6f);
                break;
            case LensFilterTestPreset.VLiveDOF:
                var vliveDof = AddEffect<VLiveDOF>();
                Override(vliveDof.focusDistance, 7.5f);
                Override(vliveDof.focusRange, 1.0f);
                Override(vliveDof.blurRadius, 14f);
                Override(vliveDof.nearBlur, 0.35f);
                Override(vliveDof.farBlur, 1f);
                Override(vliveDof.bokehThreshold, 0.25f);
                Override(vliveDof.bokehIntensity, 1.5f);
                Override(vliveDof.samples, 18);
                break;
            case LensFilterTestPreset.WaterDroplets:
                var waterDroplets = AddEffect<WaterDroplets>();
                Override(waterDroplets.density, 0.72f);
                Override(waterDroplets.size, 0.55f);
                Override(waterDroplets.refraction, 0.75f);
                Override(waterDroplets.highlight, 0.85f);
                Override(waterDroplets.fallSpeed, 0.35f);
                break;
            case LensFilterTestPreset.RainOnLens:
                var rainOnLens = AddEffect<RainOnLens>();
                Override(rainOnLens.rainAmount, 0.78f);
                Override(rainOnLens.dropletSize, 0.48f);
                Override(rainOnLens.refraction, 0.72f);
                Override(rainOnLens.highlight, 0.85f);
                Override(rainOnLens.fallSpeed, 0.4f);
                break;
            case LensFilterTestPreset.ZoomBlur:
                var zoomBlur = AddEffect<ZoomBlur>();
                Override(zoomBlur.amount, 0.55f);
                Override(zoomBlur.innerRadius, 0.06f);
                Override(zoomBlur.outerRadius, 0.92f);
                Override(zoomBlur.glow, 0.24f);
                break;
        }
    }

    T AddEffect<T>() where T : CreativeFxBase
    {
        var effect = runtimeProfile.Add<T>(true);
        effect.active = true;
        Override(effect.intensity, 1f);
        return effect;
    }

    T AddCustomPostProcess<T>() where T : VolumeComponent
    {
        var effect = runtimeProfile.Add<T>(true);
        effect.active = true;
        return effect;
    }

    static void Override<T>(VolumeParameter<T> parameter, T value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    void UpdateLabel(LensFilterTestPreset preset)
    {
        if (label == null)
            return;

        label.text = "VLive Lens Filters\n" + GetDisplayName(preset) + "\n" +
                     (preset == LensFilterTestPreset.LayerBloom ? "Only layer 30 emits bloom" : "CreativeFx custom post process");
    }

    static string GetDisplayName(LensFilterTestPreset preset)
    {
        switch (preset)
        {
            case LensFilterTestPreset.ChromaticAberrationPlus:
                return "Chromatic Aberration Plus";
            case LensFilterTestPreset.LensDistortion:
                return "Lens Distortion";
            case LensFilterTestPreset.VLiveDOF:
                return "VLive DOF";
            case LensFilterTestPreset.GenshinBloom:
                return "Genshin Bloom";
            case LensFilterTestPreset.GenshinColorGrading:
                return "Genshin Color Grading";
            case LensFilterTestPreset.ScreenTransform:
                return "Screen Transform";
            case LensFilterTestPreset.LayerBloom:
                return "Layer Bloom";
            default:
                return Nicify(preset.ToString());
        }
    }

    static string Nicify(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = value[0].ToString();
        for (var i = 1; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsUpper(character) && !char.IsUpper(value[i - 1]))
                result += " ";
            result += character;
        }

        return result;
    }

    static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}

public enum LensFilterTestPreset
{
    AnalogDamage,
    AnamorphicFlare,
    AnimeSpeedLines,
    BleachBypass,
    BlockTearGlitch,
    ChromaticAberrationPlus,
    CinemaScope,
    ColorQuantize,
    DepthFogOverlay,
    Diffusion,
    DreamBlur,
    FilmGrain,
    GenshinBloom,
    GenshinColorGrading,
    Halation,
    LensDistortion,
    LensVignette,
    LightLeak,
    LightRays,
    LightSweep,
    LightWrap,
    PixelSort,
    Prism,
    RGBGlitch,
    ScanRollGlitch,
    ShapedBokehFilter,
    StarFilter,
    ThreeStripColor,
    VLiveDOF,
    RainOnLens,
    WaterDroplets,
    ZoomBlur,
    ScreenTransform,
    LayerBloom
}
