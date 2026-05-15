using System;
using UnityEngine;

[Serializable]
public class GridPosition
{
    [SerializeField] private int x;
    public int X => x;

    [SerializeField] private int y;
    public int Y => y;

    public GridPosition(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool Matches(int x, int y)
    {
        return X == x && Y == y;
    }
}
