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
        private const string PrototypeScenePath = "Assets/Scenes/RasshiineRaidPrototype.unity";

        [Test]
        public void PrototypeSceneDoesNotPlaceBurgerProps()
        {
            var scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            var burgerNames = scene.GetRootGameObjects()
                .SelectMany(EnumerateSceneObjects)
                .Select(gameObject => gameObject.name)
                .Where(name => name.Contains("Burger"))
                .ToList();

            CollectionAssert.IsEmpty(burgerNames);
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
