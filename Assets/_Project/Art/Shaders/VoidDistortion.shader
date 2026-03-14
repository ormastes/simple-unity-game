Shader "ElementalSiege/VoidDistortion"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Float) = 0.5
        _CoreRadius ("Core Radius", Range(0.01, 0.5)) = 0.1
        _EdgeGlowColor ("Edge Glow Color", Color) = (0.4, 0.1, 0.8, 1.0)
        _EdgeGlowIntensity ("Edge Glow Intensity", Float) = 2.0
        _EdgeGlowWidth ("Edge Glow Width", Range(0.01, 0.5)) = 0.15
        _SwirlSpeed ("Swirl Speed", Float) = 1.0
        _SwirlDensity ("Swirl Density", Float) = 3.0
        _DarkCoreColor ("Dark Core Color", Color) = (0.02, 0.0, 0.05, 1.0)
        _ParticleColor ("Particle Color", Color) = (0.6, 0.2, 1.0, 1.0)
        _ParticleDensity ("Particle Density", Float) = 8.0
        _VoidRadius ("Void Radius", Range(0.1, 1.0)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VoidDistortion"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _CoreRadius;
                float4 _EdgeGlowColor;
                float _EdgeGlowIntensity;
                float _EdgeGlowWidth;
                float _SwirlSpeed;
                float _SwirlDensity;
                float4 _DarkCoreColor;
                float4 _ParticleColor;
                float _ParticleDensity;
                float _VoidRadius;
            CBUFFER_END

            // 2D hash
            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // Center of the void in UV space (0.5, 0.5)
                float2 center = float2(0.5, 0.5);
                float2 toCenter = IN.uv - center;
                float dist = length(toCenter);
                float2 dir = normalize(toCenter + 0.0001);

                // Normalize distance by void radius
                float normDist = dist / _VoidRadius;

                // Gravitational lensing: radial distortion
                // Stronger distortion closer to center, falling off outward
                float distortionFactor = 1.0 / (normDist * normDist + 0.1) - 1.0 / (1.0 + 0.1);
                distortionFactor = max(distortionFactor, 0.0);
                distortionFactor *= _DistortionStrength;

                // Add swirl rotation
                float swirlAngle = distortionFactor * _SwirlDensity + time * _SwirlSpeed * (1.0 - normDist);
                float cosA = cos(swirlAngle);
                float sinA = sin(swirlAngle);
                float2 swirlOffset = float2(
                    toCenter.x * cosA - toCenter.y * sinA,
                    toCenter.x * sinA + toCenter.y * cosA
                ) - toCenter;

                // Compute distorted screen UV
                float2 distortedUV = screenUV + (dir * distortionFactor * 0.05) + swirlOffset * 0.1;
                distortedUV = clamp(distortedUV, 0.001, 0.999);

                // Sample background scene through distortion
                half3 sceneColor = SampleSceneColor(distortedUV);

                // Dark core
                float coreMask = 1.0 - smoothstep(0.0, _CoreRadius, dist);

                // Edge glow ring
                float edgeDist = abs(dist - _VoidRadius * 0.5);
                float edgeGlow = 1.0 - smoothstep(0.0, _EdgeGlowWidth, edgeDist);
                edgeGlow *= step(dist, _VoidRadius);

                // Swirling particle effect at edges
                float angle = atan2(toCenter.y, toCenter.x);
                float particleNoise = valueNoise2D(float2(angle * _ParticleDensity, dist * 20.0 - time * 2.0));
                float particleMask = particleNoise * smoothstep(_VoidRadius, _VoidRadius * 0.3, dist);
                particleMask *= smoothstep(0.0, _VoidRadius * 0.2, dist);

                // Color composition
                half3 finalColor = sceneColor;

                // Darken toward core
                float darkening = smoothstep(_VoidRadius, 0.0, dist);
                finalColor = lerp(finalColor, _DarkCoreColor.rgb, darkening * 0.9);

                // Apply core black
                finalColor = lerp(finalColor, _DarkCoreColor.rgb, coreMask);

                // Add edge glow
                finalColor += _EdgeGlowColor.rgb * edgeGlow * _EdgeGlowIntensity;

                // Add swirling particles
                finalColor += _ParticleColor.rgb * particleMask * 0.8;

                // Alpha: full coverage within void radius, fade at edges
                float alpha = smoothstep(_VoidRadius * 1.2, _VoidRadius * 0.5, dist);
                alpha = max(alpha, edgeGlow * 0.5);
                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
