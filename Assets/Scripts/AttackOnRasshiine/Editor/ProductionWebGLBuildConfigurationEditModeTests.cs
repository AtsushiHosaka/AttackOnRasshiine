using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

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
            Assert.AreEqual("Builds/WebGLVisualQa", RasshiineSceneBuilder.VisualQaWebGLOutputPath);
            Assert.AreNotEqual(RasshiineSceneBuilder.WebGLOutputPath, RasshiineSceneBuilder.VisualQaWebGLOutputPath);
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

        [Test]
        public void ReleaseWebGLSettingsMatchTheProductionContract()
        {
            ProductionWebGLSettings.Apply();
            var violations = ProductionWebGLSettings.FindViolations();

            Assert.IsEmpty(violations, string.Join("\n", violations));
            Assert.AreEqual(
                Il2CppCodeGeneration.OptimizeSize,
                PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL));
            Assert.AreEqual(
                ManagedStrippingLevel.High,
                PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL));
            Assert.LessOrEqual(
                ProductionWebGLSettings.MaximumMemorySizeMb,
                512,
                "The browser build must fail closed before a runaway Unity heap can consume gigabytes.");
            Assert.GreaterOrEqual(
                ProductionWebGLSettings.MaximumMemorySizeMb,
                384,
                "The cap must retain at least twice the measured complete-flow working-set headroom.");
        }

        [Test]
        public void ManagedStrippingPreservesProductionJsonDtoFields()
        {
            const string linkPath = "Assets/link.xml";
            Assert.IsTrue(File.Exists(linkPath), "High managed stripping requires an explicit JsonUtility preservation contract.");

            var descriptor = File.ReadAllText(linkPath);
            StringAssert.Contains("AttackOnRasshiine.Runtime.Services.*", descriptor);
            StringAssert.Contains("AttackOnRasshiine.Runtime.Data.*", descriptor);
            StringAssert.Contains("preserve=\"fields\"", descriptor);
        }

        [Test]
        public void MobileUrpAssetExcludesUnusedBrowserFeatures()
        {
            var settings = File.ReadAllText("Assets/Settings/Mobile_RPAsset.asset");
            StringAssert.Contains("m_SupportsHDR: 0", settings);
            StringAssert.Contains("m_SupportsTerrainHoles: 0", settings);
            StringAssert.Contains("m_MixedLightingSupported: 0", settings);
            StringAssert.Contains("m_SupportsLightCookies: 0", settings);
            StringAssert.Contains("m_SupportsLightLayers: 0", settings);
            StringAssert.Contains("m_UseAdaptivePerformance: 0", settings);
            StringAssert.Contains("m_SupportDataDrivenLensFlare: 0", settings);
            StringAssert.Contains("m_SupportScreenSpaceLensFlare: 0", settings);

            var globalSettings = File.ReadAllText("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            StringAssert.Contains("m_IncludeTerrainShaders: 0", globalSettings);
        }

        [Test]
        public void RuntimeBakedButtonTexturesDiscardTheirCpuPixelCopy()
        {
            var baker = File.ReadAllText(
                "Assets/Scripts/AttackOnRasshiine/Runtime/UI/BakedTextButtonImage.cs");

            StringAssert.Contains("texture.Apply(false, true);", baker);
            StringAssert.DoesNotContain("texture.Apply(false, false);", baker);
        }

        [Test]
        public void WebGlBuildAppliesAndValidatesReleaseSettingsBeforeStrictBuild()
        {
            var builder = File.ReadAllText("Assets/Scripts/AttackOnRasshiine/Editor/RasshiineSceneBuilder.cs");

            StringAssert.Contains("ProductionWebGLSettings.Apply();", builder);
            Assert.AreEqual(BuildOptions.StrictMode, RasshiineSceneBuilder.ProductionWebGLBuildOptions);
            Assert.AreEqual(
                BuildOptions.StrictMode | BuildOptions.Development,
                RasshiineSceneBuilder.VisualQaWebGLBuildOptions);
            Assert.DoesNotThrow(() => RasshiineSceneBuilder.ValidateWebGlBuildBoundary(
                RasshiineSceneBuilder.WebGLOutputPath,
                RasshiineSceneBuilder.ProductionWebGLBuildOptions));
            Assert.DoesNotThrow(() => RasshiineSceneBuilder.ValidateWebGlBuildBoundary(
                RasshiineSceneBuilder.VisualQaWebGLOutputPath,
                RasshiineSceneBuilder.VisualQaWebGLBuildOptions));
            Assert.Throws<System.InvalidOperationException>(() => RasshiineSceneBuilder.ValidateWebGlBuildBoundary(
                RasshiineSceneBuilder.WebGLOutputPath,
                BuildOptions.StrictMode | BuildOptions.Development));
            Assert.Throws<System.InvalidOperationException>(() => RasshiineSceneBuilder.ValidateWebGlBuildBoundary(
                RasshiineSceneBuilder.VisualQaWebGLOutputPath,
                BuildOptions.StrictMode));
        }
    }
}
