using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class TilemapParser
{
    [SerializeField] private List<TilePrefabMapping> gridEntityPrefabMapping;
    private static Dictionary<TileBase, GameObject> _gridEntityPrefabDict;


    public void Initialize()
    {
        _gridEntityPrefabDict = new Dictionary<TileBase, GameObject>();
        foreach (var tpm in gridEntityPrefabMapping)
            _gridEntityPrefabDict[tpm.tileBase] = tpm.prefab;
    }

    public List<(GridEntityController gridEntity, Vector3Int gridPos)> ParseGridEntity(Tilemap targetTilemap)
    {
        List<(GridEntityController gridEntity, Vector3Int gridPos)> parsedList = new();
        
        foreach (Vector3Int gridPos in targetTilemap.cellBounds.allPositionsWithin)
        {
            TileBase curTile = targetTilemap.GetTile(gridPos);
            if (curTile == null) continue;
            if (_gridEntityPrefabDict.TryGetValue(curTile, out GameObject prefab))
            {
                GridEntityController entityController = prefab.GetComponent<GridEntityController>();
                parsedList.Add((entityController, gridPos));
                targetTilemap.SetTile(gridPos, null);
            }
        }

        return parsedList;
    }
}
