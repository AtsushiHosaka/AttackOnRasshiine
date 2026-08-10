using System.IO;
using System.Linq;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class TinyHeroMemberPrefabEditModeTests
    {
        [Test]
        public void TinyHeroMemberPrefabIsImportedWithRenderableAnimator()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);

            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponentInChildren<Animator>(true));
            Assert.Greater(prefab.GetComponentsInChildren<Renderer>(true).Length, 0);
        }

        [Test]
        public void SavedRaidScenesReferenceTinyHeroForMembers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            Assert.IsNotNull(prefab);

            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(prefab);
            var expectedReference = $"MemberPlaceholderPrefab: {{fileID: {globalObjectId.targetObjectId}, guid: {globalObjectId.assetGUID}, type: 3}}";
            var scenePaths = RasshiineSceneCatalog.GetProductionScenePaths()
                .Concat(new[] { RasshiineSceneCatalog.PrototypeScenePath })
                .Distinct();

            foreach (var scenePath in scenePaths)
            {
                var yaml = File.ReadAllText(scenePath);
                if (!yaml.Contains("MemberPlaceholderPrefab:"))
                {
                    continue;
                }

                StringAssert.Contains(expectedReference, yaml, scenePath);
            }
        }

        [Test]
        public void ServerCatalogKeysResolveToVariantsIncludedInMc01()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            Assert.IsNotNull(prefab);
            var includedNames = prefab.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.name)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            var catalog = new[]
            {
                Item("tinyhero_bronze_blade", "tinyhero/weapon/bronze_blade"),
                Item("tinyhero_forest_bow", "tinyhero/weapon/forest_bow"),
                Item("tinyhero_starlight_staff", "tinyhero/weapon/starlight_staff"),
                Item("tinyhero_suncrest_blade", "tinyhero/weapon/suncrest_blade"),
                Item("tinyhero_scout_hood", "tinyhero/head/scout_hood"),
                Item("tinyhero_guardian_coat", "tinyhero/body/guardian_coat"),
                Item("tinyhero_leaf_cape", "tinyhero/back/leaf_cape"),
                Item("tinyhero_star_charm", "tinyhero/accessory/star_charm")
            };

            foreach (var item in catalog)
            {
                var variant = TinyHeroCosmeticApplicator.ResolveVariantName(item);
                Assert.IsNotEmpty(variant, item.Code);
                Assert.IsTrue(includedNames.Contains(variant), $"MC01 is missing {variant} for {item.Code}.");
            }
        }

        [Test]
        public void CompleteLoadoutActivatesExactlyOneMc01VariantPerManagedSlot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RasshiineTheme.TinyHeroMemberPrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var equipped = new[]
                {
                    Equipped("weapon", "tinyhero_forest_bow", "tinyhero/weapon/forest_bow"),
                    Equipped("head", "tinyhero_scout_hood", "tinyhero/head/scout_hood"),
                    Equipped("body", "tinyhero_guardian_coat", "tinyhero/body/guardian_coat"),
                    Equipped("back", "tinyhero_leaf_cape", "tinyhero/back/leaf_cape"),
                    Equipped("accessory", "tinyhero_star_charm", "tinyhero/accessory/star_charm")
                };
                TinyHeroCosmeticApplicator.Apply(instance, new CosmeticInventoryDto
                {
                    Equipped = equipped.ToList()
                });

                AssertActiveVariant(instance, "weapon", "Bows");
                AssertActiveVariant(instance, "head", "Hat02");
                AssertActiveVariant(instance, "body", "Body16");
                AssertActiveVariant(instance, "back", "Cloak03");
                AssertActiveVariant(instance, "accessory", "AC01_Heart");
                Assert.IsFalse(TinyHeroCosmeticApplicator.IsSlotCandidate("back", "CloakBone01"));

                var texturedMaterials = instance.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null && material.mainTexture != null)
                    .ToArray();
                Assert.IsNotEmpty(texturedMaterials);
                Assert.IsTrue(
                    texturedMaterials.All(material => material.shader != null && material.shader.name.Contains("Universal Render Pipeline")),
                    "Tiny Hero materials must be converted from the built-in Standard shader for WebGL URP rendering.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static CosmeticItemDto Item(string code, string assetKey)
        {
            return new CosmeticItemDto { Id = code, Code = code, UnityAssetKey = assetKey };
        }

        private static EquippedCosmeticLoadoutDto Equipped(string slot, string code, string assetKey)
        {
            return new EquippedCosmeticLoadoutDto { Slot = slot, Item = Item(code, assetKey) };
        }

        private static void AssertActiveVariant(GameObject model, string slot, string expectedName)
        {
            var active = model.GetComponentsInChildren<Transform>(true)
                .Where(transform => TinyHeroCosmeticApplicator.IsSlotCandidate(slot, transform.name) && transform.gameObject.activeSelf)
                .ToArray();
            Assert.AreEqual(1, active.Length, $"{slot} should have one active modular variant.");
            Assert.AreEqual(expectedName, active[0].name, slot);
        }
    }
}
