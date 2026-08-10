using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AttackOnRasshiine.Editor
{
    public static class RasshiineBattleRenderDiagnostics
    {
        private const string ReportPath = "/private/tmp/attack-on-rasshiine-battle-render.txt";

        [MenuItem("AttackOnRasshiine/Dump Battle Render Diagnostics")]
        public static void DumpBattleRenderDiagnostics()
        {
            var report = new StringBuilder();
            var skybox = RenderSettings.skybox;
            report.AppendLine($"Skybox: {DescribeMaterial(skybox)}");
            if (skybox != null)
            {
                AppendFloat(report, skybox, "_PanoramaBlend");
                AppendFloat(report, skybox, "_CloudOpacity");
                AppendFloat(report, skybox, "_Exposure");
                if (skybox.HasProperty("_SkyPanorama"))
                {
                    var panorama = skybox.GetTexture("_SkyPanorama");
                    report.AppendLine($"  _SkyPanorama={(panorama != null ? panorama.name : "none")}");
                }
            }

            report.AppendLine("Battle panorama backdrop:");
            var panoramaBackdrop = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.name == "Fantasy Battle Sky Panorama");
            if (panoramaBackdrop == null)
            {
                report.AppendLine("  NOT FOUND");
            }
            else
            {
                var renderer = panoramaBackdrop.GetComponent<Renderer>();
                var meshFilter = panoramaBackdrop.GetComponent<MeshFilter>();
                report.AppendLine($"  active={panoramaBackdrop.gameObject.activeInHierarchy} position={panoramaBackdrop.position} scale={panoramaBackdrop.lossyScale} mesh={(meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "none")} material={DescribeMaterial(renderer != null ? renderer.sharedMaterial : null)}");
            }

            report.AppendLine("Lights:");
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                report.AppendLine($"  {light.name}: {light.type} intensity={light.intensity:0.###} color={light.color} shadows={light.shadows}");
            }

            report.AppendLine("Boss renderers:");
            var boss = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.name == "BossEnemy");
            if (boss == null)
            {
                report.AppendLine("  NOT FOUND");
            }

            else
            {
                foreach (var renderer in boss.GetComponentsInChildren<Renderer>(true))
                {
                    var propertyBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(propertyBlock);
                    report.AppendLine($"  {renderer.name} active={renderer.gameObject.activeInHierarchy} bounds={renderer.bounds.size} hasPropertyBlock={renderer.HasPropertyBlock()} blockBase={propertyBlock.GetColor("_BaseColor")} blockColor={propertyBlock.GetColor("_Color")} blockEmission={propertyBlock.GetColor("_EmissionColor")}");
                    for (var index = 0; index < renderer.sharedMaterials.Length; index++)
                    {
                        report.AppendLine($"    [{index}] {DescribeMaterial(renderer.sharedMaterials[index])}");
                    }
                }

                report.AppendLine("  Components:");
                foreach (var component in boss.GetComponentsInChildren<Component>(true))
                {
                    report.AppendLine($"    {HierarchyPath(component.transform)} :: {component.GetType().FullName}");
                }
            }

            report.AppendLine("Participant renderers:");
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                         .Where(item =>
                         {
                             var path = HierarchyPath(item.transform);
                             var shaderName = item.sharedMaterial != null && item.sharedMaterial.shader != null
                                 ? item.sharedMaterial.shader.name
                                 : string.Empty;
                             return path.Contains("Party", System.StringComparison.OrdinalIgnoreCase) ||
                                    path.Contains("Participant", System.StringComparison.OrdinalIgnoreCase) ||
                                    shaderName.Contains("Low Poly Character", System.StringComparison.Ordinal);
                         }))
            {
                report.AppendLine($"  {HierarchyPath(renderer.transform)} hasPropertyBlock={renderer.HasPropertyBlock()} material={DescribeMaterial(renderer.sharedMaterial)}");
            }

            report.AppendLine("Large/high renderers:");
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                         .Where(item => item.bounds.size.magnitude > 12f || item.bounds.center.y > 3f)
                         .OrderByDescending(item => item.bounds.size.magnitude))
            {
                report.AppendLine($"  {HierarchyPath(renderer.transform)} boundsCenter={renderer.bounds.center} boundsSize={renderer.bounds.size} material={DescribeMaterial(renderer.sharedMaterial)}");
            }

            report.AppendLine("Visible UI images:");
            foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                         .Where(item => item.enabled && item.color.a > 0.001f))
            {
                report.AppendLine($"  {HierarchyPath(image.transform)} sprite={(image.sprite != null ? image.sprite.name : "none")} color={image.color}");
            }

            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log($"AttackOnRasshiine battle render diagnostics saved: {ReportPath}");
        }

        private static string DescribeMaterial(Material material)
        {
            if (material == null)
            {
                return "null";
            }

            var color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color")
                    ? material.GetColor("_Color")
                    : Color.clear;
            var emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.clear;
            var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
            return $"{material.name} shader={material.shader?.name} color={color} emission={emission} emissionKeyword={material.IsKeywordEnabled("_EMISSION")} texture={(texture != null ? texture.name : "none")}";
        }

        private static void AppendFloat(StringBuilder report, Material material, string propertyName)
        {
            if (material.HasProperty(propertyName))
            {
                report.AppendLine($"  {propertyName}={material.GetFloat(propertyName):0.###}");
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
