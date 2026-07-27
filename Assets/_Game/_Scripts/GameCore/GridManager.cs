using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    [SerializeField] private TilemapParser tilemapParser;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap entityTilemap;

    private Dictionary<Vector3Int, bool> gridPosOccupied;
    private Dictionary<Vector3Int, GridEntityController> entityControllerMap;
    
    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap WallTilemap => wallTilemap;
    public Tilemap EntityTilemap => entityTilemap;

    public void Initialize()
    {
        gridPosOccupied = new Dictionary<Vector3Int, bool>();
        entityControllerMap = new Dictionary<Vector3Int, GridEntityController>();
        floorTilemap.CompressBounds();
        wallTilemap.CompressBounds();
        entityTilemap.CompressBounds();
        tilemapParser.Initialize();
        
        var parsedGridEntityList = tilemapParser.ParseGridEntity(entityTilemap);
        foreach (var parsedGridEntity in parsedGridEntityList)
        {
            Vector3Int gridPos = parsedGridEntity.gridPos;
            Vector3 spawnPos = entityTilemap.GetCellCenterWorld(gridPos);
            GridEntityController gridEntityController = Instantiate(
                parsedGridEntity.gridEntity, 
                spawnPos, 
                Quaternion.identity);
            
            gridEntityController.Initialize(this, gridPos);
            entityControllerMap.TryAdd(gridPos, gridEntityController);
            gridPosOccupied[gridEntityController.GetGridPos()] = true;
        }
        foreach (Vector3Int gridPos in wallTilemap.cellBounds.allPositionsWithin)
            if (wallTilemap.HasTile(gridPos))
                gridPosOccupied[gridPos] = true;
    }

    public bool IsGridPosOccupied(Vector3Int gridPos)
    {
        return gridPosOccupied.TryGetValue(gridPos, out bool occupied) && occupied;
    }

    public void SetGridPosOccupied(Vector3Int gridPos, bool occupied)
    {
        gridPosOccupied[gridPos] = occupied;
    }

    public GridEntityController GetEntityAtGridPos(Vector3Int gridPos)
    {
        if (entityControllerMap.TryGetValue(gridPos, out GridEntityController gridEntityController))
            return gridEntityController;
        return null;
    }
}
