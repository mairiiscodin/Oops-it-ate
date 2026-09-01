using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Interaction;
using OopsItAte.Levels;
using UnityEditor;
using UnityEngine;

namespace OopsItAte.Editor
{
    public static class SceneLevelMenu
    {
        private const string TileThemePath = "Assets/Assets/Grid Tile Theme.asset";

        [MenuItem("GameObject/Oops It Ate/Level Root", false, 10)]
        public static void CreateLevelRoot()
        {
            var level = new GameObject("Level");
            LevelSceneSettings settings = level.AddComponent<LevelSceneSettings>();
            settings.tileTheme = AssetDatabase.LoadAssetAtPath<GridTileTheme>(TileThemePath);
            level.AddComponent<SceneLevelBuilder>();
            Selection.activeGameObject = level;
        }

        [MenuItem("GameObject/Oops It Ate/Player Start", false, 11)]
        public static void CreatePlayerStart()
        {
            var marker = new GameObject("PlayerStart");
            marker.AddComponent<PlayerStart>();
            Selection.activeGameObject = marker;
        }

        [MenuItem("GameObject/Oops It Ate/Kitchen", false, 12)]
        public static void CreateKitchen()
        {
            var kitchen = new GameObject("Kitchen");
            kitchen.name = "Kitchen";
            kitchen.AddComponent<KitchenStation>();
            Selection.activeGameObject = kitchen;
        }

        [MenuItem("GameObject/Oops It Ate/Pet", false, 13)]
        public static void CreatePet()
        {
            var pet = new GameObject("Pet");
            pet.AddComponent<PetBody>();
            Selection.activeGameObject = pet;
        }

        [MenuItem("GameObject/Oops It Ate/Wall Blocker", false, 14)]
        public static void CreateWallBlocker()
        {
            var wall = new GameObject("WallBlocker");
            wall.name = "WallBlocker";
            wall.AddComponent<GridWall>();

            Selection.activeGameObject = wall;
        }

        [MenuItem("GameObject/Oops It Ate/Pushable Box", false, 15)]
        public static void CreatePushableBox()
        {
            var box = new GameObject("PushableBox");
            box.name = "PushableBox";
            box.AddComponent<PushableBox>();

            Selection.activeGameObject = box;
        }

        [MenuItem("GameObject/Oops It Ate/Door Exit", false, 16)]
        public static void CreateDoorExit()
        {
            var door = new GameObject("DoorExit");
            door.name = "DoorExit";
            door.AddComponent<DoorExit>();

            Selection.activeGameObject = door;
        }

        [MenuItem("Tools/Oops It Ate/Open Level Painter", false, 1)]
        public static void OpenLevelPainter()
        {
            LevelSceneSettings settings = Object.FindAnyObjectByType<LevelSceneSettings>();
            if (settings == null)
            {
                CreateLevelRoot();
                settings = Selection.activeGameObject.GetComponent<LevelSceneSettings>();
            }

            Selection.activeGameObject = settings.gameObject;
            EditorGUIUtility.PingObject(settings.gameObject);
        }
    }
}
