Shader "ElementalSiege/CrystalReflection"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _NoiseTex ("Sparkle Noise", 2D) = "white" {}
        _CrystalColor ("Crystal Color", Color) = (0.85, 0.9, 1.0, 0.6)
        _SpecularPower ("Specular Power", Float) = 128.0
        _SpecularIntensity ("Specular Intensity", Float) = 2.0
        _IridescenceStrength ("Iridescence Strength", Range(0, 1)) = 0.6
        _IridescenceScale ("Iridescence Scale", Float) = 1.0
        _RefractionStrength ("Refraction Strength", Float) = 0.05
        _Transparency ("Transparency", Range(0, 1)) = 0.5
        _SparkleScale ("Sparkle Scale", Float) = 30.0
        _SparkleSpeed ("Sparkle Speed", Float) = 3.0
        _SparkleThreshold ("Sparkle Threshold", Range(0.5, 1.0)) = 0.85
        _SparkleIntensity ("Sparkle Intensity", Float) = 3.0
        _FresnelPower ("Fresnel Power", Float) = 4.0
        _EnvironmentReflection ("Environment Reflection", Range(0, 1)) = 0.3
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
            Name "CrystalReflection"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _CrystalColor;
                float _SpecularPower;
                float _SpecularIntensity;
                float _IridescenceStrength;
                float _IridescenceScale;
                float _RefractionStrength;
                float _Transparency;
                float _SparkleScale;
                float _SparkleSpeed;
                float _SparkleThreshold;
                float _SparkleIntensity;
                float _FresnelPower;
                float _EnvironmentReflection;
            CBUFFER_END

            // Rainbow iridescence from view angle
            half3 iridescence(float cosAngle, float scale)
            {
                // Attempt thin-film interference colors
                float t = cosAngle * scale;
                half3 color;
                color.r = 0.5 + 0.5 * cos(TWO_PI * (t + 0.0));
                color.g = 0.5 + 0.5 * cos(TWO_PI * (t + 0.33));
                color.b = 0.5 + 0.5 * cos(TWO_PI * (t + 0.67));
                return color;
            }

            // Hash for sparkle noise
            float hash31(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
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
                OUT.screenPos = ComputeScreenPos(posInputs.positionCS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDir = normalize(IN.viewDirWS);

                // Base texture
                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Fresnel
                float NdotV = saturate(dot(normalWS, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Iridescence based on view angle
                half3 iridescentColor = iridescence(NdotV, _IridescenceScale);

                // Refraction
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float2 refractionOffset = normalWS.xy * _RefractionStrength;
                float2 refractedUV = clamp(screenUV + refractionOffset, 0.001, 0.999);
                half3 refractedScene = SampleSceneColor(refractedUV);

                // Specular highlights
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float NdotH = max(dot(normalWS, halfDir), 0.0);
                float spec = pow(NdotH, _SpecularPower) * _SpecularIntensity;

                // Sparkle highlights
                float3 sparkleCoord = float3(IN.uv * _SparkleScale, time * _SparkleSpeed);
                float sparkleNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                    IN.uv * _SparkleScale * 0.1 + float2(time * 0.1, time * 0.07)).r;
                float sparkleHash = hash31(floor(sparkleCoord));
                float sparkle = smoothstep(_SparkleThreshold, 1.0, sparkleHash * sparkleNoise);
                sparkle *= _SparkleIntensity;

                // Compose crystal color
                half3 crystalBase = _CrystalColor.rgb * baseTex.rgb;

                // Blend iridescence
                half3 finalColor = lerp(crystalBase, iridescentColor, _IridescenceStrength * fresnel);

                // Blend with refracted scene for transparency
                finalColor = lerp(refractedScene, finalColor, _Transparency);

                // Add environment reflection approximation via fresnel
                finalColor = lerp(finalColor, finalColor * 1.5 + iridescentColor * 0.2, fresnel * _EnvironmentReflection);

                // Add specular
                finalColor += spec * mainLight.color;

                // Add sparkles
                finalColor += sparkle * mainLight.color;

                // Alpha: base transparency + fresnel
                float alpha = lerp(_Transparency, 1.0, fresnel * 0.5);
                alpha = saturate(alpha + sparkle * 0.2);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
