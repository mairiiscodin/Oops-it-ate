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
        [SerializeField] private GridMover player;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private KitchenStation kitchen;
        [SerializeField] private PetBody[] pets;
        [SerializeField] private PushableBox[] boxes;
        [SerializeField] private KeyboardGridInput input;
        [SerializeField] private LevelExitController exitController;
        [SerializeField] private RoomCompletionAnimation completionAnimation;

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

            player = FindAnyObjectByType<GridMover>();
            kitchen = FindAnyObjectByType<KitchenStation>();
            pets = FindObjectsByType<PetBody>();
            boxes = FindObjectsByType<PushableBox>();

            if (player == null || kitchen == null || pets == null || pets.Length == 0)
            {
                Debug.LogError("Scene needs Player, KitchenStation, and at least one PetBody object.", this);
                enabled = false;
                return;
            }

            gridWorld = CreateGridWorld();
            SetupKitchen();
            SetupPets();
            SetupBoxes();
            exitController = CreateExitController();

            GridPosition playerSpawnPosition = settings.grid.WorldToGrid(player.transform.position);
            if (exitController.TryConsumeArrivalPosition(out GridPosition doorArrivalPosition))
            {
                playerSpawnPosition = doorArrivalPosition;
            }

            player.Initialize(gridWorld, playerSpawnPosition);
            inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = player.gameObject.AddComponent<PlayerInventory>();
            }
            inventory.SetHasFood(GameSession.HasFood);

            interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null)
            {
                interactor = player.gameObject.AddComponent<PlayerInteractor>();
            }

            interactor.Initialize(player, inventory, kitchen, pets, boxes);
            input = CreateInput(player, interactor, exitController);
            completionAnimation = gameObject.AddComponent<RoomCompletionAnimation>();
            completionAnimation.Initialize(pets);
            SetupCamera();
        }

        private GridWorld CreateGridWorld()
        {
            GameObject gridObject = new GameObject("Grid World");
            gridObject.transform.SetParent(transform);

            var world = gridObject.AddComponent<GridWorld>();
            if (settings.TryReadTileMap(
                out HashSet<GridPosition> mapCells,
                out HashSet<GridPosition> tileWalls,
                out HashSet<GridPosition> borderCells))
            {
                foreach (GridPosition wall in GetWallPositions())
                {
                    tileWalls.Add(wall);
                }

                world.Initialize(
                    settings.grid,
                    tileWalls,
                    mapCells,
                    borderCells,
                    settings.tileTheme);
            }
            else
            {
                world.Initialize(settings.grid, GetWallPositions(), null, null, settings.tileTheme);
            }

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

        private void SetupPets()
        {
            for (int i = 0; i < pets.Length; i++)
            {
                GridPosition position = settings.grid.WorldToGrid(pets[i].transform.position);
                if (GameSession.TryGetObjectPosition(
                    settings.RoomId,
                    $"Pet:{pets[i].name}",
                    out GridPosition savedPosition))
                {
                    position = savedPosition;
                }
                pets[i].Initialize(gridWorld, position);
            }
        }

        private void SetupBoxes()
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                GridPosition position = settings.grid.WorldToGrid(boxes[i].transform.position);
                if (GameSession.TryGetObjectPosition(
                    settings.RoomId,
                    $"Box:{boxes[i].name}",
                    out GridPosition savedPosition))
                {
                    position = savedPosition;
                }
                boxes[i].Initialize(gridWorld, position);
            }
        }

        private LevelExitController CreateExitController()
        {
            var controller = gameObject.AddComponent<LevelExitController>();
            controller.Initialize(FindObjectsByType<DoorExit>(), gridWorld, settings.RoomId);
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
            camera.transform.position = new Vector3(0f, 0f, -10f);
            gridWorld.FitCameraToLoadedBounds();
        }

    }
}
