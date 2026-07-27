using System;

public enum GridCellType
{
    Player,
    Floor,
    Wall,
    Oven,
    Dog
}

[Flags]
public enum Direction
{
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3
}
