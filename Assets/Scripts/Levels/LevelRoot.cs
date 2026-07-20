using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Input;
using OopsItAte.Interaction;
using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class LevelRoot : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        [SerializeField] private GridWorld gridWorld;
        [SerializeField] private GridMover player;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private KitchenStation kitchen;
        [SerializeField] private PetBody pet;
        [SerializeField] private KeyboardGridInput input;

        private void Awake()
        {
            if (level == null)
            {
                Debug.LogError("LevelRoot needs a LevelDefinition assigned.", this);
                enabled = false;
                return;
            }

            BuildLevel();
        }

        private void BuildLevel()
        {
            gridWorld = CreateGridWorld();
            player = CreatePlayer(gridWorld);
            inventory = player.gameObject.AddComponent<PlayerInventory>();
            kitchen = CreateKitchen(gridWorld);
            pet = CreatePet(gridWorld);
            interactor = player.gameObject.AddComponent<PlayerInteractor>();
            interactor.Initialize(player, inventory, kitchen, new[] { pet });
            input = CreateInput(player, interactor);
            SetupCamera();
        }

        private GridWorld CreateGridWorld()
        {
            GameObject gridObject = new GameObject("Grid World");
            gridObject.transform.SetParent(transform);

            var world = gridObject.AddComponent<GridWorld>();
            world.Initialize(
                level.grid,
                GetWallPositions(),
                GetMapPositions(),
                GetBorderPositions(),
                level.tileTheme);
            return world;
        }

        private IEnumerable<GridPosition> GetMapPositions()
        {
            if (level.mapCells == null || level.mapCells.Length == 0)
            {
                return null;
            }

            var cells = new GridPosition[level.mapCells.Length];
            for (int i = 0; i < level.mapCells.Length; i++)
            {
                cells[i] = ToGridPosition(level.mapCells[i]);
            }

            return cells;
        }

        private IEnumerable<GridPosition> GetWallPositions()
        {
            for (int i = 0; i < level.wallCells.Length; i++)
            {
                yield return ToGridPosition(level.wallCells[i]);
            }
        }

        private IEnumerable<GridPosition> GetBorderPositions()
        {
            for (int i = 0; i < level.borderCells.Length; i++)
            {
                yield return ToGridPosition(level.borderCells[i]);
            }
        }

        private GridMover CreatePlayer(GridWorld world)
        {
            GameObject playerObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            playerObject.name = "Player";
            playerObject.transform.SetParent(transform);
            playerObject.transform.localScale = Vector3.one * world.Settings.cellSize * 0.72f;

            var renderer = playerObject.GetComponent<MeshRenderer>();
            renderer.material = new Material(FindUnlitShader());
            renderer.material.color = new Color(0.1f, 0.55f, 1f);

            Destroy(playerObject.GetComponent<Collider>());

            var mover = playerObject.AddComponent<GridMover>();
            mover.Initialize(world, new GridPosition(level.playerStart.x, level.playerStart.y));
            return mover;
        }

        private KitchenStation CreateKitchen(GridWorld world)
        {
            GameObject kitchenObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            kitchenObject.name = "Kitchen";
            kitchenObject.transform.SetParent(transform);
            kitchenObject.transform.position = world.Settings.GridToWorld(ToGridPosition(level.kitchenPosition)) + Vector3.back * 0.25f;
            kitchenObject.transform.localScale = Vector3.one * world.Settings.cellSize;

            var renderer = kitchenObject.GetComponent<MeshRenderer>();
            renderer.material = new Material(FindUnlitShader());
            renderer.material.color = new Color(1f, 0.65f, 0.1f);

            Destroy(kitchenObject.GetComponent<Collider>());

            var station = kitchenObject.AddComponent<KitchenStation>();
            station.Initialize(world, ToGridPosition(level.kitchenPosition));
            return station;
        }

        private PetBody CreatePet(GridWorld world)
        {
            GameObject petObject = new GameObject("Pet");
            petObject.transform.SetParent(transform);

            var body = petObject.AddComponent<PetBody>();
            body.Initialize(world, ToGridPosition(level.petStart));
            return body;
        }

        private KeyboardGridInput CreateInput(GridMover mover, PlayerInteractor playerInteractor)
        {
            GameObject inputObject = new GameObject("Keyboard Grid Input");
            inputObject.transform.SetParent(transform);

            var keyboardInput = inputObject.AddComponent<KeyboardGridInput>();
            keyboardInput.Initialize(mover, playerInteractor);
            return keyboardInput;
        }

        private static GridPosition ToGridPosition(Vector2Int position)
        {
            return new GridPosition(position.x, position.y);
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
            camera.orthographicSize = Mathf.Max(level.grid.width, level.grid.height) * 0.65f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }
    }
}
