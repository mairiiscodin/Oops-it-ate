using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Input;
using OopsItAte.Interaction;
using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class SceneLevelBuilder : MonoBehaviour
    {
        [SerializeField] private LevelSceneSettings settings;
        [SerializeField] private GridWorld gridWorld;
        [SerializeField] private PlayerStart playerStart;
        [SerializeField] private GridMover player;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private KitchenStation kitchen;
        [SerializeField] private PetBody pet;
        [SerializeField] private PushableBox[] boxes;
        [SerializeField] private KeyboardGridInput input;
        [SerializeField] private LevelExitController exitController;

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            BuildLevel();
        }

        private void BuildLevel()
        {
            settings = GetComponent<LevelSceneSettings>();
            if (settings == null)
            {
                Debug.LogError("SceneLevelBuilder needs LevelSceneSettings on the same GameObject.", this);
                enabled = false;
                return;
            }

            playerStart = FindAnyObjectByType<PlayerStart>();
            kitchen = FindAnyObjectByType<KitchenStation>();
            pet = FindAnyObjectByType<PetBody>();
            boxes = FindObjectsByType<PushableBox>();

            if (playerStart == null || kitchen == null || pet == null)
            {
                Debug.LogError("Scene needs PlayerStart, KitchenStation, and PetBody objects.", this);
                enabled = false;
                return;
            }

            gridWorld = CreateGridWorld();
            SetupKitchen();
            SetupPet();
            SetupBoxes();
            player = CreatePlayer();
            inventory = player.gameObject.AddComponent<PlayerInventory>();
            interactor = player.gameObject.AddComponent<PlayerInteractor>();
            interactor.Initialize(player, inventory, kitchen, pet, boxes);
            exitController = CreateExitController();
            input = CreateInput(player, interactor, exitController);
            SetupCamera();
        }

        private GridWorld CreateGridWorld()
        {
            GameObject gridObject = new GameObject("Grid World");
            gridObject.transform.SetParent(transform);

            var world = gridObject.AddComponent<GridWorld>();
            world.Initialize(settings.grid, GetWallPositions());
            return world;
        }

        private IEnumerable<GridPosition> GetWallPositions()
        {
            GridWall[] walls = FindObjectsByType<GridWall>();
            for (int i = 0; i < walls.Length; i++)
            {
                var renderer = walls[i].GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                yield return settings.grid.WorldToGrid(walls[i].transform.position);
            }
        }

        private void SetupKitchen()
        {
            GridPosition position = settings.grid.WorldToGrid(kitchen.transform.position);
            kitchen.Initialize(gridWorld, position);
            kitchen.transform.position = settings.grid.GridToWorld(position) + Vector3.back * 0.25f;
        }

        private void SetupPet()
        {
            GridPosition position = settings.grid.WorldToGrid(pet.transform.position);
            pet.Initialize(gridWorld, position);
        }

        private void SetupBoxes()
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                GridPosition position = settings.grid.WorldToGrid(boxes[i].transform.position);
                boxes[i].Initialize(gridWorld, position);
            }
        }

        private GridMover CreatePlayer()
        {
            GameObject playerObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            playerObject.name = "Player";
            playerObject.transform.SetParent(transform);
            playerObject.transform.localScale = Vector3.one * settings.grid.cellSize * 0.72f;

            var renderer = playerObject.GetComponent<MeshRenderer>();
            renderer.material = new Material(FindUnlitShader());
            renderer.material.color = new Color(0.1f, 0.55f, 1f);

            Destroy(playerObject.GetComponent<Collider>());

            var mover = playerObject.AddComponent<GridMover>();
            mover.Initialize(gridWorld, settings.grid.WorldToGrid(playerStart.transform.position));
            return mover;
        }

        private LevelExitController CreateExitController()
        {
            var controller = gameObject.AddComponent<LevelExitController>();
            controller.Initialize(FindObjectsByType<DoorExit>(), gridWorld);
            return controller;
        }

        private KeyboardGridInput CreateInput(GridMover mover, PlayerInteractor playerInteractor, LevelExitController controller)
        {
            GameObject inputObject = new GameObject("Keyboard Grid Input");
            inputObject.transform.SetParent(transform);

            var keyboardInput = inputObject.AddComponent<KeyboardGridInput>();
            keyboardInput.Initialize(mover, playerInteractor, controller);
            return keyboardInput;
        }

        private void SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(settings.grid.width, settings.grid.height) * 0.65f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDrawGizmos()
        {
            LevelSceneSettings sceneSettings = GetComponent<LevelSceneSettings>();
            if (sceneSettings == null || sceneSettings.grid == null)
            {
                return;
            }

            DrawGridPreview(sceneSettings);
        }

        private static void DrawGridPreview(LevelSceneSettings sceneSettings)
        {
            GridSettings grid = sceneSettings.grid;
            Vector3 cellSize = Vector3.one * grid.cellSize;

            for (int y = 0; y < grid.height; y++)
            {
                for (int x = 0; x < grid.width; x++)
                {
                    Vector3 center = grid.GridToWorld(new GridPosition(x, y)) + Vector3.forward * 0.05f;
                    bool checker = (x + y) % 2 == 0;
                    Gizmos.color = checker
                        ? new Color(0.18f, 0.22f, 0.25f, 0.24f)
                        : new Color(0.23f, 0.27f, 0.3f, 0.24f);
                    Gizmos.DrawCube(center, cellSize * 0.92f);

                    Gizmos.color = new Color(0.9f, 0.95f, 1f, 0.28f);
                    Gizmos.DrawWireCube(center, cellSize * 0.92f);
                }
            }

            Vector3 min = grid.GridToWorld(new GridPosition(0, 0));
            Vector3 max = grid.GridToWorld(new GridPosition(grid.width - 1, grid.height - 1));
            Vector3 boundsCenter = (min + max) * 0.5f + Vector3.forward * 0.05f;
            Vector3 boundsSize = new Vector3(grid.width * grid.cellSize, grid.height * grid.cellSize, grid.cellSize * 0.1f);

            Gizmos.color = new Color(1f, 1f, 1f, 0.85f);
            Gizmos.DrawWireCube(boundsCenter, boundsSize);
        }
    }
}
