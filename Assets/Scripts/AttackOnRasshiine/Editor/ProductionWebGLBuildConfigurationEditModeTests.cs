using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace AttackOnRasshiine.Editor
{
    public sealed class ProductionWebGLBuildConfigurationEditModeTests
    {
        [Test]
        public void BuildSettingsContainProductionSceneFlowInOrder()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToList();

            CollectionAssert.AreEqual(RasshiineSceneBuilder.ProductionScenePaths, enabledScenes);
            Assert.AreEqual(RasshiineSceneBuilder.ProductionScenePath, enabledScenes[0]);
            CollectionAssert.DoesNotContain(enabledScenes, RasshiineSceneBuilder.PrototypeScenePath);
            CollectionAssert.DoesNotContain(enabledScenes, RasshiineSceneBuilder.LegacyProductionScenePath);
            Assert.IsFalse(enabledScenes.Any(path => path.Contains("Demo Scenes") || path.Contains("Heat - Complete Modern UI")));
        }

        [Test]
        public void ProductionSceneAssetsExistForWebGLBuild()
        {
            foreach (var scenePath in RasshiineSceneBuilder.ProductionScenePaths)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

                Assert.IsNotNull(scene, $"{scenePath} must exist for the production WebGL build.");
            }

            Assert.AreEqual("Builds/WebGL", RasshiineSceneBuilder.WebGLOutputPath);
        }
    }
}
