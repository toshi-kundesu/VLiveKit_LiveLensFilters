Shader "Hidden/toshi/LensFilters/CustomPass/LayerLightWrap"
{
    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    #pragma multi_compile_instancing

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

    struct FullscreenAttributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct FullscreenVaryings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    struct MeshAttributes
    {
        float3 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct MeshVaryings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    FullscreenVaryings VertFullscreen(FullscreenAttributes input)
    {
        FullscreenVaryings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    MeshVaryings VertMask(MeshAttributes input)
    {
        MeshVaryings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = TransformObjectToHClip(input.positionOS);
        return output;
    }

    TEXTURE2D_X(_CameraColorTexture);
    TEXTURE2D_X(_MaskTexture);
    TEXTURE2D_X(_BlurTexture);
    TEXTURE2D_X(_SourceTexture);
    TEXTURE2D_X(_DirectLightingTexture);
    TEXTURE2D_X(_DirectRimMaskTexture);

    float4 _CameraColorScaleBias;
    float4 _MaskScaleBias;
    float4 _BlurScaleBias;
    float4 _SourceScaleBias;
    float4 _DirectLightingScaleBias;
    float4 _DirectRimMaskScaleBias;
    float4 _DirectRimMaskTexelSize;
    float _DirectLightIntensity;
    float _DirectLightSoftness;
    float _DirectLightGain;
    float4 _DirectLightTint;
    // xy = reciprocal viewport dimensions, zw = viewport dimensions.
    // The mask uses the camera viewport; the blur uses the source viewport.
    float4 _CameraTexelSize;
    float4 _SourceTexelSize;
    float _Width;
    float _Softness;
    float _Intensity;
    float _BackgroundGain;
    float _Saturation;
    float4 _Tint;
    int _BlendMode;
    int _DebugMode;

    float2 ViewportUv(float2 uv, float4 scaleBias, float2 texelSize)
    {
        // Clamp before scaling into the allocation. Hardware texture clamping
        // alone would still read stale pixels beyond a smaller active viewport.
        float2 halfTexel = min(abs(texelSize) * 0.5, 0.5);
        return clamp(uv, halfTexel, 1.0 - halfTexel) * scaleBias.xy + scaleBias.zw;
    }

    float4 SampleCamera(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_CameraColorTexture, s_linear_clamp_sampler,
            ViewportUv(uv, _CameraColorScaleBias, _CameraTexelSize.xy));
    }

    float SampleMask(float2 uv)
    {
        return saturate(SAMPLE_TEXTURE2D_X(_MaskTexture, s_linear_clamp_sampler,
            ViewportUv(uv, _MaskScaleBias, _CameraTexelSize.xy)).a);
    }

    float4 BackgroundSample(float2 uv)
    {
        // Point reads keep camera and mask pixels paired before exclusion.
        // Filtering camera RGB first would mix foreground colors into the wrap.
        float3 camera = SAMPLE_TEXTURE2D_X(_CameraColorTexture, s_point_clamp_sampler,
            ViewportUv(uv, _CameraColorScaleBias, _CameraTexelSize.xy)).rgb;
        float mask = saturate(SAMPLE_TEXTURE2D_X(_MaskTexture, s_point_clamp_sampler,
            ViewportUv(uv, _MaskScaleBias, _CameraTexelSize.xy)).a);
        // Material alpha can stay fractional after alpha clipping, even when
        // the camera pixel is entirely foreground. Exclude every occupied R8
        // mask pixel here; retain its soft alpha only for the final wrap edge.
        float coverage = 1.0 - step(0.5 / 255.0, mask);
        return float4(camera * coverage, coverage);
    }

    float4 SampleSource(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_SourceTexture, s_linear_clamp_sampler,
            ViewportUv(uv, _SourceScaleBias, _SourceTexelSize.xy));
    }

    float4 FragmentMask(MeshVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return 1.0;
    }

    float4 FragmentBackground(FullscreenVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 offset = _CameraTexelSize.xy * 0.5;
        float4 background = BackgroundSample(input.texcoord + float2(-offset.x, -offset.y));
        background += BackgroundSample(input.texcoord + float2(offset.x, -offset.y));
        background += BackgroundSample(input.texcoord + float2(-offset.x, offset.y));
        background += BackgroundSample(input.texcoord + offset);
        return background * 0.25;
    }

    float SilhouetteBackgroundCoverage(float2 uv)
    {
        float mask = saturate(SAMPLE_TEXTURE2D_X(_MaskTexture, s_point_clamp_sampler,
            ViewportUv(uv, _MaskScaleBias, _CameraTexelSize.xy)).a);
        return 1.0 - step(0.5 / 255.0, mask);
    }

    float4 FragmentRimMask(FullscreenVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 offset = _CameraTexelSize.xy * 0.5;
        float coverage = SilhouetteBackgroundCoverage(input.texcoord + float2(-offset.x, -offset.y));
        coverage += SilhouetteBackgroundCoverage(input.texcoord + float2(offset.x, -offset.y));
        coverage += SilhouetteBackgroundCoverage(input.texcoord + float2(-offset.x, offset.y));
        coverage += SilhouetteBackgroundCoverage(input.texcoord + offset);
        return float4(0.0, 0.0, 0.0, coverage * 0.25);
    }

    float4 GaussianBlur(float2 uv, float2 direction)
    {
        float2 stepUv = direction * _SourceTexelSize.xy * (max(0.0, _Width) / 8.0);
        float4 result = 0.0;
        float total = 0.0;
        // Seventeen taps cover +/- Width; sigma = Width / 3.
        // RGB and coverage use the identical kernel so normalized background
        // reconstruction cannot pick up the target's own colors.
        UNITY_UNROLL
        for (int i = -8; i <= 8; ++i)
        {
            float distance = (float)i / 8.0;
            float weight = exp(-4.5 * distance * distance);
            result += SampleSource(uv + stepUv * i) * weight;
            total += weight;
        }
        return result / total;
    }

    float4 FragmentHorizontalBlur(FullscreenVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return GaussianBlur(input.texcoord, float2(1.0, 0.0));
    }

    float4 FragmentVerticalBlur(FullscreenVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return GaussianBlur(input.texcoord, float2(0.0, 1.0));
    }

    float3 CompositeWrap(float3 source, float3 background, float edge)
    {
        float amount = max(0.0, _Intensity) * edge;
        if (_BlendMode == 1)
            return source + background * amount;

        float weight = saturate(amount);
        if (_BlendMode == 2)
            return lerp(source, max(source, background), weight);

        // Screen adds only its contribution, retaining HDR source highlights.
        float3 boundedBackground = background / (1.0 + background);
        return source + (1.0 - saturate(source)) * boundedBackground * weight;
    }

    float4 FragmentComposite(FullscreenVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float4 camera = SampleCamera(input.texcoord);
        float mask = SampleMask(input.texcoord);

        if (_DebugMode == 1)
            return float4(mask.xxx, camera.a);

        // Return the untouched sample outside the target, avoiding even small
        // arithmetic changes to pixels that must not receive the effect.
        if (_DebugMode == 0 && (mask <= 0.0 || (_Intensity <= 0.0 && _DirectLightIntensity <= 0.0)))
            return camera;

        float3 wrapped = camera.rgb;
        if (_Intensity > 0.0 || _DebugMode == 2 || _DebugMode == 3)
        {
            float4 blurred = SAMPLE_TEXTURE2D_X(_BlurTexture, s_linear_clamp_sampler,
                ViewportUv(input.texcoord, _BlurScaleBias, _SourceTexelSize.xy));
            float3 background = max(0.0, blurred.rgb / max(blurred.a, 1.0e-5));
            background *= max(0.0, _BackgroundGain);
            float luminance = dot(background, float3(0.2126, 0.7152, 0.0722));
            background = max(0.0, lerp(luminance.xxx, background, max(0.0, _Saturation)));
            background *= max(0.0, _Tint.rgb);
            if (_DebugMode == 3)
                return float4(background, camera.a);
            float edge = mask * saturate(blurred.a * 2.0);
            edge = pow(edge, lerp(4.0, 1.0, saturate(_Softness)));
            wrapped = CompositeWrap(camera.rgb, background, edge);
        }

        float3 directRim = 0.0;
        if (_DirectLightIntensity > 0.0 || _DebugMode == 4 || _DebugMode == 5)
        {
            float3 lighting = max(0.0, SAMPLE_TEXTURE2D_X(_DirectLightingTexture, s_linear_clamp_sampler,
                ViewportUv(input.texcoord, _DirectLightingScaleBias, _CameraTexelSize.xy)).rgb);
            lighting *= max(0.0, _DirectLightGain) * max(0.0, _DirectLightTint.rgb);
            if (_DebugMode == 5)
                return float4(lighting * mask, camera.a);
            float coverage = SAMPLE_TEXTURE2D_X(_DirectRimMaskTexture, s_linear_clamp_sampler,
                ViewportUv(input.texcoord, _DirectRimMaskScaleBias, _DirectRimMaskTexelSize.xy)).a;
            float directEdge = mask * saturate(coverage * 2.0);
            directEdge = pow(directEdge, lerp(4.0, 1.0, saturate(_DirectLightSoftness)));
            directRim = lighting * max(0.0, _DirectLightIntensity) * directEdge;
        }

        if (_DebugMode == 4)
            return float4(directRim, camera.a);

        if (_DebugMode == 2)
            return float4(max(0.0, wrapped - camera.rgb), camera.a);

        return float4(wrapped + directRim, camera.a);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Mask"
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragmentMask
            ENDHLSL
        }

        Pass
        {
            Name "Background"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertFullscreen
            #pragma fragment FragmentBackground
            ENDHLSL
        }

        Pass
        {
            Name "Blur Horizontal"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertFullscreen
            #pragma fragment FragmentHorizontalBlur
            ENDHLSL
        }

        Pass
        {
            Name "Blur Vertical"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertFullscreen
            #pragma fragment FragmentVerticalBlur
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertFullscreen
            #pragma fragment FragmentComposite
            ENDHLSL
        }

        Pass
        {
            Name "RimMask"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertFullscreen
            #pragma fragment FragmentRimMask
            ENDHLSL
        }
    }

    Fallback Off
}
