using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttackOnRasshiine.Editor
{
    public sealed class NeonCityBackdropSceneEditModeTests
    {
        private static readonly string[] BurgerFreeScenePaths =
        {
            RasshiineSceneBuilder.PrototypeScenePath,
            RasshiineSceneBuilder.ProductionScenePath,
            "Assets/BackRock-NeonCity/Scenes/Neon City.unity"
        };

        [Test]
        public void ScenesDoNotPlaceBurgerProps()
        {
            foreach (var scenePath in BurgerFreeScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var burgerNames = scene.GetRootGameObjects()
                    .SelectMany(EnumerateSceneObjects)
                    .Select(gameObject => gameObject.name)
                    .Where(name => name.IndexOf("burger", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                CollectionAssert.IsEmpty(burgerNames, $"{scenePath} should not place burger props.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static IEnumerable<GameObject> EnumerateSceneObjects(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
            {
                foreach (var gameObject in EnumerateSceneObjects(child.gameObject))
                {
                    yield return gameObject;
                }
            }
        }
    }
}
