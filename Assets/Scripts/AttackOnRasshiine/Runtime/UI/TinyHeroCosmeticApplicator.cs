using System;
using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Services;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.UI
{
    /// <summary>
    /// Applies the server cosmetic loadout to the modular children included in
    /// RPG Tiny Hero Wave PBR's MC01 prefab. No runtime asset loading is needed:
    /// WebGL only toggles the already batched low-poly variants.
    /// </summary>
    public static class TinyHeroCosmeticApplicator
    {
        private const string DefaultBody = "Body05";
        private const string DefaultBack = "Cloak02";
        private const string DefaultAccessory = "";
        private const string DefaultWeapon = "OHS09_Sword";

        private static readonly Dictionary<Material, Material> UrpMaterialBySource = new Dictionary<Material, Material>();

        private static readonly IReadOnlyDictionary<string, string> VariantByCatalogKey =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tinyhero_bronze_blade"] = "OHS09_Sword",
                ["tinyhero/weapon/bronze_blade"] = "OHS09_Sword",
                ["tinyhero_forest_bow"] = "Bows",
                ["tinyhero/weapon/forest_bow"] = "Bows",
                ["tinyhero_starlight_staff"] = "Wand07",
                ["tinyhero/weapon/starlight_staff"] = "Wand07",
                ["tinyhero_suncrest_blade"] = "OHS16_Sword",
                ["tinyhero/weapon/suncrest_blade"] = "OHS16_Sword",
                ["tinyhero_scout_hood"] = "Hat02",
                ["tinyhero/head/scout_hood"] = "Hat02",
                ["tinyhero_guardian_coat"] = "Body16",
                ["tinyhero/body/guardian_coat"] = "Body16",
                ["tinyhero_leaf_cape"] = "Cloak03",
                ["tinyhero/back/leaf_cape"] = "Cloak03",
                ["tinyhero_star_charm"] = "AC01_Heart",
                ["tinyhero/accessory/star_charm"] = "AC01_Heart"
            };

        public static void Apply(GameObject model, CosmeticInventoryDto inventory)
        {
            if (model == null)
            {
                return;
            }

            ApplyUrpMaterials(model);

            var equippedBySlot = (inventory?.Equipped ?? new List<EquippedCosmeticLoadoutDto>())
                .Where(entry => entry?.Item != null && !string.IsNullOrWhiteSpace(entry.Slot))
                .GroupBy(entry => NormalizeSlot(entry.Slot), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Item, StringComparer.OrdinalIgnoreCase);

            ApplySlot(model, "head", equippedBySlot.TryGetValue("head", out var head) ? ResolveVariantName(head) : string.Empty);
            ApplySlot(model, "body", equippedBySlot.TryGetValue("body", out var body) ? ResolveVariantName(body) : DefaultBody);
            ApplySlot(model, "back", equippedBySlot.TryGetValue("back", out var back) ? ResolveVariantName(back) : DefaultBack);
            ApplySlot(model, "accessory", equippedBySlot.TryGetValue("accessory", out var accessory) ? ResolveVariantName(accessory) : DefaultAccessory);
            ApplySlot(model, "weapon", equippedBySlot.TryGetValue("weapon", out var weapon) ? ResolveVariantName(weapon) : DefaultWeapon);
        }

        private static void ApplyUrpMaterials(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var index = 0; index < materials.Length; index++)
                {
                    var source = materials[index];
                    if (!NeedsUrpConversion(source))
                    {
                        continue;
                    }

                    materials[index] = GetOrCreateUrpMaterial(source);
                    changed |= materials[index] != source;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static bool NeedsUrpConversion(Material material)
        {
            return material != null &&
                   material.shader != null &&
                   !material.shader.name.Contains("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) &&
                   (material.HasProperty("_MainTex") || material.mainTexture != null);
        }

        private static Material GetOrCreateUrpMaterial(Material source)
        {
            if (UrpMaterialBySource.TryGetValue(source, out var cached) && cached != null)
            {
                return cached;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return source;
            }

            var material = new Material(shader)
            {
                name = source.name + "_RasshiineURP",
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            var baseTexture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : source.mainTexture;
            var baseColor = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            SetTexture(material, "_BaseMap", baseTexture);
            SetTexture(material, "_MainTex", baseTexture);
            SetColor(material, "_BaseColor", baseColor);
            SetColor(material, "_Color", baseColor);

            var metallicMap = source.HasProperty("_MetallicGlossMap") ? source.GetTexture("_MetallicGlossMap") : null;
            SetTexture(material, "_MetallicGlossMap", metallicMap);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.12f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.28f);
            }

            var emissionMap = source.HasProperty("_EmissionMap") ? source.GetTexture("_EmissionMap") : null;
            if (emissionMap != null)
            {
                SetTexture(material, "_EmissionMap", emissionMap);
                SetColor(material, "_EmissionColor", new Color(0.42f, 0.54f, 0.72f, 1f));
                material.EnableKeyword("_EMISSION");
            }

            UrpMaterialBySource[source] = material;
            return material;
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        private static void SetColor(Material material, string property, Color color)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        public static string ResolveVariantName(CosmeticItemDto item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(item.Code) && VariantByCatalogKey.TryGetValue(item.Code.Trim(), out var byCode))
            {
                return byCode;
            }

            return !string.IsNullOrWhiteSpace(item.UnityAssetKey) &&
                   VariantByCatalogKey.TryGetValue(item.UnityAssetKey.Trim(), out var byAssetKey)
                ? byAssetKey
                : string.Empty;
        }

        public static bool IsSlotCandidate(string slot, string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            var normalized = NormalizeSlot(slot);
            return normalized switch
            {
                "head" => StartsWithNumberedVariant(objectName, "Hat") || StartsWithNumberedVariant(objectName, "HeadArmor"),
                "body" => StartsWithNumberedVariant(objectName, "Body"),
                "back" => StartsWithNumberedVariant(objectName, "Cloak") && !objectName.StartsWith("CloakBone", StringComparison.OrdinalIgnoreCase) ||
                          StartsWithNumberedVariant(objectName, "BackPack"),
                "accessory" => objectName.StartsWith("AC", StringComparison.OrdinalIgnoreCase) && objectName.Length > 3 && char.IsDigit(objectName[2]),
                "weapon" => StartsWithNumberedVariant(objectName, "OHS") ||
                            StartsWithNumberedVariant(objectName, "THS") ||
                            StartsWithNumberedVariant(objectName, "Wand") ||
                            StartsWithNumberedVariant(objectName, "Spear") ||
                            string.Equals(objectName, "Bows", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private static void ApplySlot(GameObject model, string slot, string variantName)
        {
            var candidates = model.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform != model.transform && IsSlotCandidate(slot, transform.name))
                .ToArray();
            foreach (var candidate in candidates)
            {
                candidate.gameObject.SetActive(false);
            }

            if (string.IsNullOrWhiteSpace(variantName))
            {
                return;
            }

            var matches = candidates
                .Where(candidate => string.Equals(candidate.name, variantName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0)
            {
                return;
            }

            var preferredMount = string.Equals(variantName, "Bows", StringComparison.OrdinalIgnoreCase)
                ? "weapon_l"
                : "weapon_r";
            var selected = string.Equals(NormalizeSlot(slot), "weapon", StringComparison.OrdinalIgnoreCase)
                ? matches.FirstOrDefault(candidate => HasAncestorNamed(candidate, preferredMount)) ?? matches[0]
                : matches[0];
            selected.gameObject.SetActive(true);
        }

        private static bool StartsWithNumberedVariant(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                   value.Length > prefix.Length &&
                   char.IsDigit(value[prefix.Length]);
        }

        private static bool HasAncestorNamed(Transform transform, string name)
        {
            for (var current = transform.parent; current != null; current = current.parent)
            {
                if (string.Equals(current.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeSlot(string slot)
        {
            return slot?.Trim().ToLowerInvariant() switch
            {
                "mainhand" => "weapon",
                "main_hand" => "weapon",
                "main-hand" => "weapon",
                var normalized => normalized ?? string.Empty
            };
        }
    }
}
