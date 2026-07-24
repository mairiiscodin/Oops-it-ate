using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    [SerializeField] private TilemapParser tilemapParser;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap topWallTilemap;
    [SerializeField] private Tilemap bottomWallTilemap;
    [SerializeField] private Tilemap entityTilemap;

    private Dictionary<Vector3Int, bool> gridPosOccupied;
    
    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap TopWallTilemap => topWallTilemap;
    public Tilemap BottomWallTilemap => bottomWallTilemap;
    public Tilemap EntityTilemap => entityTilemap;

    public void Initialize()
    {
        gridPosOccupied = new Dictionary<Vector3Int, bool>();
        floorTilemap.CompressBounds();
        topWallTilemap.CompressBounds();
        bottomWallTilemap.CompressBounds();
        entityTilemap.CompressBounds();
        tilemapParser.Initialize();
        
        var parsedGridEntityList = tilemapParser.ParseGridEntity(entityTilemap);
        foreach (var parsedGridEntity in parsedGridEntityList)
        {
            var gridPos = parsedGridEntity.gridPos;
            Vector3 spawnPos = entityTilemap.GetCellCenterWorld(gridPos);
            GridEntity gridEntity = Instantiate(
                parsedGridEntity.gridEntity, 
                spawnPos, 
                Quaternion.identity);
            gridEntity.Initialize(this, gridPos);
            gridPosOccupied[gridEntity.GetGridPos()] = true;
        }
        foreach (Vector3Int gridPos in topWallTilemap.cellBounds.allPositionsWithin)
            if (topWallTilemap.HasTile(gridPos))
                gridPosOccupied[gridPos] = true;
        foreach (Vector3Int gridPos in bottomWallTilemap.cellBounds.allPositionsWithin)
            if (bottomWallTilemap.HasTile(gridPos))
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
}
