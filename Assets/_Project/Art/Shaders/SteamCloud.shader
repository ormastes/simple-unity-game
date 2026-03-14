Shader "ElementalSiege/SteamCloud"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _BaseColor ("Base Color (White)", Color) = (1, 1, 1, 0.6)
        _FadeColor ("Fade Color (Gray)", Color) = (0.6, 0.6, 0.6, 0.0)
        _NoiseScale ("Noise Scale", Float) = 2.0
        _NoiseSpeed ("Noise Speed", Float) = 0.5
        _DistortionStrength ("Shape Distortion", Float) = 0.15
        _SoftParticleFade ("Soft Particle Fade Distance", Float) = 1.0
        _AlphaMultiplier ("Alpha Multiplier", Float) = 1.0
        _Lifetime ("Lifetime Fraction (set by script)", Range(0, 1)) = 0.0
        _FadeStart ("Fade Start (lifetime fraction)", Range(0, 1)) = 0.3
        _BillboardScale ("Billboard Scale", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SteamCloud"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _BaseColor;
                float4 _FadeColor;
                float _NoiseScale;
                float _NoiseSpeed;
                float _DistortionStrength;
                float _SoftParticleFade;
                float _AlphaMultiplier;
                float _Lifetime;
                float _FadeStart;
                float _BillboardScale;
            CBUFFER_END

            // Procedural noise for shape variation
            float hash2D(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float smoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash2D(i);
                float b = hash2D(i + float2(1, 0));
                float c = hash2D(i + float2(0, 1));
                float d = hash2D(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbmNoise(float2 p)
            {
                float value = 0.0;
                float amp = 0.5;
                float freq = 1.0;
                for (int i = 0; i < 4; i++)
                {
                    value += amp * smoothNoise(p * freq);
                    freq *= 2.0;
                    amp *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Billboard: align quad to face camera
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 viewDir = normalize(_WorldSpaceCameraPos - centerWS);

                // Construct billboard basis vectors
                float3 rightDir = normalize(cross(float3(0, 1, 0), viewDir));
                float3 upDir = normalize(cross(viewDir, rightDir));

                // Offset vertex in world space
                float3 vertexWS = centerWS
                    + rightDir * IN.positionOS.x * _BillboardScale
                    + upDir * IN.positionOS.y * _BillboardScale;

                OUT.positionHCS = TransformWorldToHClip(vertexWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.eyeDepth = -TransformWorldToView(vertexWS).z;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                // Animated noise for cloud shape
                float2 noiseUV = IN.uv * _NoiseScale;
                noiseUV += float2(time * _NoiseSpeed * 0.3, time * _NoiseSpeed * 0.7);

                float noise1 = fbmNoise(noiseUV);
                float noise2 = fbmNoise(noiseUV * 1.5 + float2(42.0, 17.0) + time * _NoiseSpeed * 0.2);
                float combinedNoise = (noise1 + noise2) * 0.5;

                // Distort main texture UV with noise
                float2 distortedUV = IN.uv;
                distortedUV += (combinedNoise - 0.5) * _DistortionStrength;

                // Sample particle texture
                half4 particleTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV);

                // Use vertex color from particle system for lifetime
                float lifetimeFraction = IN.color.a;

                // If _Lifetime is set by script, use that; otherwise use vertex alpha
                float effectiveLifetime = max(_Lifetime, 1.0 - lifetimeFraction);

                // Color interpolation over lifetime: white -> transparent gray
                half4 steamColor = lerp(_BaseColor, _FadeColor, effectiveLifetime);

                // Alpha fade over lifetime
                float lifetimeAlpha;
                if (effectiveLifetime < _FadeStart)
                {
                    // Fade in
                    lifetimeAlpha = smoothstep(0.0, _FadeStart, effectiveLifetime);
                }
                else
                {
                    // Fade out
                    lifetimeAlpha = 1.0 - smoothstep(_FadeStart, 1.0, effectiveLifetime);
                }

                // Soft particle blending (fade near opaque geometry)
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float depthDiff = sceneEyeDepth - IN.eyeDepth;
                float softFade = saturate(depthDiff / _SoftParticleFade);

                // Cloud shape mask from noise
                float shapeMask = smoothstep(0.2, 0.6, combinedNoise);

                // Final composition
                half3 finalColor = steamColor.rgb * particleTex.rgb;

                float alpha = particleTex.a
                    * steamColor.a
                    * lifetimeAlpha
                    * softFade
                    * shapeMask
                    * _AlphaMultiplier
                    * IN.color.a;

                alpha = saturate(alpha);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
