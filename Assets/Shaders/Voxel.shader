Shader "Basalt/Voxel"
{
    Properties
    {
        _TextureArray ("Texture Array", 2DArray) = "" {}
        _TimeOfDay ("Time of Day", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // Forward Lit Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Texture2DArray declaration via URP macros for cross-platform compatibility
            TEXTURE2D_ARRAY(_TextureArray);
            SAMPLER(sampler_TextureArray);

            // Day/night cycle uniform (0 = midnight, 0.5 = noon, 1 = midnight)
            // Luanti equivalent: time_of_day in environment.h
            half _TimeOfDay;

            // Vertex input matching VoxelVertex layout (48 bytes):
            //   POSITION  = float3 Position   (offset  0, 12 bytes)
            //   NORMAL    = float3 Normal      (offset 12, 12 bytes)
            //   TEXCOORD0 = float2 UV          (offset 24,  8 bytes)
            //   TEXCOORD1 = float4 Data        (offset 32, 16 bytes)
            //     Data.x = tileIndex, Data.y = aoLevel, Data.z = dayLight, Data.w = nightLight
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 data       : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS               : SV_POSITION;
                float2 uv                       : TEXCOORD0;
                nointerpolation float tileIndex  : TEXCOORD1;
                float  aoLevel                  : TEXCOORD2;
                float3 positionWS               : TEXCOORD3;
                float3 normalWS                 : TEXCOORD4;
                float  fogFactor                : TEXCOORD5;
                float  dayLight                 : TEXCOORD6;
                float  nightLight               : TEXCOORD7;
            };

            // AO minimum brightness — prevents pitch-black corners (matches Luanti)
            static const half AO_MIN = 0.3h;

            // Maximum voxel light level (Luanti LIGHT_SUN = 15)
            static const half LIGHT_MAX = 15.0h;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS);
                VertexNormalInputs normInputs  = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normInputs.normalWS;
                OUT.uv         = IN.uv;

                // Data.x is the texture array slice index stored as float
                // nointerpolation prevents rasterizer blending between vertices
                OUT.tileIndex = IN.data.x;

                // AO level in [0,3]; normalize to [0,1] for lerp in fragment
                OUT.aoLevel = IN.data.y * (1.0 / 3.0);

                // Day/night light levels (0-15), normalized to [0,1]
                OUT.dayLight   = IN.data.z * (1.0 / LIGHT_MAX);
                OUT.nightLight = IN.data.w * (1.0 / LIGHT_MAX);

                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Texture array sample — UV tiles via Repeat wrap mode,
                // round() recovers exact integer slice from nointerpolation float
                float slice = round(IN.tileIndex);
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(
                    _TextureArray, sampler_TextureArray,
                    float2(IN.uv.x, IN.uv.y), slice);

                // Ambient occlusion: map [0,1] to [AO_MIN, 1.0]
                half ao = lerp(AO_MIN, 1.0h, IN.aoLevel);

                // Voxel light: blend day and night light based on time of day
                // _TimeOfDay: 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset, 1 = midnight
                // dayFactor peaks at noon (0.5), drops to 0 at midnight (0/1)
                half dayFactor = saturate(sin(_TimeOfDay * 6.28318h) * 0.5h + 0.5h);

                half voxelDayLight   = IN.dayLight;
                half voxelNightLight = IN.nightLight;

                // When light propagation has not been computed (both channels == 0),
                // fall back to 1.0 so the world is not pitch-black pre-Epic 4.
                half hasLightData = step(0.001h, voxelDayLight + voxelNightLight);
                half voxelLight = lerp(1.0h,
                    lerp(voxelNightLight, voxelDayLight, dayFactor),
                    hasLightData);

                // URP main light with cascade shadow maps
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Lambert diffuse
                half NdotL = saturate(dot(normalize(IN.normalWS), mainLight.direction));

                // Ambient via spherical harmonics
                half3 ambient = SampleSH(normalize(IN.normalWS));

                // Final: texture * (ambient + diffuse * shadow) * AO * voxelLight
                half3 litColor = texColor.rgb
                    * (ambient + mainLight.color * NdotL * mainLight.shadowAttenuation)
                    * ao
                    * voxelLight;

                half4 finalColor = half4(litColor, texColor.a);

                // URP fog
                finalColor.rgb = MixFog(finalColor.rgb, IN.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }

        // Shadow Caster Pass — required for URP shadow maps
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                OUT.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth Only Pass — required for URP depth prepass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float3 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
