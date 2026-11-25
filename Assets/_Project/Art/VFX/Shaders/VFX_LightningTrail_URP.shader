Shader "MahouShoujo/VFX/LightningTrailURP"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Main Streak / Arc Texture (RGB)", 2D) = "white" {}
        _NoiseTex ("Noise / Dissolve Texture (R)", 2D) = "white" {}

        [Header(HDR Colors)]
        [HDR] _ColorCore ("Core HDR Color (Head)", Color) = (3.2, 3.8, 4.5, 1.0)
        [HDR] _ColorEdge ("Edge HDR Color (Tail)", Color) = (0.12, 0.65, 2.8, 1.0)

        [Header(UV Scrolling and Distortion)]
        _MainTexSpeed ("Main UV Scroll Speed (XY)", Vector) = (-2.6, 0.0, 0.0, 0.0)
        _DistortSpeed ("Distort UV Scroll Speed (XY)", Vector) = (-3.2, 0.6, 0.0, 0.0)
        _DistortStrength ("UV Distortion Strength", Range(0.0, 0.5)) = 0.09

        [Header(Tail Dissolve)]
        _DissolveSpeed ("Dissolve Scroll Speed (XY)", Vector) = (-3.0, 0.25, 0.0, 0.0)
        _DissolveScale ("Procedural Noise Scale", Range(1.0, 80.0)) = 32.0
        _DissolveSoftness ("Dissolve Softness", Range(0.01, 1.0)) = 0.25
        _TailFadePower ("Tail Fade Power", Range(0.2, 4.0)) = 1.35
        _AlphaBoost ("Alpha Multiplier", Range(0.1, 4.0)) = 1.4

        [Header(Blend Mode)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "LightningVFXForward"
            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uvMain      : TEXCOORD0;
                float2 uvRaw       : TEXCOORD1;
                half4  color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                half4  _ColorCore;
                half4  _ColorEdge;
                float4 _MainTexSpeed;
                float4 _DistortSpeed;
                float  _DistortStrength;
                float4 _DissolveSpeed;
                float  _DissolveScale;
                float  _DissolveSoftness;
                float  _TailFadePower;
                float  _AlphaBoost;
            CBUFFER_END

            // Lightweight procedural value noise for URP Shader Graph parity (no extra texture fetch needed if _NoiseTex is white)
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SimpleValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uvMain = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uvRaw = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float t = _Time.y;

                // 1. UV Distortion driven by scrolling noise (Stylized Station technique)
                float2 distortUV = IN.uvRaw * _NoiseTex_ST.xy + _NoiseTex_ST.zw + _DistortSpeed.xy * t;
                float texNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, distortUV).r;
                float procNoise = SimpleValueNoise(IN.uvRaw * _DissolveScale + _DissolveSpeed.xy * t);
                float combinedNoise = saturate(texNoise * 0.65 + procNoise * 0.35);

                float2 scrolledMainUV = IN.uvMain + _MainTexSpeed.xy * t + (combinedNoise - 0.5) * _DistortStrength;
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrolledMainUV);

                // 2. Gradient along Trail UV.x (0 = Head, 1 = Tail)
                float headToTail = saturate(IN.uvRaw.x);
                float tailKeep = pow(saturate(1.0 - headToTail), _TailFadePower);

                // 3. Noise Dissolve towards the tail (Gabriel Aguiar Stylized Trails technique)
                float dissolveMask = smoothstep(headToTail - _DissolveSoftness, headToTail + _DissolveSoftness, combinedNoise + tailKeep * 0.65);

                // 4. Dual-Color HDR Lerp * Main Texture * Vertex Color
                half3 gradientCol = lerp(_ColorCore.rgb, _ColorEdge.rgb, headToTail);
                float intensity = max(mainSample.r, mainSample.a);
                half3 finalRGB = gradientCol * mainSample.rgb * IN.color.rgb;
                half finalAlpha = saturate(intensity * dissolveMask * tailKeep * IN.color.a * _AlphaBoost);

                return half4(finalRGB * finalAlpha, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Particles/Unlit"
}
