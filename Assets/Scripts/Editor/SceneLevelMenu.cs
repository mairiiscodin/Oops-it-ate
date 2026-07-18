using OopsItAte.Actors;
using OopsItAte.Interaction;
using OopsItAte.Levels;
using UnityEditor;
using UnityEngine;

namespace OopsItAte.Editor
{
    public static class SceneLevelMenu
    {
        [MenuItem("GameObject/Oops It Ate/Level Root", false, 10)]
        public static void CreateLevelRoot()
        {
            var level = new GameObject("Level");
            level.AddComponent<LevelSceneSettings>();
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
            GameObject kitchen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            kitchen.name = "Kitchen";
            Object.DestroyImmediate(kitchen.GetComponent<Collider>());
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
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.name = "WallBlocker";
            Object.DestroyImmediate(wall.GetComponent<Collider>());
            wall.AddComponent<GridWall>();

            var renderer = wall.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            renderer.sharedMaterial.color = new Color(0.45f, 0.45f, 0.45f);

            Selection.activeGameObject = wall;
        }

        [MenuItem("GameObject/Oops It Ate/Pushable Box", false, 15)]
        public static void CreatePushableBox()
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Quad);
            box.name = "PushableBox";
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.AddComponent<PushableBox>();

            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            renderer.sharedMaterial.color = new Color(0.62f, 0.36f, 0.16f);

            Selection.activeGameObject = box;
        }

        [MenuItem("GameObject/Oops It Ate/Door Exit", false, 16)]
        public static void CreateDoorExit()
        {
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Quad);
            door.name = "DoorExit";
            Object.DestroyImmediate(door.GetComponent<Collider>());
            door.AddComponent<DoorExit>();

            var renderer = door.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            renderer.sharedMaterial.color = new Color(0.9f, 0.15f, 0.15f);

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
