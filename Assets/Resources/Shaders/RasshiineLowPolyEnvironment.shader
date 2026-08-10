Shader "Rasshiine/Low Poly Environment"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Color ("Legacy Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _ShadowTint ("Cool Shadow Tint", Color) = (0.24, 0.40, 0.66, 1)
        _LightTint ("Warm Key Tint", Color) = (1.00, 0.84, 0.60, 1)
        _RimColor ("Sky Rim", Color) = (0.30, 0.64, 0.96, 1)
        _FacetSteps ("Facet Steps", Range(2, 6)) = 4
        _AmbientStrength ("Ambient Strength", Range(0, 1.5)) = 0.72
        _RimStrength ("Rim Strength", Range(0, 0.6)) = 0.08
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _Color;
            half4 _EmissionColor;
            half4 _ShadowTint;
            half4 _LightTint;
            half4 _RimColor;
            half _FacetSteps;
            half _AmbientStrength;
            half _RimStrength;
            half _VertexColorStrength;
            half _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half3 vertexLighting : TEXCOORD4;
                half3 facetColor : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.vertexLighting = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
                // Imported low-poly bundle meshes do not consistently carry a COLOR
                // stream. Their materials keep strength at zero. Only the tiny
                // generated arena discs opt in, so missing attributes can never tint
                // existing character or scenery assets black.
                output.facetColor = lerp(half3(1.0h, 1.0h, 1.0h), input.color.rgb, saturate(_VertexColorStrength));
                return output;
            }

            half3 EvaluateSingleAdditionalLight(float3 positionWS, half3 normalWS, half steps)
            {
                half3 additionalLighting = 0.0h;
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    if (lightCount > 0u)
                    {
                        Light additionalLight = GetAdditionalLight(0u, positionWS);
                        half rawAdditionalLight = saturate(dot(normalWS, additionalLight.direction));
                        half facetedAdditionalLight = floor(rawAdditionalLight * steps + 0.48h) / steps;
                        additionalLighting = additionalLight.color *
                            (facetedAdditionalLight * additionalLight.distanceAttenuation);
                    }
                #endif
                return additionalLighting;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 surfaceColor = _BaseColor.rgb * input.facetColor;
                Light mainLight = GetMainLight(input.shadowCoord);
                half rawLight = saturate(dot(normalWS, mainLight.direction));
                half steps = max(_FacetSteps, 2.0h);
                half facetedLight = floor(rawLight * steps + 0.48h) / steps;
                half shadowedLight = facetedLight * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                half upwardSky = saturate(normalWS.y * 0.5h + 0.5h);
                half ambientBand = lerp(0.20h, 0.39h, upwardSky) * _AmbientStrength;
                half3 coolAmbient = surfaceColor * _ShadowTint.rgb * ambientBand;
                half3 warmKey = surfaceColor * _LightTint.rgb * mainLight.color * lerp(0.10h, 1.02h, shadowedLight);
                half3 coolFill = surfaceColor *
                    (input.vertexLighting + EvaluateSingleAdditionalLight(input.positionWS, normalWS, steps));

                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), 3.0h) * _RimStrength;
                half3 finalColor = coolAmbient + warmKey + coolFill + _RimColor.rgb * rim + _EmissionColor.rgb;
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, _BaseColor.a);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
