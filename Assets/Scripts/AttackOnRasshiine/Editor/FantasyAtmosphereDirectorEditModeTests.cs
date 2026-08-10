using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Scene;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AttackOnRasshiine.Editor
{
    public sealed class FantasyAtmosphereDirectorEditModeTests
    {
        private GameObject cameraObject;
        private GameObject keyLightObject;
        private Light previousSun;
        private Material previousSkybox;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientSky;
        private Color previousAmbientEquator;
        private Color previousAmbientGround;
        private float previousAmbientIntensity;
        private float previousReflectionIntensity;
        private bool previousFog;
        private FogMode previousFogMode;
        private Color previousFogColor;
        private float previousFogStart;
        private float previousFogEnd;

        [SetUp]
        public void SetUp()
        {
            previousSun = RenderSettings.sun;
            previousSkybox = RenderSettings.skybox;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientSky = RenderSettings.ambientSkyColor;
            previousAmbientEquator = RenderSettings.ambientEquatorColor;
            previousAmbientGround = RenderSettings.ambientGroundColor;
            previousAmbientIntensity = RenderSettings.ambientIntensity;
            previousReflectionIntensity = RenderSettings.reflectionIntensity;
            previousFog = RenderSettings.fog;
            previousFogMode = RenderSettings.fogMode;
            previousFogColor = RenderSettings.fogColor;
            previousFogStart = RenderSettings.fogStartDistance;
            previousFogEnd = RenderSettings.fogEndDistance;

            keyLightObject = new GameObject("Raid Key Light", typeof(Light));
            var keyLight = keyLightObject.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            RenderSettings.sun = keyLight;

            cameraObject = new GameObject("Atmosphere Test Camera", typeof(Camera));
        }

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (keyLightObject != null)
            {
                Object.DestroyImmediate(keyLightObject);
            }

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light != null && light.name == "Rasshiine Cool Sky Fill")
                {
                    Object.DestroyImmediate(light.gameObject);
                }
            }

            RenderSettings.sun = previousSun;
            RenderSettings.skybox = previousSkybox;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousAmbientSky;
            RenderSettings.ambientEquatorColor = previousAmbientEquator;
            RenderSettings.ambientGroundColor = previousAmbientGround;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.reflectionIntensity = previousReflectionIntensity;
            RenderSettings.fog = previousFog;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogStartDistance = previousFogStart;
            RenderSettings.fogEndDistance = previousFogEnd;
        }

        [Test]
        public void StylizedFantasySkyboxShaderIsAvailableAsAResource()
        {
            var shader = Resources.Load<Shader>("Shaders/RasshiineStylizedFantasySkybox");

            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("Rasshiine/Stylized Fantasy Skybox"));
            Assert.That(shader.isSupported, Is.True);
            using (var materialScope = new MaterialScope(shader))
            {
                Assert.That(materialScope.Material.passCount, Is.GreaterThan(0));
                Assert.That(materialScope.Material.HasProperty("_CloudCoverage"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_SunDirection"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_SkyPanorama"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_PanoramaBlend"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_PanoramaExposure"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_PanoramaTint"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_CloudWarmColor"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_FarRidgeColor"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_NearRidgeColor"), Is.True);
                Assert.That(materialScope.Material.HasProperty("_RidgeStrength"), Is.True);
            }
        }

        [Test]
        public void StylizedSkyUsesOnePanoramaSampleAndProceduralFallbackOnShaderModelTwo()
        {
            var shader = Resources.Load<Shader>("Shaders/RasshiineStylizedFantasySkybox");
            var shaderPath = AssetDatabase.GetAssetPath(shader);
            var source = File.ReadAllText(shaderPath);
            var reservedDeclaration = new Regex(
                @"\b(?:float|half|int|uint|bool)(?:[1-4](?:x[1-4])?)?\s+(?:sample|filter|matrix|texture|sampler|line|point)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            Assert.That(source, Does.Contain("#pragma target 2.0"));
            Assert.That(source, Does.Contain("FacetedCloudLayer"));
            Assert.That(source, Does.Contain("RidgeHeight"));
            Assert.That(source, Does.Not.Contain("Fbm("));
            Assert.That(source, Does.Not.Contain("ValueNoise("));
            Assert.That(source, Does.Not.Contain("tex2D("));
            Assert.That(Regex.Matches(source, @"\bSAMPLE_TEXTURE2D\s*\(").Count, Is.EqualTo(1),
                "The WebGL sky may use one panorama lookup while procedural fallback remains arithmetic-only.");
            Assert.That(source, Does.Not.Match(@"\bfor\s*\("));
            Assert.That(source, Does.Not.Match(@"\bwhile\s*\("));
            Assert.That(reservedDeclaration.IsMatch(source), Is.False,
                "Local shader identifiers must not collide with common HLSL/GLES reserved words.");

            var compilerMessages = ShaderUtil.GetShaderMessages(shader)
                .Select(message => message.message)
                .ToArray();
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, string.Join("\n", compilerMessages));
        }

        [Test]
        public void LoginPanoramaImporterStaysWithinTheWebGlTextureBudget()
        {
            const string panoramaPath = "Assets/Art/Skyboxes/LoginOption3/LoginSky_LowPoly_Option3.png";
            Assert.That(File.Exists(panoramaPath), Is.True, $"Missing generated login panorama: {panoramaPath}");

            var configureImport = typeof(RasshiineSceneBuilder).GetMethod(
                "ConfigureLoginSkyPanoramaImport",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(configureImport, Is.Not.Null);
            configureImport.Invoke(null, null);

            var importer = AssetImporter.GetAtPath(panoramaPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.anisoLevel, Is.EqualTo(0));

            var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(panoramaPath);
            Assert.That(panorama, Is.Not.Null);
            Assert.That(panorama.width, Is.LessThanOrEqualTo(2048));
            Assert.That(panorama.mipmapCount, Is.EqualTo(1));
        }

        [Test]
        public void SceneBuilderBindsGeneratedPanoramaToTheCustomSkyMaterial()
        {
            const string panoramaPath = "Assets/Art/Skyboxes/LoginOption3/LoginSky_LowPoly_Option3.png";
            var createMaterial = typeof(RasshiineSceneBuilder).GetMethod(
                "CreateSkyboxMaterial",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(createMaterial, Is.Not.Null);

            var material = createMaterial.Invoke(null, null) as Material;
            var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(panoramaPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(panorama, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Rasshiine/Stylized Fantasy Skybox"));
            Assert.That(material.HasProperty("_SkyPanorama"), Is.True);
            Assert.That(material.HasProperty("_PanoramaBlend"), Is.True);
            Assert.That(material.GetTexture("_SkyPanorama"), Is.SameAs(panorama));
            Assert.That(material.GetFloat("_PanoramaBlend"), Is.InRange(0.34f, 0.44f),
                "The authored panorama should remain visible while allowing the procedural upper-left sun and warm horizon to read.");
            Assert.That(material.GetFloat("_Rotation"), Is.InRange(150f, 160f),
                "The panorama sun must sit in the unobscured upper-left of the login camera.");
        }

        [Test]
        public void AnimatedSkyboxClonesCustomSourceWithoutDroppingPanoramaTexture()
        {
            var shader = Resources.Load<Shader>("Shaders/RasshiineStylizedFantasySkybox");
            Assert.That(shader, Is.Not.Null);

            var previousSkybox = RenderSettings.skybox;
            var panorama = new Texture2D(4, 2, TextureFormat.RGBA32, false)
            {
                name = "AnimatedSkybox Panorama Test"
            };
            var source = new Material(shader)
            {
                name = "AnimatedSkybox Custom Source"
            };
            source.SetTexture("_SkyPanorama", panorama);
            source.SetFloat("_PanoramaBlend", 0.73f);
            source.SetFloat("_PanoramaExposure", 1.07f);

            var host = new GameObject("Animated Skybox Clone Test", typeof(AnimatedSkybox));
            var animatedSkybox = host.GetComponent<AnimatedSkybox>();
            var applySkybox = typeof(AnimatedSkybox).GetMethod(
                "ApplySkybox",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var runtimeField = typeof(AnimatedSkybox).GetField(
                "runtimeSkybox",
                BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                Assert.That(applySkybox, Is.Not.Null);
                Assert.That(runtimeField, Is.Not.Null);
                animatedSkybox.Configure(source);
                applySkybox.Invoke(animatedSkybox, null);

                var firstRuntime = runtimeField.GetValue(animatedSkybox) as Material;
                Assert.That(firstRuntime, Is.Not.Null);
                Assert.That(firstRuntime, Is.Not.SameAs(source));
                Assert.That(firstRuntime.shader, Is.SameAs(source.shader));
                Assert.That(firstRuntime.GetTexture("_SkyPanorama"), Is.SameAs(panorama));
                Assert.That(firstRuntime.GetFloat("_PanoramaBlend"), Is.EqualTo(0.73f).Within(0.001f));
                Assert.That(firstRuntime.GetFloat("_PanoramaExposure"), Is.EqualTo(1.07f).Within(0.001f));

                // With no serialized source, the current runtime material becomes the next source.
                // Re-applying must clone it before destroying it, or the panorama reference is lost.
                animatedSkybox.Configure(null);
                applySkybox.Invoke(animatedSkybox, null);
                var secondRuntime = runtimeField.GetValue(animatedSkybox) as Material;
                Assert.That(secondRuntime, Is.Not.Null);
                Assert.That(secondRuntime, Is.Not.SameAs(firstRuntime));
                Assert.That(secondRuntime.shader, Is.SameAs(source.shader));
                Assert.That(secondRuntime.GetTexture("_SkyPanorama"), Is.SameAs(panorama));
                Assert.That(secondRuntime.GetFloat("_PanoramaBlend"), Is.EqualTo(0.73f).Within(0.001f));
            }
            finally
            {
                RenderSettings.skybox = previousSkybox;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(panorama);
            }
        }

        [Test]
        public void StylizedSkyPaletteKeepsBlueDepthAndWarmHorizonWithoutTextures()
        {
            var shader = Resources.Load<Shader>("Shaders/RasshiineStylizedFantasySkybox");
            using (var materialScope = new MaterialScope(shader))
            {
                var paletteMethod = typeof(AnimatedSkybox).GetMethod(
                    "ApplyFantasyPalette",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(paletteMethod, Is.Not.Null);
                paletteMethod.Invoke(null, new object[] { materialScope.Material });

                var zenith = materialScope.Material.GetColor("_ZenithColor");
                var horizon = materialScope.Material.GetColor("_HorizonColor");
                var horizonGlow = materialScope.Material.GetColor("_HorizonGlowColor");
                var cloudLight = materialScope.Material.GetColor("_CloudLightColor");
                var cloudShadow = materialScope.Material.GetColor("_CloudShadowColor");

                Assert.That(zenith.b, Is.GreaterThan(zenith.r * 8f), "The zenith must remain deeply blue.");
                Assert.That(horizon.b, Is.GreaterThan(zenith.b * 3f), "The horizon must open into atmospheric depth.");
                Assert.That(horizonGlow.r, Is.GreaterThan(horizonGlow.b * 4f), "The horizon glow must read warm.");
                Assert.That(cloudLight.b - cloudShadow.b, Is.GreaterThan(0.35f),
                    "Faceted clouds need a readable lit-face to underside range.");
                Assert.That(materialScope.Material.GetFloat("_RidgeStrength"), Is.LessThan(0.65f),
                    "Procedural ridges must support depth instead of becoming flat foreground walls.");
            }
        }

        [Test]
        public void AnimatedSkyKeepsSunInAnUnobscuredUpperLeftComposition()
        {
            var composeMethod = typeof(AnimatedSkybox).GetMethod(
                "ComposeVisibleSunDirection",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(composeMethod, Is.Not.Null);

            cameraObject.transform.rotation = Quaternion.Euler(18f, 31f, 0f);
            var offscreenDirection = -cameraObject.transform.forward;
            var visibleDirection = (Vector3)composeMethod.Invoke(
                null,
                new object[] { cameraObject.transform, offscreenDirection });
            var local = cameraObject.transform.InverseTransformDirection(visibleDirection);
            var horizontal = local.x / local.z;
            var vertical = local.y / local.z;

            Assert.That(local.z, Is.GreaterThan(0f));
            Assert.That(horizontal, Is.InRange(-0.51f, -0.39f));
            Assert.That(vertical, Is.InRange(0.23f, 0.33f));
        }

        [Test]
        public void HomePresetBuildsAWebGlSafeFilmicStack()
        {
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();
            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Home);

            var cameraData = cameraObject.GetComponent<Camera>().GetUniversalAdditionalCameraData();
            Assert.That(cameraData.renderPostProcessing, Is.True);
            Assert.That(cameraData.antialiasing, Is.EqualTo(AntialiasingMode.FastApproximateAntialiasing));
            Assert.That(cameraData.requiresDepthOption, Is.EqualTo(CameraOverrideOption.Off));
            Assert.That(cameraData.requiresColorOption, Is.EqualTo(CameraOverrideOption.Off));
            Assert.That(director.RuntimeVolume, Is.Not.Null);
            Assert.That(director.RuntimeVolume.isGlobal, Is.True);

            var profile = director.RuntimeVolume.sharedProfile;
            Assert.That(profile.TryGet(out Bloom bloom), Is.True);
            Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
            Assert.That(profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
            Assert.That(profile.TryGet(out Vignette vignette), Is.True);
            Assert.That(profile.TryGet(out DepthOfField depthOfField), Is.False,
                "The WebGL atmosphere stack must avoid depth-dependent blur passes.");
            Assert.That(profile.TryGet(out MotionBlur motionBlur), Is.False);
            Assert.That(profile.TryGet(out ChromaticAberration chromaticAberration), Is.False);
            Assert.That(profile.TryGet(out FilmGrain filmGrain), Is.False);
            Assert.That(profile.components.Count, Is.EqualTo(5));
            Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));
            Assert.That(bloom.downscale.value, Is.EqualTo(BloomDownscaleMode.Quarter));
            Assert.That(bloom.highQualityFiltering.value, Is.False);
            Assert.That(bloom.maxIterations.value, Is.LessThanOrEqualTo(4));
            Assert.That(bloom.intensity.value, Is.InRange(0.18f, 0.25f));
            Assert.That(colorAdjustments.postExposure.value, Is.InRange(0.02f, 0.08f));
            Assert.That(colorAdjustments.contrast.value, Is.InRange(18f, 22f),
                "Contrast must preserve low-poly face separation without crushing the foreground.");
            Assert.That(vignette.intensity.value, Is.InRange(0.09f, 0.13f));

            Assert.That(director.FillLight, Is.Not.Null);
            Assert.That(director.FillLight.type, Is.EqualTo(LightType.Directional));
            Assert.That(director.FillLight.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(director.FillLight.renderMode, Is.EqualTo(LightRenderMode.ForceVertex));
            Assert.That(director.FillLight.intensity, Is.InRange(0.20f, 0.26f));
            var keyLight = keyLightObject.GetComponent<Light>();
            Assert.That(keyLight.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(keyLight.shadowStrength, Is.InRange(0.62f, 0.68f));
            Assert.That(keyLight.shadowBias, Is.InRange(0.015f, 0.030f));
            Assert.That(keyLight.shadowNormalBias, Is.InRange(0.12f, 0.20f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
            Assert.That(RenderSettings.fogStartDistance, Is.GreaterThanOrEqualTo(38f),
                "Fog must preserve foreground contrast and only soften the distant layers.");
            Assert.That(RenderSettings.ambientIntensity, Is.InRange(0.62f, 0.72f));
            Assert.That(RenderSettings.ambientGroundColor.b, Is.InRange(0.09f, 0.12f),
                "Upward-facing foreground shadows need enough cool sky bounce to remain readable.");
        }

        [Test]
        public void LoginPresetKeepsBackFacetsReadableWithoutAddingAnotherShadowMap()
        {
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();
            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Login);

            var keyLight = keyLightObject.GetComponent<Light>();
            Assert.That(keyLight.color.r, Is.GreaterThan(keyLight.color.b), "The key light should remain warm.");
            Assert.That(keyLight.intensity, Is.InRange(1.78f, 1.86f));
            Assert.That(keyLight.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(director.FillLight.color.b, Is.GreaterThan(director.FillLight.color.r),
                "The non-shadowed fill should separate cool back facets from the warm key.");
            Assert.That(director.FillLight.intensity, Is.InRange(0.35f, 0.41f));
            Assert.That(director.FillLight.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(RenderSettings.ambientGroundColor.r, Is.InRange(0.13f, 0.15f));
            Assert.That(RenderSettings.ambientGroundColor.g, Is.InRange(0.18f, 0.20f));
            Assert.That(RenderSettings.ambientGroundColor.b, Is.InRange(0.20f, 0.22f));
            Assert.That(RenderSettings.ambientIntensity, Is.InRange(0.90f, 0.98f));
            Assert.That(keyLight.shadowStrength, Is.InRange(0.49f, 0.55f));

            var profile = director.RuntimeVolume.sharedProfile;
            Assert.That(profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
            Assert.That(colorAdjustments.postExposure.value, Is.InRange(0.15f, 0.22f));
            Assert.That(colorAdjustments.contrast.value, Is.InRange(9f, 15f));

            var visibleSunDirection = -keyLight.transform.forward;
            Assert.That(Vector3.Dot(visibleSunDirection, cameraObject.transform.forward), Is.GreaterThan(0.70f));
            Assert.That(Vector3.Dot(visibleSunDirection, cameraObject.transform.up), Is.GreaterThan(0.20f));
            Assert.That(Vector3.Dot(visibleSunDirection, cameraObject.transform.right), Is.LessThan(-0.45f),
                "The login sun should remain visible in the upper-left sky instead of lighting the scene from off-camera.");
        }

        [Test]
        public void LoginPresetUsesBrightCyanDistanceWithoutHeavyDepthEffects()
        {
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();
            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Login);

            var profile = director.RuntimeVolume.sharedProfile;
            Assert.That(profile.TryGet(out Bloom bloom), Is.True);
            Assert.That(profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
            Assert.That(profile.TryGet(out WhiteBalance whiteBalance), Is.True);
            Assert.That(profile.TryGet(out Vignette vignette), Is.True);
            Assert.That(profile.TryGet(out DepthOfField depthOfField), Is.False);
            Assert.That(profile.TryGet(out MotionBlur motionBlur), Is.False);
            Assert.That(profile.components.Count, Is.EqualTo(5));
            var cameraData = cameraObject.GetComponent<Camera>().GetUniversalAdditionalCameraData();
            Assert.That(cameraData.requiresDepthOption, Is.EqualTo(CameraOverrideOption.Off));
            Assert.That(cameraData.requiresColorOption, Is.EqualTo(CameraOverrideOption.Off));

            Assert.That(colorAdjustments.colorFilter.value.b,
                Is.GreaterThan(colorAdjustments.colorFilter.value.r + 0.12f),
                "The login grade should move the sky toward bright cyan without editing the skybox or geometry.");
            Assert.That(colorAdjustments.postExposure.value, Is.InRange(0.15f, 0.22f));
            Assert.That(colorAdjustments.contrast.value, Is.LessThanOrEqualTo(15f),
                "Login contrast must not crush low-poly back facets.");
            Assert.That(whiteBalance.temperature.value, Is.InRange(1f, 5f));
            Assert.That(vignette.intensity.value, Is.InRange(0.04f, 0.07f));
            Assert.That(bloom.downscale.value, Is.EqualTo(BloomDownscaleMode.Quarter));
            Assert.That(bloom.maxIterations.value, Is.LessThanOrEqualTo(4));

            Assert.That(RenderSettings.fogColor.b, Is.GreaterThan(RenderSettings.fogColor.r * 2f));
            Assert.That(RenderSettings.fogColor.g, Is.GreaterThan(RenderSettings.fogColor.r * 1.7f));
            Assert.That(RenderSettings.fogStartDistance, Is.GreaterThanOrEqualTo(42f));
            Assert.That(RenderSettings.fogEndDistance, Is.GreaterThanOrEqualTo(115f));
            Assert.That(RenderSettings.fogEndDistance - RenderSettings.fogStartDistance, Is.GreaterThanOrEqualTo(70f),
                "The login fog must remain a thin distant haze rather than flatten the foreground.");

            var keyLight = keyLightObject.GetComponent<Light>();
            Assert.That(keyLight.color.r, Is.GreaterThan(keyLight.color.b * 1.8f));
            Assert.That(director.FillLight.color.b, Is.GreaterThan(director.FillLight.color.r * 3.5f));
            Assert.That(director.FillLight.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(new[] { keyLight, director.FillLight }
                .Count(light => light != null && light.shadows != LightShadows.None), Is.EqualTo(1),
                "The lightweight login atmosphere may allocate only one shadow-casting light.");
        }

        [Test]
        public void HomeAndBattlePresetsKeepIndependentAtmosphereValues()
        {
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();

            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Home);
            var profile = director.RuntimeVolume.sharedProfile;
            Assert.That(profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
            Assert.That(profile.TryGet(out Vignette vignette), Is.True);
            Assert.That(colorAdjustments.postExposure.value, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(colorAdjustments.contrast.value, Is.EqualTo(20f).Within(0.001f));
            Assert.That(vignette.intensity.value, Is.EqualTo(0.115f).Within(0.001f));
            Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(40f).Within(0.001f));
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(98f).Within(0.001f));
            Assert.That(keyLightObject.GetComponent<Light>().intensity, Is.EqualTo(1.50f).Within(0.001f));
            Assert.That(keyLightObject.GetComponent<Light>().shadowStrength, Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(director.FillLight.intensity, Is.EqualTo(0.22f).Within(0.001f));

            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Battle);
            Assert.That(director.RuntimeVolume.enabled, Is.False,
                "The battle preset must not allocate or execute the HDR grading stack.");
            Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(13.5f).Within(0.001f));
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(43f).Within(0.001f));
            Assert.That(RenderSettings.fogColor.b, Is.GreaterThan(RenderSettings.fogColor.r * 3f));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.88f).Within(0.001f));
            Assert.That(RenderSettings.ambientGroundColor.r, Is.GreaterThan(RenderSettings.ambientGroundColor.b),
                "Warm ground ambient must support the coral canyon instead of washing it blue.");
            Assert.That(RenderSettings.ambientGroundColor.maxColorComponent, Is.LessThanOrEqualTo(0.31f),
                "The ambient floor must not flatten the boss shell or bleach the coral ground.");
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(keyLightObject.GetComponent<Light>().intensity, Is.EqualTo(1.36f).Within(0.001f));
            Assert.That(keyLightObject.GetComponent<Light>().shadowStrength, Is.EqualTo(0.62f).Within(0.001f),
                "One crisp shadow map supplies the grounded toy-diorama contact shadows.");
            Assert.That(keyLightObject.GetComponent<Light>().shadows, Is.EqualTo(LightShadows.Hard));
            Assert.That(keyLightObject.GetComponent<Light>().shadowBias, Is.LessThanOrEqualTo(0.02f));
            Assert.That(keyLightObject.GetComponent<Light>().shadowNormalBias, Is.LessThanOrEqualTo(0.13f));
            Assert.That(director.FillLight.intensity, Is.EqualTo(0.20f).Within(0.001f));
            Assert.That(director.FillLight.color.b, Is.GreaterThan(director.FillLight.color.r * 3.5f));
            Assert.That(cameraObject.GetComponent<Camera>().allowHDR, Is.False,
                "Battle uses an explicitly LDR-safe grade so Editor and WebGL do not diverge.");
            Assert.That(cameraObject.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing, Is.False);
        }

        [Test]
        public void BattleLightingKeepsWarmCoralCoolShadowsAndBossFacetsSeparatedInLdr()
        {
            cameraObject.transform.rotation = Quaternion.Euler(13f, 0f, 0f);
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();

            ApplyPresetForPlatform(
                director,
                NeonCityBackdrop.BackdropPreset.Battle,
                RuntimePlatform.WebGLPlayer);

            var camera = cameraObject.GetComponent<Camera>();
            var keyLight = keyLightObject.GetComponent<Light>();
            var visibleKeyDirection = -keyLight.transform.forward;
            var localKeyDirection = camera.transform.InverseTransformDirection(visibleKeyDirection);

            Assert.That(camera.allowHDR, Is.False);
            Assert.That(keyLight.color.r, Is.GreaterThan(keyLight.color.g));
            Assert.That(keyLight.color.g, Is.GreaterThan(keyLight.color.b),
                "A restrained warm key separates coral highlights from the purple shell without yellowing whites.");
            Assert.That(director.FillLight.color.b, Is.GreaterThan(director.FillLight.color.g));
            Assert.That(director.FillLight.color.g, Is.GreaterThan(director.FillLight.color.r),
                "The shadowless blue fill preserves readable black and violet back facets.");
            Assert.That(keyLight.intensity, Is.GreaterThan(director.FillLight.intensity * 6f));
            Assert.That(keyLight.shadowStrength, Is.GreaterThanOrEqualTo(0.58f));
            Assert.That(localKeyDirection.x / localKeyDirection.z, Is.EqualTo(-0.58f).Within(0.02f));
            Assert.That(localKeyDirection.y / localKeyDirection.z, Is.EqualTo(0.92f).Within(0.02f));
            Assert.That(RenderSettings.ambientSkyColor.b, Is.GreaterThan(RenderSettings.ambientSkyColor.r));
            Assert.That(RenderSettings.ambientGroundColor.r, Is.GreaterThan(RenderSettings.ambientGroundColor.b));
            Assert.That(RenderSettings.fogEndDistance - RenderSettings.fogStartDistance, Is.GreaterThanOrEqualTo(28f),
                "A broad linear ramp separates distant mesas without fogging the foreground party or boss.");
            Assert.That(new[] { keyLight, director.FillLight }
                .Count(light => light != null && light.shadows != LightShadows.None), Is.EqualTo(1),
                "WebGL battle lighting is limited to one hard shadow map.");
        }

        [Test]
        public void BattleSkyboxUsesBoundedProceduralDaylightAndRestoresExplorationPalette()
        {
            var shader = Resources.Load<Shader>("Shaders/RasshiineStylizedFantasySkybox");
            Assert.That(shader, Is.Not.Null);
            var originalSkybox = RenderSettings.skybox;
            var skybox = new Material(shader)
            {
                name = "Battle Skybox Palette Test"
            };
            skybox.SetFloat("_PanoramaBlend", 0.67f);
            skybox.SetFloat("_Exposure", 1.16f);
            skybox.SetColor("_SkyColor", new Color(0.07f, 0.21f, 0.49f, 1f));
            var originalSunDirection = new Vector4(0.16f, 0.27f, 0.94f, 0f);
            skybox.SetVector("_SunDirection", originalSunDirection);
            RenderSettings.skybox = skybox;

            try
            {
                var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();
                director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Battle);

                Assert.That(skybox.GetFloat("_PanoramaBlend"), Is.EqualTo(0f).Within(0.001f),
                    "Battle must use the texture-free sky instead of a camera-local panorama plate.");
                Assert.That(skybox.GetFloat("_Exposure"), Is.EqualTo(1.05f).Within(0.001f));
                Assert.That(skybox.GetFloat("_CloudCoverage"), Is.EqualTo(0f).Within(0.001f));
                Assert.That(skybox.GetFloat("_CloudOpacity"), Is.EqualTo(0f).Within(0.001f),
                    "The full-screen procedural cloud cell is replaced by bounded opaque mesh clusters.");
                Assert.That(skybox.GetFloat("_CloudScale"), Is.InRange(3.30f, 3.50f),
                    "Higher-frequency cells prevent a single cloud facet covering the upper screen.");
                Assert.That(skybox.GetFloat("_SunDiskSize"), Is.EqualTo(0.030f).Within(0.001f));
                Assert.That(skybox.GetFloat("_SunHaloSize"), Is.EqualTo(0.15f).Within(0.001f));
                Assert.That(skybox.GetFloat("_RidgeStrength"), Is.LessThanOrEqualTo(0.06f),
                    "Controller-owned canyon mesas must not be duplicated by a heavy sky ridge.");
                Assert.That(skybox.GetColor("_SkyColor").b, Is.GreaterThan(skybox.GetColor("_SkyColor").r * 40f));
                Assert.That(skybox.GetColor("_HorizonColor").maxColorComponent, Is.LessThanOrEqualTo(1f));
                Assert.That(skybox.GetColor("_CloudLightColor").maxColorComponent, Is.LessThanOrEqualTo(1f));
                var battleSunDirection = skybox.GetVector("_SunDirection");
                Assert.That(battleSunDirection.x / battleSunDirection.z, Is.EqualTo(-0.58f).Within(0.02f));
                Assert.That(battleSunDirection.y / battleSunDirection.z, Is.EqualTo(0.92f).Within(0.02f),
                    "The upper-left key needs enough rake to form readable contact shadows without hiding the open canyon view.");

                director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Home);
                Assert.That(skybox.GetFloat("_PanoramaBlend"), Is.EqualTo(0.67f).Within(0.001f));
                Assert.That(skybox.GetFloat("_Exposure"), Is.EqualTo(1.16f).Within(0.001f));
                Assert.That(skybox.GetColor("_SkyColor").b, Is.EqualTo(0.49f).Within(0.001f));
                Assert.That(skybox.GetVector("_SunDirection"), Is.EqualTo(originalSunDirection));
            }
            finally
            {
                RenderSettings.skybox = originalSkybox;
                Object.DestroyImmediate(skybox);
            }
        }

        [Test]
        public void BattleUsesSkyboxWithoutCameraLocalPanoramaPlate()
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();

            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Battle);

            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(cameraObject.transform.Find("Fantasy Battle Sky Panorama"), Is.Null,
                "A static camera-local plate destroys depth and must not be reintroduced.");
        }

        [Test]
        public void ParticipantMaterialClonePreservesAtlasWithBoundedSimpleLighting()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var source = new Material(shader)
            {
                name = "Unbounded Participant Material"
            };
            source.SetColor("_BaseColor", new Color(1.7f, 1.25f, 0.94f, 1f));
            source.SetTexture("_BaseMap", texture);
            source.SetFloat("_Metallic", 1f);
            source.SetFloat("_Smoothness", 1f);
            source.EnableKeyword("_EMISSION");
            source.SetColor("_EmissionColor", new Color(4f, 8f, 12f, 1f));

            Material bounded = null;
            try
            {
                var cloneMethod = typeof(RaidBattleController).GetMethod(
                    "TryGetCompatibleParticipantMaterial",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(cloneMethod, Is.Not.Null);
                var arguments = new object[] { source, null };
                Assert.That((bool)cloneMethod.Invoke(null, arguments), Is.True);
                bounded = arguments[1] as Material;

                Assert.That(bounded, Is.Not.Null);
                Assert.That(bounded, Is.Not.SameAs(source));
                Assert.That(bounded.shader.name, Is.EqualTo("Universal Render Pipeline/Simple Lit"));
                Assert.That(bounded.GetTexture("_BaseMap"), Is.SameAs(texture));
                Assert.That(bounded.GetColor("_BaseColor").maxColorComponent, Is.LessThanOrEqualTo(0.86f));
                Assert.That(bounded.GetFloat("_Smoothness"), Is.LessThanOrEqualTo(0.08f));
                Assert.That(bounded.GetColor("_SpecColor").maxColorComponent, Is.EqualTo(0f).Within(0.001f));
                Assert.That(bounded.GetColor("_EmissionColor").maxColorComponent, Is.EqualTo(0f).Within(0.001f));
                Assert.That(bounded.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.False);
                Assert.That(bounded.IsKeywordEnabled("_SPECGLOSSMAP"), Is.False);
                Assert.That(bounded.IsKeywordEnabled("_SPECULAR_COLOR"), Is.False);
                Assert.That(bounded.IsKeywordEnabled("_EMISSION"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(texture);
                if (bounded != null)
                {
                    Object.DestroyImmediate(bounded);
                }
            }
        }

        [Test]
        public void ParticipantNormalizationClearsRendererPropertyOverrides()
        {
            var controllerObject = new GameObject("Participant Normalization Test", typeof(RaidBattleController));
            var participant = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = participant.GetComponent<Renderer>();
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor("_BaseColor", new Color(4f, 8f, 12f, 1f));
            propertyBlock.SetColor("_EmissionColor", new Color(12f, 12f, 12f, 1f));
            renderer.SetPropertyBlock(propertyBlock);
            Assert.That(renderer.HasPropertyBlock(), Is.True);

            try
            {
                var normalize = typeof(RaidBattleController).GetMethod(
                    "NormalizeParticipantMaterials",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(normalize, Is.Not.Null);
                normalize.Invoke(controllerObject.GetComponent<RaidBattleController>(), new object[] { participant, 0 });

                Assert.That(renderer.HasPropertyBlock(), Is.False);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Simple Lit"));
            }
            finally
            {
                Object.DestroyImmediate(participant);
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void RuntimeBossUsesTextureFreeLowPolyMaterialWithWarmAndCoolFacetSeparation()
        {
            var factory = typeof(RaidBattleController).GetMethod(
                "ResolveRuntimeBossMaterial",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(factory, Is.Not.Null);
            var material = factory.Invoke(null, null) as Material;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Rasshiine/Low Poly Environment"));
            Assert.That(material.HasProperty("_MainTex"), Is.False);
            Assert.That(material.HasProperty("_BaseMap"), Is.False);
            Assert.That(material.GetColor("_BaseColor").a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(material.GetColor("_BaseColor").b, Is.InRange(0.76f, 0.80f));
            Assert.That(material.GetColor("_BaseColor").r, Is.GreaterThan(material.GetColor("_BaseColor").g * 1.8f),
                "The boss body should read violet rather than generic blue-grey stone.");
            Assert.That(material.GetFloat("_AmbientStrength"), Is.EqualTo(1.12f).Within(0.001f));
            Assert.That(material.GetFloat("_RimStrength"), Is.InRange(0.09f, 0.13f));
            Assert.That(material.GetColor("_LightTint").r, Is.GreaterThan(material.GetColor("_LightTint").b));
            Assert.That(material.GetColor("_ShadowTint").b, Is.GreaterThan(material.GetColor("_ShadowTint").r));
            Assert.That(material.GetColor("_EmissionColor").maxColorComponent, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void LoginBattleHomeTransitionRestoresAuthoredAccentLightStates()
        {
            var blueRim = new GameObject("Blue Rim Light", typeof(Light));
            var cyanPortal = new GameObject("Cyan Portal Light", typeof(Light));
            var blueLight = blueRim.GetComponent<Light>();
            var cyanLight = cyanPortal.GetComponent<Light>();
            blueLight.type = LightType.Point;
            blueLight.enabled = false;
            blueLight.shadows = LightShadows.Soft;
            cyanLight.type = LightType.Point;
            cyanLight.enabled = true;
            cyanLight.shadows = LightShadows.Hard;
            blueRim.SetActive(true);
            cyanPortal.SetActive(false);

            try
            {
                var applyLighting = typeof(NeonCityBackdrop).GetMethod(
                    "ApplyLightingPreset",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(applyLighting, Is.Not.Null);

                applyLighting.Invoke(null, new object[] { NeonCityBackdrop.BackdropPreset.Login });
                Assert.That(blueRim.activeSelf, Is.True,
                    "Login must not disable the authored rim light GameObject.");
                Assert.That(blueLight.enabled, Is.False,
                    "Login must preserve an intentionally disabled Light component.");
                Assert.That(cyanPortal.activeSelf, Is.False,
                    "Login must preserve an intentionally inactive portal light GameObject.");
                Assert.That(cyanLight.enabled, Is.True);

                applyLighting.Invoke(null, new object[] { NeonCityBackdrop.BackdropPreset.Battle });

                Assert.That(blueRim.activeSelf, Is.False);
                Assert.That(cyanPortal.activeSelf, Is.False);
                Assert.That(blueLight.enabled, Is.False);
                Assert.That(cyanLight.enabled, Is.False);
                Assert.That(blueLight.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(cyanLight.shadows, Is.EqualTo(LightShadows.None));

                applyLighting.Invoke(null, new object[] { NeonCityBackdrop.BackdropPreset.Home });
                Assert.That(blueRim.activeSelf, Is.True);
                Assert.That(blueLight.enabled, Is.False);
                Assert.That(blueLight.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(cyanPortal.activeSelf, Is.False);
                Assert.That(cyanLight.enabled, Is.True);
                Assert.That(cyanLight.shadows, Is.EqualTo(LightShadows.Hard));
            }
            finally
            {
                var applyLighting = typeof(NeonCityBackdrop).GetMethod(
                    "ApplyLightingPreset",
                    BindingFlags.NonPublic | BindingFlags.Static);
                applyLighting?.Invoke(null, new object[] { NeonCityBackdrop.BackdropPreset.Home });
                Object.DestroyImmediate(blueRim);
                Object.DestroyImmediate(cyanPortal);
            }
        }

        [Test]
        public void DedicatedBattlePresetSkipsHdrVolumeAllocation()
        {
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();
            Assert.That(director.RuntimeVolume, Is.Null);

            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Battle);

            Assert.That(director.RuntimeVolume, Is.Null);
            Assert.That(cameraObject.GetComponent<Camera>().allowHDR, Is.False);
            Assert.That(cameraObject.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing, Is.False);
        }

        [TestCase(NeonCityBackdrop.BackdropPreset.Login)]
        [TestCase(NeonCityBackdrop.BackdropPreset.Home)]
        public void WebGlExplorationPresetsSkipRuntimeVolumeAllocation(NeonCityBackdrop.BackdropPreset preset)
        {
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();

            ApplyPresetForPlatform(director, preset, RuntimePlatform.WebGLPlayer);

            var camera = cameraObject.GetComponent<Camera>();
            var cameraData = camera.GetUniversalAdditionalCameraData();
            Assert.That(director.RuntimeVolume, Is.Null,
                "WebGL Login/Home must not create a runtime Volume, Profile, Bloom, or grading components.");
            Assert.That(cameraObject.GetComponentInChildren<Volume>(true), Is.Null);
            Assert.That(camera.allowHDR, Is.False);
            Assert.That(cameraData.renderPostProcessing, Is.False);
            Assert.That(cameraData.antialiasing, Is.EqualTo(AntialiasingMode.None));
            Assert.That(cameraData.dithering, Is.False);
        }

        [Test]
        public void SwitchingAnExistingNativeVolumeToWebGlDisablesIt()
        {
            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();
            ApplyPresetForPlatform(
                director,
                NeonCityBackdrop.BackdropPreset.Home,
                RuntimePlatform.OSXEditor);
            var nativeVolume = director.RuntimeVolume;
            Assert.That(nativeVolume, Is.Not.Null);
            Assert.That(nativeVolume.enabled, Is.True);

            ApplyPresetForPlatform(
                director,
                NeonCityBackdrop.BackdropPreset.Login,
                RuntimePlatform.WebGLPlayer);

            Assert.That(director.RuntimeVolume, Is.SameAs(nativeVolume));
            Assert.That(nativeVolume.enabled, Is.False,
                "An already-created runtime Volume must not execute after the platform path disables post-processing.");
            Assert.That(cameraObject.GetComponent<Camera>().allowHDR, Is.False);
            Assert.That(cameraObject.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing, Is.False);
        }

        [Test]
        public void BattleArenaAndBackdropUseBoundedLowPolyMaterials()
        {
            var arenaFactory = typeof(RaidBattleController).GetMethod(
                "GetNatureMaterial",
                BindingFlags.NonPublic | BindingFlags.Static);
            var backdropMaterialBuilder = typeof(NeonCityBackdrop).GetMethod(
                "BuildNatureMaterials",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(arenaFactory, Is.Not.Null);
            Assert.That(backdropMaterialBuilder, Is.Not.Null);

            var arenaColor = new Color(0.27f, 0.46f, 0.22f, 1f);
            var backdropColor = new Color(0.55f, 0.72f, 0.32f, 1f);
            var arenaMaterial = arenaFactory.Invoke(
                null,
                new object[] { "battle-polish-test", arenaColor, 0.16f }) as Material;
            var backdropMaterials = backdropMaterialBuilder.Invoke(
                null,
                new object[] { "Battle Grass", "Battle Grass", new Material[0], true }) as Material[];

            AssertBattleMaterial(arenaMaterial, arenaColor);
            Assert.That(backdropMaterials, Has.Length.EqualTo(1));
            AssertBattleMaterial(backdropMaterials[0], backdropColor);
        }

        [Test]
        public void BattlePeaksUseHazeReadableMountainMaterialInsteadOfGrassSilhouettes()
        {
            var classify = typeof(NeonCityBackdrop).GetMethod(
                "GuessNatureMaterialKey",
                BindingFlags.NonPublic | BindingFlags.Static);
            var palette = typeof(NeonCityBackdrop).GetMethod(
                "ColorForBattleNatureKey",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(classify, Is.Not.Null);
            Assert.That(palette, Is.Not.Null);

            var key = classify.Invoke(
                null,
                new object[] { "Fantasy Battle Ridge Far Left", "Mesh Renderer", string.Empty, 0 }) as string;
            var color = (Color)palette.Invoke(null, new object[] { key });
            Assert.That(key, Is.EqualTo("mountain"));
            Assert.That(color.b, Is.GreaterThan(color.r));
            Assert.That(color.r, Is.GreaterThanOrEqualTo(0.48f),
                "The camera-facing ridge must retain blue-grey detail instead of becoming a black cone.");
        }

        [Test]
        public void WebGlUsesOneHardShadowCasterWhileCoolFillRemainsShadowless()
        {
            var selectMethod = typeof(FantasyAtmosphereDirector).GetMethod(
                "SelectKeyShadowMode",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(selectMethod, Is.Not.Null);

            var webGlMode = (LightShadows)selectMethod.Invoke(null, new object[] { RuntimePlatform.WebGLPlayer });
            var editorMode = (LightShadows)selectMethod.Invoke(null, new object[] { RuntimePlatform.OSXEditor });

            Assert.That(webGlMode, Is.EqualTo(LightShadows.Hard));
            Assert.That(editorMode, Is.EqualTo(LightShadows.Soft));

            var director = cameraObject.AddComponent<FantasyAtmosphereDirector>();
            director.ApplyPreset(NeonCityBackdrop.BackdropPreset.Login);
            Assert.That(director.FillLight.shadows, Is.EqualTo(LightShadows.None));
        }

        [Test]
        public void WebGlDisablesUnsupportedPostStackAndUsesBilinearUpscaling()
        {
            var shouldEnablePost = typeof(FantasyAtmosphereDirector).GetMethod(
                "ShouldEnablePostProcessing",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(shouldEnablePost, Is.Not.Null);
            Assert.That((bool)shouldEnablePost.Invoke(null, new object[]
            {
                NeonCityBackdrop.BackdropPreset.Login,
                RuntimePlatform.WebGLPlayer
            }), Is.False,
                "WebGL must not allocate a post stack that the target browser GPU refuses to execute.");
            Assert.That((bool)shouldEnablePost.Invoke(null, new object[]
            {
                NeonCityBackdrop.BackdropPreset.Home,
                RuntimePlatform.OSXEditor
            }), Is.True);
            Assert.That((bool)shouldEnablePost.Invoke(null, new object[]
            {
                NeonCityBackdrop.BackdropPreset.Battle,
                RuntimePlatform.OSXEditor
            }), Is.False,
                "Battle remains on the LDR-safe path even on native Editor platforms.");

            var mobilePipeline = File.ReadAllText("Assets/Settings/Mobile_RPAsset.asset");
            Assert.That(mobilePipeline, Does.Contain("m_RenderScale: 0.8"));
            Assert.That(mobilePipeline, Does.Contain("m_UpscalingFilter: 1"),
                "WebGL should use the supported bilinear scaler instead of automatically selecting unsupported FSR.");
            var mobileRenderer = File.ReadAllText("Assets/Settings/Mobile_Renderer.asset");
            Assert.That(mobileRenderer, Does.Contain("postProcessData: {fileID: 0}"),
                "WebGL should not instantiate unused post passes, including the unsupported FSR pass, when runtime post-processing is disabled.");

            var skyboxShader = File.ReadAllText("Assets/Resources/Shaders/RasshiineStylizedFantasySkybox.shader");
            Assert.That(skyboxShader, Does.Contain("if (_PanoramaBlend > 0.001h)"),
                "The texture-free Battle sky must skip the resident panorama sample, not merely blend its result away.");
        }

        private static void ApplyPresetForPlatform(
            FantasyAtmosphereDirector director,
            NeonCityBackdrop.BackdropPreset preset,
            RuntimePlatform platform)
        {
            var applyPreset = typeof(FantasyAtmosphereDirector).GetMethod(
                "ApplyPresetForPlatform",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(applyPreset, Is.Not.Null);
            applyPreset.Invoke(director, new object[] { preset, platform });
        }

        private static void AssertBattleMaterial(Material material, Color expectedColor)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Rasshiine/Low Poly Environment"));
            var actualColor = material.GetColor("_BaseColor");
            Assert.That(actualColor.r, Is.EqualTo(expectedColor.r).Within(0.001f));
            Assert.That(actualColor.g, Is.EqualTo(expectedColor.g).Within(0.001f));
            Assert.That(actualColor.b, Is.EqualTo(expectedColor.b).Within(0.001f));
            Assert.That(actualColor.a, Is.EqualTo(expectedColor.a).Within(0.001f));
            Assert.That(material.GetFloat("_FacetSteps"), Is.InRange(3f, 4f));
            Assert.That(material.GetFloat("_AmbientStrength"), Is.LessThanOrEqualTo(0.86f));
            Assert.That(material.GetFloat("_RimStrength"), Is.LessThanOrEqualTo(0.09f));
            Assert.That(material.GetColor("_EmissionColor").maxColorComponent, Is.EqualTo(0f).Within(0.001f));
            Assert.That(material.GetColor("_LightTint").r, Is.GreaterThan(material.GetColor("_LightTint").b),
                "The battle palette needs a warm key against its cool shadow tint.");
            Assert.That(material.GetColor("_ShadowTint").b, Is.GreaterThan(material.GetColor("_ShadowTint").r),
                "Back facets need cool depth without a realtime punctual rim light.");
        }

        private sealed class MaterialScope : System.IDisposable
        {
            public MaterialScope(Shader shader)
            {
                Material = new Material(shader);
            }

            public Material Material { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Material);
            }
        }
    }
}
