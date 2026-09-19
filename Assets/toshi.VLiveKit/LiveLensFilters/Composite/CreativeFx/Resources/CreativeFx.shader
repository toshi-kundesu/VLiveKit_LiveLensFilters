Shader "Hidden/toshi/LensFilters/CreativeFx"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vertex(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_InputTexture);
    TEXTURE2D(_PatternTexture);
    TEXTURE2D_X(_BlurSourceTexture);
    TEXTURE2D_X(_BlurPyramid0);
    TEXTURE2D_X(_BlurPyramid1);
    TEXTURE2D_X(_BlurPyramid2);
    TEXTURE2D_X(_BlurPyramid3);
    TEXTURE2D_X(_BlurPyramid4);
    float4 _BlurSourceGeometry;
    float4 _BlurGeometry0;
    float4 _BlurGeometry1;
    float4 _BlurGeometry2;
    float4 _BlurGeometry3;
    float4 _BlurGeometry4;
    float2 _BlurDirection;

    int _Mode;
    int _Steps;
    int _Pattern;
    float _Intensity;
    float _Amount;
    float _Threshold;
    float _Radius;
    float _Near;
    float _Far;
    float _Softness;
    float _Rotation;
    float _UsePatternTexture;
    float _TimeValue;
    float _Zoom;
    float2 _Offset;
    float2 _Pivot;
    float4 _Color;
    float4 _Color2;

    float Hash(float2 p)
    {
        return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
    }

    float2 Hash2(float2 p)
    {
        return float2(Hash(p), Hash(p + 19.19));
    }

    float ValueNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);

        float a = Hash(i);
        float b = Hash(i + float2(1, 0));
        float c = Hash(i + float2(0, 1));
        float d = Hash(i + float2(1, 1));
        return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
    }

    float4 SampleInput(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler,
            ClampAndScaleUVForBilinearPostProcessTexture(saturate(uv)));
    }

    float Luma(float3 c)
    {
        return dot(c, float3(0.2126, 0.7152, 0.0722));
    }

    float3 ScreenBlend(float3 a, float3 b)
    {
        return 1.0 - (1.0 - saturate(a)) * (1.0 - saturate(b));
    }

    float2 BlurUV(float2 uv, float4 geometry)
    {
        float2 halfTexel = geometry.zw * 0.5;
        return clamp(uv, halfTexel, 1.0 - halfTexel) * geometry.xy;
    }

    float3 SampleBlurLevel(float2 uv, int level)
    {
        if (level == 0)
            return SAMPLE_TEXTURE2D_X(_BlurPyramid0, s_linear_clamp_sampler, BlurUV(uv, _BlurGeometry0)).rgb;
        if (level == 1)
            return SAMPLE_TEXTURE2D_X(_BlurPyramid1, s_linear_clamp_sampler, BlurUV(uv, _BlurGeometry1)).rgb;
        if (level == 2)
            return SAMPLE_TEXTURE2D_X(_BlurPyramid2, s_linear_clamp_sampler, BlurUV(uv, _BlurGeometry2)).rgb;
        if (level == 3)
            return SAMPLE_TEXTURE2D_X(_BlurPyramid3, s_linear_clamp_sampler, BlurUV(uv, _BlurGeometry3)).rgb;
        return SAMPLE_TEXTURE2D_X(_BlurPyramid4, s_linear_clamp_sampler, BlurUV(uv, _BlurGeometry4)).rgb;
    }

    float3 BokehHighlights(float3 color)
    {
        float brightness = max(color.r, max(color.g, color.b));
        float transition = lerp(0.18, 1.35, _Softness);
        return color * smoothstep(_Threshold, _Threshold + transition, brightness);
    }

    // A continuous footprint rather than widely separated copies of the original
    // image. Variance interpolation keeps the authored blur size stable.
    float3 FilteredInput(float2 uv, float sigmaPixels)
    {
        float variance = sigmaPixels * sigmaPixels;
        if (variance < 2.85)
        {
            float3 source = SampleInput(uv).rgb;
            if (_Mode == 27)
                source = BokehHighlights(source);
            return lerp(source, SampleBlurLevel(uv, 0), saturate(variance / 2.85));
        }
        if (variance < 14.5)
            return lerp(SampleBlurLevel(uv, 0), SampleBlurLevel(uv, 1), (variance - 2.85) / 11.65);
        if (variance < 61.1)
            return lerp(SampleBlurLevel(uv, 1), SampleBlurLevel(uv, 2), (variance - 14.5) / 46.6);
        if (variance < 247.5)
            return lerp(SampleBlurLevel(uv, 2), SampleBlurLevel(uv, 3), (variance - 61.1) / 186.4);
        return lerp(SampleBlurLevel(uv, 3), SampleBlurLevel(uv, 4), saturate((variance - 247.5) / 745.6));
    }

    float3 SoftBlur(float2 uv, float radius)
    {
        return FilteredInput(uv, max(0.0, radius) * 0.7211);
    }

    float3 WideBlur(float2 uv, float radius)
    {
        return FilteredInput(uv, max(0.0, radius) * 2.1545);
    }

    float3 Halation(float2 uv, float3 src)
    {
        float radius = max(1.0, _Radius);
        float3 blur = WideBlur(uv, radius);
        float bright = smoothstep(_Threshold, _Threshold + 0.55, max(blur.r, max(blur.g, blur.b)));
        float redEdge = smoothstep(_Threshold * 0.75, _Threshold + 0.35, Luma(blur - src));
        float3 warm = lerp(float3(1.0, 0.23, 0.08), _Color.rgb, 0.35);
        float3 glow = blur * warm * bright * 2.75;
        glow += warm * redEdge * 0.25;
        return max(0, src + glow * _Intensity);
    }

    float3 ChromaticAberration(float2 uv, float3 src)
    {
        float2 center = uv - 0.5;
        float edge = pow(saturate(length(center) * 1.45), max(0.05, _Radius));
        float2 offset = normalize(center + 1e-5) * edge * _Amount * _Intensity * 0.018;
        float3 c;
        c.r = SampleInput(uv + offset).r;
        c.g = SampleInput(uv).g;
        c.b = SampleInput(uv - offset).b;
        return c;
    }

    float3 AnalogDamage(float2 uv, float3 src)
    {
        float scanLine = floor(uv.y * _ScreenSize.y);
        float wobble = (Hash(float2(scanLine, floor(_TimeValue * 24))) - 0.5) * _Intensity * _Amount * 0.035;
        float flutter = sin(uv.y * 80 + _TimeValue * 17) * _Intensity * 0.002;
        float2 duv = uv + float2(wobble + flutter, 0);
        float3 c;
        c.r = SampleInput(duv + float2(0.0025 * _Intensity, 0)).r;
        c.g = SampleInput(duv).g;
        c.b = SampleInput(duv - float2(0.0025 * _Intensity, 0)).b;
        float n = Hash(uv * _ScreenSize.xy + _TimeValue * 60) - 0.5;
        float scan = 1.0 - _Radius * _Intensity * (0.35 + 0.35 * sin(uv.y * _ScreenSize.y * 3.14159));
        return max(0, c * scan + n * _Amount * _Intensity * 0.18);
    }

    float3 Prism(float2 uv, float3 src)
    {
        float2 p = uv - 0.5;
        float a = atan2(p.y, p.x);
        float facets = lerp(18.0, 5.0, _Radius);
        float facet = floor((a + 3.14159) / (6.28318 / facets)) * (6.28318 / facets);
        float2 normal = float2(cos(facet), sin(facet));
        float edge = smoothstep(0.12, 0.72, length(p));
        float2 bend = normal * edge * _Amount * _Intensity * 0.018;
        float3 c;
        c.r = SampleInput(uv + bend * 1.4).r;
        c.g = SampleInput(uv + bend * 0.4).g;
        c.b = SampleInput(uv - bend).b;
        return lerp(src, c, saturate(edge + _Radius * 0.3));
    }

    float LightLeakLatticeHash(int2 cell)
    {
        // Shared corners must produce identical values from either adjacent
        // cell. Integer mixing avoids sin/dot rounding differences after GPU
        // optimization; the final 24-bit conversion is exact in float32.
        uint2 p = asuint(cell);
        uint h = p.x * 0x8da6b343u ^ p.y * 0xd8163841u;
        h ^= h >> 16;
        h *= 0x7feb352du;
        h ^= h >> 15;
        h *= 0x846ca68bu;
        h ^= h >> 16;
        return (h & 0x00ffffffu) * (1.0 / 16777216.0);
    }

    float LightLeakNoise(float2 p)
    {
        int2 cell = (int2)floor(p);
        float2 f = frac(p);
        // Quintic interpolation also makes the slope and curvature agree at
        // grid boundaries, keeping broad low-contrast washes free of grid lines.
        f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
        float a = LightLeakLatticeHash(cell);
        float b = LightLeakLatticeHash(cell + int2(1, 0));
        float c = LightLeakLatticeHash(cell + int2(0, 1));
        float d = LightLeakLatticeHash(cell + int2(1, 1));
        return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
    }

    float3 LightLeak(float2 uv, float3 src)
    {
        float time = _TimeValue * lerp(0.035, 0.42, _Amount);
        float softness = lerp(0.24, 0.74, _Radius);

        // Curved, unbounded falloffs avoid the straight contours made by
        // screen-aligned smoothstep cutoffs, even at maximum softness.
        float2 warp = float2(
            LightLeakNoise(uv * float2(2.3, 1.7) + float2(time * 0.4, time * 0.17)),
            LightLeakNoise(uv * float2(1.8, 2.1) + float2(7.3 - time * 0.21, time * 0.3)));
        float2 leakUV = uv + (warp - 0.5) * lerp(0.10, 0.22, _Radius);
        float2 leftP = (leakUV - float2(-0.06, 0.4 + sin(time * 0.7) * 0.13))
                     / float2(softness * 0.85, 1.0);
        float2 rightP = (leakUV - float2(1.07, 0.75 + sin(time * 0.53 + 1.4) * 0.16))
                      / float2(softness * 0.62, 0.9);
        float2 topP = (leakUV - float2(0.58 + sin(time * 0.43 + 2.1) * 0.16, 1.08))
                    / float2(0.9, softness * 0.7);
        float left = exp2(-dot(leftP, leftP) * 2.5);
        float right = exp2(-dot(rightP, rightP) * 2.5);
        float top = exp2(-dot(topP, topP) * 2.5);
        float edge = left + right * 0.42 + top * 0.32;

        float sweep = (1.0 - leakUV.x) * 0.88 + leakUV.y * 0.46;
        sweep += (LightLeakNoise(leakUV * float2(1.6, 2.0) + time * 0.37) - 0.5) * 0.22;
        float bandDistance = (sweep - 0.68) / 0.36;
        float diagonal = exp2(-bandDistance * bandDistance * 2.0);

        // Keep the burn moving continuously; frac used to teleport it from
        // one edge of the frame to the other at each wrap.
        float2 burnCenter = float2(0.03 + LightLeakNoise(float2(time, 2.1)) * 0.12,
                                  0.5 + sin(time * 0.7 + LightLeakNoise(float2(4.2, time)) * 0.8) * 0.32);
        float2 p = (leakUV - burnCenter) * float2(_ScreenSize.x / _ScreenSize.y, 1.0);
        float blob = exp2(-dot(p, p) * (5.0 / (0.52 * 0.52)));
        blob *= lerp(0.65, 1.35, LightLeakNoise(uv * float2(3.0, 8.0) + time));

        float grain = LightLeakNoise(uv * float2(16.0, 5.0) + time * 3.0);
        float leak = saturate((edge * lerp(0.7, 1.35, grain) + diagonal * 0.75 + blob) * _Intensity);
        float hot = pow(saturate(leak), lerp(3.4, 1.15, _Threshold));

        float3 warm = _Color.rgb;
        float3 cool = _Color2.rgb;
        float3 tint = lerp(warm, cool, saturate(uv.y * 0.75 + grain * 0.45));
        float3 wash = tint * leak * lerp(0.9, 2.7, _Threshold);
        // Add only the screen-blend contribution. Clamping the source itself
        // would flatten HDR highlights even as the leak intensity tends to zero.
        float3 burned = src + saturate(wash) * (1.0 - saturate(src));
        burned += warm * hot * 0.55;
        burned = lerp(burned, max(burned, float3(0.94, 0.94, 0.94) + warm * 0.28), hot * 0.55);
        return burned;
    }

    float3 StarFilter(float2 uv, float3 src)
    {
        float2 texel = _PostProcessScreenSize.zw;
        float3 streak = 0;
        float total = 0;

        UNITY_UNROLL
        for (int i = 1; i <= 8; i++)
        {
            float w = (9.0 - i) / 9.0;
            float spacing = lerp(1.0, 5.0, _Radius);
            float d = i * spacing;
            float footprint = spacing * 0.5;
            float3 hx = FilteredInput(uv + texel * float2( d, 0), footprint) + FilteredInput(uv - texel * float2( d, 0), footprint);
            float3 hy = FilteredInput(uv + texel * float2( 0, d), footprint) + FilteredInput(uv - texel * float2( 0, d), footprint);
            float3 hd = FilteredInput(uv + texel * float2( d, d), footprint) + FilteredInput(uv - texel * float2( d, d), footprint);
            float3 bright = max(0, (hx + hy + hd * 0.6) / 5.2 - _Threshold);
            streak += bright * w;
            total += w;
        }

        return ScreenBlend(src, streak / max(0.001, total) * _Intensity * 2.4);
    }

    float3 DreamBlur(float2 uv, float3 src)
    {
        float3 blur = SoftBlur(uv, max(0.25, _Radius));
        float3 lifted = blur + _Amount * 0.25;
        return lerp(src, ScreenBlend(src, lifted), _Intensity * 0.75);
    }

    float3 DepthFog(float2 uv, uint2 positionSS, float3 src)
    {
        float rawDepth = LoadCameraDepth(positionSS);
        float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
        float fog = saturate((depth - _Near) / max(0.001, _Far - _Near));
        fog *= fog * _Intensity;
        return lerp(src, _Color.rgb, fog);
    }

    float3 PixelSort(float2 uv, float3 src)
    {
        float l = Luma(src);
        float mask = smoothstep(_Threshold, _Threshold + 0.15, l);
        float row = Hash(float2(floor(uv.y * _ScreenSize.y / 3.0), floor(_TimeValue * 12)));
        float span = _Radius * _Intensity * mask * lerp(0.01, 0.12, row);
        float3 c = src;
        UNITY_UNROLL
        for (int i = 1; i <= 6; i++)
        {
            float2 suv = uv + float2(span * i / 6.0, 0);
            float3 s = SampleInput(suv).rgb;
            c = lerp(c, max(c, s), mask * (7.0 - i) / 7.0);
        }
        return c;
    }

    float3 ColorQuantize(float2 uv, float3 src)
    {
        float steps = max(2, _Steps);
        float dither = (Hash(uv * _ScreenSize.xy) - 0.5) * _Amount / steps;
        float3 q = floor(saturate(src) * steps + dither) / (steps - 1.0);
        return lerp(src, q, _Intensity);
    }

    float3 FilmGrain(float2 uv, float3 src)
    {
        float n0 = Hash(uv * _ScreenSize.xy + _TimeValue * 23.7);
        float n1 = Hash(uv * _ScreenSize.xy * 0.47 - _TimeValue * 11.3);
        float grain = (n0 + n1 - 1.0) * _Amount * _Intensity;
        float lift = lerp(1.0, 0.55, saturate(Luma(src)));
        return max(0, src + grain * lift);
    }

    float3 Vignette(float2 uv, float3 src)
    {
        float2 p = (uv - 0.5) * float2(_ScreenSize.x / _ScreenSize.y, 1.0);
        float d = dot(p, p);
        float mask = smoothstep(_Radius, _Radius + max(0.001, _Amount), d);
        float3 tint = lerp(src, src * _Color.rgb, saturate(_Color.a));
        return lerp(src, tint, mask * _Intensity);
    }

    float2 LensDistortUV(float2 uv, float amount)
    {
        float2 p = uv - 0.5;
        float r2 = dot(p, p);
        float k = 1.0 + amount * r2 + amount * 0.45 * r2 * r2;
        return p * k + 0.5;
    }

    float3 LensDistortion(float2 uv, float3 src)
    {
        float amount = (_Amount * 2.0 - 1.0) * _Intensity * 0.42;
        float2 duv = LensDistortUV(uv, amount);
        float edge = smoothstep(0.15, 0.72, length(uv - 0.5));
        float2 ca = normalize(uv - 0.5 + 1e-5) * edge * abs(amount) * _Radius * 0.018;
        float3 c;
        c.r = SampleInput(duv + ca).r;
        c.g = SampleInput(duv).g;
        c.b = SampleInput(duv - ca).b;
        return c;
    }

    float2 ScreenTransformUV(float2 uv)
    {
        float aspect = _ScreenSize.x / max(1.0, _ScreenSize.y);
        float zoom = max(0.01, _Zoom);
        float2 p = uv - _Offset - _Pivot;
        p.x *= aspect;

        float angle = -radians(_Rotation);
        float s = sin(angle);
        float c = cos(angle);
        p = float2(c * p.x - s * p.y, s * p.x + c * p.y);
        p.x /= aspect;

        return p / zoom + _Pivot;
    }

    float3 ScreenTransform(float2 uv, float3 src)
    {
        float3 transformed = SampleInput(ScreenTransformUV(uv)).rgb;
        return lerp(src, transformed, _Intensity);
    }

    float3 AnamorphicFlare(float2 uv, float3 src)
    {
        float2 texel = _PostProcessScreenSize.zw;
        float3 streak = 0;
        float total = 0;
        UNITY_UNROLL
        for (int i = 1; i <= 12; i++)
        {
            float w = (13.0 - i) / 13.0;
            float spacing = lerp(4.0, 24.0, _Radius);
            float d = i * spacing;
            float3 a = FilteredInput(uv + texel * float2(d, 0), spacing * 0.5);
            float3 b = FilteredInput(uv - texel * float2(d, 0), spacing * 0.5);
            float3 br = max(0, (a + b) * 0.5 - _Threshold);
            streak += br * w;
            total += w;
        }
        float3 flare = streak / max(0.001, total) * _Color.rgb * _Intensity * 6.0;
        return src + flare;
    }

    float3 BleachBypass(float2 uv, float3 src)
    {
        float l = Luma(src);
        float3 gray = l.xxx;
        float3 highContrast = saturate((src - 0.5) * lerp(1.0, 2.6, _Amount) + 0.5);
        float3 bypass = lerp(gray, highContrast, 0.55);
        return lerp(src, bypass, _Intensity);
    }

    float3 ThreeStripColor(float2 uv, float3 src)
    {
        float r = Luma(src * float3(1.35, 0.45, 0.25));
        float g = Luma(src * float3(0.35, 1.25, 0.35));
        float b = Luma(src * float3(0.20, 0.50, 1.35));
        float3 c = float3(r, g, b);
        c = lerp(c, c * c * (3.0 - 2.0 * c), _Amount);
        return lerp(src, c, _Intensity);
    }

    float3 LightWrap(float2 uv, float3 src)
    {
        float3 blur = WideBlur(uv, max(1.0, _Radius));
        float wrap = smoothstep(_Threshold, _Threshold + 0.35, Luma(blur));
        float3 c = ScreenBlend(src, blur * _Color.rgb * wrap * 1.8);
        return lerp(src, c, _Intensity);
    }

    float3 AnimeSpeedLines(float2 uv, float3 src)
    {
        float2 p = uv - 0.5;
        float angle = atan2(p.y, p.x);
        float radius = length(p);
        float stripes = pow(saturate(sin(angle * lerp(26.0, 86.0, _Amount) + _TimeValue * 8.0) * 0.5 + 0.5), 18.0);
        float gate = smoothstep(_Near, _Far, radius);
        float3 lineColor = lerp(_Color.rgb, src + _Color.rgb, 0.35);
        return lerp(src, ScreenBlend(src, lineColor * stripes), gate * _Intensity);
    }

    float3 RGBGlitch(float2 uv, float3 src)
    {
        float band = floor(uv.y * lerp(16.0, 96.0, _Radius));
        float active = step(1.0 - _Threshold, Hash(float2(band, floor(_TimeValue * 18.0))));
        float shift = (Hash(float2(band, _TimeValue)) - 0.5) * _Amount * _Intensity * active * 0.08;
        float3 c;
        c.r = SampleInput(uv + float2(shift, 0)).r;
        c.g = SampleInput(uv).g;
        c.b = SampleInput(uv - float2(shift, 0)).b;
        return lerp(src, c, saturate(active + _Intensity * 0.25));
    }

    float3 BlockTear(float2 uv, float3 src)
    {
        float2 cells = float2(lerp(6.0, 40.0, _Radius), lerp(4.0, 28.0, _Radius));
        float2 cell = floor(uv * cells);
        float trigger = step(1.0 - _Threshold, Hash(cell + floor(_TimeValue * 12.0)));
        float offset = (Hash(cell.yx + _TimeValue) - 0.5) * _Amount * _Intensity * trigger * 0.18;
        float2 duv = uv + float2(offset, 0);
        float3 c = SampleInput(duv).rgb;
        c = lerp(c, floor(c * _Steps) / max(1.0, _Steps - 1.0), trigger * 0.65);
        return lerp(src, c, trigger);
    }

    float3 ScanRoll(float2 uv, float3 src)
    {
        float roll = frac(uv.y + _TimeValue * lerp(0.08, 1.2, _Amount));
        float bar = smoothstep(0.0, 0.08, roll) * (1.0 - smoothstep(0.08, 0.22, roll));
        float wave = sin((uv.y * _ScreenSize.y * lerp(0.35, 1.6, _Radius)) + _TimeValue * 28.0);
        float2 duv = uv + float2(wave * bar * _Intensity * 0.015, 0);
        float3 c = SampleInput(duv).rgb * (1.0 + bar * _Intensity * 0.65);
        return lerp(src, c, saturate(bar + _Intensity * 0.15));
    }

    // Reference-style rain layer math based on Heartfelt by Martijn Steinrucken
    // and yumayanagisawa/Unity-Raindrops. The repository has an MIT LICENSE,
    // while its README and shader header identify the Heartfelt source as CC BY-NC-SA 3.0.
    float3 RainN13(float p)
    {
        float3 p3 = frac(float3(p, p, p) * float3(0.1031, 0.11369, 0.13787));
        p3 += dot(p3, p3.yzx + 19.19);
        return frac(float3((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y, (p3.y + p3.z) * p3.x));
    }

    float RainN(float t)
    {
        return frac(sin(t * 12345.564) * 7658.76);
    }

    float RainSaw(float b, float t)
    {
        return smoothstep(0.0, b, t) * smoothstep(1.0, b, t);
    }

    float2 RainDropLayer(float2 uv, float t)
    {
        float2 originalUV = uv;
        uv.y += t * 0.75;

        float2 aspect = float2(6.0, 1.0);
        float2 grid = aspect * 2.0;
        float2 id = floor(uv * grid);
        float colShift = RainN(id.x);
        uv.y += colShift;
        id = floor(uv * grid);

        float3 n = RainN13(id.x * 35.2 + id.y * 2376.1);
        float2 st = frac(uv * grid) - float2(0.5, 0.0);

        float x = n.x - 0.5;
        float y = originalUV.y * 20.0;
        float wiggle = sin(y + sin(y));
        x += wiggle * (0.5 - abs(x)) * (n.z - 0.5);
        x *= 0.7;

        float ti = frac(t + n.z);
        y = (RainSaw(0.85, ti) - 0.5) * 0.9 + 0.5;

        float2 p = float2(x, y);
        float d = length((st - p) * aspect.yx);
        float mainDrop = smoothstep(0.4, 0.0, d);

        float r = sqrt(smoothstep(1.0, y, st.y));
        float cd = abs(st.x - x);
        float trail = smoothstep(0.23 * r, 0.15 * r * r, cd);
        float trailFront = smoothstep(-0.02, 0.02, st.y - y);
        trail *= trailFront * r * r;

        y = originalUV.y;
        float trail2 = smoothstep(0.2 * r, 0.0, cd);
        float droplets = max(0.0, (sin(y * (1.0 - y) * 120.0) - st.y)) * trail2 * trailFront * n.z;
        y = frac(y * 10.0) + (st.y - 0.5);
        float dd = length(st - float2(x, y));
        droplets = smoothstep(0.3, 0.0, dd);

        float mask = mainDrop + droplets * r * trailFront;
        return float2(mask, trail);
    }

    float RainStaticDrops(float2 uv, float t)
    {
        uv *= 40.0;
        float2 id = floor(uv);
        uv = frac(uv) - 0.5;

        float3 n = RainN13(id.x * 107.45 + id.y * 3543.654);
        float2 p = (n.xy - 0.5) * 0.7;
        float d = length(uv - p);
        float fade = RainSaw(0.025, frac(t + n.z));
        return smoothstep(0.3, 0.0, d) * frac(n.z * 10.0) * fade;
    }

    float2 RainDrops(float2 uv, float t, float staticWeight, float layer1Weight, float layer2Weight)
    {
        float still = RainStaticDrops(uv, t) * staticWeight;
        float2 layer1 = RainDropLayer(uv, t) * layer1Weight;
        float2 layer2 = RainDropLayer(uv * 1.85, t) * layer2Weight;
        float coverage = smoothstep(0.3, 1.0, still + layer1.x + layer2.x);
        return float2(coverage, max(layer1.y * staticWeight, layer2.y * layer1Weight));
    }

    float3 RainOnLensReference(float2 uv, float3 src)
    {
        float rainAmount = saturate(_Amount);
        float time = _TimeValue * lerp(0.05, 1.15, _Far);
        float2 rainUV = ((uv * _ScreenSize.xy) - 0.5 * _ScreenSize.xy) / max(1.0, _ScreenSize.y);
        rainUV *= lerp(2.35, 1.05, _Radius);

        float staticDrops = smoothstep(-0.5, 1.0, rainAmount) * 2.0;
        float layer1 = smoothstep(0.25, 0.75, rainAmount);
        float layer2 = smoothstep(0.0, 0.5, rainAmount);

        float2 drops = RainDrops(rainUV, time, staticDrops, layer1, layer2);
        float2 gradientStep = float2(lerp(0.0006, 0.0025, _Threshold), 0.0);
        float dx = RainDrops(rainUV + gradientStep, time, staticDrops, layer1, layer2).x;
        float dy = RainDrops(rainUV + gradientStep.yx, time, staticDrops, layer1, layer2).x;
        float2 normal = float2(dx - drops.x, dy - drops.x);

        float refraction = lerp(0.004, 0.08, _Threshold) * _Intensity;
        float2 refractUV = uv + normal * refraction;

        float maxBlur = lerp(3.0, 6.0, rainAmount);
        float minBlur = lerp(1.0, 3.25, _Radius);
        float focus = lerp(maxBlur - drops.y, minBlur, smoothstep(0.1, 0.2, drops.x));
        float3 sharp = SampleInput(refractUV).rgb;
        float3 blurred = SoftBlur(refractUV, max(0.0, focus));
        float3 color = lerp(blurred, sharp, smoothstep(0.1, 0.2, drops.x));

        float tintAmount = saturate(_Color.a) * saturate(drops.x + rainAmount * 0.2);
        color = lerp(color, color * _Color.rgb, tintAmount);

        // Keep highlight local to droplets. The reference lightning flash changes whole-frame
        // exposure over time, which makes downstream bloom pulse even when the scene is static.
        float2 vignetteUV = uv - 0.5;
        color *= lerp(1.0, saturate(1.0 - dot(vignetteUV, vignetteUV)), saturate(_Near * 0.35));

        float3 shine = _Color.rgb * (drops.x * 0.18 + drops.y * 0.08) * _Near * _Intensity;
        return lerp(src, color + shine, saturate(rainAmount * _Intensity));
    }

    float3 WaterDroplets(float2 uv, float3 src)
    {
        float aspect = _ScreenSize.x / _ScreenSize.y;
        float cellCount = lerp(5.0, 16.0, _Amount);
        float baseRadius = lerp(0.014, 0.046, _Radius);
        float refraction = lerp(0.003, 0.035, _Threshold) * _Intensity;
        float fall = _TimeValue * lerp(0.02, 0.55, _Far);

        float2 gridUV = uv * cellCount;
        float2 baseCell = floor(gridUV);
        float2 normal = 0;
        float coverage = 0;
        float highlight = 0;

        UNITY_UNROLL
        for (int y = -1; y <= 1; y++)
        {
            UNITY_UNROLL
            for (int x = -1; x <= 1; x++)
            {
                float2 cell = baseCell + float2(x, y);
                float2 rnd = Hash2(cell);
                float appear = smoothstep(0.18, 0.9, rnd.x);
                float localY = frac(rnd.y - fall * lerp(0.35, 1.4, rnd.x));
                float2 center = (cell + float2(rnd.x, localY)) / cellCount;
                float radius = baseRadius * lerp(0.55, 1.55, Hash(cell + 7.7));

                float2 p = uv - center;
                float2 ap = float2(p.x * aspect, p.y);
                float d = length(ap);
                float inside = (1.0 - smoothstep(radius * 0.66, radius, d)) * appear;
                float rim = smoothstep(radius * 0.62, radius, d) * (1.0 - smoothstep(radius, radius * 1.18, d)) * appear;

                float trailLength = radius * lerp(2.4, 7.5, rnd.y);
                float down = center.y - uv.y;
                float trail = smoothstep(0.0, radius * 0.3, down) * (1.0 - smoothstep(trailLength, trailLength * 1.25, down));
                trail *= (1.0 - smoothstep(radius * 0.18, radius * 0.92, abs(ap.x))) * appear * rnd.x;

                float2 dir = ap / max(0.0001, d);
                normal += dir * (inside * (1.0 - d / max(0.0001, radius)) + rim * 0.72);
                normal += float2(sign(ap.x) * trail * 0.45, -trail * 0.16);
                coverage = max(coverage, inside + rim * 0.65 + trail * 0.32);

                float sparkle = pow(saturate(dot(normalize(float2(-0.6, 0.8)), -dir)), 18.0);
                highlight += (sparkle * inside + rim * 0.38 + trail * 0.08) * appear;
            }
        }

        float2 refractUV = uv + float2(normal.x / max(1.0, aspect), normal.y) * refraction;
        float3 glass = SampleInput(refractUV).rgb;
        float3 blur = SoftBlur(refractUV, lerp(0.6, 2.4, _Radius));
        glass = lerp(glass, blur, saturate(coverage * 0.28));

        float3 tinted = lerp(glass, glass * _Color.rgb, coverage * saturate(_Color.a));
        float3 shine = _Color.rgb * highlight * _Near * _Intensity * 1.5;
        return lerp(src, tinted + shine, saturate(coverage * _Intensity));
    }

    float3 CinemaScope(float2 uv, float3 src)
    {
        float screenAspect = _ScreenSize.x / _ScreenSize.y;
        float targetAspect = lerp(1.85, 2.76, _Amount);
        float visibleHeight = saturate(screenAspect / max(0.001, targetAspect));
        float barHeight = saturate((1.0 - visibleHeight) * 0.5 + _Radius * 0.18);
        float edge = max(0.0005, _Threshold * 0.04);

        float top = smoothstep(1.0 - barHeight - edge, 1.0 - barHeight, uv.y);
        float bottom = 1.0 - smoothstep(barHeight, barHeight + edge, uv.y);
        float matte = saturate(top + bottom);

        float feather = max(0.0, 1.0 - abs(uv.y - 0.5) / max(0.001, 0.5 - barHeight));
        float3 grade = lerp(src, src * lerp(float3(1.0, 1.0, 1.0), _Color2.rgb, _Color2.a), feather * _Far * 0.2);
        float3 barColor = _Color.rgb;
        return lerp(grade, barColor, matte * _Intensity);
    }

    float3 LightSweep(float2 uv, float3 src)
    {
        float2 p = uv - 0.5;
        float angle = lerp(-1.2, 1.2, _Radius);
        float2 axis = float2(cos(angle), sin(angle));
        float position = lerp(-1.35, 1.35, frac(_TimeValue * lerp(0.02, 0.8, _Far) + _Amount));
        float d = dot(p, axis) - position;
        float band = exp(-d * d / max(0.0004, _Threshold * _Threshold * 0.16));
        float edge = 1.0 - smoothstep(0.0, 0.75, abs(dot(p, float2(-axis.y, axis.x))));
        float bright = smoothstep(_Near, _Near + 0.45, Luma(src));
        float3 sweep = _Color.rgb * band * lerp(0.45, 1.5, edge) * lerp(0.35, 1.0, bright);
        return ScreenBlend(src, sweep * _Intensity);
    }

    float3 LightRays(float2 uv, float3 src)
    {
        float2 center = _Color2.xy;
        float2 dir = center - uv;
        float dist = length(dir);
        float authoredSteps = clamp((float)_Steps, 2.0, 16.0);
        float sampleCount = authoredSteps * 4.0;
        float sampleStride = (authoredSteps - 1.0) / (sampleCount - 1.0);
        float2 stepUV = dir / authoredSteps * sampleStride;
        float decay = pow(lerp(0.62, 0.92, _Amount), sampleStride);
        float3 rays = 0;
        float weight = 1.0;
        float total = 0;

        UNITY_UNROLL
        for (int i = 0; i < 64; i++)
        {
            float active = step(i + 0.5, sampleCount);
            float2 suv = uv + stepUV * i * lerp(0.3, 1.6, _Radius);
            float footprint = length(stepUV * _PostProcessScreenSize.xy) * lerp(0.3, 1.6, _Radius) * 0.5;
            float3 s = FilteredInput(suv, footprint);
            float m = smoothstep(_Threshold, _Threshold + 0.6, Luma(s)) * active;
            rays += s * m * weight;
            total += weight * active;
            weight *= decay;
        }

        float falloff = 1.0 - smoothstep(0.15, 1.0, dist);
        float3 beam = rays / max(0.001, total) * _Color.rgb * falloff * _Intensity * 4.0;
        return ScreenBlend(src, beam);
    }

    float3 ZoomBlur(float2 uv, float3 src)
    {
        float2 center = _Color2.xy;
        float2 dir = uv - center;
        float3 blur = src * 0.18;
        float total = 0.18;

        UNITY_UNROLL
        for (int i = 1; i <= 32; i++)
        {
            float t = i / 32.0;
            // Preserve the original 0.72 accumulated blur weight when increasing
            // the path sample count; the sharp center still contributes 0.18.
            float w = (1.0 - t) * (0.72 / 15.5);
            float2 path = dir * _Amount * _Intensity * 0.32;
            float2 suv = uv - path * t;
            float footprint = length(path * _PostProcessScreenSize.xy) / 64.0;
            blur += FilteredInput(suv, footprint) * w;
            total += w;
        }

        float gate = smoothstep(_Near, _Far, length(dir));
        float3 c = lerp(src, blur / max(0.001, total), gate * _Intensity);
        return lerp(c, ScreenBlend(c, _Color.rgb * 0.25), _Threshold * gate * _Intensity);
    }

    float VLiveDOFDepthCoc(float2 uv)
    {
        uint2 positionSS = (uint2)(saturate(uv) * max(float2(1.0, 1.0), _ScreenSize.xy - 1.0));
        float rawDepth = LoadCameraDepth(positionSS);
        float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
        float focusDistance = max(0.01, _Near);
        float focusRange = max(0.01, _Far);
        float nearCoc = saturate((focusDistance - depth - focusRange) / focusRange) * _Color2.x;
        float farCoc = saturate((depth - focusDistance - focusRange) / focusRange) * _Color2.y;
        return max(nearCoc, farCoc);
    }

    float3 VLiveDOF(float2 uv, float3 src)
    {
        float coc = VLiveDOFDepthCoc(uv);
        float sampleRadius = _Radius * coc;
        if (sampleRadius < 0.01)
            return src;
        float sampleCount = clamp((float)_Steps * 3.0, 12.0, 72.0);
        float footprint = sampleRadius * sqrt(3.14159265 / sampleCount) * 0.7;

        float3 blur = src * 0.18;
        float3 bokeh = 0;
        float total = 0.18;
        float2 texel = _PostProcessScreenSize.zw;

        [loop]
        for (int i = 0; i < 72; i++)
        {
            if (i >= sampleCount)
                break;
            float sample01 = (i + 0.5) / sampleCount;
            float angle = i * 2.39996323 + 0.35;
            float ring = sqrt(sample01);
            float2 disk = float2(cos(angle), sin(angle)) * ring;
            float2 suv = uv + disk * texel * sampleRadius;
            float3 s = FilteredInput(suv, footprint);
            float sampleCoc = VLiveDOFDepthCoc(suv);
            float blurWeight = lerp(0.78, 1.18, saturate(sampleCoc));

            blur += s * blurWeight;
            total += blurWeight;

            // Highlights use the same aperture integral as the image. Dividing
            // only by bright samples makes an arbitrarily faint threshold crossing
            // jump to full brightness, producing posterized patches on surfaces.
            float highlight = smoothstep(_Threshold, _Threshold + 1.0, max(s.r, max(s.g, s.b)));
            float aperture = lerp(0.8, 1.1, smoothstep(0.35, 1.0, ring));
            bokeh += s * highlight * blurWeight * aperture;
        }

        float dofBlend = saturate(coc * _Intensity);
        float3 softened = blur / max(0.001, total);
        float3 dof = lerp(src, softened, dofBlend);
        float3 bokehColor = bokeh / max(0.001, total) * _Color.rgb * _Amount * _Intensity * coc;
        return max(float3(0.0, 0.0, 0.0), dof + bokehColor);
    }

    float2 RotatePattern(float2 p, float angle)
    {
        float s = sin(angle);
        float c = cos(angle);
        return float2(c * p.x - s * p.y, s * p.x + c * p.y);
    }

    float StrokeMask(float2 p, float2 a, float2 b, float width, float softness)
    {
        float2 pa = p - a;
        float2 ba = b - a;
        float h = saturate(dot(pa, ba) / max(0.0001, dot(ba, ba)));
        float d = length(pa - ba * h);
        return 1.0 - smoothstep(width, width + softness, d);
    }

    float TreeGlyph(float2 p, float offset)
    {
        float2 q = p - float2(offset, -0.02);
        float softness = lerp(0.01, 0.11, _Softness);
        float mask = 0.0;

        mask = max(mask, StrokeMask(q, float2(0.0, -0.75), float2(0.0, 0.64), 0.055, softness));
        mask = max(mask, StrokeMask(q, float2(-0.28, 0.12), float2(0.28, 0.12), 0.055, softness));
        mask = max(mask, StrokeMask(q, float2(-0.03, 0.02), float2(-0.32, -0.46), 0.06, softness));
        mask = max(mask, StrokeMask(q, float2(0.03, 0.02), float2(0.32, -0.46), 0.06, softness));
        mask = max(mask, StrokeMask(q, float2(0.0, 0.62), float2(-0.2, 0.34), 0.045, softness));
        mask = max(mask, StrokeMask(q, float2(0.0, 0.62), float2(0.2, 0.34), 0.045, softness));

        float bounds = 1.0 - smoothstep(0.86, 1.04, length(q * float2(0.9, 0.78)));
        return saturate(mask * bounds);
    }

    float ForestPattern(float2 p)
    {
        return max(TreeGlyph(p, -0.24), TreeGlyph(p, 0.24));
    }

    float StarPattern(float2 p)
    {
        float radius = length(p);
        float angle = atan2(p.y, p.x) + 1.5707963;
        float spoke = pow(max(0.0001, saturate(cos(angle * 5.0) * 0.5 + 0.5)), 0.42);
        float edge = lerp(0.36, 0.86, spoke);
        float softness = lerp(0.02, 0.14, _Softness);
        return 1.0 - smoothstep(edge, edge + softness, radius);
    }

    float HeartPattern(float2 p)
    {
        p.y += 0.12;
        p *= 1.22;
        float x = p.x;
        float y = p.y;
        float a = x * x + y * y - 0.38;
        float heart = a * a * a - x * x * y * y * y;
        float softness = lerp(0.015, 0.18, _Softness);
        return 1.0 - smoothstep(0.0, softness, heart);
    }

    float CirclePattern(float2 p)
    {
        float softness = lerp(0.02, 0.18, _Softness);
        return 1.0 - smoothstep(0.72, 0.72 + softness, length(p));
    }

    float TexturePattern(float2 p)
    {
        float2 patternUV = p * 0.5 + 0.5;
        float bounds = step(0.0, patternUV.x) * step(patternUV.x, 1.0) *
                       step(0.0, patternUV.y) * step(patternUV.y, 1.0);
        float4 texel = SAMPLE_TEXTURE2D(_PatternTexture, s_linear_clamp_sampler, saturate(patternUV));
        float alpha = max(texel.a, Luma(texel.rgb));
        float softness = lerp(0.02, 0.36, _Softness);
        return bounds * smoothstep(0.12, 0.12 + softness, alpha);
    }

    float PatternMask(float2 p)
    {
        p = RotatePattern(p, _Rotation);

        if (_UsePatternTexture > 0.5)
            return TexturePattern(p);

        if (_Pattern == 1)
            return StarPattern(p);
        if (_Pattern == 2)
            return HeartPattern(p);
        if (_Pattern == 3)
            return CirclePattern(p);

        return ForestPattern(p);
    }

    float3 ShapedBokeh(float2 uv, float3 src)
    {
        // Integrate the aperture at twice the authored grid density. The source
        // pyramid contains highlights extracted BEFORE filtering, so interpolation
        // fills a continuous shape instead of thresholding each tap into a dot.
        float halfSamples = clamp(floor(((float)_Steps - 1.0) * 0.5) * 2.0, 2.0, 8.0);
        float radiusPixels = lerp(8.0, 96.0, _Radius);
        float2 radiusUV = _PostProcessScreenSize.zw * radiusPixels;
        float footprint = radiusPixels / halfSamples * 0.65;
        float3 bokeh = 0.0;
        float total = 0.0;

        [loop]
        for (int y = -8; y <= 8; y++)
        {
            if (abs((float)y) > halfSamples)
                continue;
            [loop]
            for (int x = -8; x <= 8; x++)
            {
                if (abs((float)x) > halfSamples)
                    continue;
                float2 p = float2(x, y) / halfSamples;
                float mask = PatternMask(p);
                if (mask < 0.001)
                    continue;
                bokeh += FilteredInput(uv - p * radiusUV, footprint) * mask;
                total += mask;
            }
        }

        // Aperture energy must not rise with the quality setting or the number
        // of occupied mask pixels. The gain only controls the artistic addition.
        float3 glow = bokeh / max(0.001, total);
        return max(float3(0.0, 0.0, 0.0), src + glow * _Color.rgb * _Amount * _Intensity * 2.5);
    }

    float4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        uint2 positionSS = (uint2)(saturate(input.texcoord) * max(float2(1.0, 1.0), _ScreenSize.xy - 1.0));
        float4 src4 = SampleInput(input.texcoord);
        float3 src = src4.rgb;
        float3 outColor = src;

        if (_Mode == 0) outColor = Halation(input.texcoord, src);
        else if (_Mode == 1) outColor = ChromaticAberration(input.texcoord, src);
        else if (_Mode == 2) outColor = AnalogDamage(input.texcoord, src);
        else if (_Mode == 3) outColor = Prism(input.texcoord, src);
        else if (_Mode == 4) outColor = LightLeak(input.texcoord, src);
        else if (_Mode == 5) outColor = StarFilter(input.texcoord, src);
        else if (_Mode == 6) outColor = DreamBlur(input.texcoord, src);
        else if (_Mode == 7) outColor = DepthFog(input.texcoord, positionSS, src);
        else if (_Mode == 8) outColor = PixelSort(input.texcoord, src);
        else if (_Mode == 9) outColor = ColorQuantize(input.texcoord, src);
        else if (_Mode == 10) outColor = FilmGrain(input.texcoord, src);
        else if (_Mode == 11) outColor = Vignette(input.texcoord, src);
        else if (_Mode == 12) outColor = LensDistortion(input.texcoord, src);
        else if (_Mode == 13) outColor = AnamorphicFlare(input.texcoord, src);
        else if (_Mode == 14) outColor = BleachBypass(input.texcoord, src);
        else if (_Mode == 15) outColor = ThreeStripColor(input.texcoord, src);
        else if (_Mode == 16) outColor = LightWrap(input.texcoord, src);
        else if (_Mode == 17) outColor = AnimeSpeedLines(input.texcoord, src);
        else if (_Mode == 18) outColor = RGBGlitch(input.texcoord, src);
        else if (_Mode == 19) outColor = BlockTear(input.texcoord, src);
        else if (_Mode == 20) outColor = ScanRoll(input.texcoord, src);
        else if (_Mode == 21) outColor = WaterDroplets(input.texcoord, src);
        else if (_Mode == 22) outColor = CinemaScope(input.texcoord, src);
        else if (_Mode == 23) outColor = LightSweep(input.texcoord, src);
        else if (_Mode == 24) outColor = LightRays(input.texcoord, src);
        else if (_Mode == 25) outColor = ZoomBlur(input.texcoord, src);
        else if (_Mode == 26) outColor = VLiveDOF(input.texcoord, src);
        else if (_Mode == 27) outColor = ShapedBokeh(input.texcoord, src);
        else if (_Mode == 28) outColor = ScreenTransform(input.texcoord, src);
        else if (_Mode == 29) outColor = RainOnLensReference(input.texcoord, src);

        return float4(outColor, src4.a);
    }

    float4 SampleBlurSource(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_BlurSourceTexture, s_linear_clamp_sampler,
            BlurUV(uv, _BlurSourceGeometry));
    }

    float4 FragmentDownsample(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        // Bilinear sampling integrates the 2x2 source pixels at each half-size
        // destination pixel center. Each source level has already been blurred.
        return SampleBlurSource(input.texcoord);
    }

    float4 FragmentGaussian(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        // Nine contiguous Gaussian taps paired into five bilinear lookups.
        float4 color = SampleBlurSource(uv) * 0.22702703;
        color += SampleBlurSource(uv + _BlurDirection * 1.38461538) * 0.31621622;
        color += SampleBlurSource(uv - _BlurDirection * 1.38461538) * 0.31621622;
        color += SampleBlurSource(uv + _BlurDirection * 3.23076923) * 0.07027027;
        color += SampleBlurSource(uv - _BlurDirection * 3.23076923) * 0.07027027;
        return color;
    }

    float4 SampleBokehHighlightSource(float2 uv)
    {
        float4 source = SampleBlurSource(uv);
        return float4(BokehHighlights(source.rgb), source.a);
    }

    float4 FragmentBokehHighlights(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        // Extract at individual source pixels before combining their energy;
        // bilinear-paired extraction can erase near-threshold, one-pixel lights.
        float4 color = SampleBokehHighlightSource(uv) * 0.22702703;
        color += SampleBokehHighlightSource(uv + _BlurDirection) * 0.19459459;
        color += SampleBokehHighlightSource(uv - _BlurDirection) * 0.19459459;
        color += SampleBokehHighlightSource(uv + _BlurDirection * 2.0) * 0.12162162;
        color += SampleBokehHighlightSource(uv - _BlurDirection * 2.0) * 0.12162162;
        color += SampleBokehHighlightSource(uv + _BlurDirection * 3.0) * 0.05405405;
        color += SampleBokehHighlightSource(uv - _BlurDirection * 3.0) * 0.05405405;
        color += SampleBokehHighlightSource(uv + _BlurDirection * 4.0) * 0.01621622;
        color += SampleBokehHighlightSource(uv - _BlurDirection * 4.0) * 0.01621622;
        return color;
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment FragmentDownsample
            ENDHLSL
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment FragmentGaussian
            ENDHLSL
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment FragmentBokehHighlights
            ENDHLSL
        }
    }
    Fallback Off
}
