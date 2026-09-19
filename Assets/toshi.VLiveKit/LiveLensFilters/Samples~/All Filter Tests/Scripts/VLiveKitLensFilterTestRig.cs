using System.Collections.Generic;
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
    [Tooltip("GameObject layer assigned to the preview model and its children for Layer Bloom and Light Wrap. The stage stays outside this layer.")]
    [Range(0, 31)] public int layerBloomLayer = 30;
    public bool showBloomOnly;
    [Tooltip("Optional model asset. A sphere is used when the model is not installed.")]
    public GameObject previewModel;
    [Tooltip("Additional rotation; the model's imported rotation is preserved.")]
    public Vector3 previewModelEuler = Vector3.zero;
    [Tooltip("Show the sample overlay in Game View.")]
    public bool showLabel = true;
    [Tooltip("Include filter selection and Auto Cycle controls in the overlay.")]
    public bool showControls = true;

    [Header("Preview Camera")]
    public Vector3 previewCameraPosition = new Vector3(1.87f, 1.598f, -3.455f);
    public Vector3 previewCameraEuler = new Vector3(0.7994769f, -23.9163f, 0f);
    [Range(1f, 179f)] public float previewCameraFieldOfView = 34f;

    const string GeneratedRootName = "Generated Preview Rig";
    const HideFlags RuntimeFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
    const int PresetCount = (int)LensFilterTestPreset.LayerBloom + 1;

    Transform generatedRoot;
    Transform lightWrapBackground;
    Volume volume;
    CustomPassVolume customPassVolume;
    VolumeProfile runtimeProfile;
    readonly List<Material> generatedMaterials = new List<Material>();
    LensFilterTestPreset appliedPreset = (LensFilterTestPreset)(-1);
    bool rebuildRequested;
    bool configurationChanged;
    GameObject builtPreviewModel;
    Vector3 builtPreviewModelEuler;
    int builtLayerBloomLayer;
    LensFilterTestPreset activePreset;
    LensFilterTestPreset observedSelectedPreset;
    bool observedAutoCycle;
    float observedCycleSeconds;
    double nextCycleAt;
    bool presetListOpen;
    Vector2 presetListScroll;
    GUIStyle labelStyle;
    GUIStyle headingStyle;
    GUIStyle buttonStyle;
    GUIStyle listStyle;
    GUIStyle countStyle;

    public LensFilterTestPreset ActivePreset => activePreset;

    [ContextMenu("Capture Preview Camera")]
    public void CapturePreviewCamera()
    {
        var camera = generatedRoot != null ? generatedRoot.GetComponentInChildren<Camera>() : null;
        if (camera == null)
            return;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Capture Preview Camera");
#endif
        previewCameraPosition = camera.transform.localPosition;
        previewCameraEuler = camera.transform.localEulerAngles;
        previewCameraFieldOfView = camera.fieldOfView;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    void OnEnable()
    {
        // Editor hot reload can retain only the GUIStyle fields from an older rig.
        labelStyle = headingStyle = buttonStyle = listStyle = countStyle = null;
        CleanupRig();
        EnsureRig();
        activePreset = ClampPreset((int)selectedPreset);
        ResetCycleClock();
        ApplyPreset(GetActivePreset(), true);
    }

    void OnDisable()
    {
        CleanupRig();
    }

    void OnValidate()
    {
        cycleSeconds = Mathf.Max(1f, cycleSeconds);
        layerBloomLayer = Mathf.Clamp(layerBloomLayer, 0, 31);
        // Inspect structural changes in Update; preset edits do not rebuild the stage.
        configurationChanged = true;
        appliedPreset = (LensFilterTestPreset)(-1);
    }

    void Update()
    {
        if (configurationChanged)
        {
            rebuildRequested = builtPreviewModel != previewModel || builtPreviewModelEuler != previewModelEuler || builtLayerBloomLayer != layerBloomLayer;
            configurationChanged = false;
        }
        UpdateCycle();
        EnsureRig();
        ApplyPreset(GetActivePreset(), false);
    }

    void OnGUI()
    {
        if (!showLabel)
            return;

        EnsureGUIStyles();
        var previousMatrix = GUI.matrix;
        var previousColor = GUI.color;
        var scale = Mathf.Clamp(Screen.height / 900f, 0.85f, 1.25f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.color = Color.white;
        var screenWidth = Screen.width / scale;
        var screenHeight = Screen.height / scale;
        const float margin = 16f;

        if (!showControls)
        {
            GUI.Label(new Rect(margin, screenHeight - 38f, screenWidth - margin * 2f, 24f), GetDisplayName(activePreset), labelStyle);
        }
        else
        {
            var panelWidth = Mathf.Min(340f, screenWidth - margin * 2f);
            var panel = new Rect(margin, margin, panelWidth, 120f);
            var dropdownHeight = Mathf.Min(320f, screenHeight - panel.yMax - margin - 6f);
            var dropdown = new Rect(panel.x, panel.yMax + 6f, panel.width, dropdownHeight);
            if (presetListOpen && Event.current.type == EventType.MouseDown && !panel.Contains(Event.current.mousePosition) && !dropdown.Contains(Event.current.mousePosition))
                presetListOpen = false;

            DrawFill(panel, new Color(0.018f, 0.026f, 0.043f, 0.9f));
            DrawFill(new Rect(panel.x, panel.y, 3f, panel.height), new Color(0.36f, 0.68f, 0.95f, 0.95f));
            GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 88f, 22f), "VLiveKit / Lens Filters", headingStyle);
            GUI.Label(new Rect(panel.xMax - 68f, panel.y + 10f, 54f, 22f), $"{(int)activePreset + 1:00} / {PresetCount}", countStyle);

            var rowY = panel.y + 39f;
            if (DrawButton(new Rect(panel.x + 14f, rowY, 32f, 34f), "<"))
                StepPreset(-1);
            if (DrawButton(new Rect(panel.x + 52f, rowY, panel.width - 104f, 34f), GetDisplayName(activePreset) + "  v", presetListOpen))
            {
                presetListOpen = !presetListOpen;
                presetListScroll.y = Mathf.Max(0f, (int)activePreset * 30f - dropdownHeight * 0.4f);
            }
            if (DrawButton(new Rect(panel.xMax - 46f, rowY, 32f, 34f), ">"))
                StepPreset(1);

            if (DrawButton(new Rect(panel.x + 14f, panel.y + 82f, 128f, 26f), autoCycle ? "Auto Cycle  ON" : "Auto Cycle  OFF", autoCycle))
                SetAutoCycle(!autoCycle);
            GUI.Label(new Rect(panel.x + 152f, panel.y + 83f, panel.width - 166f, 24f), autoCycle ? $"Next in {Mathf.CeilToInt((float)System.Math.Max(0d, nextCycleAt - Time.realtimeSinceStartupAsDouble))}s" : "Manual selection", countStyle);
            if (autoCycle)
            {
                var progress = 1f - Mathf.Clamp01((float)(nextCycleAt - Time.realtimeSinceStartupAsDouble) / Mathf.Max(1f, cycleSeconds));
                DrawFill(new Rect(panel.x + 14f, panel.yMax - 4f, (panel.width - 28f) * progress, 2f), new Color(0.36f, 0.68f, 0.95f, 0.9f));
            }

            if (presetListOpen && dropdownHeight > 30f)
            {
                DrawFill(dropdown, new Color(0.018f, 0.026f, 0.043f, 0.97f));
                var viewport = new Rect(dropdown.x + 6f, dropdown.y + 6f, dropdown.width - 12f, dropdown.height - 12f);
                var content = new Rect(0f, 0f, viewport.width - 18f, PresetCount * 30f);
                presetListScroll = GUI.BeginScrollView(viewport, presetListScroll, content, false, true);
                for (var i = 0; i < PresetCount; i++)
                {
                    var row = new Rect(0f, i * 30f, content.width, 29f);
                    if (DrawButton(row, $"{i + 1:00}   {GetDisplayName((LensFilterTestPreset)i)}", i == (int)activePreset, listStyle))
                    {
                        SelectPreset(i);
                        presetListOpen = false;
                    }
                }
                GUI.EndScrollView();
            }
        }

        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    LensFilterTestPreset GetActivePreset()
    {
        return activePreset;
    }

    public void SelectPreset(int index)
    {
        selectedPreset = ClampPreset(index);
        activePreset = selectedPreset;
        autoCycle = false;
        ResetCycleClock();
    }

    public void StepPreset(int direction)
    {
        SelectPreset(((int)activePreset + direction % PresetCount + PresetCount) % PresetCount);
    }

    public void SetAutoCycle(bool enabled)
    {
        selectedPreset = activePreset;
        autoCycle = enabled;
        ResetCycleClock();
    }

    static LensFilterTestPreset ClampPreset(int index)
    {
        return (LensFilterTestPreset)Mathf.Clamp(index, 0, PresetCount - 1);
    }

    void ResetCycleClock()
    {
        observedSelectedPreset = selectedPreset;
        observedAutoCycle = autoCycle;
        observedCycleSeconds = cycleSeconds;
        nextCycleAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(1f, cycleSeconds);
    }

    void UpdateCycle()
    {
        if (selectedPreset != observedSelectedPreset)
        {
            activePreset = ClampPreset((int)selectedPreset);
            ResetCycleClock();
        }
        else if (autoCycle != observedAutoCycle || cycleSeconds != observedCycleSeconds)
        {
            // Inspector toggles also continue from the effect currently on screen.
            selectedPreset = activePreset;
            ResetCycleClock();
        }

        if (autoCycle && Time.realtimeSinceStartupAsDouble >= nextCycleAt)
        {
            activePreset = (LensFilterTestPreset)(((int)activePreset + 1) % PresetCount);
            nextCycleAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(1f, cycleSeconds);
        }
    }

    void EnsureGUIStyles()
    {
        if (labelStyle != null && headingStyle != null && buttonStyle != null && listStyle != null && countStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        labelStyle.normal.textColor = new Color(0.9f, 0.94f, 1f);
        headingStyle = new GUIStyle(labelStyle) { fontSize = 12, fontStyle = FontStyle.Bold };
        headingStyle.normal.textColor = new Color(0.61f, 0.72f, 0.83f);
        buttonStyle = new GUIStyle(labelStyle) { fontSize = 14, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, wordWrap = true };
        listStyle = new GUIStyle(buttonStyle) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(9, 9, 0, 0), wordWrap = false };
        countStyle = new GUIStyle(labelStyle) { fontSize = 12, alignment = TextAnchor.MiddleRight };
        countStyle.normal.textColor = new Color(0.61f, 0.72f, 0.83f);
    }

    bool DrawButton(Rect rect, string text, bool selected = false, GUIStyle style = null)
    {
        var hovered = rect.Contains(Event.current.mousePosition);
        var color = selected ? new Color(0.13f, 0.29f, 0.43f, 0.95f) : new Color(0.12f, 0.16f, 0.21f, hovered ? 1f : 0.8f);
        DrawFill(rect, color);
        return GUI.Button(rect, text, style ?? buttonStyle);
    }

    static void DrawFill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void EnsureRig()
    {
        if (rebuildRequested)
            CleanupRig();

        if (generatedRoot != null)
            return;

        generatedRoot = CreateObject(GeneratedRootName, transform).transform;
        BuildPreviewObjects();
        volume = CreateObject("Global Volume", generatedRoot).AddComponent<Volume>();
        customPassVolume = CreateObject("Character Effects Custom Pass", generatedRoot).AddComponent<CustomPassVolume>();
        customPassVolume.enabled = false;
        builtPreviewModel = previewModel;
        builtPreviewModelEuler = previewModelEuler;
        builtLayerBloomLayer = layerBloomLayer;
        rebuildRequested = false;
    }

    void CleanupRig()
    {
        DestroyProfile();
        if (generatedRoot == null)
            generatedRoot = transform.Find(GeneratedRootName);

        // Only this rig's transient hierarchy is owned here; never destroy model assets or meshes.
        if (generatedRoot != null && (generatedRoot.gameObject.hideFlags & RuntimeFlags) == RuntimeFlags)
        {
            generatedRoot.gameObject.SetActive(false);
            generatedRoot.name = "Retiring Preview Rig";
            DestroyRuntimeObject(generatedRoot.gameObject);
        }

        generatedRoot = null;
        lightWrapBackground = null;
        volume = null;
        customPassVolume = null;
        foreach (var material in generatedMaterials)
            DestroyRuntimeObject(material);
        generatedMaterials.Clear();
        appliedPreset = (LensFilterTestPreset)(-1);
    }

    void DestroyProfile()
    {
        if (volume != null)
            volume.sharedProfile = null;
        if (runtimeProfile == null)
            return;

        foreach (var component in runtimeProfile.components)
            DestroyRuntimeObject(component);
        runtimeProfile.components.Clear();
        DestroyRuntimeObject(runtimeProfile);
        runtimeProfile = null;
    }

    static GameObject CreateObject(string objectName, Transform parent)
    {
        var gameObject = new GameObject(objectName) { hideFlags = RuntimeFlags };
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    void BuildPreviewObjects()
    {
        var cameraObject = CreateObject("Preview Camera", generatedRoot);
        cameraObject.transform.localPosition = previewCameraPosition;
        cameraObject.transform.localRotation = Quaternion.Euler(previewCameraEuler);
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = Mathf.Clamp(previewCameraFieldOfView, 1f, 179f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 250f;
        camera.backgroundColor = Color.black;
        camera.clearFlags = CameraClearFlags.Skybox;
        var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
        cameraData.volumeLayerMask = 1;
        cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        // Local lights leave the stage dark while revealing the model's silhouette.
        CreateAreaLight("Sculpture Key", new Vector3(-1.5f, 3.4f, -3f), new Vector3(-0.6f, 1.25f, 0.65f), new Color(1f, 0.95f, 0.88f), 25000f, new Vector2(1.4f, 2.6f));
        CreateAreaLight("Blue Rim", new Vector3(2.3f, 2.6f, 2.7f), new Vector3(-0.4f, 1f, 0.4f), new Color(0.14f, 0.42f, 1f), 5000f, new Vector2(0.7f, 2.6f));
        CreateAreaLight("Amber Edge", new Vector3(-2.4f, 1.8f, 2f), new Vector3(-0.2f, 1f, 0.4f), new Color(1f, 0.48f, 0.18f), 4000f, new Vector2(0.45f, 2.2f));

        var floor = CreateMaterial("Obsidian Floor", new Color(0.014f, 0.018f, 0.026f), 0.45f, 0.72f);
        var plinth = CreateMaterial("Charcoal Stage", new Color(0.025f, 0.03f, 0.04f), 0.5f, 0.48f);
        var ceramic = CreateMaterial("Graphite Sculpture", new Color(0.3f, 0.3f, 0.3f), 0.1f, 0.4f);
        var chrome = CreateMaterial("Polished Chrome", new Color(0.8f, 0.83f, 0.88f), 1f, 0.92f);
        var red = CreateMaterial("Vermilion", new Color(0.6f, 0.055f, 0.026f), 0f, 0.5f);
        CreatePrimitive("Dark Floor", PrimitiveType.Cube, new Vector3(0f, -0.06f, 0f), new Vector3(80f, 0.1f, 80f), floor);
        CreatePrimitive("Low Circular Stage", PrimitiveType.Cylinder, new Vector3(-0.45f, 0.055f, 0.65f), new Vector3(3.2f, 0.055f, 3.2f), plinth);
        CreatePreviewModel(new Vector3(-0.65f, 0.11f, 0.65f), 2.6f, ceramic);
        CreatePrimitive("Chrome Sphere", PrimitiveType.Sphere, new Vector3(1.45f, 0.62f, 0.7f), Vector3.one * 1.24f, chrome);
        CreatePrimitive("Vermilion Sphere", PrimitiveType.Sphere, new Vector3(0.9f, 0.3f, -0.45f), Vector3.one * 0.6f, red);

        // A small stage light array supplies distinct HDR sources for flare and bloom tests.
        var warm = CreateMaterial("Amber Emitters", Color.black, 0f, 0.4f, new Color(2400f, 630f, 110f));
        var cool = CreateMaterial("Ice Emitters", Color.black, 0f, 0.4f, new Color(400f, 1500f, 3000f));
        var dim = CreateMaterial("Blue Standby Emitters", Color.black, 0f, 0.4f, new Color(5f, 25f, 100f));
        for (var column = 0; column < 9; column++)
        {
            for (var row = 0; row < 4; row++)
            {
                var material = (column + row * 2) % 7 == 0 ? warm : (column + row) % 5 == 0 ? cool : dim;
                CreatePrimitive("Stage Lamp", PrimitiveType.Sphere,
                    new Vector3(-2.8f + column * 0.46f, 0.7f + row * 0.46f, 3.8f), Vector3.one * 0.065f, material);
            }
        }

        // Broad background colors make the selected character's light wrap easy to inspect.
        // This transient set is visible only for the Light Wrap preset.
        lightWrapBackground = CreateObject("Light Wrap Background", generatedRoot).transform;
        lightWrapBackground.gameObject.SetActive(false);
        var wrapWarm = CreateMaterial("Light Wrap Amber Panel", Color.black, 0f, 0f, new Color(720f, 240f, 60f));
        var wrapCool = CreateMaterial("Light Wrap Blue Panel", Color.black, 0f, 0f, new Color(80f, 280f, 900f));
        CreatePrimitive("Amber Background Panel", PrimitiveType.Cube, new Vector3(-3.3f, 1.65f, 3.2f),
            new Vector3(2.35f, 3.4f, 0.08f), wrapWarm, parent: lightWrapBackground);
        CreatePrimitive("Blue Background Panel", PrimitiveType.Cube, new Vector3(-0.9f, 1.65f, 3.2f),
            new Vector3(2.35f, 3.4f, 0.08f), wrapCool, parent: lightWrapBackground);

        var probeObject = CreateObject("Stage Reflections", generatedRoot);
        probeObject.transform.localPosition = new Vector3(0f, 1.3f, 0.65f);
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.resolution = 256;
        var probeData = probeObject.AddComponent<HDAdditionalReflectionData>();
        probeData.settingsRaw.cubeResolution.useOverride = true;
        probeData.settingsRaw.cubeResolution.@override = CubeReflectionResolution.CubeReflectionResolution256;
        probeData.mode = ProbeSettings.Mode.Realtime;
        probeData.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
        probeData.influenceVolume.boxSize = new Vector3(12f, 8f, 12f);
        probeData.influenceVolume.boxBlendDistancePositive = Vector3.one;
        probeData.influenceVolume.boxBlendDistanceNegative = Vector3.one;
    }

    void CreateAreaLight(string objectName, Vector3 position, Vector3 target, Color color, float nits, Vector2 size)
    {
        var lightObject = CreateObject(objectName, generatedRoot);
        lightObject.transform.localPosition = position;
        lightObject.transform.localRotation = Quaternion.LookRotation(target - position);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Rectangle;
        var data = lightObject.AddComponent<HDAdditionalLightData>();
        light.lightUnit = LightUnit.Nits;
        light.intensity = nits;
        light.color = color;
        light.useColorTemperature = false;
        light.range = 12f;
        light.shadows = LightShadows.Soft;
        light.areaSize = size;
        data.UpdateAllLightValues();
    }

    void CreatePreviewModel(Vector3 basePosition, float size, Material material)
    {
        if (previewModel == null)
        {
            CreatePrimitive("Ceramic Sphere (Optional Model Missing)", PrimitiveType.Sphere, basePosition + Vector3.up * 0.72f, Vector3.one * 1.44f, material, layerBloomLayer);
            return;
        }

        var wrapper = CreateObject("Sculpture Test Model", generatedRoot).transform;
        wrapper.gameObject.layer = layerBloomLayer;
        var model = Instantiate(previewModel, wrapper, false);
        // Apply an optional orientation outside the imported hierarchy, preserving FBX axis conversion.
        var importedRotation = model.transform.localRotation;
        model.transform.localRotation = Quaternion.Euler(previewModelEuler) * importedRotation;
        // Layer Bloom targets the sculpture, including renderers below the imported model root.
        foreach (var child in model.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.hideFlags = RuntimeFlags;
            child.gameObject.layer = layerBloomLayer;
        }

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        var bounds = new Bounds();
        var hasBounds = false;
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
            var local = renderer.localBounds;
            var matrix = wrapper.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(point);
            }
        }

        if (!hasBounds || bounds.size.sqrMagnitude < 0.000001f)
        {
            DestroyRuntimeObject(wrapper.gameObject);
            CreatePrimitive("Ceramic Sphere (Model Has No Geometry)", PrimitiveType.Sphere, basePosition + Vector3.up * 0.72f, Vector3.one * 1.44f, material, layerBloomLayer);
            return;
        }

        var scale = size / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        wrapper.localScale = Vector3.one * scale;
        wrapper.localPosition = basePosition - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
    }

    void CreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, int layer = 0, Transform parent = null)
    {
        var gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = objectName;
        gameObject.hideFlags = RuntimeFlags;
        gameObject.layer = Mathf.Clamp(layer, 0, 31);
        gameObject.transform.SetParent(parent != null ? parent : generatedRoot, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        var collider = gameObject.GetComponent<Collider>();
        collider.enabled = false;
        DestroyRuntimeObject(collider);
    }

    Material CreateMaterial(string name, Color baseColor, float metallic, float smoothness, Color emission = default)
    {
        var shader = Shader.Find("HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader) { name = name, hideFlags = RuntimeFlags };
        generatedMaterials.Add(material);
        SetColor(material, "_BaseColor", baseColor);
        SetColor(material, "_Color", baseColor);
        SetFloat(material, "_Metallic", metallic);
        SetFloat(material, "_Smoothness", smoothness);
        SetFloat(material, "_Glossiness", smoothness);
        SetColor(material, "_EmissiveColor", emission);
        SetColor(material, "_EmissionColor", emission);
        if (shader.name == "HDRP/Lit")
            HDMaterial.ValidateMaterial(material);
        else if (emission.maxColorComponent > 0f)
            material.EnableKeyword("_EMISSION");
        return material;
    }

    static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, value);
    }

    static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    void ApplyStudioEnvironment()
    {
        var exposure = runtimeProfile.Add<Exposure>(true);
        Override(exposure.mode, ExposureMode.Fixed);
        Override(exposure.fixedExposure, 9f);
        var environment = runtimeProfile.Add<VisualEnvironment>(true);
        Override(environment.skyType, (int)SkyType.Gradient);
        Override(environment.skyAmbientMode, SkyAmbientMode.Dynamic);
        var sky = runtimeProfile.Add<GradientSky>(true);
        Override(sky.top, new Color(0.012f, 0.016f, 0.026f));
        Override(sky.middle, new Color(0.004f, 0.006f, 0.01f));
        Override(sky.bottom, Color.black);
        Override(sky.skyIntensityMode, SkyIntensityMode.Multiplier);
        Override(sky.multiplier, 1f);
        Override(sky.updateMode, EnvironmentUpdateMode.OnChanged);
        var ambientOcclusion = runtimeProfile.Add<ScreenSpaceAmbientOcclusion>(true);
        Override(ambientOcclusion.intensity, 0.7f);
        Override(ambientOcclusion.radius, 0.3f);
    }

    void ApplyPreset(LensFilterTestPreset preset, bool force)
    {
        if (!force && preset == appliedPreset && runtimeProfile != null)
            return;

        appliedPreset = preset;
        DestroyProfile();
        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "Runtime " + GetDisplayName(preset);
        runtimeProfile.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        ApplyStudioEnvironment();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;
        volume.sharedProfile = runtimeProfile;

        if (lightWrapBackground != null)
            lightWrapBackground.gameObject.SetActive(preset == LensFilterTestPreset.LightWrap);

        // Disabling first lets HDRP release resources before replacing a custom pass.
        customPassVolume.enabled = false;
        customPassVolume.customPasses.Clear();
        if (preset == LensFilterTestPreset.LayerBloom)
        {
            ApplyLayerBloom();
            customPassVolume.enabled = true;
        }
        else if (preset == LensFilterTestPreset.LightWrap)
        {
            ApplyLayerLightWrap();
            customPassVolume.enabled = true;
        }
        else
            ApplyCreativeFx(preset);
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
            threshold = 0.08f,
            softKnee = 0.65f,
            sourceBoost = 1f,
            downsample = 2,
            blurIterations = 5,
            blurRadius = 2.2f,
            intensity = 0.6f,
            colorMode = LayerBloom.BloomColorMode.SourceColor,
            compositeMode = LayerBloom.BloomCompositeMode.Screen,
            tint = Color.white,
            // Keep the lit model's shading instead of lifting its entire silhouette to white.
            normalizeSourceBrightness = false,
            normalizedSourceBrightness = 1f,
            normalizationFloor = 0.03f,
            showBloomOnly = showBloomOnly
        });
    }

    void ApplyLayerLightWrap()
    {
        customPassVolume.isGlobal = true;
        customPassVolume.priority = 0f;
        customPassVolume.fadeRadius = 0f;
        customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        customPassVolume.customPasses.Clear();
        customPassVolume.customPasses.Add(new LayerLightWrap
        {
            enabled = true,
            targetLayer = 1 << layerBloomLayer,
            width = 62.5f,
            softness = 0.6f,
            intensity = 1.32f,
            backgroundExposure = 0f,
            saturation = 1f,
            tint = Color.white,
            useCameraDepth = true
        });
    }

    void ApplyCreativeFx(LensFilterTestPreset preset)
    {
        customPassVolume.customPasses.Clear();

        switch (preset)
        {
            case LensFilterTestPreset.AnalogDamage:
                var analogDamage = AddEffect<AnalogDamage>();
                Override(analogDamage.intensity, 0.34f);
                Override(analogDamage.noise, 0.41f);
                Override(analogDamage.scanlines, 0.25f);
                break;
            case LensFilterTestPreset.AnamorphicFlare:
                var anamorphicFlare = AddEffect<AnamorphicFlare>();
                Override(anamorphicFlare.intensity, 0.12f);
                Override(anamorphicFlare.threshold, 0.18f);
                Override(anamorphicFlare.length, 0.4f);
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
                Override(blockTearGlitch.intensity, 0.35f);
                Override(blockTearGlitch.probability, 0.29f);
                Override(blockTearGlitch.displacement, 0.63f);
                Override(blockTearGlitch.blockSize, 0.68f);
                Override(blockTearGlitch.quantizeSteps, 15);
                break;
            case LensFilterTestPreset.ChromaticAberrationPlus:
                var chromaticAberrationPlus = AddEffect<ChromaticAberrationPlus>();
                Override(chromaticAberrationPlus.intensity, 1f);
                Override(chromaticAberrationPlus.amount, 0.49f);
                Override(chromaticAberrationPlus.edgeBias, 0.22f);
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
                Override(colorQuantize.intensity, 1f);
                Override(colorQuantize.steps, 7);
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
                Override(diffusionEffect.sourceMode, diffusion.SourceMode.FullFrame);
                Override(diffusionEffect.blendMode, diffusion.BlendMode.Screen);
                Override(diffusionEffect.useTint, false);
                Override(diffusionEffect.tint, Color.white);
                Override(diffusionEffect.stretch, 0.75f);
                Override(diffusionEffect.threshold, 0.42f);
                Override(diffusionEffect.blurRadius, 4f);
                Override(diffusionEffect.intensity, 0.72f);
                Override(diffusionEffect.exposure, 1f);
                Override(diffusionEffect.contrast, 1f);
                Override(diffusionEffect.saturation, 1f);
                Override(diffusionEffect.bloomIntensity, 1.25f);
                Override(diffusionEffect.bloomColor, Color.white);
                break;
            case LensFilterTestPreset.DreamBlur:
                var dreamBlur = AddEffect<DreamBlur>();
                Override(dreamBlur.radius, 5.5f);
                Override(dreamBlur.lift, 0.22f);
                break;
            case LensFilterTestPreset.FilmGrain:
                var filmGrain = AddEffect<VLiveKit.LiveLensFilters.PostProcessing.FilmGrain>();
                Override(filmGrain.intensity, 0.24f);
                Override(filmGrain.amount, 0.3f);
                break;
            case LensFilterTestPreset.GenshinBloom:
                var genshinBloom = AddCustomPostProcess<GenshinBloom>();
                Override(genshinBloom.stretch, 0.75f);
                Override(genshinBloom.threshold, 0.19f);
                Override(genshinBloom.blurRadius, 6f);
                Override(genshinBloom.intensity, 2.45f);
                Override(genshinBloom.bloomColor, Color.white);
                Override(genshinBloom.tint, new Color(1f, 0.6f, 0f));
                Override(genshinBloom.bloomIntensity, 1.4f);
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
                Override(lensDistortion.intensity, 1f);
                Override(lensDistortion.amount, 0.49f);
                Override(lensDistortion.chromatic, 0.51f);
                break;
            case LensFilterTestPreset.LensVignette:
                var lensVignette = AddEffect<LensVignette>();
                Override(lensVignette.roundness, 0.75f);
                Override(lensVignette.softness, 0.42f);
                break;
            case LensFilterTestPreset.LightLeak:
                var lightLeak = AddEffect<LightLeak>();
                Override(lightLeak.intensity, 0.03f);
                Override(lightLeak.drift, 0.57f);
                Override(lightLeak.softness, 1f);
                Override(lightLeak.burn, 0.21f);
                break;
            case LensFilterTestPreset.LightRays:
                var lightRays = AddEffect<LightRays>();
                Override(lightRays.intensity, 0.28f);
                Override(lightRays.threshold, 0.19f);
                Override(lightRays.decay, 0.08f);
                Override(lightRays.length, 0.4f);
                Override(lightRays.samples, 12);
                Override(lightRays.center, new Vector2(0.5f, 0.35f));
                break;
            case LensFilterTestPreset.LightSweep:
                var lightSweep = AddEffect<LightSweep>();
                Override(lightSweep.position, 0.52f);
                Override(lightSweep.angle, 0.38f);
                Override(lightSweep.width, 0.24f);
                Override(lightSweep.threshold, 0.18f);
                break;
            case LensFilterTestPreset.PixelSort:
                var pixelSort = AddEffect<PixelSort>();
                Override(pixelSort.intensity, 1f);
                Override(pixelSort.threshold, 0.11f);
                Override(pixelSort.length, 0.57f);
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
                Override(shapedBokehFilter.rotation, -0.5f);
                Override(shapedBokehFilter.threshold, 0.28f);
                Override(shapedBokehFilter.size, 0.55f);
                Override(shapedBokehFilter.bokehIntensity, 0.12f);
                Override(shapedBokehFilter.softness, 0.23f);
                Override(shapedBokehFilter.samples, 9);
                Override(shapedBokehFilter.pattern, ShapedBokehPattern.Star);
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
                Override(vliveDof.focusDistance, 6.4f);
                Override(vliveDof.focusRange, 0.8f);
                Override(vliveDof.blurRadius, 14f);
                Override(vliveDof.nearBlur, 0.35f);
                Override(vliveDof.farBlur, 1f);
                Override(vliveDof.bokehThreshold, 0.9f);
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
                Override(rainOnLens.intensity, 1f);
                Override(rainOnLens.rainAmount, 0.34f);
                Override(rainOnLens.dropletSize, 0.34f);
                Override(rainOnLens.refraction, 1f);
                Override(rainOnLens.highlight, 0.57f);
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
            case LensFilterTestPreset.LightWrap:
                return "Light Wrap (Character)";
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
