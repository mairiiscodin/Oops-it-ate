using OopsItAte.Actors;
using OopsItAte.Interaction;
using OopsItAte.Levels;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OopsItAte.Editor
{
    internal static class LevelVisualAutoSetup
    {
        private const string VisualSourceScenePath = "Assets/Scenes/1.unity";

        private static readonly string[] PetSpriteFields =
        {
            "bigDogSide",
            "bigDogOpen3ConnectedNorth",
            "bigDogOpen3ConnectedEast",
            "bigDogOpen3ConnectedSouth",
            "bigDogOpen3ConnectedWest",
            "bigDogNE",
            "bigDogES",
            "bigDogWN",
            "bigDogWS",
            "bigDogNES",
            "bigDogNEW",
            "bigDogNSW",
            "bigDogESW",
            "bigDogFace",
            "bigDogFace1x2Vertical",
            "bigDogFace2x1Horizontal"
        };

        internal static void ApplyFromSceneOne(LevelSceneSettings settings)
        {
            if (settings == null || Application.isPlaying)
            {
                return;
            }

            Scene targetScene = settings.gameObject.scene;
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene sourceScene = SceneManager.GetSceneByPath(VisualSourceScenePath);
            bool openedSourceScene = !sourceScene.IsValid() || !sourceScene.isLoaded;

            try
            {
                if (openedSourceScene)
                {
                    sourceScene = EditorSceneManager.OpenScene(
                        VisualSourceScenePath,
                        OpenSceneMode.Additive);
                }

                PetBody sourcePet = FindInScene<PetBody>(sourceScene)
                    .FirstOrDefault(pet => pet.GetComponent<KitchenStation>() == null);
                KitchenStation sourceKitchen = FindInScene<KitchenStation>(sourceScene).FirstOrDefault();

                if (sourcePet == null || sourceKitchen == null)
                {
                    Debug.LogWarning(
                        $"Visual source scene '{VisualSourceScenePath}' needs a Pet and Kitchen.",
                        settings);
                    return;
                }

                PetBody[] targetPets = FindInScene<PetBody>(targetScene)
                    .Where(pet => pet.GetComponent<KitchenStation>() == null)
                    .ToArray();
                for (int i = 0; i < targetPets.Length; i++)
                {
                    ApplyPetVisual(sourcePet, targetPets[i]);
                }

                KitchenStation[] targetKitchens = FindInScene<KitchenStation>(targetScene);
                for (int i = 0; i < targetKitchens.Length; i++)
                {
                    ApplyChildVisual(
                        sourceKitchen.transform.Find("KitchenVisual"),
                        targetKitchens[i].transform,
                        "KitchenVisual");
                }

                EditorSceneManager.MarkSceneDirty(targetScene);
                Debug.Log(
                    $"Applied Scene 1 visuals to {targetPets.Length} pet(s) and {targetKitchens.Length} kitchen(s).",
                    settings);
            }
            finally
            {
                if (openedSourceScene && sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sourceScene, true);
                }

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        private static void ApplyPetVisual(PetBody source, PetBody target)
        {
            SerializedObject sourceObject = new SerializedObject(source);
            SerializedObject targetObject = new SerializedObject(target);
            Undo.RecordObject(target, "Setup Pet Visual");

            for (int i = 0; i < PetSpriteFields.Length; i++)
            {
                SerializedProperty sourceProperty = sourceObject.FindProperty(PetSpriteFields[i]);
                SerializedProperty targetProperty = targetObject.FindProperty(PetSpriteFields[i]);
                if (sourceProperty != null && targetProperty != null)
                {
                    targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                }
            }

            Transform visual = ApplyChildVisual(
                source.transform.Find("PetVisual"),
                target.transform,
                "PetVisual");
            SerializedProperty normalVisual = targetObject.FindProperty("normalVisual");
            if (normalVisual != null && visual != null)
            {
                normalVisual.objectReferenceValue = visual.gameObject;
            }

            targetObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static Transform ApplyChildVisual(
            Transform sourceVisual,
            Transform targetParent,
            string visualName)
        {
            if (sourceVisual == null || targetParent == null)
            {
                return null;
            }

            Transform targetVisual = targetParent.Find(visualName);
            if (targetVisual == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(sourceVisual.gameObject);
                clone.name = visualName;
                clone.transform.SetParent(targetParent, false);
                Undo.RegisterCreatedObjectUndo(clone, $"Create {visualName}");
                targetVisual = clone.transform;
            }
            else if (targetVisual != sourceVisual)
            {
                Undo.RecordObject(targetVisual, $"Setup {visualName}");
                targetVisual.localPosition = sourceVisual.localPosition;
                targetVisual.localRotation = sourceVisual.localRotation;
                targetVisual.localScale = sourceVisual.localScale;

                SpriteRenderer sourceRenderer = sourceVisual.GetComponent<SpriteRenderer>();
                if (sourceRenderer != null)
                {
                    SpriteRenderer targetRenderer = targetVisual.GetComponent<SpriteRenderer>();
                    if (targetRenderer == null)
                    {
                        targetRenderer = Undo.AddComponent<SpriteRenderer>(targetVisual.gameObject);
                    }
                    Undo.RecordObject(targetRenderer, $"Setup {visualName} Renderer");
                    EditorUtility.CopySerialized(sourceRenderer, targetRenderer);
                    EditorUtility.SetDirty(targetRenderer);
                }
            }

            return targetVisual;
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<T>();
            }

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
