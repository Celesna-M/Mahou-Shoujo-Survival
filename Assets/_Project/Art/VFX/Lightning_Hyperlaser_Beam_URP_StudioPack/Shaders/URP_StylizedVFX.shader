// Universal Stylized VFX Shader for Unity URP (SRP Batcher Compatible)
Shader "Custom/URP_StylizedVFX"
{
    Properties
    {
        [MainTexture] _NoiseTex ("Procedural Noise / Rune Texture", 2D) = "white" {}
        [HDR] _CoreColor ("Core HDR Color", Color) = (1.0, 0.95, 0.65, 1.0)
        [HDR] _EdgeColor ("Edge HDR Color", Color) = (1.0, 0.25, 0.0, 1.0)
        _ScrollSpeed ("UV Scroll Speed (XY)", Vector) = (1.1, -0.9, 0, 0)
        _DistortionStrength ("Distortion Strength", Range(0.0, 1.0)) = 0.26
        _DissolveThreshold ("Erosion / Dissolve Threshold", Range(0.0, 1.0)) = 0.30
        _Sharpness ("Cel Sharpness", Range(0.05, 0.99)) = 0.86
        _FresnelPower ("Fresnel Rim Power", Range(0.5, 6.0)) = 2.4
        _FresnelIntensity ("Fresnel Rim Glow", Range(0.0, 3.0)) = 1.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "StylizedVFXUnlit"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                half4 color       : COLOR;
                float fogCoord    : TEXCOORD3;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _CoreColor;
                half4 _EdgeColor;
                float4 _ScrollSpeed;
                float _DistortionStrength;
                float _DissolveThreshold;
                float _Sharpness;
                float _FresnelPower;
                float _FresnelIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = posInputs.positionCS;
                OUT.uv.xy = TRANSFORM_TEX(IN.uv.xy, _NoiseTex);
                // Pass ParticleSystem Custom1.xy (z = dissolve override, w = HDR glow multiplier)
                OUT.uv.zw = IN.uv.zw;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv1 = IN.uv.xy + _ScrollSpeed.xy * _Time.y;
                float2 uv2 = IN.uv.xy * 1.6 - _ScrollSpeed.xy * _Time.y * 0.6;
                float dSample = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r;
                float nVal = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1 + (dSample - 0.5) * _DistortionStrength).r;

                float edgeMask = sin(saturate(IN.uv.y) * 3.14159265);
                float combined = nVal * (0.45 + 0.55 * edgeMask);

                float hdrMult = IN.uv.w > 0.001 ? IN.uv.w : 1.0;
                float dynDissolve = saturate(_DissolveThreshold + (1.0 - IN.color.a) * 0.65);
                float feather = lerp(0.26, 0.014, _Sharpness);
                float alphaMask = smoothstep(dynDissolve, dynDissolve + feather, combined);
                clip(alphaMask - 0.02);

                float coreBand = smoothstep(dynDissolve + 0.15, dynDissolve + 0.15 + feather * 1.4, combined);
                float edgeRim = smoothstep(dynDissolve, dynDissolve + feather * 2.2, combined) * (1.0 - coreBand);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(abs(dot(N, V))), _FresnelPower) * _FresnelIntensity;

                half3 finalColor = lerp(_EdgeColor.rgb, _CoreColor.rgb, coreBand) * hdrMult;
                finalColor += _EdgeColor.rgb * hdrMult * (fresnel + edgeRim * 0.85);
                finalColor *= IN.color.rgb;
                finalColor = MixFog(finalColor, IN.fogCoord);

                return half4(finalColor, alphaMask * IN.color.a);
            }
            ENDHLSL
        }
    }
}
