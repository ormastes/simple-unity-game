Shader "ElementalSiege/FrostOverlay"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _NoiseTex ("Frost Noise", 2D) = "white" {}
        _CrystalTex ("Crystal Pattern", 2D) = "white" {}
        _FreezeProgress ("Freeze Progress", Range(0, 1)) = 0.0
        _IceTint ("Ice Tint", Color) = (0.7, 0.85, 1.0, 1.0)
        _EdgeGlowColor ("Edge Glow Color", Color) = (0.5, 0.8, 1.0, 1.0)
        _EdgeGlowWidth ("Edge Glow Width", Range(0.01, 0.3)) = 0.08
        _SpecularPower ("Specular Power", Float) = 64.0
        _SpecularIntensity ("Specular Intensity", Float) = 1.5
        _CrystalScrollSpeed ("Crystal Scroll Speed", Float) = 0.05
        _NoiseScale ("Noise Scale", Float) = 1.0
        _FrostOpacity ("Frost Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "FrostOverlay"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_CrystalTex);
            SAMPLER(sampler_CrystalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _CrystalTex_ST;
                float _FreezeProgress;
                float4 _IceTint;
                float4 _EdgeGlowColor;
                float _EdgeGlowWidth;
                float _SpecularPower;
                float _SpecularIntensity;
                float _CrystalScrollSpeed;
                float _NoiseScale;
                float _FrostOpacity;
            CBUFFER_END

            // Simple procedural noise fallback
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * valueNoise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                // Sample base texture
                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Procedural frost noise
                float2 noiseCoord = IN.uv * _NoiseScale * 8.0;
                float proceduralNoise = fbm(noiseCoord);

                // Also sample noise texture if available
                half texNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv * _NoiseScale).r;
                float combinedNoise = lerp(proceduralNoise, texNoise, 0.5);

                // Scrolling crystal pattern
                float2 crystalUV = IN.uv * 2.0;
                crystalUV.x += time * _CrystalScrollSpeed;
                crystalUV.y += time * _CrystalScrollSpeed * 0.7;
                half crystalPattern = SAMPLE_TEXTURE2D(_CrystalTex, sampler_CrystalTex, crystalUV).r;

                // Freeze mask based on progress + noise
                float freezeMask = saturate(_FreezeProgress * 1.8 - combinedNoise * 0.8);
                freezeMask = smoothstep(0.0, 1.0, freezeMask);

                // Edge glow at freeze boundary
                float edgeDist = abs(freezeMask - 0.5);
                float edgeGlow = 1.0 - smoothstep(0.0, _EdgeGlowWidth, edgeDist);
                edgeGlow *= step(0.05, _FreezeProgress) * step(_FreezeProgress, 0.95);

                // Specular highlight for icy reflection
                float3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + IN.viewDirWS);
                float spec = pow(max(dot(normalWS, halfDir), 0.0), _SpecularPower);
                half3 specColor = spec * _SpecularIntensity * mainLight.color * freezeMask;

                // Combine frost appearance
                half3 frostColor = _IceTint.rgb * (0.8 + crystalPattern * 0.2);
                half3 edgeColor = _EdgeGlowColor.rgb * edgeGlow * 2.0;

                // Final color: blend base with frost based on freeze mask
                half3 finalColor = lerp(baseTex.rgb, frostColor, freezeMask * _FrostOpacity);
                finalColor += specColor;
                finalColor += edgeColor;

                // Alpha
                float alpha = max(freezeMask * _FrostOpacity, edgeGlow);
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
