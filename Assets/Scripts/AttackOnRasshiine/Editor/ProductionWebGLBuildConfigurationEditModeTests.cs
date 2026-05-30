using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace AttackOnRasshiine.Editor
{
    public sealed class ProductionWebGLBuildConfigurationEditModeTests
    {
        [Test]
        public void BuildSettingsContainOnlyProductionScene()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToList();

            CollectionAssert.AreEqual(new[] { RasshiineSceneBuilder.ProductionScenePath }, enabledScenes);
            CollectionAssert.DoesNotContain(enabledScenes, RasshiineSceneBuilder.PrototypeScenePath);
            Assert.IsFalse(enabledScenes.Any(path => path.Contains("Demo Scenes") || path.Contains("Heat - Complete Modern UI")));
        }

        [Test]
        public void ProductionSceneAssetExistsForWebGLBuild()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RasshiineSceneBuilder.ProductionScenePath);

            Assert.IsNotNull(scene);
            Assert.AreEqual("Builds/WebGL", RasshiineSceneBuilder.WebGLOutputPath);
        }
    }
}
