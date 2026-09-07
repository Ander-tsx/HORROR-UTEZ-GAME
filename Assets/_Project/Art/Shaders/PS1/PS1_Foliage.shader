// Alpha-cut foliage with wind, for the crossed-quad trees of the exterior.
//
// A PS1 tree was two or three crossed quads with a cut-out texture, not a model, and that is
// still the only thing that carries a wood on a phone. The canopy moves by vertex animation in
// this shader, so the wind costs nothing on the CPU.
//
// Note the deliberate difference from PS1_Lit: foliage textures KEEP their mipmaps. The level
// drops them for period accuracy, but alpha-cut leaves at 360p without mipmaps boil into a
// field of crawling pixels the moment the player moves.
Shader "UtezHorror/PS1 Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        _Smoothness("Smoothness", Range(0,1)) = 0.02

        [Header(PS1)]
        _JitterResolution("Vertex Snap Resolution", Vector) = (160, 120, 0, 0)
        _AffineAmount("Affine Warp Amount", Range(0,1)) = 0.6

        [Header(Wind)]
        _SwayStrength("Sway Strength (metres)", Range(0,1)) = 0.12
        _SwaySpeed("Sway Speed", Range(0,5)) = 1.1
        _SwayBaseHeight("Height Where Sway Starts", Float) = 0.8
        _SwayTopHeight("Height Of Full Sway", Float) = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "PS1Common.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half   _Smoothness;
            float4 _JitterResolution;
            float  _AffineAmount;
            float  _SwayStrength;
            float  _SwaySpeed;
            float  _SwayBaseHeight;
            float  _SwayTopHeight;
        CBUFFER_END

        /// 0 at the trunk base, 1 at the canopy. Object-space Y, so a piece keeps its own idea
        /// of "up" regardless of where it is placed.
        float FoliageSwayWeight(float3 positionOS)
        {
            float span = max(_SwayTopHeight - _SwayBaseHeight, 1e-3);
            return saturate((positionOS.y - _SwayBaseHeight) / span);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Two-sided: crossed quads are single-sided geometry viewed from every angle, and
            // back-face culling makes half of every tree disappear as the player walks past.
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FoliageVertex
            #pragma fragment FoliageFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 uvAffine   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3  normalWS   : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings FoliageVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = PS1_Sway(positionWS, FoliageSwayWeight(input.positionOS.xyz),
                                      _SwayStrength, _SwaySpeed);

                float4 positionCS = TransformWorldToHClip(positionWS);

                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvAffine = PS1_PackAffineUV(output.uv, positionCS.w);
                output.fogFactor = ComputeFogFactor(positionCS.z);
                output.positionCS = PS1_SnapVertex(positionCS, _JitterResolution.xy);

                return output;
            }

            half4 FoliageFragment(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = PS1_MixUV(input.uv, input.uvAffine, _AffineAmount);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                // With Cull Off the back faces arrive with the wrong normal, which lights the
                // far side of every leaf as if it were in shadow.
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS) * (facing >= 0 ? 1.0 : -1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(input.positionWS));

                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.specular = half3(0.02, 0.02, 0.02);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1.0;

                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FoliageShadowVertex
            #pragma fragment FoliageShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings FoliageShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = PS1_Sway(positionWS, FoliageSwayWeight(input.positionOS.xyz),
                                      _SwayStrength, _SwaySpeed);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                // See PS1_Lit's shadow pass: snapping in the light's clip space self-shadows
                // the entire level.
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 FoliageShadowFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FoliageDepthVertex
            #pragma fragment FoliageDepthFragment
            #pragma multi_compile_instancing

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings FoliageDepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = PS1_Sway(positionWS, FoliageSwayWeight(input.positionOS.xyz),
                                      _SwayStrength, _SwaySpeed);

                output.positionCS = PS1_SnapVertex(TransformWorldToHClip(positionWS), _JitterResolution.xy);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 FoliageDepthFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Simple Lit"
}
