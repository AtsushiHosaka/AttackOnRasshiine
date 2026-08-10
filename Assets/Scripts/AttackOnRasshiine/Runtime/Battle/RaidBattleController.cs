using System.Collections;
using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace AttackOnRasshiine.Runtime.Battle
{
    public sealed class RaidBattleController : MonoBehaviour
    {
        private const float FallbackBossMinScale = 2.65f;
        private const float FallbackBossMaxScale = 3.75f;
        // The boss is staged as a broad mid-ground creature, not a full-height
        // wall. This is deliberately smaller than the old 4.70-unit target so
        // the whole horn, six feet and the canyon behind it remain readable.
        private const float EnemyBossTargetHeight = 5.40f;
        private const float EnemyBossFallbackScale = 1.25f;
        private const float MemberTargetHeight = 2.70f;
        private const float MemberFallbackScale = 0.62f;
        private const int MaxVisiblePartyMembers = 3;
        private const float MemberFormationLeftEdge = -9.80f;
        private const float MemberFormationRightEdge = -4.80f;
        private const float MemberFormationFrontDepth = -4.25f;
        private const float MemberFormationRowDepth = 0.58f;
        private const float MemberFormationCenterDepthOffset = 0.75f;
        private const float BossVisualHorizontalOffset = 2.55f;
        private const float BossVisualVerticalOffset = 0.72f;
        private const float MinRenderableHeight = 0.001f;
        private const float NearSceneryMaxLocalDepth = 5.25f;
        // LineRenderer stores its gradient colors with 8-bit channels. Keep a
        // small margin below 0.78 so quantization cannot round the value upward
        // and reintroduce a bright cyan line in the LDR WebGL path.
        private const float ArenaLineMaxChannel = 0.778f;
        private const int PulseRingSegments = 72;
        private static readonly Vector3[] BattleCloudViewportAnchors =
        {
            new(0.18f, 0.82f, 20f),
            new(0.47f, 0.76f, 22f),
            new(0.85f, 0.84f, 20f)
        };

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
        private readonly List<Transform> battleCloudClusters = new();
        private static readonly Dictionary<string, Material> NatureMaterialCache = new();
        private static readonly Dictionary<Material, Material> ParticipantMaterialCache = new();
        private static readonly Dictionary<Material, Material[]> ParticipantPaletteMaterialCache = new();
        private static readonly Color[] ParticipantIdentityPalette =
        {
            new(0.20f, 0.52f, 1.00f, 1f),
            new(1.00f, 0.22f, 0.16f, 1f),
            new(0.32f, 0.85f, 0.12f, 1f)
        };
        private static Material runtimeBossMaterial;
        private static Material runtimeBossDeepMaterial;
        private static Material runtimeBossGoldMaterial;
        private static Material runtimeBossCoreMaterial;
        private static Mesh runtimeBossFacetMesh;
        private static Mesh runtimeBossEllipsoidMesh;
        private static Mesh runtimeBossSpikeMesh;
        private Material arenaLineMaterial;
        private Material bossContactShadowMaterial;
        private Mesh generatedArenaGroundMesh;
        private Mesh meadowArenaGroundMesh;
        private Mesh battleClearingMesh;
        private Mesh bossFocusDaisMesh;
        private Mesh bossContactShadowMesh;
        private Transform bossTransform;
        private Transform scheduledSealTransform;
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

        private void OnDestroy()
        {
            DestroyArenaGeneratedObject(generatedArenaGroundMesh);
            DestroyArenaGeneratedObject(meadowArenaGroundMesh);
            DestroyArenaGeneratedObject(battleClearingMesh);
            DestroyArenaGeneratedObject(bossFocusDaisMesh);
            DestroyArenaGeneratedObject(bossContactShadowMesh);
            DestroyArenaGeneratedObject(arenaLineMaterial);
            DestroyArenaGeneratedObject(bossContactShadowMaterial);
            generatedArenaGroundMesh = null;
            meadowArenaGroundMesh = null;
            battleClearingMesh = null;
            bossFocusDaisMesh = null;
            bossContactShadowMesh = null;
            arenaLineMaterial = null;
            bossContactShadowMaterial = null;
        }

        public void LoadBattle(BossBattleState battleState)
        {
            ClearBattleStage();
            state = battleState;
            CreateArenaGrid();
            if (state == null || state.Status == BattleStatus.Scheduled)
            {
                if (state?.Status == BattleStatus.Scheduled)
                {
                    CreateScheduledRaidSeal();
                }

                ApplyControlledParticipant();
                return;
            }

            SpawnBoss();
            SpawnParticipants();
            ApplyControlledParticipant();
        }

        public void ClearBattleStage()
        {
            state = null;
            StopParticipantActionAnimations();
            ClearChildren(bossAnchor);
            ClearChildren(partyAnchor);
            ClearChildren(effectsRoot);
            participantTransforms.Clear();
            participantBasePositions.Clear();
            battleCloudClusters.Clear();
            bossTransform = null;
            scheduledSealTransform = null;
            ApplyControlledParticipant();
        }

        public void SetControlledParticipant(string userId)
        {
            controlledParticipantId = userId;
            if (ShouldRebuildVisibleParticipants())
            {
                RebuildVisibleParticipants();
            }

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
            if (result.ActionType == BattleActionType.FullPower)
            {
                StartCooperativeAttack(result.UserId, target, profile);
            }
            else if (participant != null && !string.IsNullOrEmpty(result.UserId))
            {
                StartParticipantAction(result.UserId, participant, target, profile, ResolveMemberAnimation(result.ActionType));
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

        private void StartCooperativeAttack(string leadUserId, Vector3 target, ActionVisualProfile profile)
        {
            var index = 0;
            foreach (var pair in participantTransforms)
            {
                var participantProfile = profile;
                var isLead = pair.Key == leadUserId;
                if (!isLead)
                {
                    participantProfile.LungeDistance *= 0.62f;
                }

                StartParticipantAction(pair.Key, pair.Value, target, participantProfile, ResolveCooperativeMemberAnimation(index, isLead), index * 0.055f);
                index++;
            }
        }

        private static MemberBattleAnimation ResolveCooperativeMemberAnimation(int participantIndex, bool isLead)
        {
            if (isLead)
            {
                return MemberBattleAnimation.FullPowerAttack;
            }

            switch (Mathf.Abs(participantIndex) % 3)
            {
                case 0:
                    return MemberBattleAnimation.Attack;
                case 1:
                    return MemberBattleAnimation.StrongAttack;
                default:
                    return MemberBattleAnimation.FullPowerAttack;
            }
        }

        private static MemberBattleAnimation ResolveMemberAnimation(BattleActionType actionType)
        {
            return actionType switch
            {
                BattleActionType.Strong => MemberBattleAnimation.StrongAttack,
                BattleActionType.FullPower => MemberBattleAnimation.FullPowerAttack,
                BattleActionType.Support => MemberBattleAnimation.Support,
                BattleActionType.Guard => MemberBattleAnimation.Guard,
                _ => MemberBattleAnimation.Attack
            };
        }

        private void StartParticipantAction(string userId, Transform participant, Vector3 target, ActionVisualProfile profile, MemberBattleAnimation animation, float delay = 0f)
        {
            if (participantActionCoroutines.TryGetValue(userId, out var running) && running != null)
            {
                StopCoroutine(running);
            }

            var version = ++participantActionVersion;
            participantActionVersions[userId] = version;
            participantActionCoroutines[userId] = StartCoroutine(AnimateParticipantAction(userId, version, participant, target, profile, animation, delay));
        }

        private IEnumerator AnimateParticipantAction(string userId, int version, Transform participant, Vector3 target, ActionVisualProfile profile, MemberBattleAnimation animation, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (participant == null)
            {
                if (participantActionVersions.TryGetValue(userId, out var currentVersion) && currentVersion == version)
                {
                    participantActionVersions.Remove(userId);
                    participantActionCoroutines.Remove(userId);
                }

                yield break;
            }

            var controller = participant.GetComponent<MemberAvatarController>();
            var controllerWasEnabled = controller != null && controller.enabled;
            var avatarAnimator = participant.GetComponent<MemberAvatarAnimator>();
            if (controllerWasEnabled)
            {
                controller.enabled = false;
            }

            var actionDuration = Mathf.Max(0.58f, profile.Duration + 0.32f);
            avatarAnimator?.PlayBattleAnimation(animation, actionDuration + 0.08f);
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
                for (var time = 0f; time < actionDuration; time += Time.deltaTime)
                {
                    var t = Mathf.Clamp01(time / actionDuration);
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

                avatarAnimator?.CancelBattleAnimation();

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
            if (bossTransform != null && state is { IsCompleted: false })
            {
                bossTransform.localRotation = Quaternion.Euler(0f, -8f + Mathf.Sin(idleTime * 0.35f) * 4f, 0f);
                bossTransform.localPosition = new Vector3(BossVisualHorizontalOffset, BossVisualVerticalOffset + Mathf.Sin(idleTime * 1.2f) * 0.06f, 0f);
            }

            if (scheduledSealTransform != null)
            {
                scheduledSealTransform.localRotation = Quaternion.Euler(0f, idleTime * 11f, 0f);
                scheduledSealTransform.localPosition = Vector3.right * BossVisualHorizontalOffset +
                                                       Vector3.up * (0.035f + Mathf.Sin(idleTime * 1.35f) * 0.035f);
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

                if (participantActionCoroutines.TryGetValue(pair.Key, out var runningAction) && runningAction != null)
                {
                    continue;
                }

                var avatarAnimator = pair.Value.GetComponent<MemberAvatarAnimator>();
                if (avatarAnimator != null && avatarAnimator.HasPlayableAnimator)
                {
                    avatarAnimator.PlayIdle();
                    pair.Value.localPosition = new Vector3(basePosition.x, 0f, basePosition.z);
                    continue;
                }

                var offset = pair.Value.GetSiblingIndex() * 0.45f;
                pair.Value.localPosition = basePosition + Vector3.up * (Mathf.Sin(idleTime * 1.8f + offset) * 0.035f);
            }

            PositionBattleCloudsInViewport();
        }

        private void PositionBattleCloudsInViewport()
        {
            if (battleCloudClusters.Count == 0)
            {
                return;
            }

            var battleCamera = followCamera != null ? followCamera.GetComponent<Camera>() : Camera.main;
            if (battleCamera == null)
            {
                return;
            }

            var count = Mathf.Min(battleCloudClusters.Count, BattleCloudViewportAnchors.Length);
            for (var index = 0; index < count; index++)
            {
                var cluster = battleCloudClusters[index];
                if (cluster == null)
                {
                    continue;
                }

                cluster.position = battleCamera.ViewportToWorldPoint(BattleCloudViewportAnchors[index]);
                cluster.rotation = battleCamera.transform.rotation;
            }
        }

        private void SpawnBoss()
        {
            // The imported CyberSoldier reads as a featureless humanoid silhouette
            // after the WebGL LDR conversion. This purpose-built golem uses only
            // shared texture-free materials and a few hundred cube triangles, while
            // preserving a clear low-poly silhouette and material hierarchy.
            var model = CreateProceduralLowPolyBoss();
            // The bright toy-canyon composition keeps the party clustered in the
            // lower-left and gives the boss the stronger right-hand silhouette seen
            // in the approved visual target. Moving the model (not the scene anchor)
            // preserves authored arena/camera bounds and all network coordinates.
            model.transform.localPosition = new Vector3(BossVisualHorizontalOffset, BossVisualVerticalOffset, 0f);
            model.transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
            bossTransform = model.transform;
            ConfigureBossScale(model, true);
            ConfigureBossRenderers(model, null);
            CreateBossContactShadow();
            RefreshBossScale();
            ApplyBossStatePose();
        }

        private void ApplyBossStatePose()
        {
            if (bossTransform == null || state == null || !state.IsCompleted)
            {
                return;
            }

            // Result state must not look like the live boss is still idling. A
            // lowered forward tilt reads as defeated even on a tiny portrait
            // canvas, and reducing the shared glow geometry avoids allocating a
            // result-only material or another shader pass.
            bossTransform.localPosition = new Vector3(BossVisualHorizontalOffset, -0.48f, 0.12f);
            bossTransform.localRotation = Quaternion.Euler(14f, -8f, 7f);
            foreach (var child in bossTransform.GetComponentsInChildren<Transform>(true))
            {
                if (child == bossTransform)
                {
                    continue;
                }

                if (child.name.Contains("Glow", System.StringComparison.Ordinal) ||
                    child.name.Contains("Crystal", System.StringComparison.Ordinal))
                {
                    child.localScale *= 0.62f;
                }
            }
        }

        private void CreateScheduledRaidSeal()
        {
            if (bossAnchor == null)
            {
                return;
            }

            // A scheduled raid used to leave the most important part of the stage
            // completely empty. This sealed waypoint gives the waiting screen a
            // clear, game-like focal point while reusing the boss's one 48-triangle
            // facet mesh and three shared texture-free materials.
            var root = new GameObject("RaidSealBeacon").transform;
            root.SetParent(bossAnchor, false);
            root.localPosition = Vector3.right * BossVisualHorizontalOffset + Vector3.up * 0.035f;
            scheduledSealTransform = root;

            var deep = ResolveRuntimeBossDeepMaterial();
            var gold = ResolveRuntimeBossGoldMaterial();
            var core = ResolveRuntimeBossCoreMaterial();
            CreateBossPart(root, "Seal Base", new Vector3(0f, 0.12f, 0f), new Vector3(1.55f, 0.22f, 1.55f), Vector3.zero, deep);
            CreateBossPart(root, "Seal Base Inlay", new Vector3(0f, 0.32f, 0f), new Vector3(0.94f, 0.18f, 0.94f), new Vector3(0f, 45f, 0f), gold);
            CreateBossPart(root, "Seal Core Frame", new Vector3(0f, 1.24f, 0f), new Vector3(0.72f, 1.62f, 0.54f), new Vector3(0f, 0f, 45f), deep, false);
            CreateBossPart(root, "Seal Core", new Vector3(0f, 1.24f, -0.08f), new Vector3(0.42f, 1.18f, 0.38f), new Vector3(0f, 0f, 45f), core, false);
            CreateBossPart(root, "Seal Crown", new Vector3(0f, 2.18f, 0f), new Vector3(0.28f, 0.54f, 0.28f), new Vector3(0f, 0f, 45f), gold, false);
            CreateBossPart(root, "Seal Rail Left", new Vector3(-0.48f, 1.24f, 0.06f), new Vector3(0.14f, 1.56f, 0.16f), new Vector3(0f, 0f, -18f), gold);
            CreateBossPart(root, "Seal Rail Right", new Vector3(0.48f, 1.24f, 0.06f), new Vector3(0.14f, 1.56f, 0.16f), new Vector3(0f, 0f, 18f), gold);
            CreateBossPart(root, "Seal Shard Left", new Vector3(-0.88f, 1.12f, -0.02f), new Vector3(0.24f, 0.48f, 0.24f), new Vector3(0f, 0f, 45f), core, false);
            CreateBossPart(root, "Seal Shard Right", new Vector3(0.88f, 1.12f, -0.02f), new Vector3(0.24f, 0.48f, 0.24f), new Vector3(0f, 0f, 45f), core, false);
        }

        private GameObject CreateProceduralLowPolyBoss()
        {
            var root = new GameObject("BossEnemy");
            root.transform.SetParent(bossAnchor, false);

            var violet = ResolveRuntimeBossMaterial();
            var deep = ResolveRuntimeBossDeepMaterial();
            var coral = ResolveRuntimeBossGoldMaterial();
            var core = ResolveRuntimeBossCoreMaterial();

            // Low-poly ellipsoids make a single rounded beetle silhouette. The former
            // chamfered boxes stayed visibly rectangular even after overlap and read as
            // a segmented vehicle instead of a cute faceted creature.
            CreateBossEllipsoidPart(root.transform, "Abdomen Lower", new Vector3(1.10f, 1.02f, 0.10f), new Vector3(3.10f, 1.35f, 2.05f), new Vector3(1f, -6f, -2f), deep);
            CreateBossEllipsoidPart(root.transform, "Abdomen Shell", new Vector3(1.02f, 1.64f, 0.02f), new Vector3(2.78f, 2.12f, 2.10f), new Vector3(-3f, -8f, 5f), violet);
            CreateBossEllipsoidPart(root.transform, "Abdomen Rear Shell", new Vector3(2.36f, 1.52f, 0.10f), new Vector3(1.95f, 1.68f, 1.88f), new Vector3(3f, -13f, -6f), violet);
            CreateBossEllipsoidPart(root.transform, "Thorax", new Vector3(-0.62f, 1.50f, -0.08f), new Vector3(2.20f, 1.82f, 1.90f), new Vector3(-3f, 8f, -5f), violet);
            CreateBossEllipsoidPart(root.transform, "Head", new Vector3(-1.82f, 1.16f, -0.23f), new Vector3(1.62f, 1.30f, 1.52f), new Vector3(2f, 10f, -8f), deep);
            CreateBossEllipsoidPart(root.transform, "Violet Brow", new Vector3(-1.94f, 1.51f, -0.69f), new Vector3(1.18f, 0.52f, 0.66f), new Vector3(-2f, 10f, -9f), violet);

            // Short diagonal coral shards decorate the purple dome while leaving
            // the beetle body as the dominant silhouette.
            CreateBossEllipsoidPart(root.transform, "Thorax Coral Armor", new Vector3(-0.62f, 2.17f, -0.22f), new Vector3(0.92f, 0.42f, 1.02f), new Vector3(2f, 7f, -13f), coral);
            CreateBossSpikePart(root.transform, "Shell Crystal Front", new Vector3(0.02f, 2.25f, -0.10f), new Vector3(0.46f, 0.64f, 0.46f), new Vector3(-6f, -4f, 28f), coral);
            CreateBossSpikePart(root.transform, "Shell Crystal Center", new Vector3(1.08f, 2.45f, -0.04f), new Vector3(0.52f, 0.78f, 0.52f), new Vector3(4f, 3f, -24f), coral);
            CreateBossSpikePart(root.transform, "Shell Crystal Rear", new Vector3(2.14f, 2.27f, 0.02f), new Vector3(0.46f, 0.66f, 0.46f), new Vector3(-5f, -7f, 30f), coral);
            CreateBossSpikePart(root.transform, "Shell Side Spike Near", new Vector3(2.15f, 1.68f, -1.10f), new Vector3(0.48f, 0.60f, 0.48f), new Vector3(16f, 7f, 68f), coral);
            CreateBossSpikePart(root.transform, "Shell Rear Spike", new Vector3(3.14f, 1.50f, 0.04f), new Vector3(0.48f, 0.62f, 0.48f), new Vector3(0f, -8f, 74f), coral);

            // The horn is an intentionally overlapping five-piece crescent. Its
            // broad base grows directly out of the head, bends forward, then rises
            // into a narrow tip. Generous overlap prevents WebGL aliasing from
            // turning it into detached red sticks at gameplay distance.
            CreateBossEllipsoidPart(root.transform, "Crown Left", new Vector3(-2.16f, 1.46f, -0.31f), new Vector3(1.04f, 1.42f, 0.94f), new Vector3(0f, 4f, 48f), coral);
            CreateBossEllipsoidPart(root.transform, "Crown Right", new Vector3(-2.66f, 1.96f, -0.29f), new Vector3(0.82f, 1.46f, 0.80f), new Vector3(0f, 2f, 38f), coral);
            CreateBossEllipsoidPart(root.transform, "Crown Rise", new Vector3(-3.02f, 2.57f, -0.27f), new Vector3(0.62f, 1.50f, 0.62f), new Vector3(0f, 0f, 22f), coral);
            CreateBossSpikePart(root.transform, "Crown Tip", new Vector3(-3.15f, 3.23f, -0.24f), new Vector3(0.38f, 1.02f, 0.42f), new Vector3(0f, -2f, 5f), coral);
            CreateBossSpikePart(root.transform, "Crown Upper Tine", new Vector3(-2.35f, 2.30f, -0.22f), new Vector3(0.34f, 0.74f, 0.38f), new Vector3(0f, 3f, 18f), coral);
            CreateBossSpikePart(root.transform, "Mandible Left", new Vector3(-2.42f, 0.76f, -0.61f), new Vector3(0.52f, 0.76f, 0.52f), new Vector3(0f, 7f, 62f), coral);
            CreateBossSpikePart(root.transform, "Mandible Right", new Vector3(-2.25f, 0.60f, -0.34f), new Vector3(0.46f, 0.66f, 0.48f), new Vector3(0f, -5f, 56f), coral);

            CreateBeetleLeg(root.transform, "Front Near", -1.30f, -0.84f, -46f, -66f, deep, coral);
            CreateBeetleLeg(root.transform, "Middle Near", -0.02f, -0.98f, -18f, -39f, deep, coral);
            CreateBeetleLeg(root.transform, "Rear Near", 1.25f, -0.88f, 18f, 39f, deep, coral);
            CreateBeetleLeg(root.transform, "Front Far", -1.18f, 0.72f, -40f, -60f, deep, coral);
            CreateBeetleLeg(root.transform, "Middle Far", 0.08f, 0.80f, -13f, -31f, deep, coral);
            CreateBeetleLeg(root.transform, "Rear Far", 1.42f, 0.70f, 23f, 44f, deep, coral);

            CreateBossPart(root.transform, "Core Frame", new Vector3(-1.92f, 1.12f, -0.83f), new Vector3(0.46f, 0.46f, 0.12f), new Vector3(0f, 0f, 45f), coral, false);
            CreateBossPart(root.transform, "Core Glow", new Vector3(-1.92f, 1.12f, -0.94f), new Vector3(0.27f, 0.27f, 0.07f), new Vector3(0f, 0f, 45f), core, false);
            CreateBossPart(root.transform, "Visor Glow", new Vector3(-2.26f, 1.48f, -0.80f), new Vector3(0.46f, 0.12f, 0.07f), new Vector3(0f, 9f, -5f), core, false);
            CreateBossEllipsoidPart(root.transform, "Eye Near", new Vector3(-2.27f, 1.34f, -0.86f), new Vector3(0.28f, 0.15f, 0.09f), new Vector3(0f, 8f, -12f), core, false);
            CreateBossEllipsoidPart(root.transform, "Eye Far", new Vector3(-1.91f, 1.43f, -0.91f), new Vector3(0.24f, 0.13f, 0.08f), new Vector3(0f, -4f, 10f), core, false);
            return root;
        }

        private static void CreateBeetleLeg(
            Transform parent,
            string legName,
            float localX,
            float localZ,
            float upperAngle,
            float lowerAngle,
            Material upperMaterial,
            Material lowerMaterial)
        {
            var depthScale = localZ < 0f ? 1f : 0.90f;
            var upperPosition = new Vector3(localX, 0.69f, localZ);
            var direction = Mathf.Sign(upperAngle);
            var lowerPosition = new Vector3(localX + direction * 0.38f, 0.31f, localZ * 1.17f);
            var footPosition = new Vector3(localX + direction * 0.70f, 0.08f, localZ * 1.28f);
            CreateBossPart(parent, $"{legName} Leg Upper", upperPosition, new Vector3(0.54f, 0.50f, 0.62f) * depthScale, new Vector3(0f, 0f, upperAngle), upperMaterial);
            CreateBossPart(parent, $"{legName} Leg Lower", lowerPosition, new Vector3(0.46f, 0.39f, 0.52f) * depthScale, new Vector3(0f, 0f, lowerAngle), upperMaterial);
            CreateBossPart(parent, $"{legName} Leg Coral Foot", footPosition, new Vector3(0.48f, 0.20f, 0.48f) * depthScale, new Vector3(0f, -direction * 8f, direction * 10f), lowerMaterial);
        }

        private static GameObject CreateBossPart(
            Transform parent,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool castsShadow = true)
        {
            var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localEulerAngles = localEuler;
            part.GetComponent<MeshFilter>().sharedMesh = ResolveRuntimeBossFacetMesh();
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castsShadow;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return part;
        }

        private static GameObject CreateBossEllipsoidPart(
            Transform parent,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool castsShadow = true)
        {
            return CreateBossPartWithMesh(parent, partName, localPosition, localScale, localEuler, material, ResolveRuntimeBossEllipsoidMesh(), castsShadow);
        }

        private static GameObject CreateBossSpikePart(
            Transform parent,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool castsShadow = true)
        {
            return CreateBossPartWithMesh(parent, partName, localPosition, localScale, localEuler, material, ResolveRuntimeBossSpikeMesh(), castsShadow);
        }

        private static GameObject CreateBossPartWithMesh(
            Transform parent,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            Mesh mesh,
            bool castsShadow)
        {
            var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localEulerAngles = localEuler;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castsShadow;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return part;
        }

        private static Mesh ResolveRuntimeBossEllipsoidMesh()
        {
            if (runtimeBossEllipsoidMesh != null)
            {
                return runtimeBossEllipsoidMesh;
            }

            const int segments = 10;
            const int latitudeBands = 5;
            var vertices = new List<Vector3>(segments * latitudeBands * 6);
            var triangles = new List<int>(segments * latitudeBands * 6);
            for (var latitude = 0; latitude < latitudeBands; latitude++)
            {
                var theta0 = Mathf.PI * latitude / latitudeBands;
                var theta1 = Mathf.PI * (latitude + 1) / latitudeBands;
                for (var longitude = 0; longitude < segments; longitude++)
                {
                    var phi0 = Mathf.PI * 2f * longitude / segments;
                    var phi1 = Mathf.PI * 2f * (longitude + 1) / segments;
                    var a = BossSpherePoint(theta0, phi0);
                    var b = BossSpherePoint(theta1, phi0);
                    var c = BossSpherePoint(theta1, phi1);
                    var d = BossSpherePoint(theta0, phi1);
                    if (latitude > 0)
                    {
                        AppendBossTriangle(vertices, triangles, a, b, d);
                    }

                    if (latitude < latitudeBands - 1)
                    {
                        AppendBossTriangle(vertices, triangles, d, b, c);
                    }
                }
            }

            runtimeBossEllipsoidMesh = CreateRuntimeBossMesh("RasshiineBossLowPolyEllipsoid", vertices, triangles);
            return runtimeBossEllipsoidMesh;
        }

        private static Mesh ResolveRuntimeBossSpikeMesh()
        {
            if (runtimeBossSpikeMesh != null)
            {
                return runtimeBossSpikeMesh;
            }

            const int segments = 8;
            var vertices = new List<Vector3>(segments * 6);
            var triangles = new List<int>(segments * 6);
            var tip = new Vector3(0f, 0.56f, 0f);
            var center = new Vector3(0f, -0.50f, 0f);
            for (var index = 0; index < segments; index++)
            {
                var angle0 = Mathf.PI * 2f * index / segments;
                var angle1 = Mathf.PI * 2f * (index + 1) / segments;
                var a = new Vector3(Mathf.Cos(angle0) * 0.50f, -0.50f, Mathf.Sin(angle0) * 0.50f);
                var b = new Vector3(Mathf.Cos(angle1) * 0.50f, -0.50f, Mathf.Sin(angle1) * 0.50f);
                AppendBossTriangle(vertices, triangles, tip, a, b);
                AppendBossTriangle(vertices, triangles, center, b, a);
            }

            runtimeBossSpikeMesh = CreateRuntimeBossMesh("RasshiineBossLowPolySpike", vertices, triangles);
            return runtimeBossSpikeMesh;
        }

        private static Vector3 BossSpherePoint(float theta, float phi)
        {
            var sinTheta = Mathf.Sin(theta);
            return new Vector3(
                sinTheta * Mathf.Cos(phi) * 0.50f,
                Mathf.Cos(theta) * 0.50f,
                sinTheta * Mathf.Sin(phi) * 0.50f);
        }

        private static void AppendBossTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static Mesh CreateRuntimeBossMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh ResolveRuntimeBossFacetMesh()
        {
            if (runtimeBossFacetMesh != null)
            {
                return runtimeBossFacetMesh;
            }

            const float corner = 0.44f;
            const float face = 0.56f;
            var vertices = new List<Vector3>(72);
            var triangles = new List<int>(72);
            AppendBossFacetFace(vertices, triangles, new Vector3(face, 0f, 0f),
                new Vector3(corner, -corner, -corner), new Vector3(corner, -corner, corner), new Vector3(corner, corner, corner), new Vector3(corner, corner, -corner));
            AppendBossFacetFace(vertices, triangles, new Vector3(-face, 0f, 0f),
                new Vector3(-corner, -corner, corner), new Vector3(-corner, -corner, -corner), new Vector3(-corner, corner, -corner), new Vector3(-corner, corner, corner));
            AppendBossFacetFace(vertices, triangles, new Vector3(0f, face, 0f),
                new Vector3(-corner, corner, -corner), new Vector3(corner, corner, -corner), new Vector3(corner, corner, corner), new Vector3(-corner, corner, corner));
            AppendBossFacetFace(vertices, triangles, new Vector3(0f, -face, 0f),
                new Vector3(-corner, -corner, corner), new Vector3(corner, -corner, corner), new Vector3(corner, -corner, -corner), new Vector3(-corner, -corner, -corner));
            AppendBossFacetFace(vertices, triangles, new Vector3(0f, 0f, face),
                new Vector3(corner, -corner, corner), new Vector3(-corner, -corner, corner), new Vector3(-corner, corner, corner), new Vector3(corner, corner, corner));
            AppendBossFacetFace(vertices, triangles, new Vector3(0f, 0f, -face),
                new Vector3(-corner, -corner, -corner), new Vector3(corner, -corner, -corner), new Vector3(corner, corner, -corner), new Vector3(-corner, corner, -corner));

            runtimeBossFacetMesh = new Mesh
            {
                name = "RasshiineBossFacetedBlock",
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeBossFacetMesh.SetVertices(vertices);
            runtimeBossFacetMesh.SetTriangles(triangles, 0);
            runtimeBossFacetMesh.RecalculateNormals();
            runtimeBossFacetMesh.RecalculateBounds();
            return runtimeBossFacetMesh;
        }

        private static void AppendBossFacetFace(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 center,
            Vector3 corner0,
            Vector3 corner1,
            Vector3 corner2,
            Vector3 corner3)
        {
            AppendBossFacetTriangle(vertices, triangles, center, corner0, corner1);
            AppendBossFacetTriangle(vertices, triangles, center, corner1, corner2);
            AppendBossFacetTriangle(vertices, triangles, center, corner2, corner3);
            AppendBossFacetTriangle(vertices, triangles, center, corner3, corner0);
        }

        private static void AppendBossFacetTriangle(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 center,
            Vector3 a,
            Vector3 b)
        {
            if (Vector3.Dot(Vector3.Cross(a - center, b - center), center) < 0f)
            {
                (a, b) = (b, a);
            }

            var start = vertices.Count;
            vertices.Add(center);
            vertices.Add(a);
            vertices.Add(b);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private void CreateBossContactShadow()
        {
            if (effectsRoot == null || bossAnchor == null)
            {
                return;
            }

            var shadow = new GameObject("BossContactShadow", typeof(MeshFilter), typeof(MeshRenderer));
            shadow.transform.SetParent(effectsRoot, false);
            var contactPoint = bossTransform != null ? bossTransform.position : bossAnchor.position;
            var localPosition = effectsRoot.InverseTransformPoint(contactPoint);
            localPosition.y = 0.028f;
            shadow.transform.localPosition = localPosition;
            shadow.transform.localScale = new Vector3(3.94f, 1f, 1.66f);
            shadow.GetComponent<MeshFilter>().sharedMesh = GetOrCreateLowPolyDiscMesh(
                ref bossContactShadowMesh,
                "BossContactShadowMesh",
                1f,
                16,
                0f,
                0.78f);
            var renderer = shadow.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetBossContactShadowMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material ResolveRuntimeBossMaterial()
        {
            if (runtimeBossMaterial != null)
            {
                return runtimeBossMaterial;
            }

            var shader = Resources.Load<Shader>("Shaders/RasshiineLowPolyEnvironment") ??
                         Shader.Find("Rasshiine/Low Poly Environment") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            runtimeBossMaterial = new Material(shader)
            {
                name = "Rasshiine Boss Runtime Lit",
                color = new Color(0.50f, 0.22f, 0.78f, 1f),
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            SetMaterialColor(runtimeBossMaterial, new Color(0.50f, 0.22f, 0.78f, 1f));
            if (runtimeBossMaterial.HasProperty("_Metallic"))
            {
                runtimeBossMaterial.SetFloat("_Metallic", 0f);
            }
            if (runtimeBossMaterial.HasProperty("_Smoothness"))
            {
                runtimeBossMaterial.SetFloat("_Smoothness", 0.08f);
            }
            if (runtimeBossMaterial.HasProperty("_ShadowTint"))
            {
                runtimeBossMaterial.SetColor("_ShadowTint", new Color(0.38f, 0.25f, 0.58f, 1f));
            }
            if (runtimeBossMaterial.HasProperty("_LightTint"))
            {
                runtimeBossMaterial.SetColor("_LightTint", new Color(1.0f, 0.82f, 0.68f, 1f));
            }
            if (runtimeBossMaterial.HasProperty("_RimColor"))
            {
                runtimeBossMaterial.SetColor("_RimColor", new Color(0.28f, 0.78f, 0.88f, 1f));
            }
            if (runtimeBossMaterial.HasProperty("_FacetSteps"))
            {
                runtimeBossMaterial.SetFloat("_FacetSteps", 3f);
            }
            if (runtimeBossMaterial.HasProperty("_AmbientStrength"))
            {
                runtimeBossMaterial.SetFloat("_AmbientStrength", 1.12f);
            }
            if (runtimeBossMaterial.HasProperty("_RimStrength"))
            {
                runtimeBossMaterial.SetFloat("_RimStrength", 0.11f);
            }
            runtimeBossMaterial.DisableKeyword("_EMISSION");
            if (runtimeBossMaterial.HasProperty("_EmissionColor"))
            {
                runtimeBossMaterial.SetColor("_EmissionColor", Color.black);
            }

            return runtimeBossMaterial;
        }

        private static Material ResolveRuntimeBossDeepMaterial()
        {
            return runtimeBossDeepMaterial ??= CreateRuntimeBossPaletteMaterial(
                "Rasshiine Boss Deep Facets",
                new Color(0.22f, 0.20f, 0.30f, 1f),
                new Color(0.34f, 0.30f, 0.50f, 1f),
                1.02f,
                0.08f,
                Color.black);
        }

        private static Material ResolveRuntimeBossGoldMaterial()
        {
            return runtimeBossGoldMaterial ??= CreateRuntimeBossPaletteMaterial(
                "Rasshiine Boss Coral Armor",
                new Color(0.86f, 0.28f, 0.22f, 1f),
                new Color(0.55f, 0.30f, 0.40f, 1f),
                1.02f,
                0.08f,
                Color.black);
        }

        private static Material ResolveRuntimeBossCoreMaterial()
        {
            return runtimeBossCoreMaterial ??= CreateRuntimeBossPaletteMaterial(
                "Rasshiine Boss Cyan Core",
                new Color(0.06f, 0.76f, 0.82f, 1f),
                new Color(0.18f, 0.62f, 0.80f, 1f),
                1.10f,
                0.10f,
                new Color(0.04f, 0.20f, 0.24f, 1f));
        }

        private static Material CreateRuntimeBossPaletteMaterial(
            string materialName,
            Color baseColor,
            Color shadowTint,
            float ambientStrength,
            float rimStrength,
            Color emission)
        {
            var shader = Resources.Load<Shader>("Shaders/RasshiineLowPolyEnvironment") ??
                         Shader.Find("Rasshiine/Low Poly Environment") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = baseColor,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            SetMaterialColor(material, baseColor);
            if (material.HasProperty("_ShadowTint"))
            {
                material.SetColor("_ShadowTint", shadowTint);
            }
            if (material.HasProperty("_LightTint"))
            {
                material.SetColor("_LightTint", new Color(1f, 0.88f, 0.68f, 1f));
            }
            if (material.HasProperty("_RimColor"))
            {
                material.SetColor("_RimColor", new Color(0.18f, 0.74f, 0.94f, 1f));
            }
            if (material.HasProperty("_FacetSteps"))
            {
                material.SetFloat("_FacetSteps", 3f);
            }
            if (material.HasProperty("_AmbientStrength"))
            {
                material.SetFloat("_AmbientStrength", ambientStrength);
            }
            if (material.HasProperty("_RimStrength"))
            {
                material.SetFloat("_RimStrength", rimStrength);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
            }
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
            return material;
        }

        private Material GetBossContactShadowMaterial()
        {
            if (bossContactShadowMaterial != null)
            {
                return bossContactShadowMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            bossContactShadowMaterial = new Material(shader)
            {
                name = "M_Runtime_BossContactShadow",
                color = new Color(0.035f, 0.065f, 0.11f, 0.28f),
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            bossContactShadowMaterial.SetOverrideTag("RenderType", "Transparent");
            if (bossContactShadowMaterial.HasProperty("_Color"))
            {
                bossContactShadowMaterial.SetColor("_Color", bossContactShadowMaterial.color);
            }
            if (bossContactShadowMaterial.HasProperty("_BaseColor"))
            {
                bossContactShadowMaterial.SetColor("_BaseColor", bossContactShadowMaterial.color);
            }
            return bossContactShadowMaterial;
        }

        private static void ConfigureBossRenderers(GameObject model, Material material)
        {
            if (model == null)
            {
                return;
            }

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private void SpawnParticipants()
        {
            if (state?.Participants == null)
            {
                return;
            }

            var visibleParticipantIndices = ResolveVisibleParticipantIndices();
            var visibleCount = visibleParticipantIndices.Count;
            for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                var participantIndex = visibleParticipantIndices[visibleIndex];
                var participant = state.Participants[participantIndex];
                var memberPrefab = theme.MemberPlaceholderPrefab;
                var materialOverride = memberPrefab == null ? theme.MemberMaterial : null;
                var model = InstantiateModel(memberPrefab, partyAnchor, participant.Nickname, materialOverride, false);
                model.transform.position = CalculateParticipantFormationWorldPosition(visibleIndex, visibleCount);
                ConfigureParticipantModel(model, memberPrefab != null, participantIndex, participant.UserId);
                ApplyBattleRoleSilhouette(model, participant.Role);
                FaceTarget(model.transform, ResolveBossCenter());
                model.GetComponent<MemberAvatarAnimator>()?.PlayIdle(0f);
                participantTransforms[participant.UserId] = model.transform;
                participantBasePositions[participant.UserId] = model.transform.localPosition;
            }
        }

        private List<int> ResolveVisibleParticipantIndices()
        {
            var visibleIndices = new List<int>(MaxVisiblePartyMembers);
            if (state?.Participants == null)
            {
                return visibleIndices;
            }

            var visibleCount = Mathf.Min(MaxVisiblePartyMembers, state.Participants.Count);
            for (var index = 0; index < visibleCount; index++)
            {
                visibleIndices.Add(index);
            }

            if (string.IsNullOrEmpty(controlledParticipantId))
            {
                return visibleIndices;
            }

            var controlledIndex = -1;
            for (var index = 0; index < state.Participants.Count; index++)
            {
                if (state.Participants[index].UserId == controlledParticipantId)
                {
                    controlledIndex = index;
                    break;
                }
            }

            if (controlledIndex < 0 || visibleIndices.Contains(controlledIndex))
            {
                return visibleIndices;
            }

            if (visibleIndices.Count < MaxVisiblePartyMembers)
            {
                visibleIndices.Add(controlledIndex);
            }
            else if (visibleIndices.Count > 0)
            {
                visibleIndices[visibleIndices.Count - 1] = controlledIndex;
            }

            return visibleIndices;
        }

        private bool ShouldRebuildVisibleParticipants()
        {
            if (state?.Participants == null || state.Status == BattleStatus.Scheduled || partyAnchor == null)
            {
                return false;
            }

            var desiredIndices = ResolveVisibleParticipantIndices();
            if (participantTransforms.Count != desiredIndices.Count)
            {
                return true;
            }

            foreach (var participantIndex in desiredIndices)
            {
                var participantId = state.Participants[participantIndex].UserId;
                if (!participantTransforms.ContainsKey(participantId))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildVisibleParticipants()
        {
            StopParticipantActionAnimations();
            if (partyAnchor != null)
            {
                for (var index = 0; index < partyAnchor.childCount; index++)
                {
                    partyAnchor.GetChild(index).gameObject.SetActive(false);
                }
            }

            ClearChildren(partyAnchor);
            participantTransforms.Clear();
            participantBasePositions.Clear();
            SpawnParticipants();
        }

        private Vector3 CalculateParticipantFormationWorldPosition(int index, int count)
        {
            var formationCenter = ResolveBossCenter();
            // Raising the live boss for the approved composition must not make
            // the heroes float; their authored foot planes stay on PartyAnchor.
            formationCenter.y = partyAnchor != null ? partyAnchor.position.y : 0f;
            return formationCenter + Vector3.forward * MemberFormationCenterDepthOffset + CalculateParticipantFormationOffset(index, count);
        }

        private Vector3 ResolveBossCenter()
        {
            if (bossTransform != null)
            {
                return bossTransform.position;
            }

            if (bossAnchor != null)
            {
                return bossAnchor.TransformPoint(Vector3.right * BossVisualHorizontalOffset);
            }

            return Vector3.right * BossVisualHorizontalOffset;
        }

        private static Vector3 CalculateParticipantFormationOffset(int index, int count)
        {
            if (count <= 1)
            {
                return new Vector3(-3.2f, 0f, MemberFormationFrontDepth);
            }

            // Only the three presentation representatives are rendered. The full
            // authoritative participant list remains in state for HP, actions and
            // score logic, while this left-side arc keeps each hero readable.
            var normalizedIndex = Mathf.Clamp01(index / (float)Mathf.Max(1, count - 1));
            var x = Mathf.Lerp(MemberFormationLeftEdge, MemberFormationRightEdge, normalizedIndex);
            var rowOffset = index % 2 == 0 ? 0f : -MemberFormationRowDepth;
            var arcLift = Mathf.Sin(normalizedIndex * Mathf.PI) * 0.22f;
            return new Vector3(x, 0f, MemberFormationFrontDepth + rowOffset + arcLift);
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

        private void ConfigureParticipantModel(GameObject model, bool usesAuthoredPrefab, int participantIndex, string participantId)
        {
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
            }

            var scale = usesAuthoredPrefab
                ? CalculateScaleFactorForTargetHeight(model, MemberTargetHeight, MemberFallbackScale, false)
                : MemberFallbackScale;
            model.transform.localScale = Vector3.one * scale;
            var avatarAnimator = model.GetComponent<MemberAvatarAnimator>() ?? model.AddComponent<MemberAvatarAnimator>();
            avatarAnimator.ResolveAnimator();
            avatarAnimator.ConfigureVariation(ComputeStableAnimationSeed(participantId, participantIndex));
            // MC01 is modular and ships with many body, cloak, accessory and
            // weapon variants enabled together. Resolve one valid lightweight
            // loadout first; overlapping coplanar costumes otherwise collapse
            // into an unreadable white/cyan silhouette.
            TinyHeroCosmeticApplicator.Apply(model, null);
            NormalizeParticipantMaterials(model, participantIndex);
        }

        private static void ApplyBattleRoleSilhouette(GameObject model, BattleRole role)
        {
            if (model == null)
            {
                return;
            }

            // Reuse authored Tiny Hero variants instead of tinting or replacing
            // their texture materials. The three on-screen representatives now
            // read as distinct combat roles while keeping the original PBR art.
            switch (role)
            {
                case BattleRole.Healer:
                    SelectBattleRoleVariant(model, "body", "Body16");
                    SelectBattleRoleVariant(model, "back", "Cloak03");
                    SelectBattleRoleVariant(model, "accessory", "AC01_Heart");
                    SelectBattleRoleVariant(model, "weapon", "Wand07");
                    break;
                case BattleRole.Defender:
                    SelectBattleRoleVariant(model, "body", "Body16");
                    SelectBattleRoleVariant(model, "back", "Cloak02");
                    SelectBattleRoleVariant(model, "accessory", "AC05_Horn04");
                    SelectBattleRoleVariant(model, "weapon", "OHS16_Sword");
                    break;
                case BattleRole.Supporter:
                    SelectBattleRoleVariant(model, "body", "Body05");
                    SelectBattleRoleVariant(model, "back", "Cloak03");
                    SelectBattleRoleVariant(model, "accessory", "AC01_Heart");
                    SelectBattleRoleVariant(model, "weapon", "Wand07");
                    break;
                default:
                    SelectBattleRoleVariant(model, "body", "Body05");
                    SelectBattleRoleVariant(model, "back", "Cloak02");
                    SelectBattleRoleVariant(model, "accessory", "AC05_Horn04");
                    SelectBattleRoleVariant(model, "weapon", "OHS09_Sword");
                    break;
            }
        }

        private static void SelectBattleRoleVariant(GameObject model, string slot, string variantName)
        {
            var candidates = model.GetComponentsInChildren<Transform>(true);
            Transform firstMatch = null;
            Transform mountedWeaponMatch = null;
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                if (candidate == model.transform || !TinyHeroCosmeticApplicator.IsSlotCandidate(slot, candidate.name))
                {
                    continue;
                }

                candidate.gameObject.SetActive(false);
                if (!string.Equals(candidate.name, variantName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                firstMatch ??= candidate;
                if (string.Equals(slot, "weapon", System.StringComparison.OrdinalIgnoreCase) &&
                    HasBattleRoleAncestor(candidate, "weapon_r"))
                {
                    mountedWeaponMatch = candidate;
                }
            }

            (mountedWeaponMatch ?? firstMatch)?.gameObject.SetActive(true);
        }

        private static bool HasBattleRoleAncestor(Transform candidate, string ancestorName)
        {
            for (var current = candidate.parent; current != null; current = current.parent)
            {
                if (string.Equals(current.name, ancestorName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ComputeStableAnimationSeed(string participantId, int participantIndex)
        {
            unchecked
            {
                var hash = 17 + participantIndex * 31;
                if (!string.IsNullOrEmpty(participantId))
                {
                    for (var index = 0; index < participantId.Length; index++)
                    {
                        hash = hash * 23 + participantId[index];
                    }
                }

                return hash == int.MinValue ? 0 : Mathf.Abs(hash);
            }
        }

        private void NormalizeParticipantMaterials(GameObject model, int participantIndex)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var index = 0; index < materials.Length; index++)
                {
                    var material = materials[index];
                    if (IsBrokenOrErrorMaterial(material))
                    {
                        materials[index] = CreateParticipantRepairMaterial(participantIndex, index);
                        changed = true;
                    }
                    else if (TryGetCompatibleParticipantMaterial(material, out var compatibleMaterial))
                    {
                        materials[index] = IsParticipantPaletteTarget(renderer.transform)
                            ? ResolveParticipantPaletteMaterial(compatibleMaterial, participantIndex)
                            : compatibleMaterial;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }

                // Imported character packages occasionally leave per-renderer color
                // or emission overrides behind. Those overrides take precedence over
                // the bounded runtime material and reproduced the white/cyan WebGL
                // silhouettes even after emission was removed from the material.
                renderer.SetPropertyBlock(null);
            }
        }

        private static bool IsBrokenOrErrorMaterial(Material material)
        {
            if (material == null || material.shader == null)
            {
                return true;
            }

            var shaderName = material.shader.name;
            if (!string.IsNullOrEmpty(shaderName) && shaderName.Contains("InternalErrorShader"))
            {
                return true;
            }

            var color = Color.white;
            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
            }

            return color.r > 0.82f && color.g < 0.24f && color.b > 0.72f;
        }

        private static bool IsParticipantPaletteTarget(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                var name = current.name;
                if (name.StartsWith("Hair", System.StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Body", System.StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Cloak", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static Material ResolveParticipantPaletteMaterial(Material source, int participantIndex)
        {
            if (source == null)
            {
                return null;
            }

            if (!ParticipantPaletteMaterialCache.TryGetValue(source, out var variants) || variants == null)
            {
                variants = new Material[3];
                ParticipantPaletteMaterialCache[source] = variants;
            }

            var paletteIndex = Mathf.Abs(participantIndex) % variants.Length;
            if (variants[paletteIndex] != null)
            {
                return variants[paletteIndex];
            }

            var material = new Material(source)
            {
                name = $"{source.name}_Party{paletteIndex}",
                color = ParticipantIdentityPalette[paletteIndex],
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            SetMaterialColor(material, ParticipantIdentityPalette[paletteIndex]);
            variants[paletteIndex] = material;
            return material;
        }

        private static bool TryGetCompatibleParticipantMaterial(Material source, out Material material)
        {
            material = null;
            if (source == null || source.shader == null)
            {
                return false;
            }

            if (ParticipantMaterialCache.TryGetValue(source, out var cachedMaterial) && cachedMaterial != null)
            {
                material = cachedMaterial;
                return true;
            }

            var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            if (shader == null)
            {
                return false;
            }

            var sourceTexture = GetParticipantMainTexture(source);
            var color = Color.white;
            if (source.HasProperty("_BaseColor"))
            {
                color = source.GetColor("_BaseColor");
            }
            else if (source.HasProperty("_Color"))
            {
                color = source.GetColor("_Color");
            }
            color = ClampParticipantBaseColor(color);

            material = new Material(shader)
            {
                name = source.name + "_RuntimeSimpleLitLdr",
                color = color,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            SetMaterialColor(material, color);
            SetMaterialTexture(material, "_BaseMap", sourceTexture);
            SetMaterialTexture(material, "_MainTex", sourceTexture);
            CopyParticipantTextureTransform(source, material);

            // URP Simple Lit is the stable single-main-light path on Metal and
            // WebGL. Keep the authored color atlas, but remove the legacy PBR
            // metallic/emission contribution that clipped costumes to cyan-white.
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }
            if (material.HasProperty("_SpecColor"))
            {
                material.SetColor("_SpecColor", Color.black);
            }
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SPECGLOSSMAP");
            material.DisableKeyword("_SPECULAR_COLOR");

            material.DisableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            ParticipantMaterialCache[source] = material;
            return true;
        }

        private static Color ClampParticipantBaseColor(Color source)
        {
            return new Color(
                Mathf.Clamp(source.r, 0.035f, 0.86f),
                Mathf.Clamp(source.g, 0.035f, 0.86f),
                Mathf.Clamp(source.b, 0.035f, 0.86f),
                Mathf.Clamp01(source.a));
        }

        private static Texture GetParticipantMainTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
            {
                return material.GetTexture("_BaseMap");
            }

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
            {
                return material.GetTexture("_MainTex");
            }

            return material.mainTexture;
        }

        private static void CopyParticipantTextureTransform(Material source, Material target)
        {
            if (source == null || target == null || !target.HasProperty("_BaseMap"))
            {
                return;
            }

            var sourceProperty = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null
                ? "_BaseMap"
                : source.HasProperty("_MainTex") ? "_MainTex" : string.Empty;
            if (string.IsNullOrEmpty(sourceProperty))
            {
                return;
            }

            target.SetTextureScale("_BaseMap", source.GetTextureScale(sourceProperty));
            target.SetTextureOffset("_BaseMap", source.GetTextureOffset(sourceProperty));
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetMaterialTexture(Material material, string propertyName, Texture texture)
        {
            if (material == null || texture == null || !material.HasProperty(propertyName))
            {
                return;
            }

            material.SetTexture(propertyName, texture);
        }

        private static void SetMaterialColorProperty(Material material, string propertyName, Color color)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void SetMaterialFloatProperty(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static Material CreateParticipantRepairMaterial(int participantIndex, int materialIndex)
        {
            var primary = ParticipantIdentityPalette[Mathf.Abs(participantIndex) % ParticipantIdentityPalette.Length];
            var color = Color.Lerp(primary, Color.white, Mathf.Clamp01(materialIndex) * 0.14f);
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Diffuse");
            var material = new Material(shader)
            {
                name = $"M_Runtime_TinyHeroRepair_{participantIndex:00}_{materialIndex:00}",
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.05f);
            }
            if (material.HasProperty("_SpecColor"))
            {
                material.SetColor("_SpecColor", Color.black);
            }
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SPECGLOSSMAP");
            material.DisableKeyword("_SPECULAR_COLOR");

            material.DisableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            return material;
        }

        private static void ApplyParticipantMaterial(GameObject model, Material material)
        {
            if (material == null)
            {
                return;
            }

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private Vector3 CalculateCurrentBossScale()
        {
            if (state?.Boss == null || state.Boss.MaxHp <= 0)
            {
                return bossMaxScale;
            }

            var hp01 = Mathf.Clamp01(state.Boss.CurrentHp / (float)state.Boss.MaxHp);
            // HP is already communicated by the HUD. Extreme physical shrinking
            // made the completed boss look like a distant live miniature instead
            // of a defeated threat; retain at least 94% of the authored silhouette.
            return bossMaxScale * Mathf.Lerp(0.94f, 1f, hp01);
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
            var material = GetArenaLineMaterial();
            if (!CreateLowPolyNatureArena())
            {
                CreateMeadowArenaGround();
            }

            CreateBattleClearingMesh();
            CreateBossFocusDaisMesh();

            var ringCoral = CreateArenaLineColor(new Color(0.76f, 0.25f, 0.20f, 1f), 0.18f);
            var ringSand = CreateArenaLineColor(new Color(0.78f, 0.64f, 0.48f, 1f), 0.22f);
            // LineRenderer quantizes its gradient keys to 8-bit alpha; 0.50 rounds to
            // 128/255 (0.50196). Keep the translucent WebGL grid strictly below 0.5.
            CreateRing(1.58f, material, CreateArenaLineColor(new Color(0.78f, 0.64f, 0.48f, 1f), 0.28f));
            for (var ring = 1; ring <= 3; ring++)
            {
                CreateRing(ring * 2.15f, material, ring % 2 == 0 ? ringSand : ringCoral);
            }

            for (var spoke = 0; spoke < 6; spoke++)
            {
                var angle = spoke / 6f * Mathf.PI * 2f;
                var start = new Vector3(Mathf.Cos(angle) * 1.25f, 0.006f, Mathf.Sin(angle) * 1.25f);
                var end = new Vector3(Mathf.Cos(angle) * 6.15f, 0.006f, Mathf.Sin(angle) * 6.15f);
                CreateLine($"Arena_Spoke_{spoke}", start, end, material, CreateArenaLineColor(new Color(0.72f, 0.47f, 0.34f, 1f), 0.13f), 0.012f);
            }
        }

        private bool CreateLowPolyNatureArena()
        {
            if (effectsRoot == null)
            {
                return false;
            }

            var arenaRoot = new GameObject("Low Poly Nature Battle Arena").transform;
            arenaRoot.SetParent(effectsRoot, false);
            arenaRoot.localPosition = Vector3.zero;
            CreateGeneratedArenaGround(arenaRoot);
            CreateToyCanyonSetPieces(arenaRoot);
            return true;
        }

        private void CreateToyCanyonSetPieces(Transform arenaRoot)
        {
            // The approved direction is a saturated, toy-like canyon rather than a
            // generic green meadow. Every object below reuses either the generated
            // arena disc or the boss's 24-triangle faceted block. There are no
            // textures, punctual lights, colliders, particles, or post effects.
            var canyonShadow = GetNatureMaterial("canyon-shadow", new Color(0.64f, 0.47f, 0.45f, 1f), 0.04f);
            var canyonCoral = GetNatureMaterial("canyon-coral", new Color(1.00f, 0.43f, 0.29f, 1f), 0.05f);
            var canyonPeach = GetNatureMaterial("canyon-peach", new Color(1.00f, 0.67f, 0.46f, 1f), 0.05f);
            var canyonSandstone = GetNatureMaterial("canyon-sandstone", new Color(0.92f, 0.80f, 0.70f, 1f), 0.05f);
            var lime = GetNatureMaterial("canyon-lime", new Color(0.67f, 0.88f, 0.12f, 1f), 0.04f);
            var violet = GetNatureMaterial("canyon-violet", new Color(0.58f, 0.24f, 0.86f, 1f), 0.06f);
            var cloudWhite = GetNatureMaterial("sky-cloud-white", new Color(1.00f, 1.00f, 1.00f, 1f), 0.02f);
            var bridgeWood = GetNatureMaterial("bridge-wood", new Color(0.78f, 0.42f, 0.20f, 1f), 0.04f);
            ConfigureCloudMaterial(cloudWhite);
            ConfigureBridgeMaterial(bridgeWood);

            CreateCanyonWater(arenaRoot);

            // Foreground side ledges frame the party without hiding the controls.
            CreateCanyonMesa(arenaRoot, "LowPoly Rock Foreground Left", new Vector3(-7.45f, 0f, -1.65f), new Vector3(2.70f, 0.78f, 2.35f), canyonShadow, canyonPeach, true);
            CreateCanyonMesa(arenaRoot, "LowPoly Rock Foreground Right", new Vector3(7.35f, 0f, -1.25f), new Vector3(2.90f, 0.92f, 2.45f), canyonShadow, canyonSandstone, true);

            // Midground walls produce the strong coral/sand color field and hard
            // contact shadows from the reference without filling the boss lane.
            CreateCanyonMesa(arenaRoot, "LowPoly Rock Mid Left", new Vector3(-7.35f, 0f, 3.70f), new Vector3(2.75f, 1.35f, 2.45f), canyonShadow, canyonCoral, true);
            CreateCanyonMesa(arenaRoot, "LowPoly Rock Mid Right", new Vector3(7.45f, 0f, 4.05f), new Vector3(2.82f, 1.48f, 2.45f), canyonShadow, canyonPeach, true);

            // Rear banks sit on both sides of the cyan lagoon. Shadow casting is
            // disabled here to keep WebGL's one directional shadow map inexpensive.
            CreateCanyonMesa(arenaRoot, "LowPoly Distant Mesa Left", new Vector3(-8.10f, 0f, 8.65f), new Vector3(3.85f, 3.18f, 2.75f), canyonSandstone, canyonCoral, false);
            CreateCanyonMesa(arenaRoot, "LowPoly Distant Mesa Right", new Vector3(8.05f, 0f, 8.55f), new Vector3(3.72f, 3.02f, 2.68f), canyonSandstone, canyonPeach, false);
            CreateCanyonMesa(arenaRoot, "LowPoly Far Mesa Left", new Vector3(-5.05f, 0f, 11.15f), new Vector3(2.60f, 2.28f, 1.90f), canyonSandstone, canyonCoral, false);
            CreateCanyonMesa(arenaRoot, "LowPoly Far Mesa Right", new Vector3(5.20f, 0f, 11.25f), new Vector3(2.72f, 2.38f, 1.96f), canyonSandstone, canyonPeach, false);
            // These two banks physically receive the suspension bridge. Without
            // them the deck and ropes read as debris floating in the sky.
            CreateCanyonMesa(arenaRoot, "LowPoly Bridge Bank Left", new Vector3(-5.05f, 0f, 9.05f), new Vector3(2.10f, 2.22f, 2.05f), canyonShadow, canyonCoral, false);
            CreateCanyonMesa(arenaRoot, "LowPoly Bridge Bank Right", new Vector3(4.75f, 0f, 9.05f), new Vector3(2.10f, 2.16f, 2.05f), canyonShadow, canyonPeach, false);

            CreateLimeShrub(arenaRoot, 1, new Vector3(-5.15f, 0f, 0.45f), 0.82f, lime);
            CreateLimeShrub(arenaRoot, 2, new Vector3(5.25f, 0f, 0.75f), 0.70f, lime);
            CreateLimeShrub(arenaRoot, 3, new Vector3(-4.55f, 0f, 6.55f), 0.76f, lime);
            CreateLimeShrub(arenaRoot, 4, new Vector3(4.70f, 0f, 6.85f), 0.88f, lime);
            CreateLimeShrub(arenaRoot, 5, new Vector3(-6.25f, 0f, 5.05f), 0.58f, lime);
            CreateLimeShrub(arenaRoot, 6, new Vector3(-2.20f, 0f, 7.10f), 0.54f, lime);
            CreateLimeShrub(arenaRoot, 7, new Vector3(2.55f, 0f, 7.45f), 0.62f, lime);

            CreateCanyonCrystalCluster(arenaRoot, 1, new Vector3(-5.45f, 0f, 4.85f), 1.00f, violet);
            CreateCanyonCrystalCluster(arenaRoot, 2, new Vector3(5.65f, 0f, 5.45f), 0.82f, violet);
            CreateCanyonCrystalCluster(arenaRoot, 3, new Vector3(-6.75f, 0f, 1.65f), 0.66f, violet);
            CreateCanyonCrystalCluster(arenaRoot, 4, new Vector3(-4.05f, 0f, 7.65f), 0.78f, violet);

            // Small opaque mesh clouds replace the full-screen procedural cell
            // that produced a translucent diagonal wedge in WebGL. Nine parts
            // share the same 24-triangle mesh and material, cast no shadows and
            // remain distant enough to behave like clean graphic sky accents.
            battleCloudClusters.Add(CreateCanyonCloudCluster(arenaRoot, 1, new Vector3(-7.40f, 4.85f, 15.50f), 1.02f, cloudWhite));
            battleCloudClusters.Add(CreateCanyonCloudCluster(arenaRoot, 2, new Vector3(0.20f, 5.35f, 18.20f), 0.88f, cloudWhite));
            battleCloudClusters.Add(CreateCanyonCloudCluster(arenaRoot, 3, new Vector3(7.30f, 4.95f, 16.40f), 0.96f, cloudWhite));
            PositionBattleCloudsInViewport();
            CreateCanyonBridge(arenaRoot, new Vector3(-0.30f, 2.58f, 9.02f), bridgeWood);

            // Small asymmetric stones stop the arena from reading as a single flat
            // disc while sharing the same matte sandstone material and facet mesh.
            CreateGroundedCanyonFacet(arenaRoot, "LowPoly Pebble 01", new Vector3(-4.35f, 0f, -1.30f), new Vector3(0.64f, 0.25f, 0.48f), new Vector3(0f, 18f, 8f), canyonSandstone, true);
            CreateGroundedCanyonFacet(arenaRoot, "LowPoly Pebble 02", new Vector3(4.65f, 0f, -0.65f), new Vector3(0.54f, 0.30f, 0.68f), new Vector3(0f, -24f, -5f), canyonCoral, true);
            CreateGroundedCanyonFacet(arenaRoot, "LowPoly Pebble 03", new Vector3(-2.90f, 0f, 5.55f), new Vector3(0.42f, 0.22f, 0.50f), new Vector3(0f, 38f, 4f), canyonSandstone, false);
        }

        private void CreateCanyonWater(Transform parent)
        {
            var water = new GameObject("LowPoly Cyan Canyon Water", typeof(MeshFilter), typeof(MeshRenderer));
            water.transform.SetParent(parent, false);
            // Keep the lagoon behind the combatants as a narrow canyon ribbon.
            // The previous oval reached under the boss and split the arena in two.
            water.transform.localPosition = new Vector3(-0.15f, 0.045f, 9.10f);
            water.transform.localScale = new Vector3(0.40f, 1f, 0.25f);
            water.GetComponent<MeshFilter>().sharedMesh = generatedArenaGroundMesh;
            var renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetNatureMaterial("canyon-water", new Color(0.02f, 0.80f, 0.92f, 1f), 0.12f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void CreateCanyonMesa(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 dimensions,
            Material baseMaterial,
            Material capMaterial,
            bool castsShadow)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;

            CreateGroundedCanyonFacet(root, "Shadow Facet", Vector3.zero, dimensions, new Vector3(0f, 7f, 0f), baseMaterial, castsShadow);
            var capDimensions = new Vector3(dimensions.x * 0.93f, dimensions.y * 0.34f, dimensions.z * 0.92f);
            var capPosition = new Vector3(0.05f, dimensions.y * 1.02f, -0.02f);
            CreateGroundedCanyonFacet(root, "Sunlit Coral Cap", capPosition, capDimensions, new Vector3(0f, -6f, 0f), capMaterial, castsShadow);
        }

        private static void CreateLimeShrub(Transform parent, int index, Vector3 localPosition, float scale, Material material)
        {
            var root = new GameObject($"LowPoly Lime Shrub {index:00}").transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            CreateGroundedCanyonFacet(root, "Lime Crown Left", new Vector3(-0.24f * scale, 0f, 0f), new Vector3(0.70f, 0.62f, 0.66f) * scale, new Vector3(0f, index * 29f, -8f), material, true);
            CreateGroundedCanyonFacet(root, "Lime Crown Right", new Vector3(0.27f * scale, 0.05f, 0.06f), new Vector3(0.62f, 0.74f, 0.64f) * scale, new Vector3(0f, index * -23f, 6f), material, true);
        }

        private static void CreateCanyonCrystalCluster(Transform parent, int index, Vector3 localPosition, float scale, Material material)
        {
            var root = new GameObject($"LowPoly Crystal Cluster {index:00}").transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            CreateGroundedCanyonFacet(root, "Violet Crystal Tall", Vector3.zero, new Vector3(0.42f, 1.28f, 0.44f) * scale, new Vector3(0f, index * 31f, -6f), material, true);
            CreateGroundedCanyonFacet(root, "Violet Crystal Left", new Vector3(-0.47f * scale, 0f, 0.06f), new Vector3(0.34f, 0.82f, 0.38f) * scale, new Vector3(0f, index * 19f, 15f), material, true);
            CreateGroundedCanyonFacet(root, "Violet Crystal Right", new Vector3(0.43f * scale, 0f, -0.02f), new Vector3(0.31f, 0.68f, 0.35f) * scale, new Vector3(0f, index * -17f, -13f), material, true);
        }

        private static Transform CreateCanyonCloudCluster(Transform parent, int index, Vector3 localPosition, float scale, Material material)
        {
            var root = new GameObject($"LowPoly Cloud Cluster {index:00}").transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            CreateBossPart(root, "Cloud Facet Center", Vector3.zero, new Vector3(1.04f, 0.52f, 0.48f) * scale, new Vector3(0f, index * 7f, 0f), material, false);
            CreateBossPart(root, "Cloud Facet Left", new Vector3(-0.76f, -0.03f, 0.03f) * scale, new Vector3(0.68f, 0.40f, 0.40f) * scale, new Vector3(0f, -8f, 4f), material, false);
            CreateBossPart(root, "Cloud Facet Right", new Vector3(0.80f, 0.05f, -0.02f) * scale, new Vector3(0.72f, 0.43f, 0.42f) * scale, new Vector3(0f, 9f, -3f), material, false);
            return root;
        }

        private static void ConfigureCloudMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_ShadowTint"))
            {
                material.SetColor("_ShadowTint", new Color(0.74f, 0.84f, 0.95f, 1f));
            }
            if (material.HasProperty("_LightTint"))
            {
                material.SetColor("_LightTint", new Color(1.00f, 0.99f, 0.94f, 1f));
            }
            if (material.HasProperty("_RimColor"))
            {
                material.SetColor("_RimColor", new Color(0.76f, 0.90f, 1.00f, 1f));
            }
            if (material.HasProperty("_AmbientStrength"))
            {
                material.SetFloat("_AmbientStrength", 1.16f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", new Color(0.08f, 0.10f, 0.12f, 1f));
            }
        }

        private static void ConfigureBridgeMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_ShadowTint"))
            {
                material.SetColor("_ShadowTint", new Color(0.52f, 0.25f, 0.12f, 1f));
            }
            if (material.HasProperty("_LightTint"))
            {
                material.SetColor("_LightTint", new Color(1.00f, 0.84f, 0.58f, 1f));
            }
            if (material.HasProperty("_AmbientStrength"))
            {
                material.SetFloat("_AmbientStrength", 1.12f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", new Color(0.055f, 0.022f, 0.008f, 1f));
            }
        }

        private static void CreateCanyonBridge(Transform parent, Vector3 localPosition, Material material)
        {
            var root = new GameObject("LowPoly Canyon Bridge").transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;

            const int plankCount = 9;
            const float halfSpan = 3.36f;
            const float plankStep = halfSpan * 2f / (plankCount - 1);
            CreateBossPart(root, "Bridge Deck", new Vector3(0f, -0.02f, 0f), new Vector3(3.56f, 0.10f, 0.70f), Vector3.zero, material, false);
            for (var plankIndex = 0; plankIndex < plankCount; plankIndex++)
            {
                var normalized = plankIndex / (float)(plankCount - 1);
                var x = Mathf.Lerp(-halfSpan, halfSpan, normalized);
                var sag = Mathf.Sin(normalized * Mathf.PI) * -0.12f;
                CreateBossPart(root, $"Bridge Plank {plankIndex + 1:00}", new Vector3(x, 0.13f + sag, 0f), new Vector3(0.78f, 0.14f, 0.80f), new Vector3(0f, (plankIndex % 2 == 0 ? -1.5f : 1.5f), 0f), material, false);
            }

            for (var side = -1; side <= 1; side += 2)
            {
                for (var segmentIndex = 0; segmentIndex < plankCount - 1; segmentIndex++)
                {
                    var startX = -halfSpan + segmentIndex * plankStep;
                    var endX = startX + plankStep;
                    var startNormalized = segmentIndex / (float)(plankCount - 1);
                    var endNormalized = (segmentIndex + 1) / (float)(plankCount - 1);
                    var startY = 0.94f - Mathf.Sin(startNormalized * Mathf.PI) * 0.38f;
                    var endY = 0.94f - Mathf.Sin(endNormalized * Mathf.PI) * 0.38f;
                    var slope = Mathf.Atan2(endY - startY, endX - startX) * Mathf.Rad2Deg;
                    CreateBossPart(root, $"Bridge Rail {(side < 0 ? "Near" : "Far")} {segmentIndex + 1:00}",
                        new Vector3((startX + endX) * 0.5f, (startY + endY) * 0.5f, side * 0.72f),
                        new Vector3(0.76f, 0.065f, 0.075f), new Vector3(0f, 0f, slope), material, false);
                }

                // Five slim suspenders per side visually connect rope to deck;
                // they share the same tiny mesh/material and cast no shadows.
                for (var hangerIndex = 0; hangerIndex < plankCount; hangerIndex += 2)
                {
                    var normalized = hangerIndex / (float)(plankCount - 1);
                    var x = Mathf.Lerp(-halfSpan, halfSpan, normalized);
                    var ropeY = 0.94f - Mathf.Sin(normalized * Mathf.PI) * 0.38f;
                    var deckY = 0.18f - Mathf.Sin(normalized * Mathf.PI) * 0.12f;
                    CreateBossPart(root, $"Bridge Hanger {(side < 0 ? "Near" : "Far")} {hangerIndex + 1:00}",
                        new Vector3(x, (ropeY + deckY) * 0.5f, side * 0.72f),
                        new Vector3(0.055f, (ropeY - deckY) * 0.48f, 0.055f), Vector3.zero, material, false);
                }
            }

            CreateBossPart(root, "Bridge Post Near Left", new Vector3(-3.44f, 0.54f, -0.72f), new Vector3(0.12f, 0.90f, 0.12f), Vector3.zero, material, false);
            CreateBossPart(root, "Bridge Post Near Right", new Vector3(3.44f, 0.54f, -0.72f), new Vector3(0.12f, 0.90f, 0.12f), Vector3.zero, material, false);
            CreateBossPart(root, "Bridge Post Far Left", new Vector3(-3.44f, 0.54f, 0.72f), new Vector3(0.12f, 0.90f, 0.12f), Vector3.zero, material, false);
            CreateBossPart(root, "Bridge Post Far Right", new Vector3(3.44f, 0.54f, 0.72f), new Vector3(0.12f, 0.90f, 0.12f), Vector3.zero, material, false);
        }

        private static GameObject CreateGroundedCanyonFacet(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool castsShadow)
        {
            // RasshiineBossFacetedBlock has a 1.12-unit vertical bound. Offset by
            // half of that height so every scaled part rests on the local ground.
            localPosition.y += localScale.y * 0.56f - 0.14f;
            return CreateBossPart(parent, objectName, localPosition, localScale, localEuler, material, castsShadow);
        }

        private void CreateMeadowArenaGround()
        {
            var ground = new GameObject("MeadowArenaGround", typeof(MeshFilter), typeof(MeshRenderer));
            ground.name = "MeadowArenaGround";
            ground.transform.SetParent(effectsRoot, false);
            ground.transform.localPosition = new Vector3(0f, -0.055f, 0f);
            ground.GetComponent<MeshFilter>().sharedMesh = GetOrCreateLowPolyDiscMesh(
                ref meadowArenaGroundMesh,
                "LowPolyBattleDisc_FallbackGround",
                8.4f,
                22,
                0.025f,
                0.92f);

            var renderer = ground.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterial = GetNatureMaterial("fallback-canyon-sand", new Color(1.00f, 0.65f, 0.42f, 1f), 0.05f);
        }

        private void CreateGeneratedArenaGround(Transform parent)
        {
            var ground = new GameObject("LowPoly Generated Arena Ground", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, -0.14f, 1.0f);
            ground.GetComponent<MeshFilter>().sharedMesh = GetOrCreateLowPolyDiscMesh(
                ref generatedArenaGroundMesh,
                "LowPolyBattleDisc_NatureGround",
                8.6f,
                26,
                0.055f,
                0.74f);
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterial = GetNatureMaterial("generated-canyon-sand", new Color(1.00f, 0.65f, 0.42f, 1f), 0.05f);
        }

        private void CreateBattleClearingMesh()
        {
            var clearing = new GameObject("LowPolyBattleClearing", typeof(MeshFilter), typeof(MeshRenderer));
            clearing.transform.SetParent(effectsRoot, false);
            clearing.transform.localPosition = new Vector3(0f, -0.006f, 0f);
            clearing.GetComponent<MeshFilter>().sharedMesh = GetOrCreateLowPolyDiscMesh(
                ref battleClearingMesh,
                "LowPolyBattleDisc_Clearing",
                5.55f,
                24,
                0.012f,
                0.86f);

            var renderer = clearing.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterial = GetNatureMaterial("battle-canyon-clearing", new Color(1.00f, 0.70f, 0.48f, 1f), 0.05f);
        }

        private void CreateBossFocusDaisMesh()
        {
            var focusPosition = Vector3.right * BossVisualHorizontalOffset;
            if (bossAnchor != null && effectsRoot != null)
            {
                focusPosition = effectsRoot.InverseTransformPoint(
                    bossAnchor.TransformPoint(Vector3.right * BossVisualHorizontalOffset));
            }

            var terrace = new GameObject("LowPolyBossRuneTerrace", typeof(MeshFilter), typeof(MeshRenderer));
            terrace.transform.SetParent(effectsRoot, false);
            terrace.transform.localPosition = new Vector3(focusPosition.x, -0.018f, focusPosition.z);
            terrace.transform.localScale = new Vector3(2.05f, 1f, 1.45f);
            terrace.GetComponent<MeshFilter>().sharedMesh = GetOrCreateLowPolyDiscMesh(
                ref bossFocusDaisMesh,
                "BossFocusDaisMesh",
                1.55f,
                16,
                0.012f,
                0.72f);
            var terraceRenderer = terrace.GetComponent<MeshRenderer>();
            terraceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            terraceRenderer.receiveShadows = true;
            terraceRenderer.lightProbeUsage = LightProbeUsage.Off;
            terraceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terraceRenderer.sharedMaterial = GetNatureMaterial("boss-rune-terrace", new Color(0.98f, 0.50f, 0.34f, 1f), 0.04f);

            var dais = new GameObject("LowPolyBossFocusDais", typeof(MeshFilter), typeof(MeshRenderer));
            dais.transform.SetParent(effectsRoot, false);
            dais.transform.localPosition = new Vector3(focusPosition.x, 0.002f, focusPosition.z);
            dais.GetComponent<MeshFilter>().sharedMesh = GetOrCreateLowPolyDiscMesh(
                ref bossFocusDaisMesh,
                "BossFocusDaisMesh",
                1.55f,
                16,
                0.012f,
                0.72f);

            var renderer = dais.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterial = GetNatureMaterial("boss-focus-dais", new Color(1.00f, 0.70f, 0.48f, 1f), 0.04f);
        }

        private static Mesh GetOrCreateLowPolyDiscMesh(ref Mesh cachedMesh, string meshName, float radius, int segments, float heightVariation, float innerRadiusRatio)
        {
            if (cachedMesh != null)
            {
                return cachedMesh;
            }

            cachedMesh = CreateLowPolyDiscMesh(meshName, radius, segments, heightVariation, innerRadiusRatio);
            return cachedMesh;
        }

        private static Mesh CreateLowPolyDiscMesh(string meshName, float radius, int segments, float heightVariation, float innerRadiusRatio)
        {
            var clampedSegments = Mathf.Max(segments, 8);
            var innerVertices = new Vector3[clampedSegments];
            var outerVertices = new Vector3[clampedSegments];
            for (var i = 0; i < clampedSegments; i++)
            {
                var angle = i / (float)clampedSegments * Mathf.PI * 2f;
                var wave = Mathf.Sin(i * 1.73f) * 0.035f + Mathf.Cos(i * 2.31f) * 0.025f;
                var outerRadius = radius * (1f + wave);
                var innerRadius = outerRadius * innerRadiusRatio;
                var y = Mathf.Sin(i * 2.07f) * heightVariation;
                innerVertices[i] = new Vector3(Mathf.Cos(angle) * innerRadius, y * 0.35f, Mathf.Sin(angle) * innerRadius);
                outerVertices[i] = new Vector3(Mathf.Cos(angle) * outerRadius, y, Mathf.Sin(angle) * outerRadius);
            }

            // Duplicate vertices per face. Shared center/ring vertices smoothed the
            // normals and turned the arena into a single flat olive disc; 3 tiny
            // triangles per segment retain a genuine polygonal read for under 100
            // triangles and allow deterministic color bands without a texture.
            var vertices = new List<Vector3>(clampedSegments * 9);
            var triangles = new List<int>(clampedSegments * 9);
            var colors = new List<Color>(clampedSegments * 9);
            for (var i = 0; i < clampedSegments; i++)
            {
                var next = (i + 1) % clampedSegments;
                var centerTone = 0.91f + (i % 3) * 0.055f;
                var outerTone = 0.86f + ((i + 1) % 4) * 0.045f;
                AppendDiscTriangle(vertices, triangles, colors, Vector3.zero, innerVertices[next], innerVertices[i], centerTone);
                AppendDiscTriangle(vertices, triangles, colors, innerVertices[i], outerVertices[next], outerVertices[i], outerTone);
                AppendDiscTriangle(vertices, triangles, colors, innerVertices[i], innerVertices[next], outerVertices[next], Mathf.Min(1.04f, outerTone + 0.055f));
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendDiscTriangle(
            ICollection<Vector3> vertices,
            ICollection<int> triangles,
            ICollection<Color> colors,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            float tone)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            var facet = new Color(tone, tone, tone, 1f);
            colors.Add(facet);
            colors.Add(facet);
            colors.Add(facet);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static bool HasAny(GameObject[] prefabs)
        {
            if (prefabs == null)
            {
                return false;
            }

            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject Pick(GameObject[] prefabs, int index)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return null;
            }

            for (var offset = 0; offset < prefabs.Length; offset++)
            {
                var prefab = prefabs[Mathf.Abs(index + offset) % prefabs.Length];
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        private static GameObject SpawnNaturePrefab(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = Instantiate(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localEulerAngles = localEuler;
            instance.transform.localScale = localScale;
            ConfigureArenaNatureInstance(instance);
            return instance;
        }

        private static GameObject SpawnNaturePrefabFootprint(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localEuler, float targetFootprint)
        {
            var instance = SpawnNaturePrefab(prefab, parent, name, localPosition, localEuler, Vector3.one);
            if (instance == null)
            {
                return null;
            }

            if (!TryGetRendererBounds(instance, true, out var bounds))
            {
                instance.transform.localScale = Vector3.one * 0.35f;
                return instance;
            }

            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            var scale = footprint > MinRenderableHeight ? targetFootprint / footprint : 0.35f;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 0.35f;
            }

            instance.transform.localScale *= Mathf.Clamp(scale, 0.01f, 4.0f);
            PlaceInstanceBottomAtLocalY(instance, parent, localPosition.y);
            return instance;
        }

        private static GameObject SpawnNaturePrefabFitted(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localEuler, float targetHeight, float targetFootprint)
        {
            var instance = SpawnNaturePrefab(prefab, parent, name, localPosition, localEuler, Vector3.one);
            if (instance == null)
            {
                return null;
            }

            if (!TryGetRendererBounds(instance, true, out var bounds))
            {
                instance.transform.localScale = Vector3.one * 0.25f;
                return instance;
            }

            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            var heightScale = bounds.size.y > MinRenderableHeight ? targetHeight / bounds.size.y : float.PositiveInfinity;
            var footprintScale = footprint > MinRenderableHeight ? targetFootprint / footprint : float.PositiveInfinity;
            var scale = Mathf.Min(heightScale, footprintScale);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 0.25f;
            }

            instance.transform.localScale *= Mathf.Clamp(scale, 0.006f, 3.2f);
            PlaceInstanceBottomAtLocalY(instance, parent, localPosition.y);
            return instance;
        }

        private static void PlaceInstanceBottomAtLocalY(GameObject instance, Transform parent, float localGroundY)
        {
            if (!TryGetRendererBounds(instance, true, out var bounds))
            {
                return;
            }

            var worldGroundY = parent.TransformPoint(Vector3.up * localGroundY).y;
            instance.transform.position += Vector3.up * (worldGroundY - bounds.min.y);
        }

        private static void ConfigureArenaNatureInstance(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                DestroyArenaGeneratedObject(collider);
            }

            var castsShadows = ShouldCastArenaNatureShadows(instance);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.sharedMaterials = BuildArenaNatureMaterials(instance.name, renderer.name, renderer.sharedMaterials);
            }
        }

        private static bool ShouldCastArenaNatureShadows(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            var token = instance.name.ToLowerInvariant();
            if (token.Contains("grass") ||
                token.Contains("flower") ||
                token.Contains("cloud") ||
                token.Contains("meadow") ||
                token.Contains("detail") ||
                token.Contains("terrain") ||
                token.Contains("hill") ||
                token.Contains("mountain") ||
                token.Contains("background") ||
                token.Contains("distant") ||
                token.Contains(" far ") ||
                token.Contains(" back "))
            {
                return false;
            }

            var isTreeOrRock = token.Contains("tree") || token.Contains("rock") || token.Contains("stone");
            return isTreeOrRock && instance.transform.localPosition.z <= NearSceneryMaxLocalDepth;
        }

        private static void ScrubArenaNatureMaterials(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = BuildArenaNatureMaterials(renderer.transform.name, renderer.name, renderer.sharedMaterials);
            }
        }

        private static Material[] BuildArenaNatureMaterials(string instanceName, string rendererName, Material[] sources)
        {
            if (sources == null || sources.Length == 0)
            {
                return new[] { GetNatureMaterial("grass", new Color(0.55f, 0.72f, 0.32f, 1f), 0.14f) };
            }

            var replacements = new Material[sources.Length];
            for (var i = 0; i < sources.Length; i++)
            {
                var sourceName = sources[i] != null ? sources[i].name : string.Empty;
                var key = GuessArenaNatureMaterialKey(instanceName, rendererName, sourceName, i);
                replacements[i] = GetNatureMaterial(key, ColorForArenaNatureKey(key), key == "water" ? 0.45f : 0.16f);
            }

            return replacements;
        }

        private static string GuessArenaNatureMaterialKey(string instanceName, string rendererName, string sourceName, int materialIndex)
        {
            var token = (instanceName + " " + rendererName + " " + sourceName).ToLowerInvariant();
            if (token.Contains("water") || token.Contains("lake"))
            {
                return "water";
            }

            if (token.Contains("cloud"))
            {
                return "cloud";
            }

            if (token.Contains("flower"))
            {
                return materialIndex % 2 == 0 ? "flower" : "leaf";
            }

            if (token.Contains("rock") || token.Contains("stone"))
            {
                return "rock";
            }

            if (token.Contains("mountain"))
            {
                return "mountain";
            }

            if (token.Contains("trunk") || token.Contains("bark") || token.Contains("wood") || token.Contains("stem") || token.Contains("branch"))
            {
                return "trunk";
            }

            if (token.Contains("tree") || token.Contains("pine") || token.Contains("fir") || token.Contains("oak") || token.Contains("apple") || token.Contains("birch"))
            {
                return "leaf";
            }

            if (token.Contains("terrain") || token.Contains("hill") || token.Contains("grass") || token.Contains("bush") || token.Contains("vegetation"))
            {
                return materialIndex % 3 == 0 ? "grass" : "leaf";
            }

            return "grass";
        }

        private static Color ColorForArenaNatureKey(string key)
        {
            switch (key)
            {
                case "water":
                    return new Color(0.24f, 0.67f, 0.86f, 1f);
                case "cloud":
                    return new Color(0.94f, 0.96f, 0.98f, 1f);
                case "flower":
                    return new Color(0.98f, 0.58f, 0.30f, 1f);
                case "rock":
                    return new Color(0.66f, 0.69f, 0.76f, 1f);
                case "mountain":
                    return new Color(0.64f, 0.75f, 0.89f, 1f);
                case "trunk":
                    return new Color(0.52f, 0.31f, 0.17f, 1f);
                case "leaf":
                    return new Color(0.46f, 0.76f, 0.28f, 1f);
                default:
                    return new Color(0.64f, 0.81f, 0.34f, 1f);
            }
        }

        private static Material GetNatureMaterial(string key, Color color, float smoothness)
        {
            if (NatureMaterialCache.TryGetValue(key, out var cachedMaterial) && cachedMaterial != null)
            {
                return cachedMaterial;
            }

            var shader = Resources.Load<Shader>("Shaders/RasshiineLowPolyEnvironment") ??
                         Shader.Find("Rasshiine/Low Poly Environment") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                name = "M_Runtime_Nature_" + key
            };
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_ShadowTint"))
            {
                var shadowTint = key.Contains("water")
                    ? new Color(0.16f, 0.58f, 0.72f, 1f)
                    : key.Contains("violet")
                        ? new Color(0.38f, 0.24f, 0.58f, 1f)
                        : key.Contains("lime")
                            ? new Color(0.38f, 0.58f, 0.16f, 1f)
                            : key.Contains("canyon") || key.Contains("dais") || key.Contains("terrace")
                                ? new Color(0.58f, 0.34f, 0.34f, 1f)
                                : key.Contains("cloud")
                                    ? new Color(0.52f, 0.65f, 0.80f, 1f)
                                    : new Color(0.32f, 0.46f, 0.62f, 1f);
                material.SetColor("_ShadowTint", shadowTint);
            }
            if (material.HasProperty("_LightTint"))
            {
                var canyonPalette = key.Contains("canyon") || key.Contains("dais") || key.Contains("terrace");
                material.SetColor("_LightTint", canyonPalette
                    ? new Color(1.0f, 0.96f, 0.86f, 1f)
                    : new Color(1.0f, 0.91f, 0.75f, 1f));
            }
            if (material.HasProperty("_RimColor"))
            {
                material.SetColor("_RimColor", new Color(0.22f, 0.60f, 0.82f, 1f));
            }
            if (material.HasProperty("_FacetSteps"))
            {
                material.SetFloat("_FacetSteps", key.Contains("rock") || key.Contains("mountain") ? 3f : 4f);
            }
            if (material.HasProperty("_AmbientStrength"))
            {
                var generatedFacetMaterial = key == "generated-canyon-sand" ||
                                             key == "fallback-canyon-sand" ||
                                             key == "battle-canyon-clearing" ||
                                             key == "canyon-water" ||
                                             key == "boss-focus-dais" ||
                                             key == "boss-rune-terrace";
                var canyonPalette = key.Contains("canyon") || key.Contains("dais") || key.Contains("terrace");
                material.SetFloat("_AmbientStrength",
                    key.Contains("mountain") ? 0.82f :
                    key.Contains("cloud") ? 0.78f :
                    generatedFacetMaterial ? 1.18f :
                    canyonPalette ? 1.06f : 0.80f);
            }
            if (material.HasProperty("_RimStrength"))
            {
                material.SetFloat("_RimStrength", key.Contains("water") ? 0.08f : 0.035f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }
            if (material.HasProperty("_VertexColorStrength"))
            {
                var generatedFacetMaterial = key == "generated-canyon-sand" ||
                                             key == "fallback-canyon-sand" ||
                                             key == "battle-canyon-clearing" ||
                                             key == "canyon-water" ||
                                             key == "boss-focus-dais" ||
                                             key == "boss-rune-terrace";
                material.SetFloat("_VertexColorStrength", generatedFacetMaterial ? 0.44f : 0f);
            }

            NatureMaterialCache[key] = material;
            return material;
        }

        private Material GetArenaLineMaterial()
        {
            if (arenaLineMaterial != null)
            {
                return arenaLineMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored") ??
                         theme?.FloorLineMaterial?.shader;
            arenaLineMaterial = new Material(shader)
            {
                name = "M_Runtime_ArenaLines_Transparent",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            arenaLineMaterial.SetOverrideTag("RenderType", "Transparent");
            arenaLineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            arenaLineMaterial.DisableKeyword("_ALPHATEST_ON");
            if (arenaLineMaterial.HasProperty("_Color"))
            {
                arenaLineMaterial.SetColor("_Color", Color.white);
            }

            if (arenaLineMaterial.HasProperty("_BaseColor"))
            {
                arenaLineMaterial.SetColor("_BaseColor", Color.white);
            }

            if (arenaLineMaterial.HasProperty("_Surface"))
            {
                arenaLineMaterial.SetFloat("_Surface", 1f);
            }

            if (arenaLineMaterial.HasProperty("_SrcBlend"))
            {
                arenaLineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (arenaLineMaterial.HasProperty("_DstBlend"))
            {
                arenaLineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (arenaLineMaterial.HasProperty("_ZWrite"))
            {
                arenaLineMaterial.SetInt("_ZWrite", 0);
            }

            return arenaLineMaterial;
        }

        private static Color CreateArenaLineColor(Color source, float alpha)
        {
            var red = Mathf.Max(0f, source.r);
            var green = Mathf.Max(0f, source.g);
            var blue = Mathf.Max(0f, source.b);
            var maxChannel = Mathf.Max(red, Mathf.Max(green, blue));
            var brightnessScale = maxChannel > ArenaLineMaxChannel ? ArenaLineMaxChannel / maxChannel : 1f;
            return new Color(
                red * brightnessScale,
                green * brightnessScale,
                blue * brightnessScale,
                Mathf.Clamp01(alpha));
        }

        private static void DestroyArenaGeneratedObject(Object target)
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

        private void CreateLine(string objectName, Vector3 a, Vector3 b, Material material, Color color, float width)
        {
            var lineObject = new GameObject(objectName, typeof(LineRenderer));
            lineObject.transform.SetParent(effectsRoot, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            ConfigureArenaLineRenderer(line, material, color);
            line.widthMultiplier = width;
        }

        private void CreateRing(float radius, Material material, Color color)
        {
            var lineObject = new GameObject($"Grid_Ring_{radius:0.0}", typeof(LineRenderer));
            lineObject.transform.SetParent(effectsRoot, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 96;
            ConfigureArenaLineRenderer(line, material, color);
            line.widthMultiplier = 0.034f;
            for (var index = 0; index < line.positionCount; index++)
            {
                var angle = index / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0.01f, Mathf.Sin(angle) * radius));
            }
        }

        private static void ConfigureArenaLineRenderer(LineRenderer line, Material material, Color color)
        {
            line.sharedMaterial = material;
            line.startColor = color;
            line.endColor = color;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
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
            foreach (var participant in participantTransforms.Values)
            {
                participant.GetComponent<MemberAvatarAnimator>()?.CancelBattleAnimation();
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                DestroyArenaGeneratedObject(parent.GetChild(index).gameObject);
            }
        }
    }
}
