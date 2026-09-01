using OopsItAte.Grid;
using UnityEditor;
using UnityEngine;

namespace OopsItAte.Editor
{
    [InitializeOnLoad]
    public static class GridTileThemeAutoSetup
    {
        private const string ThemePath = "Assets/Assets/Grid Tile Theme.asset";

        static GridTileThemeAutoSetup()
        {
            EditorApplication.delayCall += ApplyCurrentTileAssets;
        }

        internal static void ApplyCurrentTileAssets()
        {
            GridTileTheme theme = AssetDatabase.LoadAssetAtPath<GridTileTheme>(ThemePath);
            if (theme == null)
            {
                Debug.LogWarning($"Grid tile theme was not found at '{ThemePath}'.");
                return;
            }

            Sprite floor = FindSprite("Assets/Assets/FloorTile.aseprite", "FloorTile");
            Sprite doorUp = FindSprite("Assets/Assets/DoorUp.aseprite", "DoorUp");
            Sprite doorDown = FindSprite("Assets/Assets/DoorDown.aseprite", "DoorDown");
            Sprite doorLeft = FindSprite("Assets/Assets/DoorLeft.aseprite", "DoorLeft");
            Sprite doorRight = FindSprite("Assets/Assets/DoorRight.aseprite", "DoorRight");

            if (floor == null || doorUp == null || doorDown == null
                || doorLeft == null || doorRight == null)
            {
                Debug.LogWarning(
                    "Grid tile theme setup is waiting for floor and door assets to finish importing.");
                return;
            }

            bool changed = false;
            changed |= AssignIfMissing(ref theme.floor, floor);
            changed |= AssignIfMissing(ref theme.doorUp, doorUp);
            changed |= AssignIfMissing(ref theme.doorDown, doorDown);
            changed |= AssignIfMissing(ref theme.doorLeft, doorLeft);
            changed |= AssignIfMissing(ref theme.doorRight, doorRight);

            if (!changed)
            {
                return;
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("Grid tile theme automatically linked the current Aseprite floor and door assets.");
        }

        private static bool AssignIfMissing(ref Sprite target, Sprite value)
        {
            if (target != null || value == null)
            {
                return false;
            }

            target = value;
            return true;
        }

        private static Sprite FindSprite(string path, string spriteName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Sprite firstSprite = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is Sprite sprite))
                {
                    continue;
                }

                if (firstSprite == null)
                {
                    firstSprite = sprite;
                }

                if (sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return firstSprite != null
                ? firstSprite
                : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    internal sealed class GridTileThemeAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (importedAssets[i].EndsWith(".aseprite")
                    || importedAssets[i].EndsWith("Grid Tile Theme.asset"))
                {
                    EditorApplication.delayCall += GridTileThemeAutoSetup.ApplyCurrentTileAssets;
                    return;
                }
            }
        }
    }
}
