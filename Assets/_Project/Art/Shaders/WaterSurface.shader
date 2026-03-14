Shader "ElementalSiege/WaterSurface"
{
    Properties
    {
        _MainTex ("Water Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _TintColor ("Water Tint", Color) = (0.1, 0.4, 0.8, 0.7)
        _WaveAmplitude ("Wave Amplitude", Float) = 0.05
        _WaveFrequency ("Wave Frequency", Float) = 3.0
        _WaveSpeed ("Wave Speed", Float) = 1.5
        _WaveDirectionX ("Wave Direction X", Float) = 1.0
        _DepthAlphaScale ("Depth Alpha Scale", Float) = 1.0
        _RefractionStrength ("Refraction Strength", Float) = 0.02
        _FoamColor ("Foam Color", Color) = (0.9, 0.95, 1.0, 1.0)
        _FoamWidth ("Foam Width", Float) = 0.3
        _FoamNoiseScale ("Foam Noise Scale", Float) = 10.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _FresnelPower ("Fresnel Power", Float) = 3.0
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
        Cull Off

        Pass
        {
            Name "WaterSurface"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _TintColor;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _WaveDirectionX;
                float _DepthAlphaScale;
                float _RefractionStrength;
                float4 _FoamColor;
                float _FoamWidth;
                float _FoamNoiseScale;
                float _Smoothness;
                float _FresnelPower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posOS = IN.positionOS.xyz;
                float time = _Time.y;

                // Sine wave vertex displacement along Y
                float wave1 = sin(posOS.x * _WaveFrequency * _WaveDirectionX + time * _WaveSpeed) * _WaveAmplitude;
                float wave2 = sin(posOS.x * _WaveFrequency * 0.7 + posOS.z * _WaveFrequency * 0.5 + time * _WaveSpeed * 1.3) * _WaveAmplitude * 0.5;
                float wave3 = cos(posOS.z * _WaveFrequency * 1.2 + time * _WaveSpeed * 0.8) * _WaveAmplitude * 0.3;

                posOS.y += wave1 + wave2 + wave3;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos = ComputeScreenPos(posInputs.positionCS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // UV animation for water texture
                float2 waterUV1 = IN.uv + float2(time * 0.03, time * 0.02);
                float2 waterUV2 = IN.uv * 1.5 + float2(-time * 0.02, time * 0.04);

                half4 waterTex1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, waterUV1);
                half4 waterTex2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, waterUV2);
                half4 waterTex = (waterTex1 + waterTex2) * 0.5;

                // Refraction: offset screen UVs
                float2 refractionOffset = (waterTex.rg * 2.0 - 1.0) * _RefractionStrength;
                float2 refractedUV = screenUV + refractionOffset;
                refractedUV = clamp(refractedUV, 0.001, 0.999);

                half3 sceneColor = SampleSceneColor(refractedUV);

                // Depth-based alpha (depth difference at water surface)
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEyeDepth = IN.screenPos.w;
                float depthDiff = sceneEyeDepth - surfaceEyeDepth;
                float depthAlpha = saturate(depthDiff * _DepthAlphaScale);

                // Foam at object intersection
                float foamNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv * _FoamNoiseScale + float2(time * 0.1, 0)).r;
                float foamMask = 1.0 - smoothstep(0.0, _FoamWidth, depthDiff + foamNoise * 0.1);

                // Fresnel for reflection approximation
                float3 normalWS = normalize(IN.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, normalize(IN.viewDirWS))), _FresnelPower);

                // Specular
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + normalize(IN.viewDirWS));
                float spec = pow(max(dot(normalWS, halfDir), 0.0), _Smoothness * 128.0);

                // Compose final color
                half3 waterColor = _TintColor.rgb * waterTex.rgb;

                // Blend refracted scene with water color based on depth
                half3 finalColor = lerp(sceneColor, waterColor, depthAlpha * _TintColor.a);

                // Add fresnel reflection tint
                finalColor = lerp(finalColor, waterColor * 1.5, fresnel * 0.3);

                // Add specular
                finalColor += spec * mainLight.color * 0.5;

                // Add foam
                finalColor = lerp(finalColor, _FoamColor.rgb, foamMask * _FoamColor.a);

                float alpha = saturate(_TintColor.a + fresnel * 0.3 + foamMask * 0.5);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
