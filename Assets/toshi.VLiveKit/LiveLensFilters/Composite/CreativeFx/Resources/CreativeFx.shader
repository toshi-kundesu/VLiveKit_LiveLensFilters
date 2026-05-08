Shader "Hidden/VLiveKit/LiveLensFilters/CreativeFx"
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
    float4 _InputTexture_TexelSize;

    int _Mode;
    int _Steps;
    float _Intensity;
    float _Amount;
    float _Threshold;
    float _Radius;
    float _Near;
    float _Far;
    float _TimeValue;
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
        return SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, saturate(uv));
    }

    float Luma(float3 c)
    {
        return dot(c, float3(0.2126, 0.7152, 0.0722));
    }

    float3 ScreenBlend(float3 a, float3 b)
    {
        return 1.0 - (1.0 - saturate(a)) * (1.0 - saturate(b));
    }

    float3 SoftBlur(float2 uv, float radius)
    {
        float2 texel = _InputTexture_TexelSize.xy * radius;
        float3 c = SampleInput(uv).rgb * 0.24;
        c += SampleInput(uv + texel * float2( 1,  0)).rgb * 0.12;
        c += SampleInput(uv + texel * float2(-1,  0)).rgb * 0.12;
        c += SampleInput(uv + texel * float2( 0,  1)).rgb * 0.12;
        c += SampleInput(uv + texel * float2( 0, -1)).rgb * 0.12;
        c += SampleInput(uv + texel * float2( 1,  1)).rgb * 0.07;
        c += SampleInput(uv + texel * float2(-1,  1)).rgb * 0.07;
        c += SampleInput(uv + texel * float2( 1, -1)).rgb * 0.07;
        c += SampleInput(uv + texel * float2(-1, -1)).rgb * 0.07;
        return c;
    }

    float3 WideBlur(float2 uv, float radius)
    {
        float2 texel = _InputTexture_TexelSize.xy * radius;
        float3 c = SampleInput(uv).rgb * 0.18;
        c += SampleInput(uv + texel * float2( 1.5,  0.0)).rgb * 0.09;
        c += SampleInput(uv + texel * float2(-1.5,  0.0)).rgb * 0.09;
        c += SampleInput(uv + texel * float2( 0.0,  1.5)).rgb * 0.09;
        c += SampleInput(uv + texel * float2( 0.0, -1.5)).rgb * 0.09;
        c += SampleInput(uv + texel * float2( 2.8,  1.2)).rgb * 0.07;
        c += SampleInput(uv + texel * float2(-2.8,  1.2)).rgb * 0.07;
        c += SampleInput(uv + texel * float2( 2.8, -1.2)).rgb * 0.07;
        c += SampleInput(uv + texel * float2(-2.8, -1.2)).rgb * 0.07;
        c += SampleInput(uv + texel * float2( 4.5,  0.0)).rgb * 0.055;
        c += SampleInput(uv + texel * float2(-4.5,  0.0)).rgb * 0.055;
        c += SampleInput(uv + texel * float2( 0.0,  4.5)).rgb * 0.055;
        c += SampleInput(uv + texel * float2( 0.0, -4.5)).rgb * 0.055;
        return c;
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

    float3 LightLeak(float2 uv, float3 src)
    {
        float time = _TimeValue * lerp(0.035, 0.42, _Amount);
        float softness = lerp(0.24, 0.74, _Radius);

        float left = pow(saturate(1.0 - smoothstep(0.0, softness, uv.x)), 1.35);
        float right = pow(saturate(smoothstep(1.0 - softness * 0.72, 1.0, uv.x)), 1.8);
        float top = pow(saturate(smoothstep(1.0 - softness * 0.8, 1.0, uv.y)), 1.45);
        float edge = left + right * 0.42 + top * 0.32;

        float sweep = (1.0 - uv.x) * 0.88 + uv.y * 0.46;
        sweep += (ValueNoise(float2(uv.y * 2.0 + time, time * 0.37)) - 0.5) * 0.22;
        float diagonal = smoothstep(0.28, 0.58, sweep) * (1.0 - smoothstep(0.72, 1.18, sweep));

        float2 burnCenter = float2(0.03 + ValueNoise(float2(time, 2.1)) * 0.12,
                                  frac(0.23 + time * 0.18 + ValueNoise(float2(4.2, time)) * 0.4));
        float2 p = (uv - burnCenter) * float2(_ScreenSize.x / _ScreenSize.y, 1.0);
        float blob = 1.0 - smoothstep(0.0, 0.52, length(p));
        blob *= lerp(0.65, 1.35, ValueNoise(uv * float2(3.0, 8.0) + time));

        float grain = ValueNoise(uv * float2(16.0, 5.0) + time * 3.0);
        float leak = saturate((edge * lerp(0.7, 1.35, grain) + diagonal * 0.75 + blob) * _Intensity);
        float hot = pow(saturate(leak), lerp(3.4, 1.15, _Threshold));

        float3 warm = _Color.rgb;
        float3 cool = _Color2.rgb;
        float3 tint = lerp(warm, cool, saturate(uv.y * 0.75 + grain * 0.45));
        float3 wash = tint * leak * lerp(0.9, 2.7, _Threshold);
        float3 burned = ScreenBlend(src, wash);
        burned += warm * hot * 0.55;
        burned = lerp(burned, max(burned, float3(0.94, 0.94, 0.94) + warm * 0.28), hot * 0.55);
        return burned;
    }

    float3 StarFilter(float2 uv, float3 src)
    {
        float2 texel = _InputTexture_TexelSize.xy;
        float3 streak = 0;
        float total = 0;

        UNITY_UNROLL
        for (int i = 1; i <= 8; i++)
        {
            float w = (9.0 - i) / 9.0;
            float d = i * lerp(1.0, 5.0, _Radius);
            float3 hx = SampleInput(uv + texel * float2( d, 0)).rgb + SampleInput(uv - texel * float2( d, 0)).rgb;
            float3 hy = SampleInput(uv + texel * float2( 0, d)).rgb + SampleInput(uv - texel * float2( 0, d)).rgb;
            float3 hd = SampleInput(uv + texel * float2( d, d)).rgb + SampleInput(uv - texel * float2( d, d)).rgb;
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

    float3 AnamorphicFlare(float2 uv, float3 src)
    {
        float2 texel = _InputTexture_TexelSize.xy;
        float3 streak = 0;
        float total = 0;
        UNITY_UNROLL
        for (int i = 1; i <= 12; i++)
        {
            float w = (13.0 - i) / 13.0;
            float d = i * lerp(4.0, 24.0, _Radius);
            float3 a = SampleInput(uv + texel * float2(d, 0)).rgb;
            float3 b = SampleInput(uv - texel * float2(d, 0)).rgb;
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
        float2 stepUV = dir / max(1.0, _Steps);
        float3 rays = 0;
        float weight = 1.0;
        float total = 0;

        UNITY_UNROLL
        for (int i = 0; i < 16; i++)
        {
            float active = step(i + 0.5, (float)_Steps);
            float2 suv = uv + stepUV * i * lerp(0.3, 1.6, _Radius);
            float3 s = SampleInput(suv).rgb;
            float m = smoothstep(_Threshold, _Threshold + 0.6, Luma(s)) * active;
            rays += s * m * weight;
            total += weight * active;
            weight *= lerp(0.62, 0.92, _Amount);
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
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10.0;
            float w = (1.0 - t) * 0.16;
            float2 suv = uv - dir * t * _Amount * _Intensity * 0.32;
            blur += SampleInput(suv).rgb * w;
            total += w;
        }

        float gate = smoothstep(_Near, _Far, length(dir));
        float3 c = lerp(src, blur / max(0.001, total), gate * _Intensity);
        return lerp(c, ScreenBlend(c, _Color.rgb * 0.25), _Threshold * gate * _Intensity);
    }

    float4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        uint2 positionSS = input.texcoord * _ScreenSize.xy;
        float4 src4 = LOAD_TEXTURE2D_X(_InputTexture, positionSS);
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

        return float4(outColor, src4.a);
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            ENDHLSL
        }
    }
    Fallback Off
}
