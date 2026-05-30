using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class NeonCityBackdrop : MonoBehaviour
    {
        public enum BackdropPreset
        {
            Login,
            Home,
            Battle
        }

        [SerializeField] private BackdropPreset initialPreset = BackdropPreset.Login;
        [SerializeField] private Transform loginRoot;
        [SerializeField] private Transform homeRoot;
        [SerializeField] private Transform battleRoot;
        [SerializeField] private Transform[] idleRotators = System.Array.Empty<Transform>();
        [SerializeField] private ParticleSystem[] ambienceParticles = System.Array.Empty<ParticleSystem>();
        [SerializeField] private float hologramRotationSpeed = 10f;
        [SerializeField] private float maxParticleEmissionRate = 55f;

        private BackdropPreset currentPreset;

        public void SetPreset(BackdropPreset preset)
        {
            currentPreset = preset;
            SetRootActive(loginRoot, preset == BackdropPreset.Login);
            SetRootActive(homeRoot, preset == BackdropPreset.Home);
            SetRootActive(battleRoot, preset == BackdropPreset.Battle);
            ApplyParticleBudget();
        }

        private void Awake()
        {
            currentPreset = initialPreset;
            SetPreset(currentPreset);
        }

        private void OnEnable()
        {
            SetPreset(currentPreset);
        }

        private void Update()
        {
            var delta = hologramRotationSpeed * Time.deltaTime;
            for (var i = 0; i < idleRotators.Length; i++)
            {
                var rotator = idleRotators[i];
                if (rotator == null || !rotator.gameObject.activeInHierarchy)
                {
                    continue;
                }

                rotator.Rotate(Vector3.up, delta, Space.World);
            }
        }

        private void ApplyParticleBudget()
        {
            for (var i = 0; i < ambienceParticles.Length; i++)
            {
                var particle = ambienceParticles[i];
                if (particle == null)
                {
                    continue;
                }

                var main = particle.main;
                main.maxParticles = Mathf.Min(main.maxParticles, 160);

                var emission = particle.emission;
                var rate = emission.rateOverTime;
                if (rate.mode == ParticleSystemCurveMode.Constant && rate.constant > maxParticleEmissionRate)
                {
                    emission.rateOverTime = maxParticleEmissionRate;
                }

                if (particle.gameObject.activeInHierarchy && !particle.isPlaying)
                {
                    particle.Play();
                }
            }
        }

        private static void SetRootActive(Transform root, bool active)
        {
            if (root != null && root.gameObject.activeSelf != active)
            {
                root.gameObject.SetActive(active);
            }
        }
    }
}
