using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class AnimatedSkybox : MonoBehaviour
    {
        private const string StylizedSkyboxResourcePath = "Shaders/RasshiineStylizedFantasySkybox";
        private const string StylizedSkyboxShaderName = "Rasshiine/Stylized Fantasy Skybox";
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private const float SafeSunLeftMin = -0.50f;
        private const float SafeSunLeftMax = -0.40f;
        private const float SafeSunHeightMin = 0.24f;
        private const float SafeSunHeightMax = 0.32f;

        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private float rotationSpeed = 0.012f;
        [SerializeField] private float exposurePulseAmplitude = 0.004f;
        [SerializeField] private float exposurePulseSpeed = 0.12f;
        [SerializeField] private Light sunLight;
        [SerializeField] private float sunRotationSpeed = 0.006f;
        [SerializeField] private bool updateAmbientProbe = true;
        [SerializeField] private float ambientProbeInterval = 10f;

        private Material runtimeSkybox;
        private float baseExposure = 1f;
        private float rotation;
        private float ambientProbeTimer;
        private Quaternion initialSunRotation = Quaternion.identity;

        public void Configure(Material material, float speed = -1f, Light sun = null)
        {
            skyboxMaterial = material;
            if (sun != null)
            {
                sunLight = sun;
            }

            if (speed >= 0f)
            {
                rotationSpeed = speed;
            }

            if (Application.isPlaying && isActiveAndEnabled)
            {
                ApplySkybox();
            }
        }

        public void SynchronizeSun(Light sun)
        {
            if (sun == null)
            {
                return;
            }

            sunLight = sun;
            initialSunRotation = sun.transform.rotation;
            RenderSettings.sun = sun;
            ApplyVisibleSunDirection();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ResolveSunLight();
                ApplySkybox();
            }
        }

        private void OnDisable()
        {
            DestroyRuntimeSkybox();
        }

        private void Update()
        {
            if (runtimeSkybox == null)
            {
                ApplySkybox();
            }

            if (runtimeSkybox == null)
            {
                return;
            }

            if (runtimeSkybox.HasProperty(RotationId))
            {
                // Keep the authored sun/cloud composition stable. Cloud drift is handled in the
                // shader, while this very slow rotation only prevents the sky from feeling frozen.
                var restrainedRotationSpeed = Mathf.Min(Mathf.Abs(rotationSpeed), 0.018f);
                rotation = Mathf.Repeat(rotation + restrainedRotationSpeed * Time.deltaTime, 360f);
                runtimeSkybox.SetFloat(RotationId, rotation);
            }

            if (runtimeSkybox.HasProperty(ExposureId))
            {
                var restrainedPulseAmplitude = Mathf.Min(Mathf.Abs(exposurePulseAmplitude), 0.006f);
                var pulse = Mathf.Sin(Time.time * exposurePulseSpeed) * restrainedPulseAmplitude;
                runtimeSkybox.SetFloat(ExposureId, Mathf.Max(0.01f, baseExposure + pulse));
            }

            if (sunLight != null)
            {
                // Fast sun motion makes contact shadows visibly swim across the low-poly facets.
                var restrainedRotationSpeed = Mathf.Min(Mathf.Abs(sunRotationSpeed), 0.008f);
                sunLight.transform.rotation = Quaternion.AngleAxis(Time.time * restrainedRotationSpeed, Vector3.up) * initialSunRotation;
                RenderSettings.sun = sunLight;
                ApplyVisibleSunDirection();
            }

            if (!updateAmbientProbe || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return;
            }

            ambientProbeTimer += Time.deltaTime;
            if (ambientProbeTimer >= ambientProbeInterval)
            {
                ambientProbeTimer = 0f;
                DynamicGI.UpdateEnvironment();
            }
        }

        private void ApplySkybox()
        {
            var source = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
            var stylizedShader = Resources.Load<Shader>(StylizedSkyboxResourcePath) ?? Shader.Find(StylizedSkyboxShaderName);
            var proceduralShader = Shader.Find("Skybox/Procedural");
            if (source == null && stylizedShader == null && proceduralShader == null)
            {
                return;
            }

            Material nextSkybox;
            if (stylizedShader != null)
            {
                nextSkybox = source != null &&
                             source.shader != null &&
                             source.shader.name == StylizedSkyboxShaderName
                    ? new Material(source)
                    : new Material(stylizedShader);
                nextSkybox.name = "Rasshiine Stylized Fantasy Sky (Runtime)";
            }
            else
            {
                nextSkybox = source != null
                    ? new Material(source)
                    : new Material(proceduralShader);
                nextSkybox.name = source != null
                    ? $"{source.name} (Runtime Fantasy Sky)"
                    : "Runtime Fantasy Sky";
            }

            // Clone before destroying the previous runtime material. When no serialized source is
            // assigned, RenderSettings.skybox can itself be that runtime material; destroying it
            // first would discard the generated panorama texture on a re-apply.
            DestroyRuntimeSkybox();
            runtimeSkybox = nextSkybox;
            ApplyFantasyPalette(runtimeSkybox);

            if (runtimeSkybox.HasProperty(RotationId))
            {
                rotation = runtimeSkybox.GetFloat(RotationId);
            }

            if (runtimeSkybox.HasProperty(ExposureId))
            {
                baseExposure = runtimeSkybox.GetFloat(ExposureId);
            }

            RenderSettings.skybox = runtimeSkybox;
            ambientProbeTimer = ambientProbeInterval;
            ResolveSunLight();
            ApplyVisibleSunDirection();
        }

        private static void ApplyFantasyPalette(Material material)
        {
            SetFloatIfPresent(material, "_SunSize", 0.040f);
            SetFloatIfPresent(material, "_SunSizeConvergence", 2.0f);
            SetFloatIfPresent(material, "_AtmosphereThickness", 0.76f);
            SetColorIfPresent(material, "_SkyTint", new Color(0.075f, 0.29f, 0.60f, 1f));
            SetColorIfPresent(material, "_GroundColor", new Color(0.055f, 0.15f, 0.28f, 1f));
            SetColorIfPresent(material, "_Tint", new Color(0.55f, 0.76f, 0.98f, 1f));
            SetColorIfPresent(material, "_ZenithColor", new Color(0.010f, 0.050f, 0.17f, 1f));
            SetColorIfPresent(material, "_SkyColor", new Color(0.025f, 0.22f, 0.56f, 1f));
            SetColorIfPresent(material, "_HorizonColor", new Color(0.32f, 0.64f, 0.94f, 1f));
            SetColorIfPresent(material, "_HorizonGlowColor", new Color(1.0f, 0.49f, 0.18f, 1f));
            SetColorIfPresent(material, "_SunColor", new Color(1.0f, 0.91f, 0.68f, 1f));
            SetColorIfPresent(material, "_CloudLightColor", new Color(0.90f, 0.96f, 1.0f, 1f));
            SetColorIfPresent(material, "_CloudShadowColor", new Color(0.24f, 0.42f, 0.62f, 1f));
            SetColorIfPresent(material, "_CloudWarmColor", new Color(1.0f, 0.66f, 0.31f, 1f));
            SetColorIfPresent(material, "_FarRidgeColor", new Color(0.34f, 0.56f, 0.76f, 1f));
            SetColorIfPresent(material, "_NearRidgeColor", new Color(0.16f, 0.34f, 0.55f, 1f));
            SetFloatIfPresent(material, "_SunDiskSize", 0.055f);
            SetFloatIfPresent(material, "_SunHaloSize", 0.42f);
            SetFloatIfPresent(material, "_HorizonGlow", 0.90f);
            SetFloatIfPresent(material, "_CloudCoverage", 0.52f);
            SetFloatIfPresent(material, "_CloudOpacity", 0.88f);
            SetFloatIfPresent(material, "_CloudScale", 1.02f);
            SetFloatIfPresent(material, "_CloudSpeed", 0.008f);
            SetFloatIfPresent(material, "_RidgeStrength", 0.32f);
            SetFloatIfPresent(material, "_Exposure", 1.12f);
        }

        private void ApplyVisibleSunDirection()
        {
            if (runtimeSkybox == null || sunLight == null || !runtimeSkybox.HasProperty(SunDirectionId))
            {
                return;
            }

            var sunDirection = ComposeVisibleSunDirection(transform, -sunLight.transform.forward);
            runtimeSkybox.SetVector(SunDirectionId, new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
        }

        // Keep the rendered disk in the open upper-left sky. The directional light remains within
        // a few degrees of this composition, so the visible sun and the facet lighting still agree.
        private static Vector3 ComposeVisibleSunDirection(Transform cameraTransform, Vector3 litSunDirection)
        {
            if (cameraTransform == null)
            {
                return litSunDirection.normalized;
            }

            var localDirection = cameraTransform.InverseTransformDirection(litSunDirection.normalized);
            if (localDirection.z <= 0.10f)
            {
                localDirection = new Vector3(SafeSunLeftMax, SafeSunHeightMax, 1f);
            }
            else
            {
                var inverseDepth = 1f / localDirection.z;
                var horizontal = Mathf.Clamp(localDirection.x * inverseDepth, SafeSunLeftMin, SafeSunLeftMax);
                var vertical = Mathf.Clamp(localDirection.y * inverseDepth, SafeSunHeightMin, SafeSunHeightMax);
                localDirection = new Vector3(horizontal, vertical, 1f);
            }

            return cameraTransform.TransformDirection(localDirection.normalized);
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

        private void ResolveSunLight()
        {
            if (sunLight == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (var i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional)
                    {
                        sunLight = lights[i];
                        break;
                    }
                }
            }

            if (sunLight == null)
            {
                return;
            }

            initialSunRotation = sunLight.transform.rotation;
            RenderSettings.sun = sunLight;
        }

        private void DestroyRuntimeSkybox()
        {
            if (runtimeSkybox == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeSkybox);
            }
            else
            {
                DestroyImmediate(runtimeSkybox);
            }

            runtimeSkybox = null;
        }
    }
}
