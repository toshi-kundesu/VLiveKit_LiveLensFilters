using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using VLiveKit.LiveLensFilters.PostProcessing;

namespace VLiveKit.LiveLensFilters.Editor
{
    [CanEditMultipleObjects, CustomEditor(typeof(diffusion))]
    sealed class DiffusionEditor : CustomPostProcessVolumeComponentEditor
    {
        // HDRP's orders settings type is internal; use its own registration check
        // so our custom controls retain the stock missing-registration warning.
        static readonly Type ordersType = typeof(HDRenderPipeline).Assembly.GetType(
            "UnityEngine.Rendering.HighDefinition.CustomPostProcessOrdersSettings");
        static readonly MethodInfo getOrders = ordersType == null ? null : typeof(GraphicsSettings)
            .GetMethod(nameof(GraphicsSettings.GetRenderPipelineSettings), BindingFlags.Public | BindingFlags.Static)
            ?.MakeGenericMethod(ordersType);
        static readonly MethodInfo isRegistered = ordersType?.GetMethod("IsCustomPostProcessRegistered");
        static readonly object[] registrationArguments = { typeof(diffusion) };
        SerializedDataParameter sourceMode, blendMode, useTint, tint, stretch, threshold;
        SerializedDataParameter blurRadius, intensity, exposure, contrast, saturation, bloomIntensity, bloomColor;

        public override void OnEnable()
        {
            base.OnEnable();
            var fields = new PropertyFetcher<diffusion>(serializedObject);
            sourceMode = Unpack(fields.Find(x => x.sourceMode));
            blendMode = Unpack(fields.Find(x => x.blendMode));
            useTint = Unpack(fields.Find(x => x.useTint));
            tint = Unpack(fields.Find(x => x.tint));
            stretch = Unpack(fields.Find(x => x.stretch));
            threshold = Unpack(fields.Find(x => x.threshold));
            blurRadius = Unpack(fields.Find(x => x.blurRadius));
            intensity = Unpack(fields.Find(x => x.intensity));
            exposure = Unpack(fields.Find(x => x.exposure));
            contrast = Unpack(fields.Find(x => x.contrast));
            saturation = Unpack(fields.Find(x => x.saturation));
            bloomIntensity = Unpack(fields.Find(x => x.bloomIntensity));
            bloomColor = Unpack(fields.Find(x => x.bloomColor));
        }

        public override void OnInspectorGUI()
        {
            // Retain HDRP's registration warning and Graphics Settings link.
            var orders = getOrders?.Invoke(null, null);
            if (orders == null || isRegistered == null || !(bool)isRegistered.Invoke(orders, registrationArguments))
            {
                base.OnInspectorGUI();
                return;
            }

            PropertyField(sourceMode);
            PropertyField(blendMode);
            PropertyField(useTint);
            using (new EditorGUI.DisabledScope(useTint.overrideState.boolValue
                && !useTint.overrideState.hasMultipleDifferentValues
                && !useTint.value.hasMultipleDifferentValues && !useTint.value.boolValue))
                PropertyField(tint, new GUIContent("Tint", "Additional color multiplier. Enabled only when Use Tint is on."));
            PropertyField(stretch);
            using (new EditorGUI.DisabledScope(sourceMode.overrideState.boolValue
                && !sourceMode.overrideState.hasMultipleDifferentValues && !sourceMode.value.hasMultipleDifferentValues
                && sourceMode.value.intValue == (int)diffusion.SourceMode.FullFrame))
                PropertyField(threshold, new GUIContent("Threshold", "Used only by Highlights Only. Full Frame diffuses the whole image."));
            PropertyField(blurRadius);
            PropertyField(intensity, new GUIContent("Intensity", "Final blend saturates at 1. Values above 1 are retained for weighted Volume blending."));
            PropertyField(exposure, new GUIContent("Exposure", "Brightness of the diffusion layer; multiplies Bloom Intensity."));
            PropertyField(contrast, new GUIContent("Contrast", "Contrast of the diffusion layer."));
            PropertyField(saturation, new GUIContent("Saturation", "Saturation of the diffusion layer."));
            PropertyField(bloomIntensity);
            PropertyField(bloomColor);
        }
    }
}
