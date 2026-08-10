Shader "Rasshiine/Stylized Fantasy Skybox"
{
    Properties
    {
        [NoScaleOffset] _SkyPanorama ("Generated Sky Panorama", 2D) = "white" {}
        _PanoramaTint ("Panorama Tint", Color) = (1, 1, 1, 1)
        _PanoramaBlend ("Panorama Blend", Range(0, 1)) = 0
        _PanoramaExposure ("Panorama Exposure", Range(0, 2)) = 1
        _ZenithColor ("Zenith", Color) = (0.010, 0.050, 0.17, 1)
        _SkyColor ("Upper Sky", Color) = (0.025, 0.22, 0.56, 1)
        _HorizonColor ("Horizon", Color) = (0.32, 0.64, 0.94, 1)
        _GroundColor ("Below Horizon", Color) = (0.055, 0.15, 0.28, 1)
        _HorizonGlowColor ("Horizon Glow", Color) = (1.0, 0.49, 0.18, 1)
        _SunColor ("Sun", Color) = (1.0, 0.88, 0.58, 1)
        _CloudLightColor ("Cloud Light", Color) = (0.90, 0.96, 1.0, 1)
        _CloudShadowColor ("Cloud Shadow", Color) = (0.24, 0.42, 0.62, 1)
        _CloudWarmColor ("Cloud Sun Edge", Color) = (1.0, 0.66, 0.31, 1)
        _FarRidgeColor ("Far Ridge", Color) = (0.34, 0.56, 0.76, 1)
        _NearRidgeColor ("Near Ridge", Color) = (0.16, 0.34, 0.55, 1)
        _SunDirection ("Sun Direction", Vector) = (-0.42, 0.38, -0.82, 0)
        _SunDiskSize ("Sun Disk Size", Range(0.001, 0.08)) = 0.035
        _SunHaloSize ("Sun Halo Size", Range(0.03, 0.45)) = 0.28
        _HorizonGlow ("Horizon Glow", Range(0, 1.5)) = 0.82
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.52
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.88
        _CloudScale ("Cloud Scale", Range(0.4, 4)) = 1.02
        _CloudSpeed ("Cloud Speed", Range(0, 0.12)) = 0.008
        _RidgeStrength ("Ridge Strength", Range(0, 1)) = 0.32
        _Exposure ("Exposure", Range(0, 2)) = 1.12
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "StylizedFantasySky"
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SkyPanorama);
            SAMPLER(sampler_SkyPanorama);

            CBUFFER_START(UnityPerMaterial)
                half4 _PanoramaTint;
                half _PanoramaBlend;
                half _PanoramaExposure;
                half4 _ZenithColor;
                half4 _SkyColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _HorizonGlowColor;
                half4 _SunColor;
                half4 _CloudLightColor;
                half4 _CloudShadowColor;
                half4 _CloudWarmColor;
                half4 _FarRidgeColor;
                half4 _NearRidgeColor;
                float4 _SunDirection;
                half _SunDiskSize;
                half _SunHaloSize;
                half _HorizonGlow;
                half _CloudCoverage;
                half _CloudOpacity;
                half _CloudScale;
                half _CloudSpeed;
                half _RidgeStrength;
                half _Exposure;
                half _Rotation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes vertexData)
            {
                Varyings rasterData;
                rasterData.positionCS = TransformObjectToHClip(vertexData.positionOS.xyz);
                rasterData.directionWS = vertexData.positionOS.xyz;
                return rasterData;
            }

            // One scalar hash per angular cell keeps the procedural fallback texture-free and cheap on WebGL.
            float HashScalar(float scalarValue)
            {
                scalarValue = frac(scalarValue * 0.1031);
                scalarValue *= scalarValue + 33.33;
                return frac(scalarValue * (scalarValue + scalarValue));
            }

            float TrianglePeak(float horizontalPosition, float center, float width)
            {
                return saturate(1.0 - abs(horizontalPosition - center) / max(width, 0.001));
            }

            float FacetedCloudLayer(
                float angularCoordinate,
                float verticalCoordinate,
                float cellCount,
                float altitude,
                float verticalSize,
                float drift,
                float randomSeed,
                out float facetLighting)
            {
                float scaledAngle = frac(angularCoordinate + drift) * cellCount;
                float angularCell = floor(scaledAngle);
                float localHorizontal = frac(scaledAngle) * 2.0 - 1.0;
                float cellRandom = HashScalar(angularCell + randomSeed);
                float widthRandom = HashScalar(angularCell + randomSeed + 29.71);
                float visibleThreshold = lerp(0.84, 0.36, _CloudCoverage);
                float cellVisibility = smoothstep(visibleThreshold, visibleThreshold + 0.075, cellRandom);

                float cloudWidth = lerp(0.62, 0.94, widthRandom);
                float leftLobe = TrianglePeak(localHorizontal, -0.43, cloudWidth * 0.52);
                float centerLobe = TrianglePeak(localHorizontal, 0.02, cloudWidth * 0.68);
                float rightLobe = TrianglePeak(localHorizontal, 0.47, cloudWidth * 0.47);
                float crown = max(centerLobe, max(leftLobe * 0.82, rightLobe * 0.72));
                float horizontalMask = 1.0 - smoothstep(cloudWidth - 0.055, cloudWidth, abs(localHorizontal));

                float cloudBottom = altitude - verticalSize * 0.34;
                float cloudTop = altitude + verticalSize * (0.14 + crown * 0.86);
                float verticalEdge = 0.008;
                float verticalMask = smoothstep(cloudBottom - verticalEdge, cloudBottom + verticalEdge, verticalCoordinate);
                verticalMask *= 1.0 - smoothstep(cloudTop - verticalEdge, cloudTop + verticalEdge, verticalCoordinate);

                float normalizedHeight = saturate((verticalCoordinate - cloudBottom) / max(verticalSize, 0.001));
                float horizontalFacet = floor(saturate(localHorizontal * 0.5 + 0.5) * 4.0) * 0.25;
                float verticalFacet = floor(normalizedHeight * 3.0) / 3.0;
                float diagonalFacet = step(0.50, frac((localHorizontal * 2.0) + normalizedHeight * 3.0));
                float underside = 1.0 - smoothstep(0.12, 0.46, normalizedHeight);
                facetLighting = saturate(
                    0.20 + horizontalFacet * 0.24 + verticalFacet * 0.48 + diagonalFacet * 0.14 - underside * 0.18);
                return horizontalMask * verticalMask * cellVisibility;
            }

            float RidgeHeight(float angularCoordinate, float cellCount, float baseHeight, float peakHeight, float randomSeed)
            {
                float ridgeCellCoordinate = frac(angularCoordinate) * cellCount;
                float ridgeCell = floor(ridgeCellCoordinate);
                float ridgeLocal = frac(ridgeCellCoordinate) * 2.0 - 1.0;
                float ridgeRandom = HashScalar(ridgeCell + randomSeed);
                float triangularPeak = 1.0 - abs(ridgeLocal);
                return baseHeight + triangularPeak * peakHeight * lerp(0.62, 1.0, ridgeRandom);
            }

            half4 Frag(Varyings rasterData) : SV_Target
            {
                float3 viewDirection = normalize(rasterData.directionWS);
                float rotationRadians = _Rotation * 0.01745329252;
                float rotationSine = sin(rotationRadians);
                float rotationCosine = cos(rotationRadians);
                viewDirection.xz = mul(
                    float2x2(rotationCosine, -rotationSine, rotationSine, rotationCosine),
                    viewDirection.xz);

                float skyHeight = viewDirection.y;
                float aboveHorizon = saturate(skyHeight);
                float upperBlend = smoothstep(0.005, 0.58, aboveHorizon);
                float zenithBlend = smoothstep(0.40, 0.96, aboveHorizon);
                float horizonBand = exp2(-abs(skyHeight) * 9.2);

                half3 skyGradient = lerp(_HorizonColor.rgb, _SkyColor.rgb, upperBlend);
                skyGradient = lerp(skyGradient, _ZenithColor.rgb, zenithBlend);
                half3 belowHorizon = lerp(_GroundColor.rgb * 0.70, _HorizonColor.rgb * 0.78, saturate((skyHeight + 0.34) * 2.8));
                half3 finalColor = lerp(belowHorizon, skyGradient, smoothstep(-0.045, 0.050, skyHeight));

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunAlignment = saturate(dot(viewDirection, sunDirection));
                float haloExponent = lerp(48.0, 6.0, saturate(_SunHaloSize * 2.3));
                float sunDistance = 1.0 - sunAlignment;
                float sunDisk = 1.0 - smoothstep(_SunDiskSize * 0.008, _SunDiskSize * 0.038, sunDistance);
                float sunHalo = pow(sunAlignment, haloExponent);

                float2 horizontalDirection = viewDirection.xz * rsqrt(max(dot(viewDirection.xz, viewDirection.xz), 0.00001));
                float2 horizontalSun = sunDirection.xz * rsqrt(max(dot(sunDirection.xz, sunDirection.xz), 0.00001));
                float towardSun = saturate(dot(horizontalDirection, horizontalSun) * 0.5 + 0.5);
                float horizonSunGlow = pow(towardSun, 7.0);
                half3 warmHorizon = lerp(_HorizonColor.rgb, _HorizonGlowColor.rgb, 0.74);
                float warmHorizonMix = horizonBand * (0.025 + horizonSunGlow * 0.30) * _HorizonGlow;
                finalColor = lerp(finalColor, warmHorizon, saturate(warmHorizonMix));

                float angularCoordinate = atan2(viewDirection.x, viewDirection.z) * 0.159154943 + 0.5;
                float ridgeMistTop = RidgeHeight(angularCoordinate + 0.061, 27.0, -0.025, 0.052, 89.0);
                float ridgeFarTop = RidgeHeight(angularCoordinate + 0.026, 19.0, -0.036, 0.078, 41.0);
                float ridgeNearTop = RidgeHeight(angularCoordinate, 13.0, -0.055, 0.072, 7.0);
                float ridgeMistMask = 1.0 - smoothstep(ridgeMistTop - 0.005, ridgeMistTop + 0.005, skyHeight);
                float farRidgeMask = 1.0 - smoothstep(ridgeFarTop - 0.006, ridgeFarTop + 0.006, skyHeight);
                float nearRidgeMask = 1.0 - smoothstep(ridgeNearTop - 0.006, ridgeNearTop + 0.006, skyHeight);
                float ridgeLowerFade = smoothstep(-0.34, -0.15, skyHeight);
                half3 mistRidgeColor = lerp(_HorizonColor.rgb, _FarRidgeColor.rgb, 0.52);
                finalColor = lerp(finalColor, mistRidgeColor, ridgeMistMask * ridgeLowerFade * _RidgeStrength * 0.28);
                finalColor = lerp(finalColor, _FarRidgeColor.rgb, farRidgeMask * ridgeLowerFade * _RidgeStrength * 0.52);
                finalColor = lerp(finalColor, _NearRidgeColor.rgb, nearRidgeMask * ridgeLowerFade * _RidgeStrength * 0.78);

                float cloudTime = _Time.y * _CloudSpeed;
                float farFacetLighting;
                float farCloudMask = FacetedCloudLayer(
                    angularCoordinate,
                    skyHeight,
                    17.0 * _CloudScale,
                    0.38,
                    0.115,
                    -cloudTime * 0.58,
                    73.0,
                    farFacetLighting);
                float nearFacetLighting;
                float nearCloudMask = FacetedCloudLayer(
                    angularCoordinate,
                    skyHeight,
                    10.0 * _CloudScale,
                    0.19,
                    0.175,
                    cloudTime,
                    17.0,
                    nearFacetLighting);

                half3 farCloudColor = lerp(_CloudShadowColor.rgb * 0.82, _CloudLightColor.rgb, farFacetLighting * 0.80);
                half3 nearCloudColor = lerp(_CloudShadowColor.rgb, _CloudLightColor.rgb, nearFacetLighting);
                float warmCloudEdge = pow(sunAlignment, 8.0);
                farCloudColor = lerp(farCloudColor, _CloudWarmColor.rgb, warmCloudEdge * 0.18);
                nearCloudColor = lerp(nearCloudColor, _CloudWarmColor.rgb, warmCloudEdge * 0.30);
                finalColor = lerp(finalColor, farCloudColor, farCloudMask * _CloudOpacity * 0.62);
                finalColor = lerp(finalColor, nearCloudColor, nearCloudMask * _CloudOpacity);

                float cloudOcclusion = saturate(nearCloudMask + farCloudMask * 0.60);
                float visibleSun = lerp(1.0, 0.62, cloudOcclusion);
                finalColor += _SunColor.rgb * sunHalo * lerp(0.38, 0.20, cloudOcclusion);
                finalColor = lerp(finalColor, _SunColor.rgb * 1.90, sunDisk * visibleSun);
                finalColor += _HorizonGlowColor.rgb * horizonBand * horizonSunGlow * 0.055 * _HorizonGlow;

                half3 outputColor = max(finalColor * _Exposure, 0.0);
                // _PanoramaBlend is uniform for the whole draw. The explicit
                // branch prevents a needless 2K texture lookup for every Battle
                // pixel while retaining the authored panorama in exploration.
                [branch]
                if (_PanoramaBlend > 0.001h)
                {
                    float panoramaLongitude = atan2(viewDirection.x, viewDirection.z) * 0.159154943 + 0.5;
                    float panoramaLatitude = asin(clamp(viewDirection.y, -1.0, 1.0)) * 0.318309886 + 0.5;
                    half3 panoramaColor = SAMPLE_TEXTURE2D(
                        _SkyPanorama,
                        sampler_SkyPanorama,
                        float2(frac(panoramaLongitude), saturate(panoramaLatitude))).rgb;
                    panoramaColor *= _PanoramaTint.rgb * _PanoramaExposure;
                    outputColor = lerp(outputColor, panoramaColor, saturate(_PanoramaBlend));
                }

                // Arithmetic dithering avoids a texture allocation and reduces WebGL gradient banding.
                float ditherValue = frac(52.9829189 * frac(dot(rasterData.positionCS.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                outputColor += ditherValue / 255.0;
                return half4(max(outputColor, 0.0), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
