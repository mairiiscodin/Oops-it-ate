using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Interaction;
using OopsItAte.Levels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OopsItAte.Editor
{
    [CustomEditor(typeof(LevelSceneSettings))]
    public sealed class LevelSceneSettingsEditor : UnityEditor.Editor
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string TileThemePath = "Assets/Assets/Grid Tile Theme.asset";
        private static readonly char[] Palette = { '.', '#', ' ', '_', 'S', 'K', 'P', 'B', '1', '2', '3', '4' };

        private char selectedTile = '.';
        private Vector2 mapScroll;
        private int requestedWidth;
        private int requestedHeight;

        private void OnEnable()
        {
            var settings = (LevelSceneSettings)target;
            EnsureTileTheme(settings);
            requestedWidth = Mathf.Max(1, settings.grid.width);
            requestedHeight = Mathf.Max(1, settings.grid.height);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "tileMap", "doorLinks");
            serializedObject.ApplyModifiedProperties();

            LevelSceneSettings settings = (LevelSceneSettings)target;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Quick Level Painter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Chọn ký hiệu rồi click/kéo trên bảng. S Player, K Kitchen, P Pet, B Box, 1-9 Door.",
                MessageType.Info);

            DrawSizeControls(settings);
            DrawPalette();
            DrawMap(settings);
            DrawDoorLinks(settings);
            DrawActions(settings);
            DrawValidation(settings);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Raw Map", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string rawMap = EditorGUILayout.TextArea(settings.tileMap, GUILayout.MinHeight(90f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "Edit Level Map");
                settings.tileMap = rawMap;
                settings.TryReadTileMap(out _, out _, out _);
                EditorUtility.SetDirty(settings);
                SceneView.RepaintAll();
            }
        }

        private void DrawSizeControls(LevelSceneSettings settings)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                requestedWidth = EditorGUILayout.IntField("Size", requestedWidth);
                GUILayout.Label("x", GUILayout.Width(12f));
                requestedHeight = EditorGUILayout.IntField(requestedHeight, GUILayout.Width(48f));

                if (GUILayout.Button("Resize", GUILayout.Width(62f)))
                {
                    ResizeMap(settings, Mathf.Max(1, requestedWidth), Mathf.Max(1, requestedHeight));
                }

                if (GUILayout.Button("New Room", GUILayout.Width(76f)))
                {
                    CreateRoom(settings, Mathf.Max(3, requestedWidth), Mathf.Max(3, requestedHeight));
                }
            }

        }

        private void DrawPalette()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Brush", GUILayout.Width(42f));
                foreach (char tile in Palette)
                {
                    Color previous = GUI.backgroundColor;
                    if (tile == selectedTile)
                    {
                        GUI.backgroundColor = new Color(0.25f, 0.75f, 1f);
                    }

                    if (GUILayout.Button(GetTileLabel(tile), GUILayout.Width(27f), GUILayout.Height(24f)))
                    {
                        selectedTile = tile;
                    }

                    GUI.backgroundColor = previous;
                }
            }
        }

        private void DrawMap(LevelSceneSettings settings)
        {
            string[] rows = GetFixedRows(settings, out int width, out int height);
            if (rows.Length == 0)
            {
                EditorGUILayout.HelpBox("Bấm New Room để tạo map đầu tiên.", MessageType.Warning);
                return;
            }

            mapScroll = EditorGUILayout.BeginScrollView(
                mapScroll,
                true,
                true,
                GUILayout.MinHeight(Mathf.Min(460f, height * 25f + 18f)));

            for (int row = 0; row < height; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int x = 0; x < width; x++)
                    {
                        char tile = rows[row][x];
                        Color previous = GUI.backgroundColor;
                        GUI.backgroundColor = GetTileColor(tile);
                        bool clicked = GUILayout.Button(
                            GetTileLabel(tile), GUILayout.Width(24f), GUILayout.Height(24f));
                        Rect cellRect = GUILayoutUtility.GetLastRect();
                        bool dragged = Event.current.type == EventType.MouseDrag
                            && Event.current.button == 0
                            && cellRect.Contains(Event.current.mousePosition);
                        if (clicked || dragged)
                        {
                            if (dragged) Event.current.Use();
                            SetCell(settings, rows, row, x, selectedTile);
                            GUIUtility.ExitGUI();
                        }
                        GUI.backgroundColor = previous;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDoorLinks(LevelSceneSettings settings)
        {
            SortedSet<char> markers = FindDoorMarkers(settings.GetRows());
            if (markers.Count == 0)
            {
                return;
            }

            EnsureDoorLinks(settings, markers);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Door Targets", EditorStyles.boldLabel);

            foreach (char marker in markers)
            {
                LevelSceneSettings.DoorLink link = settings.doorLinks.First(item => item.Marker == marker);
                SceneAsset current = string.IsNullOrWhiteSpace(link.targetScenePath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<SceneAsset>(link.targetScenePath);

                EditorGUILayout.LabelField($"Door {marker}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                SceneAsset selected = (SceneAsset)EditorGUILayout.ObjectField(
                    "Target Scene", current, typeof(SceneAsset), false);
                DoorDirection direction = (DoorDirection)EditorGUILayout.EnumPopup(
                    "Open Direction", link.direction);
                string doorId = EditorGUILayout.TextField(
                    "Door ID",
                    string.IsNullOrWhiteSpace(link.doorId)
                        ? $"{settings.RoomId}_Door_{marker}"
                        : link.doorId);
                string targetDoorId = EditorGUILayout.TextField(
                    "Target Door ID", link.targetDoorId ?? string.Empty);
                bool startsLocked = EditorGUILayout.Toggle("Starts Locked", link.startsLocked);
                bool requiresFood = startsLocked
                    && EditorGUILayout.Toggle("Food Unlocks", link.requiresFood);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(settings, "Configure Door");
                    link.targetScenePath = selected == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(selected);
                    link.direction = direction;
                    link.doorId = doorId.Trim();
                    link.targetDoorId = targetDoorId.Trim();
                    link.startsLocked = startsLocked;
                    link.requiresFood = requiresFood;
                    EditorUtility.SetDirty(settings);
                }
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawActions(LevelSceneSettings settings)
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Objects From Map", GUILayout.Height(30f)))
                {
                    SyncSceneObjects(settings);
                }

                if (GUILayout.Button("Add Door Scenes To Build", GUILayout.Height(30f)))
                {
                    AddDoorScenesToBuild(settings);
                }
            }

            if (GUILayout.Button("Setup Pet/Kitchen Visuals Like Scene 1", GUILayout.Height(26f)))
            {
                LevelVisualAutoSetup.ApplyFromSceneOne(settings);
            }
        }

        private static void DrawValidation(LevelSceneSettings settings)
        {
            List<string> issues = Validate(settings);
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Map hợp lệ và sẵn sàng Play.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", issues.Select(issue => "• " + issue)), MessageType.Warning);
        }

        private static void CreateRoom(LevelSceneSettings settings, int width, int height)
        {
            char[][] cells = new char[height][];
            for (int row = 0; row < height; row++)
            {
                cells[row] = new char[width];
                for (int x = 0; x < width; x++)
                {
                    bool border = row == 0 || row == height - 1 || x == 0 || x == width - 1;
                    cells[row][x] = border ? '#' : '.';
                }
            }

            if (width > 2 && height > 2) cells[height - 2][1] = 'S';
            if (width > 3 && height > 2) cells[height - 2][2] = 'K';
            if (width > 4 && height > 2) cells[height - 2][width - 2] = 'P';
            ApplyMap(settings, cells.Select(row => new string(row)).ToArray(), "Create Level Room");
        }

        private static void ResizeMap(LevelSceneSettings settings, int width, int height)
        {
            string[] oldRows = GetFixedRows(settings, out int oldWidth, out int oldHeight);
            char[][] cells = new char[height][];
            for (int row = 0; row < height; row++)
            {
                cells[row] = Enumerable.Repeat(' ', width).ToArray();
            }

            for (int oldRow = 0; oldRow < oldHeight; oldRow++)
            {
                int y = oldHeight - 1 - oldRow;
                if (y >= height) continue;
                int newRow = height - 1 - y;
                for (int x = 0; x < Mathf.Min(oldWidth, width); x++)
                {
                    cells[newRow][x] = oldRows[oldRow][x];
                }
            }

            ApplyMap(settings, cells.Select(row => new string(row)).ToArray(), "Resize Level Map");
        }

        private static void SetCell(LevelSceneSettings settings, string[] rows, int row, int x, char tile)
        {
            char[] cells = rows[row].ToCharArray();
            cells[x] = tile;
            rows[row] = new string(cells);
            ApplyMap(settings, rows, "Paint Level Cell");
        }

        private static void ApplyMap(LevelSceneSettings settings, string[] rows, string undoName)
        {
            Undo.RecordObject(settings, undoName);
            EnsureTileTheme(settings);
            settings.tileMap = string.Join("\n", rows);
            settings.TryReadTileMap(out _, out _, out _);
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            SceneView.RepaintAll();
        }

        private static void EnsureTileTheme(LevelSceneSettings settings)
        {
            if (settings == null || settings.tileTheme != null)
            {
                return;
            }

            GridTileTheme theme = AssetDatabase.LoadAssetAtPath<GridTileTheme>(TileThemePath);
            if (theme == null)
            {
                return;
            }

            settings.tileTheme = theme;
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
        }

        private static string[] GetFixedRows(LevelSceneSettings settings, out int width, out int height)
        {
            string[] source = settings.GetRows();
            height = source.Length;
            width = source.Length == 0 ? 0 : source.Max(row => row.Length);
            string[] rows = new string[height];
            for (int i = 0; i < height; i++)
            {
                rows[i] = source[i].PadRight(width, ' ');
            }
            return rows;
        }

        private static void EnsureDoorLinks(LevelSceneSettings settings, IEnumerable<char> markers)
        {
            List<LevelSceneSettings.DoorLink> links = (settings.doorLinks ?? Array.Empty<LevelSceneSettings.DoorLink>())
                .Where(link => link != null)
                .ToList();
            bool changed = false;
            foreach (char marker in markers)
            {
                LevelSceneSettings.DoorLink existing = links.FirstOrDefault(link => link.Marker == marker);
                if (existing == null)
                {
                    existing = new LevelSceneSettings.DoorLink { marker = marker - '0' };
                    links.Add(existing);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.doorId))
                {
                    existing.doorId = $"{settings.RoomId}_Door_{marker}";
                    changed = true;
                }
            }

            if (!changed) return;
            Undo.RecordObject(settings, "Create Door Links");
            settings.doorLinks = links.OrderBy(link => link.marker).ToArray();
            EditorUtility.SetDirty(settings);
        }

        private static SortedSet<char> FindDoorMarkers(IEnumerable<string> rows)
        {
            var result = new SortedSet<char>();
            foreach (string row in rows)
            {
                foreach (char tile in row)
                {
                    if (tile >= '1' && tile <= '9') result.Add(tile);
                }
            }
            return result;
        }

        private static void SyncSceneObjects(LevelSceneSettings settings)
        {
            string[] rows = GetFixedRows(settings, out int width, out int height);
            if (rows.Length == 0)
            {
                Debug.LogWarning("Level map is empty.", settings);
                return;
            }

            settings.TryReadTileMap(out _, out _, out _);
            var markers = new Dictionary<char, List<GridPosition>>();
            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    char tile = rows[row][x];
                    if (tile == 'S' || tile == 'K' || tile == 'P' || tile == 'B'
                        || (tile >= '1' && tile <= '9'))
                    {
                        if (!markers.TryGetValue(tile, out List<GridPosition> positions))
                        {
                            positions = new List<GridPosition>();
                            markers[tile] = positions;
                        }
                        positions.Add(new GridPosition(x, y));
                    }
                }
            }

            SyncSingle<GridMover>(settings, markers, 'S', "Player", CreatePlayer);
            SyncSingle<KitchenStation>(settings, markers, 'K', "Kitchen", CreateKitchen);
            SyncMany<PetBody>(settings, markers.TryGetValue('P', out var pets) ? pets : null, 'P', "Pet", CreatePet);
            SyncMany<PushableBox>(settings, markers.TryGetValue('B', out var boxes) ? boxes : null, 'B', "PushableBox", CreateBox);
            SyncDoors(settings, markers);
            LevelVisualAutoSetup.ApplyFromSceneOne(settings);

            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            EditorSceneManager.SaveScene(settings.gameObject.scene);
            Selection.activeGameObject = settings.gameObject;
            Debug.Log(
                $"Synced and saved scene objects from map '{settings.gameObject.scene.name}'.",
                settings);
        }

        private static void SyncSingle<T>(
            LevelSceneSettings settings,
            IReadOnlyDictionary<char, List<GridPosition>> markers,
            char marker,
            string objectName,
            Func<GameObject> factory) where T : Component
        {
            T[] sceneObjects = UnityEngine.Object.FindObjectsByType<T>()
                .Where(item => item.gameObject.scene == settings.gameObject.scene)
                .ToArray();
            if (!markers.TryGetValue(marker, out List<GridPosition> positions) || positions.Count == 0)
            {
                foreach (T item in sceneObjects)
                {
                    LevelMapObject mapObject = item.GetComponent<LevelMapObject>();
                    if (mapObject != null && mapObject.Marker == marker)
                    {
                        Undo.DestroyObjectImmediate(item.gameObject);
                    }
                }
                return;
            }

            T instance = sceneObjects.FirstOrDefault();
            if (instance == null)
            {
                GameObject created = factory();
                created.name = objectName;
                created.transform.SetParent(settings.transform);
                Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
                instance = created.GetComponent<T>();
            }
            MarkMapObject(instance.gameObject, marker);
            MoveToGrid(settings, instance.transform, positions[0]);
        }

        private static void SyncMany<T>(
            LevelSceneSettings settings,
            IReadOnlyList<GridPosition> positions,
            char marker,
            string objectName,
            Func<GameObject> factory) where T : Component
        {
            positions = positions ?? Array.Empty<GridPosition>();
            T[] objects = UnityEngine.Object.FindObjectsByType<T>()
                .Where(item => item.gameObject.scene == settings.gameObject.scene)
                .OrderBy(item => item.GetComponent<LevelMapObject>() == null ? 1 : 0)
                .ToArray();
            int i;
            for (i = 0; i < positions.Count; i++)
            {
                T instance;
                if (i < objects.Length)
                {
                    instance = objects[i];
                }
                else
                {
                    GameObject created = factory();
                    created.name = $"{objectName} ({i + 1})";
                    created.transform.SetParent(settings.transform);
                    Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
                    instance = created.GetComponent<T>();
                }
                MarkMapObject(instance.gameObject, marker);
                MoveToGrid(settings, instance.transform, positions[i]);
            }

            for (; i < objects.Length; i++)
            {
                LevelMapObject mapObject = objects[i].GetComponent<LevelMapObject>();
                if (mapObject != null && mapObject.Marker == marker)
                {
                    Undo.DestroyObjectImmediate(objects[i].gameObject);
                }
            }
        }

        private static void SyncDoors(
            LevelSceneSettings settings,
            IReadOnlyDictionary<char, List<GridPosition>> markers)
        {
            var used = new HashSet<DoorExit>();
            DoorExit[] existing = UnityEngine.Object.FindObjectsByType<DoorExit>()
                .Where(item => item.gameObject.scene == settings.gameObject.scene)
                .ToArray();
            for (char marker = '1'; marker <= '9'; marker++)
            {
                if (!markers.TryGetValue(marker, out List<GridPosition> positions) || positions.Count == 0) continue;
                DoorExit door = existing.FirstOrDefault(item => !used.Contains(item) && item.name == $"Door {marker}")
                    ?? existing.FirstOrDefault(item => !used.Contains(item));
                if (door == null)
                {
                    GameObject created = CreateDoor();
                    created.transform.SetParent(settings.transform);
                    Undo.RegisterCreatedObjectUndo(created, "Create Door");
                    door = created.GetComponent<DoorExit>();
                }

                used.Add(door);
                MarkMapObject(door.gameObject, marker);
                Undo.RecordObject(door.gameObject, "Configure Door");
                door.gameObject.name = $"Door {marker}";
                MoveToGrid(settings, door.transform, positions[0]);
                Undo.RecordObject(door, "Configure Door");
                LevelSceneSettings.DoorLink link = settings.doorLinks
                    .First(item => item.Marker == marker);
                DoorDirection direction = link.direction;
                if (direction == DoorDirection.Auto)
                {
                    direction = InferDoorDirection(settings.GetRows(), positions[0]);
                }
                door.SetOpeningDirection(direction);
                string localDoorId = string.IsNullOrWhiteSpace(link.doorId)
                    ? $"{settings.RoomId}_Door_{marker}"
                    : link.doorId.Trim();
                door.ConfigureAdventure(
                    settings.RoomId,
                    localDoorId,
                    link.targetDoorId,
                    link.startsLocked,
                    link.requiresFood);
                door.SetTargetScene(link.TargetSceneName);
                EditorUtility.SetDirty(door);
            }

            foreach (DoorExit door in existing)
            {
                LevelMapObject mapObject = door.GetComponent<LevelMapObject>();
                if (!used.Contains(door) && mapObject != null && char.IsDigit(mapObject.Marker))
                {
                    Undo.DestroyObjectImmediate(door.gameObject);
                }
            }
        }

        private static void MarkMapObject(GameObject gameObject, char marker)
        {
            LevelMapObject mapObject = gameObject.GetComponent<LevelMapObject>();
            if (mapObject == null)
            {
                mapObject = Undo.AddComponent<LevelMapObject>(gameObject);
            }
            Undo.RecordObject(mapObject, "Mark Map Object");
            mapObject.Configure(marker);
            EditorUtility.SetDirty(mapObject);
        }

        private static void MoveToGrid(LevelSceneSettings settings, Transform targetTransform, GridPosition position)
        {
            Undo.RecordObject(targetTransform, "Move Level Object");
            targetTransform.position = settings.grid.GridToWorld(position);
            EditorUtility.SetDirty(targetTransform);
        }

        private static GameObject CreatePlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null)
            {
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            var result = new GameObject("Player");
            result.AddComponent<GridMover>();
            return result;
        }

        private static GameObject CreateKitchen() => CreateLevelObject<KitchenStation>("Kitchen");
        private static GameObject CreatePet()
        {
            var result = new GameObject("Pet");
            result.AddComponent<PetBody>();
            return result;
        }
        private static GameObject CreateBox() => CreateLevelObject<PushableBox>("PushableBox");
        private static GameObject CreateDoor() => CreateLevelObject<DoorExit>("Door");

        private static DoorDirection InferDoorDirection(IReadOnlyList<string> rows, GridPosition door)
        {
            int height = rows.Count;
            var candidates = new[]
            {
                new { Direction = DoorDirection.Up, Inside = new GridPosition(door.X, door.Y - 1) },
                new { Direction = DoorDirection.Down, Inside = new GridPosition(door.X, door.Y + 1) },
                new { Direction = DoorDirection.Left, Inside = new GridPosition(door.X + 1, door.Y) },
                new { Direction = DoorDirection.Right, Inside = new GridPosition(door.X - 1, door.Y) }
            };

            foreach (var candidate in candidates)
            {
                int row = height - 1 - candidate.Inside.Y;
                if (row < 0 || row >= height || candidate.Inside.X < 0
                    || candidate.Inside.X >= rows[row].Length)
                {
                    continue;
                }

                char tile = rows[row][candidate.Inside.X];
                if (tile == '.' || tile == '#' || tile == 'S' || tile == 'K'
                    || tile == 'P' || tile == 'B')
                {
                    return candidate.Direction;
                }
            }

            return DoorDirection.Auto;
        }

        private static GameObject CreateLevelObject<T>(string objectName) where T : Component
        {
            var result = new GameObject(objectName);
            result.name = objectName;
            result.AddComponent<T>();
            return result;
        }

        private static void AddDoorScenesToBuild(LevelSceneSettings settings)
        {
            var paths = new HashSet<string>(EditorBuildSettings.scenes.Select(scene => scene.path));
            foreach (LevelSceneSettings.DoorLink link in settings.doorLinks ?? Array.Empty<LevelSceneSettings.DoorLink>())
            {
                if (link != null && !string.IsNullOrWhiteSpace(link.targetScenePath)) paths.Add(link.targetScenePath);
            }
            string currentPath = settings.gameObject.scene.path;
            if (!string.IsNullOrWhiteSpace(currentPath)) paths.Add(currentPath);
            EditorBuildSettings.scenes = paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
            Debug.Log("Added the current level and all door targets to Build Profiles > Scene List.", settings);
        }

        private static List<string> Validate(LevelSceneSettings settings)
        {
            string[] rows = settings.GetRows();
            var issues = new List<string>();
            ValidateUnique(rows, 'S', "Player", issues);
            ValidateUnique(rows, 'K', "Kitchen", issues);
            ValidateAtLeastOne(rows, 'P', "Pet", issues);

            SortedSet<char> doors = FindDoorMarkers(rows);
            var usedDoorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (char marker in doors)
            {
                int count = rows.Sum(row => row.Count(tile => tile == marker));
                if (count > 1)
                {
                    issues.Add($"Door {marker} xuất hiện {count} lần; mỗi số chỉ nên dùng 1 lần.");
                }
                if (!settings.TryGetDoorTarget(marker, out _))
                {
                    issues.Add($"Door {marker} chưa chọn target scene.");
                }
                if (settings.TryGetDoorLink(marker, out LevelSceneSettings.DoorLink link))
                {
                    if (string.IsNullOrWhiteSpace(link.doorId))
                    {
                        issues.Add($"Door {marker} chưa có Door ID; Sync sẽ tự tạo ID.");
                    }
                    else if (!usedDoorIds.Add(link.doorId.Trim()))
                    {
                        issues.Add($"Door ID '{link.doorId}' đang bị trùng trong room.");
                    }
                    if (string.IsNullOrWhiteSpace(link.targetDoorId))
                    {
                        issues.Add($"Door {marker} chưa có Target Door ID; sẽ dùng cửa quay về kiểu cũ.");
                    }
                }
            }

            if (settings.GetComponent<SceneLevelBuilder>() == null)
            {
                issues.Add("Level object thiếu SceneLevelBuilder.");
            }
            return issues;
        }

        private static void ValidateUnique(IEnumerable<string> rows, char marker, string label, ICollection<string> issues)
        {
            int count = rows.Sum(row => row.Count(tile => tile == marker));
            if (count == 0) issues.Add($"Map thiếu {label} ({marker}).");
            else if (count > 1) issues.Add($"Map có {count} {label}; chỉ nên có 1.");
        }

        private static void ValidateAtLeastOne(IEnumerable<string> rows, char marker, string label, ICollection<string> issues)
        {
            int count = rows.Sum(row => row.Count(tile => tile == marker));
            if (count == 0) issues.Add($"Map thiếu {label} ({marker}).");
        }

        private static string GetTileLabel(char tile)
        {
            if (tile == ' ') return "×";
            if (tile == '.') return "·";
            if (tile == '_') return "_";
            return tile.ToString();
        }

        private static Color GetTileColor(char tile)
        {
            switch (tile)
            {
                case '#': return new Color(0.45f, 0.45f, 0.45f);
                case 'S': return new Color(0.2f, 0.65f, 1f);
                case 'K': return new Color(1f, 0.65f, 0.15f);
                case 'P': return new Color(0.9f, 0.4f, 0.75f);
                case 'B': return new Color(0.62f, 0.36f, 0.16f);
                case '_': return new Color(0.8f, 0.25f, 0.25f);
                case ' ': return new Color(0.18f, 0.18f, 0.18f);
                default: return char.IsDigit(tile) ? new Color(0.9f, 0.15f, 0.15f) : Color.white;
            }
        }
    }
}
