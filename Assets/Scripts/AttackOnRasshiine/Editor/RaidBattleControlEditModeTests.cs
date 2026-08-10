using System.Linq;
using System.Reflection;
using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidBattleControlEditModeTests
    {
        [Test]
        public void ControlledMemberGetsMovementControllerAnimatorAndFollowCameraTarget()
        {
            var root = new GameObject("RaidBattleControlTestRoot");
            try
            {
                var theme = CreateTheme(root.transform);
                var bossAnchor = CreateChild(root.transform, "BossAnchor");
                var partyAnchor = CreateChild(root.transform, "PartyAnchor");
                var effectsRoot = CreateChild(root.transform, "EffectsRoot");
                var cameraObject = new GameObject("RaidBattleControlTestCamera", typeof(Camera), typeof(RaidFollowCamera));
                cameraObject.transform.SetParent(root.transform, false);
                var followCamera = cameraObject.GetComponent<RaidFollowCamera>();
                var controllerObject = new GameObject("RaidBattleControlTestController", typeof(RaidBattleController));
                controllerObject.transform.SetParent(root.transform, false);
                var battleController = controllerObject.GetComponent<RaidBattleController>();
                battleController.Configure(theme, bossAnchor, partyAnchor, effectsRoot, followCamera);

                var repository = new LocalGameRepository();
                var member = repository.Members[repository.Members.Count - 1];
                repository.StartBattle();

                battleController.LoadBattle(repository.ActiveBattle);
                battleController.SetControlledParticipant(member.Id);

                Assert.AreEqual(6, repository.ActiveBattle.Participants.Count,
                    "The authoritative battle roster must retain all connected members.");
                Assert.AreEqual(3, partyAnchor.childCount,
                    "Only three presentation representatives should be rendered in the battle composition.");

                var participant = partyAnchor
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => candidate.name == member.Nickname);

                Assert.IsNotNull(participant);
                Assert.IsTrue(participant.TryGetComponent<MemberAvatarController>(out var controller));
                Assert.IsTrue(controller.enabled);
                Assert.IsTrue(participant.TryGetComponent<MemberAvatarAnimator>(out var avatarAnimator));
                Assert.IsTrue(avatarAnimator.HasPlayableAnimator);
                Assert.Greater(avatarAnimator.AnimationSeed, 0);
                var participantMaterials = participant
                    .GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .ToArray();
                var repairMaterialCount = participantMaterials.Count(material => material.name.StartsWith("M_Runtime_TinyHeroRepair", System.StringComparison.Ordinal));
                Assert.Greater(participantMaterials.Length, 0);
                Assert.Less(repairMaterialCount, participantMaterials.Length);
                Assert.IsTrue(participantMaterials.Any(HasTexture));

                var activeBodies = partyAnchor
                    .GetComponentsInChildren<Transform>(true)
                    .Where(candidate => candidate.gameObject.activeInHierarchy && TinyHeroCosmeticApplicator.IsSlotCandidate("body", candidate.name))
                    .Select(candidate => candidate.name)
                    .Distinct()
                    .ToArray();
                var activeWeapons = partyAnchor
                    .GetComponentsInChildren<Transform>(true)
                    .Where(candidate => candidate.gameObject.activeInHierarchy && TinyHeroCosmeticApplicator.IsSlotCandidate("weapon", candidate.name))
                    .Select(candidate => candidate.name)
                    .Distinct()
                    .ToArray();
                Assert.GreaterOrEqual(activeBodies.Length, 2,
                    "Authored body variants should separate party roles without replacing their textures.");
                Assert.GreaterOrEqual(activeWeapons.Length, 2,
                    "The three visible roles need distinct authored weapon silhouettes.");

                var visibleParticipants = Enumerable.Range(0, partyAnchor.childCount)
                    .Select(index => partyAnchor.GetChild(index).gameObject)
                    .ToArray();
                Assert.IsTrue(visibleParticipants.All(candidate =>
                    TryGetActiveRendererBounds(candidate, out var bounds) && bounds.size.y >= 2.45f && bounds.size.y <= 2.90f),
                    "Visible Tiny Heroes should match the large readable toy proportions in the approved composition.");
                var participantCenters = visibleParticipants
                    .Select(candidate => TryGetActiveRendererBounds(candidate, out var bounds) ? bounds.center.x : 0f)
                    .OrderBy(value => value)
                    .ToArray();
                Assert.Less(participantCenters[0], -6.0f);
                Assert.Greater(participantCenters[participantCenters.Length - 1] - participantCenters[0], 4.45f,
                    "The three heroes must form a broad left-side lineup instead of a center-screen cluster.");

                var animationSeeds = partyAnchor
                    .GetComponentsInChildren<MemberAvatarAnimator>(true)
                    .Select(animator => animator.AnimationSeed)
                    .Distinct()
                    .ToArray();
                Assert.Greater(animationSeeds.Length, 1);

                var targetField = typeof(RaidFollowCamera).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.AreSame(participant, targetField?.GetValue(followCamera));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CooperativeAttackUsesMixedMemberAttackAnimations()
        {
            var method = typeof(RaidBattleController).GetMethod("ResolveCooperativeMemberAnimation", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var leadAnimation = (MemberBattleAnimation)method.Invoke(null, new object[] { 0, true });
            var allyAnimations = Enumerable.Range(1, 6)
                .Select(index => (MemberBattleAnimation)method.Invoke(null, new object[] { index, false }))
                .Distinct()
                .ToArray();

            Assert.AreEqual(MemberBattleAnimation.FullPowerAttack, leadAnimation);
            Assert.Greater(allyAnimations.Length, 1);
            Assert.IsTrue(allyAnimations.All(animation =>
                animation == MemberBattleAnimation.Attack ||
                animation == MemberBattleAnimation.StrongAttack ||
                animation == MemberBattleAnimation.FullPowerAttack));
        }

        [Test]
        public void MemberAnimationVariantIndexChangesAcrossParticipants()
        {
            var variants = Enumerable.Range(0, 6)
                .Select(index => MemberAvatarAnimator.CalculateVariantStartIndex(index * 41 + 7, MemberBattleAnimation.Attack, 1, 3))
                .Distinct()
                .ToArray();

            Assert.Greater(variants.Length, 1);
        }

        [Test]
        public void MemberIdleKeepsAuthoredStandingPoseWithoutPlayingIdleState()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            Assert.IsNotNull(prefab);

            var instance = Object.Instantiate(prefab);
            try
            {
                var avatarAnimator = instance.GetComponent<MemberAvatarAnimator>() ?? instance.AddComponent<MemberAvatarAnimator>();
                avatarAnimator.ResolveAnimator();
                avatarAnimator.ConfigureVariation(41);
                avatarAnimator.PlayIdle();

                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator);
                Assert.IsFalse(animator.enabled);
                Assert.IsEmpty(avatarAnimator.CurrentStateName);

                avatarAnimator.PlayBattleAnimation(MemberBattleAnimation.Attack, 0.1f);
                Assert.IsTrue(animator.enabled);
                avatarAnimator.CancelBattleAnimation();
                Assert.IsFalse(animator.enabled);
                Assert.IsEmpty(avatarAnimator.CurrentStateName);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BattleArenaUsesBoundedBrightToyCanyonScenery()
        {
            var root = new GameObject("RaidBattleNatureArenaTestRoot");
            try
            {
                var theme = CreateTheme(root.transform);
                var bossAnchor = CreateChild(root.transform, "BossAnchor");
                var partyAnchor = CreateChild(root.transform, "PartyAnchor");
                var effectsRoot = CreateChild(root.transform, "EffectsRoot");
                var cameraObject = new GameObject("RaidBattleNatureArenaTestCamera", typeof(Camera), typeof(RaidFollowCamera));
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.position = new Vector3(0f, 5.25f, -12.5f);
                cameraObject.transform.rotation = Quaternion.Euler(17f, 0f, 0f);
                var battleCamera = cameraObject.GetComponent<Camera>();
                var battleController = new GameObject("RaidBattleNatureArenaTestController", typeof(RaidBattleController)).GetComponent<RaidBattleController>();
                battleController.transform.SetParent(root.transform, false);
                battleController.Configure(theme, bossAnchor, partyAnchor, effectsRoot, cameraObject.GetComponent<RaidFollowCamera>());

                var repository = new LocalGameRepository();
                repository.StartBattle();
                battleController.LoadBattle(repository.ActiveBattle);

                var arenaRoot = effectsRoot.Find("Low Poly Nature Battle Arena");
                Assert.IsNotNull(arenaRoot);
                Assert.IsNotNull(effectsRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name.StartsWith("LowPoly Rock", System.StringComparison.Ordinal)));
                Assert.IsNotNull(arenaRoot.Find("LowPoly Cyan Canyon Water"));
                Assert.IsNotNull(arenaRoot.Find("LowPoly Canyon Bridge"));
                Assert.IsNotNull(effectsRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name.StartsWith("LowPoly Lime Shrub", System.StringComparison.Ordinal)));
                Assert.IsNotNull(effectsRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name.StartsWith("LowPoly Crystal Cluster", System.StringComparison.Ordinal)));
                Assert.IsNotNull(effectsRoot.Find("LowPolyBattleClearing"));
                Assert.IsNotNull(effectsRoot.Find("LowPolyBossFocusDais"));
                Assert.IsNotNull(effectsRoot.Find("LowPolyBossRuneTerrace"));
                Assert.IsNull(effectsRoot.Find("MeadowArenaGround"));
                Assert.IsFalse(arenaRoot.GetComponentsInChildren<Renderer>(true).Any(HasMagentaMaterial));

                var facetedGround = arenaRoot.Find("LowPoly Generated Arena Ground").GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(facetedGround.vertexCount, facetedGround.colors.Length,
                    "The texture-free arena should carry deterministic per-face color bands.");
                Assert.LessOrEqual(facetedGround.triangles.Length / 3, 96,
                    "Polygonal ground variation must remain cheaper than a texture-backed terrain.");
                for (var triangle = 0; triangle < facetedGround.triangles.Length; triangle += 3)
                {
                    var indices = facetedGround.triangles;
                    var normals = facetedGround.normals;
                    Assert.That(Vector3.Angle(normals[indices[triangle]], normals[indices[triangle + 1]]), Is.LessThan(0.01f));
                    Assert.That(Vector3.Angle(normals[indices[triangle]], normals[indices[triangle + 2]]), Is.LessThan(0.01f));
                }

                var scenery = arenaRoot.GetComponentsInChildren<Transform>(true);
                var foregroundRocks = scenery.Where(item => item.name.StartsWith("LowPoly Rock", System.StringComparison.Ordinal)).ToArray();
                var distantMesas = scenery.Where(item => item.name.StartsWith("LowPoly Distant Mesa", System.StringComparison.Ordinal) || item.name.StartsWith("LowPoly Far Mesa", System.StringComparison.Ordinal)).ToArray();
                var limeShrubs = scenery.Where(item => item.name.StartsWith("LowPoly Lime Shrub", System.StringComparison.Ordinal)).ToArray();
                var crystals = scenery.Where(item => item.name.StartsWith("LowPoly Crystal Cluster", System.StringComparison.Ordinal)).ToArray();
                var clouds = scenery.Where(item => item.name.StartsWith("LowPoly Cloud", System.StringComparison.Ordinal)).ToArray();

                Assert.GreaterOrEqual(foregroundRocks.Length, 4);
                Assert.GreaterOrEqual(distantMesas.Length, 4);
                Assert.That(limeShrubs, Has.Length.EqualTo(7));
                Assert.That(crystals, Has.Length.EqualTo(4));
                Assert.That(clouds, Has.Length.EqualTo(3),
                    "Three tiny opaque facet clusters replace the giant full-screen procedural cloud cell.");
                AssertRenderersUseShadowMode(foregroundRocks, ShadowCastingMode.On);
                AssertRenderersUseShadowMode(distantMesas, ShadowCastingMode.Off);
                AssertRenderersUseShadowMode(limeShrubs, ShadowCastingMode.On);
                AssertRenderersUseShadowMode(crystals, ShadowCastingMode.On);
                AssertRenderersUseShadowMode(clouds, ShadowCastingMode.Off);
                var cloudRenderers = clouds.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
                Assert.AreEqual(9, cloudRenderers.Length);
                Assert.IsTrue(cloudRenderers.All(renderer => !renderer.receiveShadows));
                Assert.AreEqual(1, cloudRenderers.Select(renderer => renderer.sharedMaterial).Distinct().Count());
                Assert.AreEqual(1, clouds.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true))
                    .Select(filter => filter.sharedMesh).Distinct().Count());
                var cloudViewportPositions = clouds
                    .Select(root => battleCamera.WorldToViewportPoint(root.position))
                    .OrderBy(position => position.x)
                    .ToArray();
                Assert.That(cloudViewportPositions[0].x, Is.EqualTo(0.18f).Within(0.002f));
                Assert.That(cloudViewportPositions[1].x, Is.EqualTo(0.47f).Within(0.002f));
                Assert.That(cloudViewportPositions[2].x, Is.EqualTo(0.85f).Within(0.002f));
                Assert.IsTrue(cloudViewportPositions.All(position => position.y >= 0.75f && position.y <= 0.85f && position.z > 0f),
                    "All three cloud groups must remain inside the clear upper viewport band.");
                Assert.IsTrue(cloudRenderers.All(renderer =>
                {
                    var color = renderer.sharedMaterial.GetColor("_BaseColor");
                    return color.r >= 0.99f && color.g >= 0.99f && color.b >= 0.99f;
                }));
                Assert.IsTrue(cloudRenderers.All(renderer => renderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent >= 0.10f));

                var bridge = arenaRoot.Find("LowPoly Canyon Bridge");
                var bridgeRenderers = bridge.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(40, bridgeRenderers.Length);
                Assert.IsTrue(bridgeRenderers.All(renderer => renderer.shadowCastingMode == ShadowCastingMode.Off && !renderer.receiveShadows));
                Assert.AreEqual(1, bridgeRenderers.Select(renderer => renderer.sharedMaterial).Distinct().Count());
                Assert.AreEqual(1, bridge.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).Distinct().Count());
                Assert.That(bridge.Find("Bridge Deck").localScale.y, Is.GreaterThanOrEqualTo(0.10f));
                Assert.IsNotNull(bridge.Find("Bridge Rail Near 08"));
                Assert.That(bridge.Find("Bridge Rail Near 03").localScale.y, Is.GreaterThanOrEqualTo(0.06f));
                Assert.AreEqual(9, bridge.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("Bridge Plank", System.StringComparison.Ordinal)));
                Assert.AreEqual(8, bridge.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("Bridge Rail Near", System.StringComparison.Ordinal)));
                Assert.AreEqual(8, bridge.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("Bridge Rail Far", System.StringComparison.Ordinal)));
                Assert.AreEqual(5, bridge.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("Bridge Hanger Near", System.StringComparison.Ordinal)));
                Assert.AreEqual(5, bridge.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("Bridge Hanger Far", System.StringComparison.Ordinal)));
                Assert.That(bridge.localPosition.x, Is.InRange(-0.35f, -0.25f));
                Assert.That(bridge.localPosition.y, Is.InRange(2.54f, 2.62f));
                Assert.That(bridge.localPosition.z, Is.InRange(8.98f, 9.06f));
                Assert.That(bridge.Find("Bridge Plank 05").localPosition.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(bridge.Find("Bridge Plank 05").localScale.x, Is.GreaterThanOrEqualTo(0.76f),
                    "Nine overlapping planks must read as one continuous crossing in WebGL.");
                var bridgeColor = bridgeRenderers[0].sharedMaterial.GetColor("_BaseColor");
                Assert.That(bridgeColor.r, Is.GreaterThan(bridgeColor.g * 1.7f));
                Assert.That(bridgeColor.g, Is.GreaterThan(bridgeColor.b * 1.8f));

                var waterRenderer = arenaRoot.Find("LowPoly Cyan Canyon Water").GetComponent<MeshRenderer>();
                Assert.That(waterRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(waterRenderer.receiveShadows, Is.False);
                Assert.That(waterRenderer.sharedMaterial.GetColor("_BaseColor").g, Is.GreaterThan(waterRenderer.sharedMaterial.GetColor("_BaseColor").r * 8f));
                Assert.That(waterRenderer.sharedMaterial.GetColor("_BaseColor").b, Is.GreaterThan(0.7f));
                Assert.That(arenaRoot.Find("LowPoly Cyan Canyon Water").localScale.x, Is.InRange(0.38f, 0.42f));
                Assert.That(arenaRoot.Find("LowPoly Cyan Canyon Water").localScale.z, Is.InRange(0.23f, 0.27f));
                Assert.That(arenaRoot.Find("LowPoly Cyan Canyon Water").localPosition.z, Is.InRange(9.05f, 9.15f));
                Assert.IsNotNull(arenaRoot.Find("LowPoly Bridge Bank Left"));
                Assert.IsNotNull(arenaRoot.Find("LowPoly Bridge Bank Right"));

                var groundColor = arenaRoot.Find("LowPoly Generated Arena Ground").GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
                Assert.That(groundColor.r, Is.GreaterThan(groundColor.g * 1.45f));
                Assert.That(groundColor.g, Is.GreaterThan(groundColor.b * 1.45f));

                var uniqueMeshes = arenaRoot.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh != null)
                    .Select(filter => filter.sharedMesh)
                    .Distinct()
                    .ToArray();
                Assert.That(uniqueMeshes, Has.Length.EqualTo(2), "The canyon must reuse one ground mesh and one faceted prop mesh.");
                Assert.LessOrEqual(arenaRoot.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh != null)
                    .Sum(filter => filter.sharedMesh.triangles.Length / 3), 2600);
                Assert.IsEmpty(arenaRoot.GetComponentsInChildren<Light>(true));
                Assert.IsEmpty(arenaRoot.GetComponentsInChildren<Collider>(true));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ScheduledBattleUsesAReusableLowPolySealInsteadOfAnEmptyArena()
        {
            var root = new GameObject("RaidBattleScheduledSealTestRoot");
            try
            {
                var theme = CreateTheme(root.transform);
                var bossAnchor = CreateChild(root.transform, "BossAnchor");
                var partyAnchor = CreateChild(root.transform, "PartyAnchor");
                var effectsRoot = CreateChild(root.transform, "EffectsRoot");
                var battleController = new GameObject("RaidBattleScheduledSealTestController", typeof(RaidBattleController)).GetComponent<RaidBattleController>();
                battleController.transform.SetParent(root.transform, false);
                battleController.Configure(theme, bossAnchor, partyAnchor, effectsRoot);

                var repository = new LocalGameRepository();
                Assert.AreEqual(BattleStatus.Scheduled, repository.ActiveBattle.Status);
                battleController.LoadBattle(repository.ActiveBattle);

                var seal = bossAnchor.Find("RaidSealBeacon");
                Assert.IsNotNull(seal);
                Assert.That(seal.localPosition.x, Is.InRange(2.50f, 2.60f));
                Assert.IsNull(bossAnchor.Find("BossEnemy"), "Scheduled state should tease the raid without spawning a live boss.");
                var renderers = seal.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.InRange(8, 12));
                Assert.AreEqual(1, seal.GetComponentsInChildren<MeshFilter>(true)
                    .Select(filter => filter.sharedMesh)
                    .Distinct()
                    .Count(), "Every seal part must reuse the same faceted block mesh.");
                Assert.LessOrEqual(seal.GetComponentsInChildren<MeshFilter>(true)
                    .Sum(filter => filter.sharedMesh.triangles.Length / 3), 500);
                Assert.IsEmpty(seal.GetComponentsInChildren<Light>(true));
                Assert.IsEmpty(seal.GetComponentsInChildren<Collider>(true));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CompletedBattleUsesAStableDefeatedBossPoseAndRetainsSilhouette()
        {
            var root = new GameObject("RaidBattleCompletedPoseTestRoot");
            try
            {
                var theme = CreateTheme(root.transform);
                var bossAnchor = CreateChild(root.transform, "BossAnchor");
                var partyAnchor = CreateChild(root.transform, "PartyAnchor");
                var effectsRoot = CreateChild(root.transform, "EffectsRoot");
                var battleController = new GameObject("RaidBattleCompletedPoseTestController", typeof(RaidBattleController)).GetComponent<RaidBattleController>();
                battleController.transform.SetParent(root.transform, false);
                battleController.Configure(theme, bossAnchor, partyAnchor, effectsRoot);

                var repository = new LocalGameRepository();
                repository.StartBattle();
                repository.ActiveBattle.Status = BattleStatus.Completed;
                repository.ActiveBattle.Phase = BattlePhase.Completed;
                repository.ActiveBattle.Outcome = BattleOutcome.Victory;
                repository.ActiveBattle.Boss.CurrentHp = 0;
                battleController.LoadBattle(repository.ActiveBattle);

                var boss = bossAnchor.Find("BossEnemy");
                Assert.IsNotNull(boss);
                Assert.That(boss.localPosition.x, Is.InRange(2.50f, 2.60f),
                    "The approved composition places the large boss right of center.");
                Assert.LessOrEqual(boss.localPosition.y, -0.45f);
                Assert.Greater(Quaternion.Angle(Quaternion.identity, boss.localRotation), 10f);
                Assert.That(boss.localScale.x, Is.GreaterThan(0f));
                Assert.That(boss.Find("Core Glow").localScale.magnitude,
                    Is.LessThan(boss.Find("Core Frame").localScale.magnitude),
                    "The defeated core should visibly power down without a unique material allocation.");
                Assert.IsTrue(TryGetRendererBounds(boss.gameObject, out var bounds));
                Assert.Greater(bounds.size.y, 3.55f, "HP zero must retain a readable defeated boss silhouette.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBossUsesBoundedLowPolyPartsMaterialSeparationAndContactShadow()
        {
            var root = new GameObject("RaidBattleProceduralBossTestRoot");
            try
            {
                var theme = CreateTheme(root.transform);
                var bossAnchor = CreateChild(root.transform, "BossAnchor");
                var partyAnchor = CreateChild(root.transform, "PartyAnchor");
                var effectsRoot = CreateChild(root.transform, "EffectsRoot");
                var battleController = new GameObject("RaidBattleProceduralBossTestController", typeof(RaidBattleController)).GetComponent<RaidBattleController>();
                battleController.transform.SetParent(root.transform, false);
                battleController.Configure(theme, bossAnchor, partyAnchor, effectsRoot);

                var repository = new LocalGameRepository();
                repository.StartBattle();
                battleController.LoadBattle(repository.ActiveBattle);

                var boss = bossAnchor.Find("BossEnemy");
                Assert.IsNotNull(boss);
                Assert.IsNull(boss.GetComponentInChildren<SkinnedMeshRenderer>(true));
                Assert.IsNull(boss.GetComponentInChildren<Animator>(true));
                Assert.IsEmpty(boss.GetComponentsInChildren<Collider>(true));
                Assert.IsNotNull(boss.Find("Core Glow"));
                Assert.IsNotNull(boss.Find("Visor Glow"));
                Assert.IsNotNull(boss.Find("Crown Left"));
                Assert.IsNotNull(boss.Find("Crown Right"));
                Assert.IsNotNull(boss.Find("Crown Rise"));
                Assert.IsNotNull(boss.Find("Crown Tip"));
                Assert.IsNotNull(boss.Find("Abdomen Shell"));
                Assert.IsNotNull(boss.Find("Mandible Left"));
                Assert.AreEqual(18, boss.GetComponentsInChildren<Transform>(true)
                    .Count(transform => transform.name.Contains(" Leg ", System.StringComparison.Ordinal)),
                    "The beetle silhouette needs six three-part legs without adding a rig.");

                var renderers = boss.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.InRange(42, 44), "The boss should stay authored but bounded for WebGL draw cost.");
                var materials = renderers
                    .Select(renderer => renderer.sharedMaterial)
                    .Where(material => material != null)
                    .Distinct()
                    .ToArray();
                Assert.AreEqual(4, materials.Length, "Violet body, deep facets, coral armor and cyan core must remain visually distinct.");
                Assert.IsTrue(materials.All(material => material.shader.name == "Rasshiine/Low Poly Environment"));
                Assert.IsTrue(materials.All(material => material.GetColor("_BaseColor").maxColorComponent <= 0.88f));
                Assert.IsTrue(materials.Any(material => material.GetColor("_BaseColor").b > material.GetColor("_BaseColor").r * 1.5f));
                Assert.IsTrue(materials.Any(material => material.GetColor("_BaseColor").r > material.GetColor("_BaseColor").b * 2.5f));

                var bossMeshFilters = boss.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter.sharedMesh != null)
                    .ToArray();
                Assert.LessOrEqual(bossMeshFilters.Select(filter => filter.sharedMesh).Distinct().Count(), 3,
                    "Blocks, rounded shell facets and tapered spikes must each reuse one bounded shared mesh.");
                var triangleCount = bossMeshFilters.Sum(filter => filter.sharedMesh.triangles.Length / 3);
                Assert.LessOrEqual(triangleCount, 2600, "The runtime boss must remain a genuinely low-poly WebGL asset.");

                var coreRenderer = boss.Find("Core Glow").GetComponent<MeshRenderer>();
                Assert.AreEqual(ShadowCastingMode.Off, coreRenderer.shadowCastingMode);
                Assert.IsFalse(coreRenderer.receiveShadows);
                Assert.That(coreRenderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent, Is.InRange(0.20f, 0.25f));

                Assert.IsTrue(TryGetRendererBounds(boss.gameObject, out var bossBounds));
                Assert.That(bossBounds.size.y, Is.InRange(5.20f, 5.60f));
                Assert.That(bossBounds.size.x, Is.GreaterThan(bossBounds.size.y * 1.50f),
                    "The raid boss must read as a wide beetle/creature, not an upright mannequin.");
                Assert.That(boss.Find("Abdomen Shell").localPosition.y, Is.LessThan(1.75f));
                Assert.That(boss.Find("Crown Left").localEulerAngles.z, Is.InRange(43f, 49f));
                Assert.That(boss.Find("Crown Left").localScale.x, Is.GreaterThan(boss.Find("Crown Tip").localScale.x * 2.4f),
                    "The forward horn must visibly taper from a broad base into a small tip.");
                Assert.Less(Vector3.Distance(boss.Find("Crown Left").localPosition, boss.Find("Crown Right").localPosition), 0.75f);
                Assert.Less(Vector3.Distance(boss.Find("Crown Right").localPosition, boss.Find("Crown Rise").localPosition), 0.75f);
                Assert.Greater(boss.Find("Abdomen Shell").localScale.y, boss.Find("Shell Crystal Center").localScale.y * 1.65f,
                    "Purple shell mass must dominate the short coral dorsal shards.");
                Assert.That(boss.Find("Front Near Leg Upper").localScale.y, Is.LessThan(0.65f));
                Assert.That(boss.Find("Front Near Leg Lower").localScale.y, Is.LessThan(0.52f));

                var contactShadow = effectsRoot.Find("BossContactShadow");
                Assert.IsNotNull(contactShadow);
                var shadowRenderer = contactShadow.GetComponent<MeshRenderer>();
                Assert.AreEqual(ShadowCastingMode.Off, shadowRenderer.shadowCastingMode);
                Assert.IsFalse(shadowRenderer.receiveShadows);
                Assert.LessOrEqual(shadowRenderer.sharedMaterial.color.a, 0.30f);
                Assert.LessOrEqual(contactShadow.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, 48);
                Assert.That(contactShadow.localScale.x, Is.InRange(3.85f, 4.00f));
                Assert.That(contactShadow.localScale.z, Is.InRange(1.60f, 1.72f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ParticipantPaletteCreatesReusableBlueRedAndGreenHeroIdentities()
        {
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader);
            var source = new Material(shader) { name = "ParticipantPaletteTestSource" };
            try
            {
                var resolver = typeof(RaidBattleController).GetMethod(
                    "ResolveParticipantPaletteMaterial",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(resolver);

                var blue = (Material)resolver.Invoke(null, new object[] { source, 0 });
                var red = (Material)resolver.Invoke(null, new object[] { source, 1 });
                var green = (Material)resolver.Invoke(null, new object[] { source, 2 });
                var blueAgain = (Material)resolver.Invoke(null, new object[] { source, 3 });

                Assert.AreSame(blue, blueAgain, "Palette variants should be shared instead of allocated per participant.");
                Assert.That(blue.color.b, Is.GreaterThan(blue.color.r * 3f));
                Assert.That(red.color.r, Is.GreaterThan(red.color.g * 3f));
                Assert.That(green.color.g, Is.GreaterThan(green.color.r * 2f));
                Assert.IsTrue(blue.enableInstancing && red.enableInstancing && green.enableInstancing);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void BattleGridUsesOneTransparentVertexColorMaterialWithoutBrightHdrColors()
        {
            var root = new GameObject("RaidBattleGridMaterialTestRoot");
            try
            {
                var theme = CreateTheme(root.transform);
                var bossAnchor = CreateChild(root.transform, "BossAnchor");
                var partyAnchor = CreateChild(root.transform, "PartyAnchor");
                var effectsRoot = CreateChild(root.transform, "EffectsRoot");
                var battleController = new GameObject("RaidBattleGridMaterialTestController", typeof(RaidBattleController)).GetComponent<RaidBattleController>();
                battleController.transform.SetParent(root.transform, false);
                battleController.Configure(theme, bossAnchor, partyAnchor, effectsRoot);

                var repository = new LocalGameRepository();
                repository.StartBattle();
                battleController.LoadBattle(repository.ActiveBattle);

                var gridLines = effectsRoot.GetComponentsInChildren<LineRenderer>(true)
                    .Where(line => line.name.StartsWith("Grid_Ring_", System.StringComparison.Ordinal) || line.name.StartsWith("Arena_Spoke_", System.StringComparison.Ordinal))
                    .ToArray();
                Assert.AreEqual(10, gridLines.Length);
                var sharedMaterial = gridLines[0].sharedMaterial;
                Assert.IsNotNull(sharedMaterial);
                Assert.AreEqual("Sprites/Default", sharedMaterial.shader.name);
                Assert.GreaterOrEqual(sharedMaterial.renderQueue, (int)RenderQueue.Transparent);

                foreach (var line in gridLines)
                {
                    Assert.AreSame(sharedMaterial, line.sharedMaterial);
                    Assert.AreEqual(ShadowCastingMode.Off, line.shadowCastingMode);
                    Assert.IsFalse(line.receiveShadows);
                    Assert.Greater(line.startColor.a, 0f);
                    Assert.LessOrEqual(line.startColor.a, 0.50f);
                    Assert.LessOrEqual(Mathf.Max(line.startColor.r, Mathf.Max(line.startColor.g, line.startColor.b)), 0.7801f);
                    Assert.AreEqual(line.startColor, line.endColor);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReloadingBattleReusesGeneratedMeshesAndOnDestroyReleasesOwnedResources()
        {
            var root = new GameObject("RaidBattleResourceLifetimeTestRoot");
            try
            {
                var theme = CreateTheme(root.transform);
                var bossAnchor = CreateChild(root.transform, "BossAnchor");
                var partyAnchor = CreateChild(root.transform, "PartyAnchor");
                var effectsRoot = CreateChild(root.transform, "EffectsRoot");
                var controllerObject = new GameObject("RaidBattleResourceLifetimeTestController", typeof(RaidBattleController));
                controllerObject.transform.SetParent(root.transform, false);
                var battleController = controllerObject.GetComponent<RaidBattleController>();
                battleController.Configure(theme, bossAnchor, partyAnchor, effectsRoot);

                var repository = new LocalGameRepository();
                repository.StartBattle();
                battleController.LoadBattle(repository.ActiveBattle);

                var firstNatureMesh = effectsRoot.Find("Low Poly Nature Battle Arena/LowPoly Generated Arena Ground").GetComponent<MeshFilter>().sharedMesh;
                var firstClearingMesh = effectsRoot.Find("LowPolyBattleClearing").GetComponent<MeshFilter>().sharedMesh;
                var firstDaisMesh = effectsRoot.Find("LowPolyBossFocusDais").GetComponent<MeshFilter>().sharedMesh;
                var firstLineMaterial = effectsRoot.GetComponentsInChildren<LineRenderer>(true)
                    .First(line => line.name.StartsWith("Grid_Ring_", System.StringComparison.Ordinal))
                    .sharedMaterial;
                var firstContactShadowMesh = effectsRoot.Find("BossContactShadow").GetComponent<MeshFilter>().sharedMesh;
                var firstContactShadowMaterial = effectsRoot.Find("BossContactShadow").GetComponent<MeshRenderer>().sharedMaterial;

                battleController.LoadBattle(repository.ActiveBattle);

                var secondNatureMesh = effectsRoot.Find("Low Poly Nature Battle Arena/LowPoly Generated Arena Ground").GetComponent<MeshFilter>().sharedMesh;
                var secondClearingMesh = effectsRoot.Find("LowPolyBattleClearing").GetComponent<MeshFilter>().sharedMesh;
                var secondDaisMesh = effectsRoot.Find("LowPolyBossFocusDais").GetComponent<MeshFilter>().sharedMesh;
                var secondLineMaterial = effectsRoot.GetComponentsInChildren<LineRenderer>(true)
                    .First(line => line.name.StartsWith("Grid_Ring_", System.StringComparison.Ordinal))
                    .sharedMaterial;
                var ownedDiscMeshes = effectsRoot.GetComponentsInChildren<MeshFilter>(true)
                    .Select(filter => filter.sharedMesh)
                    .Where(mesh => mesh != null && mesh.name.StartsWith("LowPolyBattleDisc_", System.StringComparison.Ordinal))
                    .ToArray();

                Assert.AreSame(firstNatureMesh, secondNatureMesh);
                Assert.AreSame(firstClearingMesh, secondClearingMesh);
                Assert.AreSame(firstDaisMesh, secondDaisMesh);
                Assert.AreSame(firstLineMaterial, secondLineMaterial);
                Assert.AreEqual(3, ownedDiscMeshes.Length,
                    "Ground, its shared water overlay, and the clearing are the only low-poly disc renderers.");
                Assert.AreEqual(10, effectsRoot.GetComponentsInChildren<LineRenderer>(true).Count(line =>
                    line.name.StartsWith("Grid_Ring_", System.StringComparison.Ordinal) ||
                    line.name.StartsWith("Arena_Spoke_", System.StringComparison.Ordinal)));

                // Scheduled/empty states retain the same authored canyon identity;
                // visuals must not collapse into a generic green fallback when no
                // participant or boss data has arrived yet.
                battleController.LoadBattle(null);
                var firstWaitingMesh = effectsRoot.Find("Low Poly Nature Battle Arena/LowPoly Generated Arena Ground").GetComponent<MeshFilter>().sharedMesh;
                battleController.LoadBattle(null);
                var secondWaitingMesh = effectsRoot.Find("Low Poly Nature Battle Arena/LowPoly Generated Arena Ground").GetComponent<MeshFilter>().sharedMesh;
                var waitingRenderer = effectsRoot.Find("Low Poly Nature Battle Arena/LowPoly Generated Arena Ground").GetComponent<MeshRenderer>();

                Assert.AreSame(firstWaitingMesh, secondWaitingMesh);
                Assert.AreEqual(ShadowCastingMode.Off, waitingRenderer.shadowCastingMode);
                Assert.AreEqual(LightProbeUsage.Off, waitingRenderer.lightProbeUsage);

                var onDestroy = typeof(RaidBattleController).GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(onDestroy);
                onDestroy.Invoke(battleController, null);
                Assert.IsTrue(firstNatureMesh == null);
                Assert.IsTrue(firstClearingMesh == null);
                Assert.IsTrue(firstDaisMesh == null);
                Assert.IsTrue(firstLineMaterial == null);
                Assert.IsTrue(firstContactShadowMesh == null);
                Assert.IsTrue(firstContactShadowMaterial == null);
                Object.DestroyImmediate(controllerObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RasshiineTheme CreateTheme(Transform parent)
        {
            var themeObject = new GameObject("RaidBattleControlTestTheme", typeof(RasshiineTheme));
            themeObject.transform.SetParent(parent, false);
            var theme = themeObject.GetComponent<RasshiineTheme>();
            theme.MemberPlaceholderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            theme.BattleTerrainPrefabs = LoadPrefabs(
                RasshiineTheme.NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_a_01.prefab",
                RasshiineTheme.NatureModularTerrainPrefabDir + "/Terrain/MT/NoLOD/M/MT_Terrain_M_b_01.prefab");
            theme.BattleHillPrefabs = LoadPrefabs(
                RasshiineTheme.NatureVegetationBonusPrefabDir + "/Hills/Hill_m_01.prefab");
            theme.BattleMountainPrefabs = LoadPrefabs(
                RasshiineTheme.NatureModularTerrainPrefabDir + "/Mountains/MT/NoLOD/M/MT_Mountain_M_a_02.prefab");
            theme.BattleTreePrefabs = LoadPrefabs(
                RasshiineTheme.NatureTreePrefabDir + "/Oak_Trees/Oak_Tree_m_01.prefab",
                RasshiineTheme.NatureTreePrefabDir + "/Simple_Trees/Simple_Tree_m_01.prefab");
            theme.BattleGrassPrefabs = LoadPrefabs(
                RasshiineTheme.NatureVegetationPrefabDir + "/Grass/MeshGrass/OneSide/Grass_a_OneS_01.prefab");
            theme.BattleFlowerPrefabs = LoadPrefabs(
                RasshiineTheme.NatureVegetationPrefabDir + "/Flowers/TwoSided/Flower_a_TwoS_01.prefab");
            theme.BattleRockPrefabs = LoadPrefabs(
                RasshiineTheme.NatureRockPrefabDir + "/Round_Rocks/Rock_Round_s_2C_01.prefab",
                RasshiineTheme.NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_03.prefab",
                RasshiineTheme.NatureRockPrefabDir + "/Round_Rocks/Rock_Round_m_2C_07.prefab");
            theme.BattleCloudPrefabs = LoadPrefabs(
                RasshiineTheme.NatureCloudPrefabDir + "/Cloud_01.prefab");
            return theme;
        }

        private static GameObject[] LoadPrefabs(params string[] paths)
        {
            return paths
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .Where(prefab => prefab != null)
                .ToArray();
        }

        private static bool HasMagentaMaterial(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                {
                    return true;
                }

                if (material.shader != null && material.shader.name.Contains("InternalErrorShader"))
                {
                    return true;
                }

                var color = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.HasProperty("_Color")
                        ? material.color
                        : Color.clear;
                if (color.r > 0.8f && color.g < 0.35f && color.b > 0.75f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRendererBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetActiveRendererBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static bool HasTexture(Material material)
        {
            if (material == null)
            {
                return false;
            }

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
            {
                return true;
            }

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
            {
                return true;
            }

            return material.mainTexture != null;
        }

        private static void AssertRenderersUseShadowMode(Transform[] roots, ShadowCastingMode expectedMode)
        {
            var renderers = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.IsNotEmpty(renderers);
            Assert.IsTrue(renderers.All(renderer => renderer.shadowCastingMode == expectedMode));
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }
    }
}
