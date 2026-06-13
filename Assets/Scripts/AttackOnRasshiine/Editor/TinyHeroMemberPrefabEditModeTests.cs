using System.IO;
using System.Linq;
using AttackOnRasshiine.Runtime.Scene;
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
    }
}
