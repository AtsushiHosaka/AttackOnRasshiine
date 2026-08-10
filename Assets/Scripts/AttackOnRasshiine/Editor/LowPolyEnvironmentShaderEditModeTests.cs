using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class LowPolyEnvironmentShaderEditModeTests
    {
        [Test]
        public void TextureFreeFacetShaderIsAvailableAndWebGlSafe()
        {
            var shader = Resources.Load<Shader>("Shaders/RasshiineLowPolyEnvironment");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("Rasshiine/Low Poly Environment"));
            Assert.That(shader.isSupported, Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(shader),
                Is.EqualTo("Assets/Resources/Shaders/RasshiineLowPolyEnvironment.shader"));

            var material = new Material(shader);
            try
            {
                Assert.That(material.FindPass("ForwardLit"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.HasProperty("_BaseColor"), Is.True);
                Assert.That(material.HasProperty("_EmissionColor"), Is.True);
                Assert.That(material.HasProperty("_FacetSteps"), Is.True);
                Assert.That(material.HasProperty("_ShadowTint"), Is.True);
                Assert.That(material.HasProperty("_LightTint"), Is.True);
                Assert.That(material.HasProperty("_RimColor"), Is.True);
                Assert.That(material.HasProperty("_AmbientStrength"), Is.True);
                Assert.That(material.HasProperty("_RimStrength"), Is.True);
                Assert.That(material.HasProperty("_Cull"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }

            var source = File.ReadAllText(AssetDatabase.GetAssetPath(shader));
            Assert.That(source, Does.Contain("#pragma target 2.0"));
            Assert.That(source, Does.Contain("GetMainLight"));
            Assert.That(source, Does.Contain("GetShadowCoord"));
            Assert.That(source, Does.Contain("_MAIN_LIGHT_SHADOWS_SCREEN"));
            Assert.That(source, Does.Contain("_SHADOWS_SOFT_LOW"));
            Assert.That(source, Does.Contain("_SHADOWS_SOFT_MEDIUM"));
            Assert.That(source, Does.Contain("_SHADOWS_SOFT_HIGH"));
            Assert.That(source, Does.Contain("_ADDITIONAL_LIGHTS_VERTEX"));
            Assert.That(source, Does.Contain("_ADDITIONAL_LIGHTS"));
            Assert.That(source, Does.Contain("VertexLighting"));
            Assert.That(source, Does.Contain("GetAdditionalLight(0u"));
            Assert.That(source, Does.Contain("Name \"ShadowCaster\""));
            Assert.That(source, Does.Contain("ApplyShadowBias"));
            Assert.That(source, Does.Contain("Name \"DepthOnly\""));
            Assert.That(source, Does.Not.Contain("UsePass"));
            Assert.That(source, Does.Contain(
                "#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\""));
            Assert.That(source, Does.Contain(
                "#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl\""));
            Assert.That(source, Does.Contain(
                "#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl\""));
            Assert.That(source, Does.Not.Contain("#include \"Assets/"));
            Assert.That(
                source.Split(new[] { "CBUFFER_START(UnityPerMaterial)" }, System.StringSplitOptions.None).Length - 1,
                Is.EqualTo(1));
            Assert.That(source, Does.Not.Contain("tex2D("));
            Assert.That(source, Does.Not.Contain("SAMPLE_TEXTURE"));
            Assert.That(source, Does.Not.Match(@"\bfor\s*\("));
            Assert.That(source, Does.Not.Match(@"\bwhile\s*\("));

            var compilerMessages = ShaderUtil.GetShaderMessages(shader)
                .Select(message => message.message)
                .ToArray();
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, string.Join("\n", compilerMessages));
        }
    }
}
