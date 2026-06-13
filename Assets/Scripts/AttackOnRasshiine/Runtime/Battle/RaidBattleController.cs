using System.Collections;
using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Battle
{
    public sealed class RaidBattleController : MonoBehaviour
    {
        private const float FallbackBossMinScale = 3.4f;
        private const float FallbackBossMaxScale = 4.8f;
        private const float EnemyBossTargetHeight = 4.8f;
        private const float EnemyBossFallbackScale = 1.65f;
        private const float MemberTargetHeight = 1.45f;
        private const float MemberFallbackScale = 0.82f;
        private const float MemberFormationStartAngle = 220f;
        private const float MemberFormationEndAngle = 320f;
        private const float MemberFormationRadius = 4.85f;
        private const float MemberFormationStagger = 0.55f;
        private const float MinRenderableHeight = 0.001f;
        private const int PulseRingSegments = 72;

        private struct ActionVisualProfile
        {
            public Color Primary;
            public Color Secondary;
            public Color LabelColor;
            public float Width;
            public float Duration;
            public float ArcHeight;
            public float LungeDistance;
            public float PulseRadius;
            public float ShakeDistance;
            public float ShakeDuration;
            public bool PulsesParty;
        }

        [SerializeField] private RasshiineTheme theme;
        [SerializeField] private Transform bossAnchor;
        [SerializeField] private Transform partyAnchor;
        [SerializeField] private Transform effectsRoot;
        [SerializeField] private RaidFollowCamera followCamera;

        private readonly Dictionary<string, Transform> participantTransforms = new();
        private readonly Dictionary<string, Vector3> participantBasePositions = new();
        private readonly Dictionary<string, Coroutine> participantActionCoroutines = new();
        private readonly Dictionary<string, int> participantActionVersions = new();
        private Transform bossTransform;
        private BossBattleState state;
        private float idleTime;
        private Vector3 bossMinScale = Vector3.one * FallbackBossMinScale;
        private Vector3 bossMaxScale = Vector3.one * FallbackBossMaxScale;
        private string controlledParticipantId;
        private int participantActionVersion;

        public void Configure(RasshiineTheme newTheme, Transform newBossAnchor, Transform newPartyAnchor, Transform newEffectsRoot, RaidFollowCamera newFollowCamera = null)
        {
            theme = newTheme;
            bossAnchor = newBossAnchor;
            partyAnchor = newPartyAnchor;
            effectsRoot = newEffectsRoot;
            followCamera = newFollowCamera;
        }

        public void LoadBattle(BossBattleState battleState)
        {
            state = battleState;
            StopParticipantActionAnimations();
            ClearChildren(bossAnchor);
            ClearChildren(partyAnchor);
            ClearChildren(effectsRoot);
            participantTransforms.Clear();
            participantBasePositions.Clear();
            CreateArenaGrid();
            if (state == null || state.Status == BattleStatus.Scheduled)
            {
                ApplyControlledParticipant();
                return;
            }

            SpawnBoss();
            SpawnParticipants();
            ApplyControlledParticipant();
        }

        public void SetControlledParticipant(string userId)
        {
            controlledParticipantId = userId;
            ApplyControlledParticipant();
        }

        public void RefreshBossScale()
        {
            if (bossTransform == null)
            {
                return;
            }

            bossTransform.localScale = CalculateCurrentBossScale();
        }

        public IEnumerator PlayAction(BattleActionResult result)
        {
            if (result == null || bossTransform == null)
            {
                yield break;
            }

            participantTransforms.TryGetValue(result.UserId, out var participant);
            var profile = ResolveActionVisual(result);
            var origin = participant != null
                ? participant.position + Vector3.up * 1.2f
                : new Vector3(-2f, 1f, -2f);
            var target = bossTransform.position + Vector3.up * 2.3f;
            if (participant != null && !string.IsNullOrEmpty(result.UserId))
            {
                StartParticipantAction(result.UserId, participant, target, profile);
            }

            if (profile.PulsesParty)
            {
                StartCoroutine(PlayPartyPulse(result, profile));
            }

            yield return PlayProjectileArc(result, origin, target, profile);
            StartCoroutine(ShowFloatingLabel(target + Vector3.up * 0.6f, BuildActionLabel(result), profile.LabelColor));
            if (result.Damage > 0)
            {
                StartCoroutine(ShakeBoss(profile.ShakeDistance, profile.ShakeDuration));
            }

            StartCoroutine(PlayImpactPulse(target, profile));
            if (state?.Boss != null && state.Boss.CurrentHp <= 0)
            {
                StartCoroutine(PlayBossDefeatBurst(target));
            }

            RefreshBossScale();
        }

        private IEnumerator PlayProjectileArc(BattleActionResult result, Vector3 origin, Vector3 target, ActionVisualProfile profile)
        {
            var lineObject = new GameObject($"FX_{result.ActionType}_{result.Nickname}", typeof(LineRenderer));
            lineObject.transform.SetParent(effectsRoot, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = profile.Width;
            line.material = theme.ProjectileMaterial != null ? theme.ProjectileMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = profile.Primary;
            line.endColor = profile.Secondary;

            var duration = profile.Duration;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var t = time / duration;
                var arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * profile.ArcHeight;
                line.SetPosition(0, origin);
                line.SetPosition(1, Vector3.Lerp(origin, target, t) + arc);
                yield return null;
            }

            line.SetPosition(1, target);
            Destroy(lineObject, 0.24f);
        }

        private ActionVisualProfile ResolveActionVisual(BattleActionResult result)
        {
            var profile = new ActionVisualProfile
            {
                Primary = theme.Magenta,
                Secondary = theme.Cyan,
                LabelColor = theme.Gold,
                Width = 0.09f,
                Duration = 0.34f,
                ArcHeight = 0.9f,
                LungeDistance = 0.9f,
                PulseRadius = 1.2f,
                ShakeDistance = 0.06f,
                ShakeDuration = 0.26f
            };

            switch (result.ActionType)
            {
                case BattleActionType.Strong:
                    profile.Width = 0.12f;
                    profile.ArcHeight = 1.08f;
                    profile.LungeDistance = 1.05f;
                    profile.PulseRadius = 1.45f;
                    profile.ShakeDistance = 0.075f;
                    profile.Secondary = theme.Gold;
                    break;
                case BattleActionType.FullPower:
                    profile.Width = 0.16f;
                    profile.Duration = 0.46f;
                    profile.ArcHeight = 1.35f;
                    profile.LungeDistance = 1.35f;
                    profile.PulseRadius = 1.9f;
                    profile.ShakeDistance = 0.11f;
                    profile.ShakeDuration = 0.34f;
                    profile.Primary = theme.Gold;
                    profile.Secondary = theme.Magenta;
                    break;
                case BattleActionType.Support:
                    profile.Width = 0.11f;
                    profile.Duration = 0.42f;
                    profile.ArcHeight = 0.72f;
                    profile.LungeDistance = 0.35f;
                    profile.PulseRadius = result.Heal > 0 ? 2.7f : 2.15f;
                    profile.ShakeDistance = 0.035f;
                    profile.ShakeDuration = 0.2f;
                    profile.Primary = result.Heal > 0 ? theme.Mint : theme.Cyan;
                    profile.Secondary = result.Heal > 0 ? theme.Cyan : theme.Gold;
                    profile.LabelColor = result.Heal > 0 ? theme.Mint : theme.Cyan;
                    profile.PulsesParty = true;
                    break;
                case BattleActionType.Guard:
                    profile.Width = 0.1f;
                    profile.Duration = 0.36f;
                    profile.ArcHeight = 0.58f;
                    profile.LungeDistance = 0.2f;
                    profile.PulseRadius = 1.8f;
                    profile.ShakeDistance = 0.03f;
                    profile.ShakeDuration = 0.18f;
                    profile.Primary = theme.Cyan;
                    profile.Secondary = theme.Mint;
                    profile.LabelColor = theme.Cyan;
                    profile.PulsesParty = true;
                    break;
            }

            return profile;
        }

        private void StartParticipantAction(string userId, Transform participant, Vector3 target, ActionVisualProfile profile)
        {
            if (participantActionCoroutines.TryGetValue(userId, out var running) && running != null)
            {
                StopCoroutine(running);
            }

            var version = ++participantActionVersion;
            participantActionVersions[userId] = version;
            participantActionCoroutines[userId] = StartCoroutine(AnimateParticipantAction(userId, version, participant, target, profile));
        }

        private IEnumerator AnimateParticipantAction(string userId, int version, Transform participant, Vector3 target, ActionVisualProfile profile)
        {
            var controller = participant.GetComponent<MemberAvatarController>();
            var controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled)
            {
                controller.enabled = false;
            }

            var startPosition = participant.position;
            var baseScale = participant.localScale;
            var toTarget = target - startPosition;
            toTarget.y = 0f;
            var lungeTarget = startPosition;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                lungeTarget += toTarget.normalized * profile.LungeDistance;
            }

            try
            {
                var duration = 0.32f;
                for (var time = 0f; time < duration; time += Time.deltaTime)
                {
                    var t = Mathf.Clamp01(time / duration);
                    var punch = Mathf.Sin(t * Mathf.PI);
                    participant.position = Vector3.Lerp(startPosition, lungeTarget, punch);
                    participant.localScale = baseScale * (1f + punch * 0.08f);
                    FaceTarget(participant, target);
                    yield return null;
                }
            }
            finally
            {
                if (participant != null)
                {
                    participant.position = startPosition;
                    participant.localScale = baseScale;
                    FaceTarget(participant, target);
                }

                if (controller != null)
                {
                    controller.enabled = controllerWasEnabled;
                }

                if (participantActionVersions.TryGetValue(userId, out var currentVersion) && currentVersion == version)
                {
                    participantActionVersions.Remove(userId);
                    participantActionCoroutines.Remove(userId);
                }
            }
        }

        private IEnumerator PlayPartyPulse(BattleActionResult result, ActionVisualProfile profile)
        {
            var center = CalculatePartyCenter();
            var label = result.Heal > 0 ? $"+{result.Heal} HP" : result.ActionType == BattleActionType.Guard ? "GUARD" : "SUPPORT";
            StartCoroutine(ShowFloatingLabel(center + Vector3.up * 2.1f, label, profile.LabelColor));
            yield return ExpandRing(center + Vector3.up * 0.08f, profile.Primary, 0.45f, profile.PulseRadius, 0.48f, 0.055f);
        }

        private IEnumerator PlayImpactPulse(Vector3 target, ActionVisualProfile profile)
        {
            yield return ExpandRing(target, profile.Secondary, 0.25f, profile.PulseRadius, 0.38f, profile.Width * 0.75f);
        }

        private IEnumerator PlayBossDefeatBurst(Vector3 target)
        {
            StartCoroutine(ShowFloatingLabel(target + Vector3.up * 1.05f, "BREAK", theme.Gold));
            StartCoroutine(ExpandRing(target, theme.Gold, 0.35f, 2.4f, 0.58f, 0.11f));
            yield return new WaitForSeconds(0.08f);
            StartCoroutine(ExpandRing(target + Vector3.up * 0.35f, theme.Magenta, 0.2f, 1.85f, 0.48f, 0.08f));
            yield return new WaitForSeconds(0.08f);
            StartCoroutine(ExpandRing(target + Vector3.down * 0.25f, theme.Cyan, 0.25f, 2.1f, 0.5f, 0.08f));
        }

        private IEnumerator ExpandRing(Vector3 center, Color color, float startRadius, float endRadius, float duration, float width)
        {
            var ringObject = new GameObject("FX_PulseRing", typeof(LineRenderer));
            ringObject.transform.SetParent(effectsRoot, false);
            var line = ringObject.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = PulseRingSegments;
            line.material = theme.ProjectileMaterial != null ? theme.ProjectileMaterial : new Material(Shader.Find("Sprites/Default"));
            line.widthMultiplier = width;

            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var t = Mathf.Clamp01(time / duration);
                var alpha = 1f - t;
                var faded = new Color(color.r, color.g, color.b, alpha);
                line.startColor = faded;
                line.endColor = faded;
                SetRingPositions(line, center, Mathf.Lerp(startRadius, endRadius, t));
                yield return null;
            }

            Destroy(ringObject);
        }

        private Vector3 CalculatePartyCenter()
        {
            if (participantTransforms.Count == 0)
            {
                return partyAnchor != null ? partyAnchor.position : Vector3.zero;
            }

            var center = Vector3.zero;
            foreach (var participant in participantTransforms.Values)
            {
                center += participant.position;
            }

            return center / participantTransforms.Count;
        }

        private static void SetRingPositions(LineRenderer line, Vector3 center, float radius)
        {
            for (var index = 0; index < PulseRingSegments; index++)
            {
                var angle = index / (float)PulseRingSegments * Mathf.PI * 2f;
                line.SetPosition(index, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private static void FaceTarget(Transform transform, Vector3 target)
        {
            var direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static string BuildActionLabel(BattleActionResult result)
        {
            if (result.Heal > 0 && result.Damage > 0)
            {
                return $"{result.Damage} / +{result.Heal}HP";
            }

            if (result.Heal > 0)
            {
                return $"+{result.Heal}HP";
            }

            if (result.Damage > 0)
            {
                return $"{result.Damage} DMG";
            }

            return result.ActionType switch
            {
                BattleActionType.Guard => "GUARD",
                BattleActionType.Support => "SUPPORT",
                _ => "ACTION"
            };
        }

        private void Update()
        {
            idleTime += Time.deltaTime;
            if (bossTransform != null)
            {
                bossTransform.localRotation = Quaternion.Euler(0f, Mathf.Sin(idleTime * 0.35f) * 6f, 0f);
                bossTransform.localPosition = new Vector3(0f, Mathf.Sin(idleTime * 1.2f) * 0.08f, 0f);
            }

            foreach (var pair in participantTransforms)
            {
                if (pair.Key == controlledParticipantId)
                {
                    continue;
                }

                if (!participantBasePositions.TryGetValue(pair.Key, out var basePosition))
                {
                    continue;
                }

                var offset = pair.Value.GetSiblingIndex() * 0.45f;
                pair.Value.localPosition = basePosition + Vector3.up * (Mathf.Sin(idleTime * 1.8f + offset) * 0.035f);
            }
        }

        private void SpawnBoss()
        {
            var enemyPrefab = theme.EnemyPrefab != null ? theme.EnemyPrefab : theme.MentorPlaceholderPrefab;
            var materialOverride = theme.EnemyPrefab != null ? null : theme.BossMaterial;
            var model = InstantiateModel(enemyPrefab, bossAnchor, "BossEnemy", materialOverride, true);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bossTransform = model.transform;
            ConfigureBossScale(model, theme.EnemyPrefab != null);
            RefreshBossScale();
        }

        private void SpawnParticipants()
        {
            if (state == null)
            {
                return;
            }

            var count = state.Participants.Count;
            for (var index = 0; index < count; index++)
            {
                var participant = state.Participants[index];
                var angle = Mathf.Lerp(MemberFormationStartAngle, MemberFormationEndAngle, count == 1 ? 0.5f : index / (float)(count - 1));
                var radius = MemberFormationRadius + (index % 2) * MemberFormationStagger;
                var x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                var z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                var memberPrefab = theme.MemberPlaceholderPrefab;
                var materialOverride = memberPrefab == null ? theme.MemberMaterial : null;
                var model = InstantiateModel(memberPrefab, partyAnchor, participant.Nickname, materialOverride, false);
                model.transform.localPosition = new Vector3(x, 0f, z);
                model.transform.LookAt(bossAnchor.position + Vector3.up * 1.2f);
                ConfigureParticipantModel(model, memberPrefab != null);
                participantTransforms[participant.UserId] = model.transform;
                participantBasePositions[participant.UserId] = model.transform.localPosition;
            }
        }

        private void ApplyControlledParticipant()
        {
            foreach (var pair in participantTransforms)
            {
                var existingController = pair.Value.GetComponent<MemberAvatarController>();
                if (existingController != null)
                {
                    existingController.enabled = false;
                }
            }

            if (followCamera == null && Camera.main != null)
            {
                followCamera = Camera.main.GetComponent<RaidFollowCamera>();
                if (followCamera == null)
                {
                    followCamera = Camera.main.gameObject.AddComponent<RaidFollowCamera>();
                }
            }

            if (string.IsNullOrEmpty(controlledParticipantId) || !participantTransforms.TryGetValue(controlledParticipantId, out var controlledTransform))
            {
                followCamera?.SetOverview();
                return;
            }

            var controller = controlledTransform.GetComponent<MemberAvatarController>();
            if (controller == null)
            {
                controller = controlledTransform.gameObject.AddComponent<MemberAvatarController>();
            }

            controller.enabled = true;
            controller.Configure(bossAnchor, new Rect(-8.5f, -8.8f, 17f, 9.8f));
            followCamera?.Follow(controlledTransform, bossAnchor);
        }

        private GameObject InstantiateModel(GameObject prefab, Transform parent, string objectName, Material overrideMaterial, bool isBoss)
        {
            GameObject instance;
            if (prefab != null)
            {
                instance = Instantiate(prefab, parent);
                instance.name = objectName;
            }
            else
            {
                instance = CreateFallbackModel(objectName, isBoss);
                instance.transform.SetParent(parent, false);
            }

            if (overrideMaterial != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = overrideMaterial;
                }
            }

            return instance;
        }

        private static void ConfigureParticipantModel(GameObject model, bool usesAuthoredPrefab)
        {
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
            }

            var scale = usesAuthoredPrefab
                ? CalculateScaleFactorForTargetHeight(model, MemberTargetHeight, MemberFallbackScale, false)
                : MemberFallbackScale;
            model.transform.localScale = Vector3.one * scale;
        }

        private Vector3 CalculateCurrentBossScale()
        {
            if (state?.Boss == null || state.Boss.MaxHp <= 0)
            {
                return bossMaxScale;
            }

            var hp01 = Mathf.Clamp01(state.Boss.CurrentHp / (float)state.Boss.MaxHp);
            return Vector3.Lerp(bossMinScale, bossMaxScale, hp01);
        }

        private void ConfigureBossScale(GameObject model, bool usesEnemyPrefab)
        {
            if (!usesEnemyPrefab)
            {
                bossMinScale = Vector3.one * FallbackBossMinScale;
                bossMaxScale = Vector3.one * FallbackBossMaxScale;
                return;
            }

            var authoredScale = model.transform.localScale;
            var targetScaleFactor = CalculateScaleFactorForTargetHeight(model, EnemyBossTargetHeight, EnemyBossFallbackScale);
            bossMaxScale = authoredScale * targetScaleFactor;
            bossMinScale = bossMaxScale * (FallbackBossMinScale / FallbackBossMaxScale);
        }

        private static float CalculateScaleFactorForTargetHeight(GameObject model, float targetHeight, float fallbackScale, bool includeInactiveRenderers = true)
        {
            if (!TryGetRendererBounds(model, includeInactiveRenderers, out var bounds) || bounds.size.y <= MinRenderableHeight)
            {
                return fallbackScale;
            }

            return targetHeight / bounds.size.y;
        }

        private static bool TryGetRendererBounds(GameObject model, bool includeInactiveRenderers, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(includeInactiveRenderers))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private GameObject CreateFallbackModel(string objectName, bool isBoss)
        {
            var root = new GameObject(objectName);
            var body = GameObject.CreatePrimitive(isBoss ? PrimitiveType.Cylinder : PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = isBoss ? new Vector3(0.8f, 1.6f, 0.8f) : new Vector3(0.5f, 0.8f, 0.5f);
            body.transform.localPosition = Vector3.up * (isBoss ? 1.6f : 0.8f);

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = Vector3.up * (isBoss ? 3.1f : 1.8f);
            core.transform.localScale = Vector3.one * (isBoss ? 0.65f : 0.35f);
            return root;
        }

        private void CreateArenaGrid()
        {
            var material = theme.FloorLineMaterial != null ? theme.FloorLineMaterial : new Material(Shader.Find("Sprites/Default"));
            for (var i = -10; i <= 10; i++)
            {
                CreateLine($"Grid_X_{i}", new Vector3(i, 0f, -10f), new Vector3(i, 0f, 10f), material, i == 0 ? theme.Magenta : theme.Cyan, 0.018f);
                CreateLine($"Grid_Z_{i}", new Vector3(-10f, 0f, i), new Vector3(10f, 0f, i), material, i == 0 ? theme.Magenta : theme.Cyan, 0.018f);
            }

            for (var ring = 1; ring <= 3; ring++)
            {
                CreateRing(ring * 2.2f, material, ring % 2 == 0 ? theme.Magenta : theme.Cyan);
            }
        }

        private void CreateLine(string objectName, Vector3 a, Vector3 b, Material material, Color color, float width)
        {
            var lineObject = new GameObject(objectName, typeof(LineRenderer));
            lineObject.transform.SetParent(effectsRoot, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.material = material;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
        }

        private void CreateRing(float radius, Material material, Color color)
        {
            var lineObject = new GameObject($"Grid_Ring_{radius:0.0}", typeof(LineRenderer));
            lineObject.transform.SetParent(effectsRoot, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 96;
            line.material = material;
            line.widthMultiplier = 0.024f;
            line.startColor = color;
            line.endColor = color;
            for (var index = 0; index < line.positionCount; index++)
            {
                var angle = index / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0.01f, Mathf.Sin(angle) * radius));
            }
        }

        private IEnumerator ShakeBoss(float distance, float duration)
        {
            if (bossTransform == null)
            {
                yield break;
            }

            var basePosition = bossTransform.localPosition;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                bossTransform.localPosition = basePosition + Random.insideUnitSphere * distance;
                yield return null;
            }

            bossTransform.localPosition = basePosition;
        }

        private IEnumerator ShowFloatingLabel(Vector3 worldPosition, string value, Color color)
        {
            var labelObject = new GameObject("FloatingBattleLabel", typeof(TextMesh));
            labelObject.transform.SetParent(effectsRoot, false);
            labelObject.transform.position = worldPosition;
            var mesh = labelObject.GetComponent<TextMesh>();
            mesh.text = value;
            mesh.fontSize = 64;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            for (var time = 0f; time < 0.9f; time += Time.deltaTime)
            {
                labelObject.transform.position += Vector3.up * (Time.deltaTime * 0.7f);
                if (cameraTransform != null)
                {
                    labelObject.transform.rotation = Quaternion.LookRotation(labelObject.transform.position - cameraTransform.position);
                }

                mesh.color = new Color(color.r, color.g, color.b, 1f - time / 0.9f);
                yield return null;
            }

            Destroy(labelObject);
        }

        private void StopParticipantActionAnimations()
        {
            if (participantActionCoroutines.Count == 0)
            {
                return;
            }

            var runningCoroutines = new List<Coroutine>(participantActionCoroutines.Values);
            foreach (var coroutine in runningCoroutines)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }

            participantActionCoroutines.Clear();
            participantActionVersions.Clear();
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                Destroy(parent.GetChild(index).gameObject);
            }
        }
    }
}
