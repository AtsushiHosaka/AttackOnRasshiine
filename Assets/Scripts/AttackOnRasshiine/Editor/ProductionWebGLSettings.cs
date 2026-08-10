using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    /// <summary>
    /// The release build must not depend on whichever Web settings happen to be
    /// selected in a developer's Editor. Keep the browser memory/size contract in
    /// one place and apply it immediately before every production WebGL build.
    /// </summary>
    public static class ProductionWebGLSettings
    {
        public const int InitialMemorySizeMb = 32;
        // The complete production flow stabilizes below 192 MB in browser
        // soak tests. Keep enough headroom for transient allocations without
        // allowing a runaway heap to grow toward Unity's desktop-oriented
        // 2 GB default and take down a memory-constrained browser tab.
        public const int MaximumMemorySizeMb = 512;
        public const int GeometricMemoryGrowthCapMb = 96;
        public const float GeometricMemoryGrowthStep = 0.2f;
        public const int ProductionUiMaxTextureSize = 1024;
        public const int ProductionUiCompressionQuality = 100;

        public static readonly string[] ProductionUiTexturePaths =
        {
            "Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Danger.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Primary.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_Button_Secondary.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_HexBadge_Frame.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_Input_Field.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_Panel_LogFrame.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_Panel_RaidFrame.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_ProgressFill_Cyan.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_ProgressFill_Magenta.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_Progress_Frame.png",
            "Assets/Art/DesignSystem/Textures/UI/T_UI_StatCard.png"
        };

        [MenuItem("AttackOnRasshiine/Apply Production WebGL Settings")]
        public static void Apply()
        {
            var target = NamedBuildTarget.WebGL;

            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.stripUnusedMeshComponents = true;

            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.wasm2023 = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.initialMemorySize = InitialMemorySizeMb;
            PlayerSettings.WebGL.maximumMemorySize = MaximumMemorySizeMb;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.geometricMemoryGrowthStep = GeometricMemoryGrowthStep;
            PlayerSettings.WebGL.memoryGeometricGrowthCap = GeometricMemoryGrowthCapMb;

            // The application intentionally throws and catches validation,
            // transport and JSON exceptions. "None" would abort the WebGL
            // player on those normal production error paths.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            UnityEditor.WebGL.UserBuildSettings.codeOptimization =
                UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;

            ConfigureProductionUiTextures();
            AssetDatabase.SaveAssets();
            ValidateOrThrow();
        }

        public static void ConfigureProductionUiTextures()
        {
            foreach (var assetPath in ProductionUiTexturePaths)
            {
                ConfigureProductionUiTexture(assetPath);
            }
        }

        public static IReadOnlyList<string> FindViolations()
        {
            var violations = new List<string>();
            var target = NamedBuildTarget.WebGL;

            AddIf(violations,
                PlayerSettings.GetIl2CppCodeGeneration(target) != Il2CppCodeGeneration.OptimizeSize,
                "IL2CPP code generation must optimize for size.");
            AddIf(violations,
                PlayerSettings.GetManagedStrippingLevel(target) != ManagedStrippingLevel.High,
                "Managed stripping level must be High.");
            AddIf(violations, !PlayerSettings.stripEngineCode, "Unused engine code stripping must be enabled.");
            AddIf(violations, !PlayerSettings.stripUnusedMeshComponents, "Unused mesh component stripping must be enabled.");
            AddIf(violations, !PlayerSettings.WebGL.dataCaching, "WebGL data caching must be enabled.");
            AddIf(violations,
                PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Brotli,
                "WebGL output must use Brotli compression.");
            AddIf(violations,
                PlayerSettings.WebGL.debugSymbolMode != WebGLDebugSymbolMode.Off,
                "Release WebGL debug symbols must be disabled.");
            AddIf(violations, !PlayerSettings.WebGL.wasm2023, "WebAssembly 2023 must be enabled.");
            AddIf(violations, PlayerSettings.WebGL.threadsSupport, "WebGL threads must remain disabled for broad hosting compatibility.");
            AddIf(violations, PlayerSettings.WebGL.decompressionFallback, "WebGL decompression fallback must remain disabled.");
            AddIf(violations,
                PlayerSettings.WebGL.exceptionSupport != WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                "WebGL must retain explicitly-thrown exception support used by production error handling.");
            AddIf(violations,
                PlayerSettings.WebGL.initialMemorySize != InitialMemorySizeMb,
                $"WebGL initial memory must be {InitialMemorySizeMb} MB.");
            AddIf(violations,
                PlayerSettings.WebGL.maximumMemorySize != MaximumMemorySizeMb,
                $"WebGL maximum memory must be {MaximumMemorySizeMb} MB.");
            AddIf(violations,
                PlayerSettings.WebGL.memoryGrowthMode != WebGLMemoryGrowthMode.Geometric,
                "WebGL memory growth must be geometric.");
            AddIf(violations,
                Math.Abs(PlayerSettings.WebGL.geometricMemoryGrowthStep - GeometricMemoryGrowthStep) > 0.0001f,
                $"WebGL geometric memory growth step must be {GeometricMemoryGrowthStep:0.0}.");
            AddIf(violations,
                PlayerSettings.WebGL.memoryGeometricGrowthCap != GeometricMemoryGrowthCapMb,
                $"WebGL memory growth cap must be {GeometricMemoryGrowthCapMb} MB.");
            AddIf(violations,
                UnityEditor.WebGL.UserBuildSettings.codeOptimization != UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO,
                "WebAssembly code optimization must use DiskSizeLTO.");

            foreach (var assetPath in ProductionUiTexturePaths)
            {
                ValidateProductionUiTexture(assetPath, violations);
            }

            return violations;
        }

        public static void ValidateOrThrow()
        {
            var violations = FindViolations();
            if (violations.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Production WebGL settings are invalid:\n- " + string.Join("\n- ", violations));
        }

        private static void ConfigureProductionUiTexture(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Production UI texture is missing or invalid: {assetPath}");
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            var webGl = importer.GetPlatformTextureSettings("WebGL");
            var changed = importer.textureType != TextureImporterType.Sprite ||
                          importer.spriteImportMode != SpriteImportMode.Single ||
                          importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                          !importer.alphaIsTransparency ||
                          importer.mipmapEnabled ||
                          importer.wrapMode != TextureWrapMode.Clamp ||
                          importer.filterMode != FilterMode.Bilinear ||
                          importer.anisoLevel != 0 ||
                          importer.maxTextureSize != ProductionUiMaxTextureSize ||
                          importer.textureCompression != TextureImporterCompression.CompressedHQ ||
                          importer.compressionQuality != ProductionUiCompressionQuality ||
                          importer.isReadable ||
                          !importer.sRGBTexture ||
                          textureSettings.spriteGenerateFallbackPhysicsShape ||
                          !webGl.overridden ||
                          webGl.maxTextureSize != ProductionUiMaxTextureSize ||
                          webGl.format != TextureImporterFormat.Automatic ||
                          webGl.textureCompression != TextureImporterCompression.CompressedHQ ||
                          webGl.compressionQuality != ProductionUiCompressionQuality;

            textureSettings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(textureSettings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.maxTextureSize = ProductionUiMaxTextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = ProductionUiCompressionQuality;
            importer.isReadable = false;
            importer.sRGBTexture = true;

            webGl.name = "WebGL";
            webGl.overridden = true;
            webGl.maxTextureSize = ProductionUiMaxTextureSize;
            webGl.format = TextureImporterFormat.Automatic;
            webGl.textureCompression = TextureImporterCompression.CompressedHQ;
            webGl.compressionQuality = ProductionUiCompressionQuality;
            importer.SetPlatformTextureSettings(webGl);

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ValidateProductionUiTexture(string assetPath, ICollection<string> violations)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                violations.Add($"Production UI texture is missing or invalid: {assetPath}");
                return;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            var webGl = importer.GetPlatformTextureSettings("WebGL");
            AddIf(violations,
                importer.mipmapEnabled || importer.isReadable || textureSettings.spriteGenerateFallbackPhysicsShape,
                $"{assetPath} must not keep mipmaps, CPU-readable pixels or a physics shape.");
            AddIf(violations,
                importer.maxTextureSize > ProductionUiMaxTextureSize ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ,
                $"{assetPath} must use high-quality compression at no more than {ProductionUiMaxTextureSize}px.");
            AddIf(violations,
                !webGl.overridden ||
                webGl.maxTextureSize > ProductionUiMaxTextureSize ||
                webGl.format != TextureImporterFormat.Automatic ||
                webGl.textureCompression != TextureImporterCompression.CompressedHQ,
                $"{assetPath} must have the production WebGL texture override.");
        }

        private static void AddIf(ICollection<string> violations, bool condition, string message)
        {
            if (condition)
            {
                violations.Add(message);
            }
        }
    }
}
