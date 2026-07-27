using System;
using UnityEngine;

public static class DirectionConverter
{
    public static Direction FromVector2IntToEnum(Vector2 v)
    {
        if (v.x == 0 && Mathf.Approximately(v.y, 1)) return Direction.Up;
        if (v.x == 0 && Mathf.Approximately(v.y, -1)) return Direction.Down;
        if (v.y == 0 && Mathf.Approximately(v.x, -1)) return Direction.Left;
        if (v.y == 0 && Mathf.Approximately(v.x, 1)) return Direction.Right;
        return Direction.Down;
    }

    public static Vector2Int FromEnumToVector2Int(Direction v)
    {
        return v switch
        {
            Direction.Up => new Vector2Int(0, 1),
            Direction.Down => new Vector2Int(0, -1),
            Direction.Left => new Vector2Int(-1, 0),
            Direction.Right => new Vector2Int(1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(v), v, null)
        };
    }
}
