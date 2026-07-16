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
            Sprite north = FindSprite("Assets/Assets/BorderN.aseprite", "BorderN");
            Sprite otherSides = FindSprite(
                "Assets/Assets/BorderEorSorW.aseprite",
                "BorderEorSorW");

            if (floor == null || north == null || otherSides == null)
            {
                Debug.LogWarning(
                    "Grid tile theme setup is waiting for FloorTile, BorderN and BorderEorSorW to finish importing.");
                return;
            }

            bool changed = false;
            changed |= AssignIfMissing(ref theme.floor, floor);
            changed |= AssignIfMissing(ref theme.borderNorth, north);
            changed |= AssignIfMissing(ref theme.borderEast, otherSides);
            changed |= AssignIfMissing(ref theme.borderSouth, otherSides);
            changed |= AssignIfMissing(ref theme.borderWest, otherSides);

            if (!changed)
            {
                return;
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("Grid tile theme automatically linked the current Aseprite floor and border assets.");
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
