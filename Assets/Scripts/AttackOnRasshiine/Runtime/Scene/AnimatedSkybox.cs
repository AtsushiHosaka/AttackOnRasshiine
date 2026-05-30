using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class AnimatedSkybox : MonoBehaviour
    {
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private float rotationSpeed = 2.35f;
        [SerializeField] private float exposurePulseAmplitude = 0.045f;
        [SerializeField] private float exposurePulseSpeed = 0.32f;
        [SerializeField] private Light sunLight;
        [SerializeField] private float sunRotationSpeed = 0.75f;
        [SerializeField] private bool updateAmbientProbe = true;
        [SerializeField] private float ambientProbeInterval = 1.1f;

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
                rotation = Mathf.Repeat(rotation + rotationSpeed * Time.deltaTime, 360f);
                runtimeSkybox.SetFloat(RotationId, rotation);
            }

            if (runtimeSkybox.HasProperty(ExposureId))
            {
                var pulse = Mathf.Sin(Time.time * exposurePulseSpeed) * exposurePulseAmplitude;
                runtimeSkybox.SetFloat(ExposureId, Mathf.Max(0.01f, baseExposure + pulse));
            }

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.AngleAxis(Time.time * sunRotationSpeed, Vector3.up) * initialSunRotation;
                RenderSettings.sun = sunLight;
            }

            if (!updateAmbientProbe)
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
            if (source == null)
            {
                return;
            }

            DestroyRuntimeSkybox();
            runtimeSkybox = new Material(source)
            {
                name = $"{source.name} (Runtime Animated)"
            };

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
