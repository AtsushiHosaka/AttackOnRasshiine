using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AttackOnRasshiine.Runtime.Scene
{
    /// <summary>
    /// Creates a lightweight runtime grading stack and a complementary cool fill light.
    /// The stack deliberately avoids depth-dependent effects so it remains practical in WebGL.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FantasyAtmosphereDirector : MonoBehaviour
    {
        private const string VolumeName = "Rasshiine Fantasy Atmosphere Volume";
        private const string FillLightName = "Rasshiine Cool Sky Fill";

        private Camera targetCamera;
        private Volume runtimeVolume;
        private VolumeProfile runtimeProfile;
        private Bloom bloom;
        private Tonemapping tonemapping;
        private ColorAdjustments colorAdjustments;
        private WhiteBalance whiteBalance;
        private Vignette vignette;
        private Light keyLight;
        private Light fillLight;
        private GameObject createdFillLight;
        private NeonCityBackdrop.BackdropPreset currentPreset = NeonCityBackdrop.BackdropPreset.Login;
        private Material battleSkyboxMaterial;
        private readonly Dictionary<string, float> battleSkyboxFloatSnapshot = new();
        private readonly Dictionary<string, Color> battleSkyboxColorSnapshot = new();
        private readonly Dictionary<string, Vector4> battleSkyboxVectorSnapshot = new();

        private static readonly string[] BattleSkyboxFloatProperties =
        {
            "_PanoramaBlend",
            "_PanoramaExposure",
            "_SunDiskSize",
            "_SunHaloSize",
            "_HorizonGlow",
            "_CloudCoverage",
            "_CloudOpacity",
            "_CloudScale",
            "_RidgeStrength",
            "_Exposure"
        };

        private static readonly string[] BattleSkyboxColorProperties =
        {
            "_ZenithColor",
            "_SkyColor",
            "_HorizonColor",
            "_GroundColor",
            "_HorizonGlowColor",
            "_SunColor",
            "_CloudLightColor",
            "_CloudShadowColor",
            "_CloudWarmColor",
            "_FarRidgeColor",
            "_NearRidgeColor"
        };

        private static readonly string[] BattleSkyboxVectorProperties =
        {
            "_SunDirection"
        };

        public Volume RuntimeVolume => runtimeVolume;
        public Light FillLight => fillLight;

        private void OnEnable()
        {
            // Runtime callers explicitly supply the active backdrop preset. Avoid
            // allocating the default login volume for one frame when this component
            // is first added by a dedicated battle scene.
            if (targetCamera != null)
            {
                ApplyPreset(currentPreset);
            }
        }

        public void ApplyPreset(NeonCityBackdrop.BackdropPreset preset)
        {
            ApplyPresetForPlatform(preset, Application.platform);
        }

        private void ApplyPresetForPlatform(
            NeonCityBackdrop.BackdropPreset preset,
            RuntimePlatform platform)
        {
            currentPreset = preset;
            var enablePostProcessing = ShouldEnablePostProcessing(preset, platform);
            EnsureCameraRendering(enablePostProcessing);
            ResolveLights();
            if (enablePostProcessing)
            {
                EnsureVolume(platform);
                ApplyPostProcessing(preset);
            }
            else
            {
                DisableRuntimeVolume();
            }
            ApplyEnvironment(preset);
            ApplyLights(preset, platform);
            ApplySkyboxPreset(preset);
        }

        private void LateUpdate()
        {
            if (currentPreset != NeonCityBackdrop.BackdropPreset.Battle)
            {
                return;
            }

            // AnimatedSkybox gently pulses _Exposure in Update. Reassert only the
            // bounded LDR exposure after that update; the rest of the palette is
            // reapplied only if the runtime sky material itself changes.
            if (RenderSettings.skybox != battleSkyboxMaterial)
            {
                ApplySkyboxPreset(currentPreset);
                return;
            }

            SetFloatIfPresent(battleSkyboxMaterial, "_Exposure", 1.05f);
            ApplyBattleSkySunDirection(battleSkyboxMaterial);
            if (targetCamera != null && battleSkyboxMaterial != null && battleSkyboxMaterial.HasProperty("_SunDirection"))
            {
                // Keep the sun clear of the back button and boss HP frame. This is
                // reasserted after AnimatedSkybox.Update without another light or
                // texture lookup, so the wide WebGL composition retains a visible
                // warm focal point in the open upper-left sky.
                var localSunDirection = new Vector3(-0.58f, 0.92f, 1f).normalized;
                var worldSunDirection = targetCamera.transform.TransformDirection(localSunDirection);
                battleSkyboxMaterial.SetVector("_SunDirection", new Vector4(
                    worldSunDirection.x,
                    worldSunDirection.y,
                    worldSunDirection.z,
                    0f));
            }
        }

        private void EnsureCameraRendering(bool enablePostProcessing)
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return;
            }

            // Battle and WebGL are graded entirely with LDR-safe materials and two
            // inexpensive directional lights. Keeping HDR/post disabled makes the
            // Editor battle preview match WebGL and avoids allocating a volume stack
            // that browser GPUs cannot execute reliably.
            targetCamera.clearFlags = CameraClearFlags.Skybox;
            targetCamera.allowHDR = enablePostProcessing;
            targetCamera.allowMSAA = false;

            var cameraData = targetCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = enablePostProcessing;
            cameraData.renderShadows = true;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.antialiasing = enablePostProcessing
                ? AntialiasingMode.FastApproximateAntialiasing
                : AntialiasingMode.None;
            cameraData.dithering = enablePostProcessing;
            cameraData.stopNaN = true;
        }

        private static bool ShouldEnablePostProcessing(
            NeonCityBackdrop.BackdropPreset preset,
            RuntimePlatform platform)
        {
            return preset != NeonCityBackdrop.BackdropPreset.Battle &&
                   platform != RuntimePlatform.WebGLPlayer;
        }

        private void EnsureVolume(RuntimePlatform platform)
        {
            if (runtimeVolume != null && runtimeProfile != null)
            {
                runtimeVolume.enabled = true;
                return;
            }

            var volumeTransform = transform.Find(VolumeName);
            var volumeObject = volumeTransform != null
                ? volumeTransform.gameObject
                : new GameObject(VolumeName, typeof(Volume));
            volumeObject.transform.SetParent(transform, false);
            volumeObject.hideFlags = HideFlags.DontSave;

            runtimeVolume = volumeObject.GetComponent<Volume>();
            runtimeVolume.isGlobal = true;
            runtimeVolume.priority = 80f;
            runtimeVolume.weight = 1f;

            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "Rasshiine Fantasy Atmosphere (Runtime)";
            runtimeProfile.hideFlags = HideFlags.DontSave;
            runtimeVolume.sharedProfile = runtimeProfile;

            bloom = runtimeProfile.Add<Bloom>(true);
            tonemapping = runtimeProfile.Add<Tonemapping>(true);
            colorAdjustments = runtimeProfile.Add<ColorAdjustments>(true);
            whiteBalance = runtimeProfile.Add<WhiteBalance>(true);
            vignette = runtimeProfile.Add<Vignette>(true);

            tonemapping.mode.value = TonemappingMode.ACES;
            bloom.highQualityFiltering.value = false;
            bloom.downscale.value = BloomDownscaleMode.Quarter;
            bloom.filter.value = BloomFilterMode.Dual;
            bloom.maxIterations.value = platform == RuntimePlatform.WebGLPlayer ? 3 : 4;
            bloom.clamp.value = 6f;
            vignette.center.value = new Vector2(0.50f, 0.49f);
            vignette.rounded.value = false;
        }

        private void DisableRuntimeVolume()
        {
            if (runtimeVolume != null)
            {
                runtimeVolume.enabled = false;
            }
        }

        private void ApplyPostProcessing(NeonCityBackdrop.BackdropPreset preset)
        {
            if (bloom == null || colorAdjustments == null || whiteBalance == null || vignette == null)
            {
                return;
            }

            switch (preset)
            {
                case NeonCityBackdrop.BackdropPreset.Home:
                    bloom.threshold.value = 1.02f;
                    bloom.intensity.value = 0.20f;
                    bloom.scatter.value = 0.58f;
                    bloom.tint.value = new Color(0.76f, 0.88f, 1.0f, 1f);
                    colorAdjustments.postExposure.value = 0.04f;
                    colorAdjustments.contrast.value = 20f;
                    colorAdjustments.saturation.value = 18f;
                    colorAdjustments.colorFilter.value = new Color(1.0f, 0.98f, 0.94f, 1f);
                    whiteBalance.temperature.value = 4f;
                    whiteBalance.tint.value = 1f;
                    vignette.intensity.value = 0.115f;
                    vignette.smoothness.value = 0.81f;
                    vignette.color.value = new Color(0.008f, 0.038f, 0.078f, 1f);
                    break;

                case NeonCityBackdrop.BackdropPreset.Battle:
                    bloom.threshold.value = 0.86f;
                    bloom.intensity.value = 0.32f;
                    bloom.scatter.value = 0.54f;
                    bloom.tint.value = new Color(0.60f, 0.82f, 1.0f, 1f);
                    colorAdjustments.postExposure.value = -0.08f;
                    colorAdjustments.contrast.value = 20f;
                    colorAdjustments.saturation.value = 10f;
                    colorAdjustments.colorFilter.value = new Color(0.90f, 0.96f, 1.0f, 1f);
                    whiteBalance.temperature.value = -5f;
                    whiteBalance.tint.value = 2f;
                    vignette.intensity.value = 0.15f;
                    vignette.smoothness.value = 0.76f;
                    vignette.color.value = new Color(0.005f, 0.025f, 0.075f, 1f);
                    break;

                default:
                    bloom.threshold.value = 0.98f;
                    bloom.intensity.value = 0.20f;
                    bloom.scatter.value = 0.58f;
                    bloom.tint.value = new Color(0.72f, 0.92f, 1.0f, 1f);
                    colorAdjustments.postExposure.value = 0.18f;
                    colorAdjustments.contrast.value = 12f;
                    colorAdjustments.saturation.value = 16f;
                    colorAdjustments.colorFilter.value = new Color(0.92f, 1.0f, 1.08f, 1f);
                    whiteBalance.temperature.value = 3f;
                    whiteBalance.tint.value = -1f;
                    vignette.intensity.value = 0.055f;
                    vignette.smoothness.value = 0.76f;
                    vignette.color.value = new Color(0.015f, 0.055f, 0.10f, 1f);
                    break;
            }
        }

        private static void ApplyEnvironment(NeonCityBackdrop.BackdropPreset preset)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            switch (preset)
            {
                case NeonCityBackdrop.BackdropPreset.Home:
                    RenderSettings.fogColor = new Color(0.25f, 0.45f, 0.68f, 1f);
                    RenderSettings.fogStartDistance = 40f;
                    RenderSettings.fogEndDistance = 98f;
                    RenderSettings.ambientSkyColor = new Color(0.28f, 0.44f, 0.64f, 1f);
                    RenderSettings.ambientEquatorColor = new Color(0.16f, 0.24f, 0.33f, 1f);
                    RenderSettings.ambientGroundColor = new Color(0.065f, 0.090f, 0.105f, 1f);
                    RenderSettings.ambientIntensity = 0.68f;
                    RenderSettings.reflectionIntensity = 0.48f;
                    break;

                case NeonCityBackdrop.BackdropPreset.Battle:
                    // The reference scene reads as a sunlit toy diorama: only the
                    // distant canyon layers receive cyan haze while foreground
                    // contact shadows retain a cool, saturated edge. Keeping the
                    // ambient floor below the key light prevents the coral ground
                    // and purple shell from collapsing into one flat value.
                    RenderSettings.fogColor = new Color(0.28f, 0.78f, 0.92f, 1f);
                    RenderSettings.fogStartDistance = 13.5f;
                    RenderSettings.fogEndDistance = 43f;
                    RenderSettings.ambientSkyColor = new Color(0.58f, 0.84f, 1.00f, 1f);
                    RenderSettings.ambientEquatorColor = new Color(0.38f, 0.62f, 0.73f, 1f);
                    RenderSettings.ambientGroundColor = new Color(0.30f, 0.21f, 0.24f, 1f);
                    RenderSettings.ambientIntensity = 0.88f;
                    RenderSettings.reflectionIntensity = 0.12f;
                    break;

                default:
                    RenderSettings.fogColor = new Color(0.40f, 0.72f, 0.90f, 1f);
                    RenderSettings.fogStartDistance = 44f;
                    RenderSettings.fogEndDistance = 120f;
                    RenderSettings.ambientSkyColor = new Color(0.46f, 0.69f, 0.84f, 1f);
                    RenderSettings.ambientEquatorColor = new Color(0.26f, 0.38f, 0.46f, 1f);
                    RenderSettings.ambientGroundColor = new Color(0.14f, 0.19f, 0.21f, 1f);
                    RenderSettings.ambientIntensity = 0.94f;
                    RenderSettings.reflectionIntensity = 0.56f;
                    break;
            }
        }

        private void ResolveLights()
        {
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            keyLight = null;
            fillLight = null;
            for (var index = 0; index < lights.Length; index++)
            {
                var candidate = lights[index];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name == "Raid Key Light")
                {
                    keyLight = candidate;
                }
                else if (candidate.name == FillLightName)
                {
                    fillLight = candidate;
                }
            }

            if (keyLight == null)
            {
                keyLight = RenderSettings.sun;
            }

            if (fillLight != null)
            {
                return;
            }

            createdFillLight = new GameObject(FillLightName, typeof(Light));
            createdFillLight.hideFlags = HideFlags.DontSave;
            fillLight = createdFillLight.GetComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.shadows = LightShadows.None;
            fillLight.renderMode = LightRenderMode.ForceVertex;
            fillLight.bounceIntensity = 0f;
        }

        private void ApplyLights(NeonCityBackdrop.BackdropPreset preset, RuntimePlatform platform)
        {
            if (keyLight != null)
            {
                keyLight.gameObject.SetActive(true);
                keyLight.type = LightType.Directional;
                keyLight.shadows = preset == NeonCityBackdrop.BackdropPreset.Battle
                    ? LightShadows.Hard
                    : SelectKeyShadowMode(platform);
                keyLight.renderMode = LightRenderMode.ForcePixel;
                keyLight.shadowStrength = preset == NeonCityBackdrop.BackdropPreset.Battle
                    ? 0.62f
                    : preset == NeonCityBackdrop.BackdropPreset.Home ? 0.65f : 0.52f;
                keyLight.shadowBias = preset == NeonCityBackdrop.BackdropPreset.Battle ? 0.018f : 0.026f;
                keyLight.shadowNormalBias = preset == NeonCityBackdrop.BackdropPreset.Battle ? 0.12f : 0.18f;
                keyLight.shadowNearPlane = preset == NeonCityBackdrop.BackdropPreset.Battle ? 0.16f : 0.22f;

                if (preset == NeonCityBackdrop.BackdropPreset.Home)
                {
                    keyLight.color = new Color(1.0f, 0.80f, 0.56f, 1f);
                    keyLight.intensity = 1.50f;
                    AimKeyLightAtVisibleSun(-0.72f, 0.38f);
                }
                else if (preset == NeonCityBackdrop.BackdropPreset.Battle)
                {
                    keyLight.color = new Color(1.0f, 0.89f, 0.76f, 1f);
                    keyLight.intensity = 1.36f;
                    // Light from the same upper-left/front direction as the visible
                    // sun. A lower grazing angle gives the party and beetle visible
                    // contact shadows, while the shadowless fill keeps black facets
                    // from crushing on WebGL's LDR path.
                    AimKeyLightAtVisibleSun(-0.58f, 0.92f);
                }
                else
                {
                    keyLight.color = new Color(1.0f, 0.84f, 0.54f, 1f);
                    keyLight.intensity = 1.82f;
                    AimKeyLightAtVisibleSun(-0.78f, 0.36f);
                }

                RenderSettings.sun = keyLight;
                var animatedSkybox = targetCamera != null ? targetCamera.GetComponent<AnimatedSkybox>() : null;
                animatedSkybox?.SynchronizeSun(keyLight);
            }

            if (fillLight == null)
            {
                return;
            }

            fillLight.gameObject.SetActive(true);
            fillLight.transform.rotation = Quaternion.Euler(116f, 146f, 0f);
            fillLight.color = preset == NeonCityBackdrop.BackdropPreset.Battle
                ? new Color(0.24f, 0.56f, 0.86f, 1f)
                : preset == NeonCityBackdrop.BackdropPreset.Home
                    ? new Color(0.28f, 0.52f, 0.92f, 1f)
                    : new Color(0.28f, 0.66f, 1.0f, 1f);
            fillLight.intensity = preset == NeonCityBackdrop.BackdropPreset.Battle
                ? 0.20f
                : preset == NeonCityBackdrop.BackdropPreset.Home ? 0.22f : 0.38f;
        }

        private void ApplySkyboxPreset(NeonCityBackdrop.BackdropPreset preset)
        {
            if (preset != NeonCityBackdrop.BackdropPreset.Battle)
            {
                RestoreBattleSkyboxPalette();
                return;
            }

            var skybox = RenderSettings.skybox;
            if (skybox == null)
            {
                return;
            }

            if (battleSkyboxMaterial != skybox)
            {
                RestoreBattleSkyboxPalette();
                battleSkyboxMaterial = skybox;
                battleSkyboxFloatSnapshot.Clear();
                battleSkyboxColorSnapshot.Clear();
                battleSkyboxVectorSnapshot.Clear();
                CaptureBattleSkyboxPalette(skybox);
            }

            // Battle uses the texture-free procedural color path for parallax and
            // avoids sampling the 2K panorama on every fragment in WebGL. Clouds
            // are bounded opaque arena meshes; the sky shader's full-screen cloud
            // cells are disabled below because one cell became a giant HUD wedge.
            SetFloatIfPresent(skybox, "_PanoramaBlend", 0f);
            SetFloatIfPresent(skybox, "_PanoramaExposure", 0.86f);
            SetFloatIfPresent(skybox, "_SunDiskSize", 0.030f);
            SetFloatIfPresent(skybox, "_SunHaloSize", 0.15f);
            SetFloatIfPresent(skybox, "_HorizonGlow", 0.24f);
            SetFloatIfPresent(skybox, "_CloudCoverage", 0f);
            SetFloatIfPresent(skybox, "_CloudOpacity", 0f);
            SetFloatIfPresent(skybox, "_CloudScale", 3.40f);
            SetFloatIfPresent(skybox, "_RidgeStrength", 0.05f);
            SetFloatIfPresent(skybox, "_Exposure", 1.05f);

            SetColorIfPresent(skybox, "_ZenithColor", new Color(0.002f, 0.24f, 0.80f, 1f));
            SetColorIfPresent(skybox, "_SkyColor", new Color(0.002f, 0.46f, 0.92f, 1f));
            SetColorIfPresent(skybox, "_HorizonColor", new Color(0.02f, 0.66f, 0.97f, 1f));
            SetColorIfPresent(skybox, "_GroundColor", new Color(0.08f, 0.62f, 0.76f, 1f));
            SetColorIfPresent(skybox, "_HorizonGlowColor", new Color(0.98f, 0.66f, 0.38f, 1f));
            SetColorIfPresent(skybox, "_SunColor", new Color(1.0f, 0.94f, 0.78f, 1f));
            SetColorIfPresent(skybox, "_CloudLightColor", new Color(0.95f, 0.97f, 0.99f, 1f));
            SetColorIfPresent(skybox, "_CloudShadowColor", new Color(0.55f, 0.73f, 0.86f, 1f));
            SetColorIfPresent(skybox, "_CloudWarmColor", new Color(1.0f, 0.84f, 0.68f, 1f));
            SetColorIfPresent(skybox, "_FarRidgeColor", new Color(0.36f, 0.72f, 0.82f, 1f));
            SetColorIfPresent(skybox, "_NearRidgeColor", new Color(0.22f, 0.62f, 0.75f, 1f));
            ApplyBattleSkySunDirection(skybox);
        }

        private void ApplyBattleSkySunDirection(Material skybox)
        {
            if (skybox == null || targetCamera == null || !skybox.HasProperty("_SunDirection"))
            {
                return;
            }

            // Place the disk in the open sky above the boss frame. AnimatedSkybox
            // updates the general sun in Update; LateUpdate reapplies this Battle
            // composition without another light, texture, or fullscreen pass.
            var direction = (targetCamera.transform.forward -
                             targetCamera.transform.right * 0.58f +
                             targetCamera.transform.up * 0.92f).normalized;
            skybox.SetVector("_SunDirection", new Vector4(direction.x, direction.y, direction.z, 0f));
        }

        private void CaptureBattleSkyboxPalette(Material material)
        {
            for (var index = 0; index < BattleSkyboxFloatProperties.Length; index++)
            {
                var propertyName = BattleSkyboxFloatProperties[index];
                if (material.HasProperty(propertyName))
                {
                    battleSkyboxFloatSnapshot[propertyName] = material.GetFloat(propertyName);
                }
            }

            for (var index = 0; index < BattleSkyboxColorProperties.Length; index++)
            {
                var propertyName = BattleSkyboxColorProperties[index];
                if (material.HasProperty(propertyName))
                {
                    battleSkyboxColorSnapshot[propertyName] = material.GetColor(propertyName);
                }
            }

            for (var index = 0; index < BattleSkyboxVectorProperties.Length; index++)
            {
                var propertyName = BattleSkyboxVectorProperties[index];
                if (material.HasProperty(propertyName))
                {
                    battleSkyboxVectorSnapshot[propertyName] = material.GetVector(propertyName);
                }
            }
        }

        private void RestoreBattleSkyboxPalette()
        {
            if (battleSkyboxMaterial != null)
            {
                foreach (var property in battleSkyboxFloatSnapshot)
                {
                    SetFloatIfPresent(battleSkyboxMaterial, property.Key, property.Value);
                }

                foreach (var property in battleSkyboxColorSnapshot)
                {
                    SetColorIfPresent(battleSkyboxMaterial, property.Key, property.Value);
                }

                foreach (var property in battleSkyboxVectorSnapshot)
                {
                    SetVectorIfPresent(battleSkyboxMaterial, property.Key, property.Value);
                }
            }

            battleSkyboxMaterial = null;
            battleSkyboxFloatSnapshot.Clear();
            battleSkyboxColorSnapshot.Clear();
            battleSkyboxVectorSnapshot.Clear();
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetVectorIfPresent(Material material, string propertyName, Vector4 value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetVector(propertyName, value);
            }
        }

        private static LightShadows SelectKeyShadowMode(RuntimePlatform platform)
        {
            return platform == RuntimePlatform.WebGLPlayer ? LightShadows.Hard : LightShadows.Soft;
        }

        private void AimKeyLightAtVisibleSun(float horizontalOffset, float verticalOffset)
        {
            if (keyLight == null || targetCamera == null)
            {
                return;
            }

            var sunDirection = (targetCamera.transform.forward +
                                targetCamera.transform.right * horizontalOffset +
                                targetCamera.transform.up * verticalOffset).normalized;
            keyLight.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
        }

        private void OnDestroy()
        {
            RestoreBattleSkyboxPalette();

            if (runtimeProfile != null)
            {
                DestroyRuntimeObject(runtimeProfile);
            }

            if (createdFillLight != null)
            {
                DestroyRuntimeObject(createdFillLight);
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }
    }
}
