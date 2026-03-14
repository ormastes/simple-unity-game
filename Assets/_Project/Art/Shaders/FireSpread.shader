Shader "ElementalSiege/FireSpread"
{
    Properties
    {
        _MainTex ("Flame Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _BurnProgress ("Burn Progress", Range(0, 1)) = 0.0
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.3
        _ScrollSpeedX ("Scroll Speed X", Float) = 0.0
        _ScrollSpeedY ("Scroll Speed Y", Float) = -1.5
        _DistortionStrength ("Distortion Strength", Float) = 0.1
        _DistortionSpeed ("Distortion Speed", Float) = 2.0
        _BaseColor ("Base Color (Yellow)", Color) = (1, 0.95, 0.2, 1)
        _MidColor ("Mid Color (Orange)", Color) = (1, 0.5, 0.0, 1)
        _TipColor ("Tip Color (Red)", Color) = (0.9, 0.1, 0.0, 1)
        _GlowIntensity ("Glow Intensity", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FireSpread"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 noiseUV : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float _BurnProgress;
                float _AlphaCutoff;
                float _ScrollSpeedX;
                float _ScrollSpeedY;
                float _DistortionStrength;
                float _DistortionSpeed;
                float4 _BaseColor;
                float4 _MidColor;
                float4 _TipColor;
                float _GlowIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.noiseUV = TRANSFORM_TEX(IN.uv, _NoiseTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                // Sine wave distortion
                float2 distortion;
                distortion.x = sin(IN.uv.y * 10.0 + time * _DistortionSpeed) * _DistortionStrength;
                distortion.y = cos(IN.uv.x * 8.0 + time * _DistortionSpeed * 0.7) * _DistortionStrength * 0.5;

                // Scrolling UVs for flame animation
                float2 scrollUV = IN.uv;
                scrollUV.x += time * _ScrollSpeedX + distortion.x;
                scrollUV.y += time * _ScrollSpeedY + distortion.y;

                // Sample textures
                half4 flameTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV);

                float2 noiseUV = IN.noiseUV + float2(time * 0.3, time * -0.8);
                half noiseVal = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                // Burn progress mask: fire starts from bottom (uv.y = 0)
                float burnMask = saturate(_BurnProgress * 1.5 - IN.uv.y + noiseVal * 0.4);
                burnMask = smoothstep(0.0, 0.6, burnMask);

                // Color gradient: base (yellow) -> mid (orange) -> tip (red)
                // Use UV.y as gradient factor (0 = base, 1 = tips)
                float gradientT = saturate(IN.uv.y + noiseVal * 0.3);
                half4 flameColor;
                if (gradientT < 0.5)
                {
                    flameColor = lerp(_BaseColor, _MidColor, gradientT * 2.0);
                }
                else
                {
                    flameColor = lerp(_MidColor, _TipColor, (gradientT - 0.5) * 2.0);
                }

                // Combine
                half alpha = flameTex.r * noiseVal * burnMask;

                // Alpha cutoff for irregular edges
                clip(alpha - _AlphaCutoff);

                half4 finalColor = flameColor * alpha * _GlowIntensity;
                finalColor.a = alpha;

                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
