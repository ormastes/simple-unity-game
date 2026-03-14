Shader "ElementalSiege/LightningGlow"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _GlowColor ("Glow Color", Color) = (0.3, 0.4, 1.0, 1.0)
        _OuterGlowColor ("Outer Glow Color", Color) = (0.5, 0.2, 0.8, 0.5)
        _GlowIntensity ("Glow Intensity", Float) = 3.0
        _CoreWidth ("Core Width", Range(0.01, 0.5)) = 0.15
        _GlowFalloff ("Glow Falloff", Float) = 2.0
        _FlickerSpeed ("Flicker Speed", Float) = 15.0
        _FlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0.3
        _NoiseScale ("Noise Scale", Float) = 5.0
        _AlphaMultiplier ("Alpha Multiplier", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LightningGlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CoreColor;
                float4 _GlowColor;
                float4 _OuterGlowColor;
                float _GlowIntensity;
                float _CoreWidth;
                float _GlowFalloff;
                float _FlickerSpeed;
                float _FlickerIntensity;
                float _NoiseScale;
                float _AlphaMultiplier;
            CBUFFER_END

            // Hash-based noise for flickering
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float noise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(hash11(i), hash11(i + 1.0), f);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                // Sample main texture (used with LineRenderer)
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Distance from center line (V=0.5 is center for LineRenderer)
                float distFromCenter = abs(IN.uv.y - 0.5) * 2.0;

                // Core brightness: bright white center
                float coreMask = 1.0 - smoothstep(0.0, _CoreWidth, distFromCenter);

                // Glow falloff from center
                float glowMask = pow(max(1.0 - distFromCenter, 0.0), _GlowFalloff);

                // Time-based flickering noise
                float flickerNoise1 = noise1D(time * _FlickerSpeed);
                float flickerNoise2 = noise1D(time * _FlickerSpeed * 1.7 + 42.0);
                float flickerNoise3 = noise1D(IN.uv.x * _NoiseScale + time * _FlickerSpeed * 0.5);

                float flicker = 1.0 - _FlickerIntensity + _FlickerIntensity * flickerNoise1 * flickerNoise2;
                float spatialFlicker = 1.0 - _FlickerIntensity * 0.5 + _FlickerIntensity * 0.5 * flickerNoise3;

                // Color composition: core (white) -> inner glow -> outer glow
                half3 coreContrib = _CoreColor.rgb * coreMask * _GlowIntensity * 1.5;
                half3 innerGlowContrib = _GlowColor.rgb * glowMask * _GlowIntensity;
                half3 outerGlowContrib = _OuterGlowColor.rgb * pow(max(glowMask, 0.0), 0.5) * _GlowIntensity * 0.3;

                half3 finalColor = (coreContrib + innerGlowContrib + outerGlowContrib) * flicker * spatialFlicker;

                // Apply vertex color (LineRenderer gradient support)
                finalColor *= IN.color.rgb;

                // Alpha: strong at center, fading outward
                float alpha = saturate(glowMask * flicker * _AlphaMultiplier) * texColor.a * IN.color.a;

                // Apply texture modulation
                finalColor *= texColor.rgb;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
