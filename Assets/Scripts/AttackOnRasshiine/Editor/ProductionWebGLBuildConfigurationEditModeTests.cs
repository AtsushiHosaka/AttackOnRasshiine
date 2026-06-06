using System.IO;
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

        [Test]
        public void ProjectSettingsIdentifyProductionWebGLApplication()
        {
            var settings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");

            StringAssert.Contains("companyName: AtsushiHosaka", settings);
            StringAssert.Contains("productName: AttackOnRasshiine", settings);
            StringAssert.Contains("webGLTemplate: APPLICATION:Default", settings);
            StringAssert.Contains("webGLDataCaching: 1", settings);
            StringAssert.Contains("webGLCompressionFormat: 0", settings);
        }
    }
}
