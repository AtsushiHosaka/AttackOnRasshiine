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
        [SerializeField] private RasshiineTheme theme;
        [SerializeField] private Transform bossAnchor;
        [SerializeField] private Transform partyAnchor;
        [SerializeField] private Transform effectsRoot;
        [SerializeField] private RaidFollowCamera followCamera;

        private readonly Dictionary<string, Transform> participantTransforms = new();
        private readonly Dictionary<string, Vector3> participantBasePositions = new();
        private Transform bossTransform;
        private BossBattleState state;
        private float idleTime;
        private string controlledParticipantId;

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
            if (bossTransform == null || state?.Boss == null)
            {
                return;
            }

            var hp01 = Mathf.Clamp01(state.Boss.CurrentHp / (float)state.Boss.MaxHp);
            bossTransform.localScale = Vector3.one * Mathf.Lerp(3.4f, 4.8f, hp01);
        }

        public IEnumerator PlayAction(BattleActionResult result)
        {
            if (result == null || bossTransform == null)
            {
                yield break;
            }

            var origin = participantTransforms.TryGetValue(result.UserId, out var participant)
                ? participant.position + Vector3.up * 1.2f
                : new Vector3(-2f, 1f, -2f);
            var target = bossTransform.position + Vector3.up * 2.3f;
            var lineObject = new GameObject($"FX_{result.ActionType}_{result.Nickname}", typeof(LineRenderer));
            lineObject.transform.SetParent(effectsRoot, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = result.ActionType == BattleActionType.FullPower ? 0.14f : 0.08f;
            line.material = theme.ProjectileMaterial != null ? theme.ProjectileMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = result.ActionType == BattleActionType.Support ? theme.Mint : theme.Magenta;
            line.endColor = theme.Cyan;

            var duration = 0.34f;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var t = time / duration;
                var arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.9f;
                line.SetPosition(0, origin);
                line.SetPosition(1, Vector3.Lerp(origin, target, t) + arc);
                yield return null;
            }

            line.SetPosition(1, target);
            StartCoroutine(ShowFloatingLabel(target + Vector3.up * 0.6f, result.Damage > 0 ? $"{result.Damage}" : result.SupportEffect, result.Damage > 0 ? theme.Gold : theme.Mint));
            StartCoroutine(ShakeBoss());
            Destroy(lineObject, 0.24f);
            RefreshBossScale();
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
            var model = InstantiateModel(theme.MentorPlaceholderPrefab, bossAnchor, "BossMentor", theme.BossMaterial, true);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one * 4.4f;
            bossTransform = model.transform;
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
                var angle = Mathf.Lerp(215f, 325f, count == 1 ? 0.5f : index / (float)(count - 1));
                var radius = 6.2f + (index % 2) * 0.8f;
                var x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                var z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                var model = InstantiateModel(theme.MemberPlaceholderPrefab, partyAnchor, participant.Nickname, theme.MemberMaterial, false);
                model.transform.localPosition = new Vector3(x, 0f, z);
                model.transform.LookAt(bossAnchor.position + Vector3.up * 1.2f);
                model.transform.localScale = Vector3.one * 0.82f;
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

        private IEnumerator ShakeBoss()
        {
            if (bossTransform == null)
            {
                yield break;
            }

            var basePosition = bossTransform.localPosition;
            for (var time = 0f; time < 0.26f; time += Time.deltaTime)
            {
                bossTransform.localPosition = basePosition + Random.insideUnitSphere * 0.06f;
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
