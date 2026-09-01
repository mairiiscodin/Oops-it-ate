using System;
using System.Linq;
using OopsItAte.Levels;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace OopsItAte.Editor
{
    public static class RoomCompletionAnimationAutoSetup
    {
        private const string SourcePath = "Assets/Assets/OopsIAte.aseprite";
        private const string ResourcesFolder = "Assets/Resources";
        private const string DataPath = ResourcesFolder + "/OopsItAteCompletionFrames.asset";

        [InitializeOnLoadMethod]
        private static void QueueRefresh()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.delayCall += Refresh;
        }

        [DidReloadScripts]
        private static void RefreshAfterScriptsReload()
        {
            EditorApplication.delayCall += Refresh;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += Refresh;
            }
        }

        [MenuItem("Tools/Oops It Ate/Refresh Room Completion Animation")]
        public static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
                .OfType<Sprite>()
                .OrderBy(sprite => GetFrameNumber(sprite.name))
                .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
            if (frames.Length == 0)
            {
                Debug.LogWarning("Could not create the room completion animation because OopsIAte has no imported frames.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            RoomCompletionAnimationData data =
                AssetDatabase.LoadAssetAtPath<RoomCompletionAnimationData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RoomCompletionAnimationData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            SerializedObject serializedData = new SerializedObject(data);
            SerializedProperty frameProperty = serializedData.FindProperty("frames");
            bool changed = frameProperty.arraySize != frames.Length;
            frameProperty.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                SerializedProperty element = frameProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != frames[i])
                {
                    element.objectReferenceValue = frames[i];
                    changed = true;
                }
            }

            if (changed)
            {
                serializedData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Debug.Log($"Room completion animation linked {frames.Length} Oops It Ate frames.");
            }
        }

        private static int GetFrameNumber(string spriteName)
        {
            int underscoreIndex = spriteName.LastIndexOf('_');
            return underscoreIndex >= 0
                && int.TryParse(spriteName.Substring(underscoreIndex + 1), out int frameNumber)
                    ? frameNumber
                    : int.MaxValue;
        }
    }
}
