using System;
using System.Collections.Generic;
using System.Linq;
using OopsItAte.Grid;
using OopsItAte.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OopsItAte.Editor
{
    /// <summary>
    /// Builds <see cref="MiniMapLayout"/> from the door graph of every gameplay scene in Build Settings.
    /// Each door's side of the map decides where the connected room is placed.
    /// </summary>
    public static class MiniMapLayoutGenerator
    {
        private const string StartSceneName = "1";
        private const int MaxPlacementAttempts = 16;

        private sealed class RoomScan
        {
            public string SceneName;
            public string RoomId;
            public readonly List<(string targetScene, Vector2Int direction)> Doors =
                new List<(string, Vector2Int)>();
        }

        [MenuItem("Tools/Oops It Ate/Generate Mini Map Layout")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Dictionary<string, RoomScan> scans = ScanBuildScenes();
            if (scans.Count == 0)
            {
                Debug.LogWarning("No gameplay scene with LevelSceneSettings found in Build Settings.");
                return;
            }

            MiniMapLayout layout = LoadOrCreateLayout();
            Undo.RecordObject(layout, "Generate Mini Map Layout");
            BuildLayout(scans, layout);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Selection.activeObject = layout;
            Debug.Log($"Mini map layout generated with {layout.rooms.Length} rooms "
                + $"and {layout.links.Length} links: {MiniMapLayout.AssetPath}", layout);
        }

        private static Dictionary<string, RoomScan> ScanBuildScenes()
        {
            var scans = new Dictionary<string, RoomScan>(StringComparer.OrdinalIgnoreCase);
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled)
                {
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(buildScene.path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                }

                try
                {
                    RoomScan scan = ScanScene(scene);
                    if (scan != null)
                    {
                        scans[scan.SceneName] = scan;
                    }
                }
                finally
                {
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            return scans;
        }

        private static RoomScan ScanScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            LevelSceneSettings settings = roots
                .Select(root => root.GetComponentInChildren<LevelSceneSettings>(true))
                .FirstOrDefault(found => found != null);
            if (settings == null)
            {
                return null;
            }

            var scan = new RoomScan
            {
                SceneName = scene.name,
                RoomId = string.IsNullOrWhiteSpace(settings.roomId) ? scene.name : settings.roomId.Trim()
            };

            // Reads the map bounds; this also syncs grid width/height like the runtime builder does.
            settings.TryReadTileMap(out HashSet<GridPosition> mapCells, out _, out _);
            foreach (DoorExit door in roots.SelectMany(root => root.GetComponentsInChildren<DoorExit>(true)))
            {
                if (string.IsNullOrWhiteSpace(door.TargetSceneName))
                {
                    continue;
                }

                GridPosition doorCell = settings.grid.WorldToGrid(door.transform.position);
                scan.Doors.Add((door.TargetSceneName.Trim(), GetDoorDirection(door, doorCell, mapCells, settings.grid)));
            }

            return scan;
        }

        private static Vector2Int GetDoorDirection(
            DoorExit door,
            GridPosition doorCell,
            HashSet<GridPosition> mapCells,
            GridSettings grid)
        {
            switch (door.OpeningDirection)
            {
                case DoorDirection.Up: return Vector2Int.up;
                case DoorDirection.Down: return Vector2Int.down;
                case DoorDirection.Left: return Vector2Int.left;
                case DoorDirection.Right: return Vector2Int.right;
            }

            float minX = 0f, maxX = grid.width - 1, minY = 0f, maxY = grid.height - 1;
            if (mapCells.Count > 0)
            {
                minX = mapCells.Min(cell => cell.X);
                maxX = mapCells.Max(cell => cell.X);
                minY = mapCells.Min(cell => cell.Y);
                maxY = mapCells.Max(cell => cell.Y);
            }

            // Compare against the map center, normalised by its half size, so doors
            // on the long side of a wide room still resolve to the correct edge.
            float x = (doorCell.X - (minX + maxX) * 0.5f) / Mathf.Max(0.5f, (maxX - minX + 1f) * 0.5f);
            float y = (doorCell.Y - (minY + maxY) * 0.5f) / Mathf.Max(0.5f, (maxY - minY + 1f) * 0.5f);
            if (Mathf.Abs(x) >= Mathf.Abs(y))
            {
                return x >= 0f ? Vector2Int.right : Vector2Int.left;
            }

            return y >= 0f ? Vector2Int.up : Vector2Int.down;
        }

        private static void BuildLayout(Dictionary<string, RoomScan> scans, MiniMapLayout layout)
        {
            var cells = new Dictionary<string, Vector2Int>(StringComparer.OrdinalIgnoreCase);
            var occupied = new HashSet<Vector2Int>();
            var links = new List<MiniMapLayout.Link>();
            var linkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Rooms unreachable from the start scene are laid out as new islands below.
            IEnumerable<string> seeds = scans.ContainsKey(StartSceneName)
                ? new[] { StartSceneName }.Concat(scans.Keys)
                : scans.Keys;
            foreach (string seed in seeds)
            {
                if (cells.ContainsKey(seed))
                {
                    continue;
                }

                Vector2Int seedCell = occupied.Count == 0
                    ? Vector2Int.zero
                    : new Vector2Int(0, occupied.Min(cell => cell.y) - 2);
                cells[seed] = seedCell;
                occupied.Add(seedCell);

                var queue = new Queue<string>();
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    RoomScan room = scans[queue.Dequeue()];
                    foreach ((string targetScene, Vector2Int direction) in room.Doors)
                    {
                        if (!scans.ContainsKey(targetScene))
                        {
                            continue;
                        }

                        AddLink(room.SceneName, targetScene, links, linkKeys);
                        if (cells.ContainsKey(targetScene))
                        {
                            continue;
                        }

                        Vector2Int cell = cells[room.SceneName] + direction;
                        for (int attempt = 0; attempt < MaxPlacementAttempts && occupied.Contains(cell); attempt++)
                        {
                            cell += direction;
                        }

                        cells[targetScene] = cell;
                        occupied.Add(cell);
                        queue.Enqueue(targetScene);
                    }
                }
            }

            layout.rooms = cells
                .Select(pair => new MiniMapLayout.Room { roomId = scans[pair.Key].RoomId, cell = pair.Value })
                .ToArray();
            layout.links = links
                .Select(link => new MiniMapLayout.Link
                {
                    roomA = scans[link.roomA].RoomId,
                    roomB = scans[link.roomB].RoomId
                })
                .ToArray();
        }

        private static void AddLink(
            string sceneA,
            string sceneB,
            List<MiniMapLayout.Link> links,
            HashSet<string> linkKeys)
        {
            string key = string.CompareOrdinal(sceneA, sceneB) < 0
                ? $"{sceneA}|{sceneB}"
                : $"{sceneB}|{sceneA}";
            if (linkKeys.Add(key))
            {
                links.Add(new MiniMapLayout.Link { roomA = sceneA, roomB = sceneB });
            }
        }

        private static MiniMapLayout LoadOrCreateLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<MiniMapLayout>(MiniMapLayout.AssetPath);
            if (layout != null)
            {
                return layout;
            }

            layout = ScriptableObject.CreateInstance<MiniMapLayout>();
            AssetDatabase.CreateAsset(layout, MiniMapLayout.AssetPath);
            return layout;
        }
    }
}
