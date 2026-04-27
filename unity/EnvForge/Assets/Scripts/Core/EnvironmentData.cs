using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnvironmentData
{
    [SerializeField] private int width;
    public int Width => width;

    [SerializeField] private int height;
    public int Height => height;

    [SerializeField] private List<GridPosition> obstacles = new();
    public IReadOnlyList<GridPosition> Obstacles => obstacles;

    [SerializeField] private GridPosition goal;
    public GridPosition Goal => goal;

    [SerializeField] private GridPosition robotStart;
    public GridPosition RobotStart => robotStart;

    public void SetSize(int width, int height)
    {
        this.width = width;
        this.height = height;
    }

    public void SetGoal(int x, int y)
    {
        goal = new GridPosition(
            Mathf.Clamp(x, 0, width - 1),
            Mathf.Clamp(y, 0, height - 1)
        );

        obstacles.RemoveAll(p => IsGoal(p.X, p.Y));
    }

    public void SetRobotStart(int x, int y)
    {
        robotStart = new GridPosition(
            Mathf.Clamp(x, 0, width - 1),
            Mathf.Clamp(y, 0, height - 1)
        );

        obstacles.RemoveAll(p => IsRobotStart(p.X, p.Y));
    }

    public void ClearObstacles()
    {
        obstacles.Clear();
    }

    public void AddObstacleAt(int x, int y)
    {
        if (OutOfArea(x, y) || IsGoal(x, y) || IsRobotStart(x, y))
        {
            return;
        }

        if (HasObstacleAt(x, y))
        {
            return;
        }

        obstacles.Add(new GridPosition(x, y));
    }

    public void ResizePreservingState(int newWidth, int newHeight)
    {
        width = EnsureMin(newWidth);
        height = EnsureMin(newHeight);

        goal = ClampGoalToBounds(goal);
        robotStart = ClampRobotStartToBounds(robotStart);
        robotStart = ResolveConflict(robotStart, goal);

        obstacles.RemoveAll(p =>
            OutOfArea(p.X, p.Y) || IsGoal(p.X, p.Y) || IsRobotStart(p.X, p.Y)
        );
    }

    public void NormalizeForEditor()
    {
        width = EnsureMin(width);
        height = EnsureMin(height);

        goal = ClampGoalToBounds(goal);
        robotStart = ClampRobotStartToBounds(robotStart);
        robotStart = ResolveConflict(robotStart, goal);

        obstacles.RemoveAll(p =>
            OutOfArea(p.X, p.Y) || IsGoal(p.X, p.Y) || IsRobotStart(p.X, p.Y)
        );
    }

    public void RemoveObstacleAt(int x, int y)
    {
        obstacles.RemoveAll(p => p.X == x && p.Y == y);
    }

    public bool HasObstacleAt(int x, int y)
    {
        return obstacles.Exists(p => p.X == x && p.Y == y);
    }

    public int EnsureMin(int x)
    {
        return Mathf.Max(2, x);
    }

    private bool OutOfArea(int x, int y)
    {
        return x < 0 || x >= width || y < 0 || y >= height;
    }

    private bool IsGoal(int x, int y)
    {
        return goal != null && x == goal.X && y == goal.Y;
    }

    private bool IsRobotStart(int x, int y)
    {
        return robotStart != null && x == robotStart.X && y == robotStart.Y;
    }

    private bool IsSamePosition(GridPosition a, GridPosition b)
    {
        return a != null && b != null && a.X == b.X && a.Y == b.Y;
    }

    private GridPosition ResolveConflict(GridPosition s, GridPosition g)
    {
        if (!IsSamePosition(s, g))
        {
            return s;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (g.X != x || g.Y != y)
                {
                    return new GridPosition(x, y);
                }
            }
        }

        // code should not reach this point
        return new GridPosition(0, 0);
    }

    private GridPosition ClampGoalToBounds(GridPosition pos)
    {
        if (pos == null)
        {
            return new GridPosition(width - 1, height - 1);
        }

        int clampedX = Mathf.Clamp(pos.X, 0, width - 1);
        int clampedY = Mathf.Clamp(pos.Y, 0, height - 1);

        return new GridPosition(clampedX, clampedY);
    }

    private GridPosition ClampRobotStartToBounds(GridPosition pos)
    {
        if (pos == null)
        {
            return new GridPosition(0, 0);
        }

        int clampedX = Mathf.Clamp(pos.X, 0, width - 1);
        int clampedY = Mathf.Clamp(pos.Y, 0, height - 1);

        return new GridPosition(clampedX, clampedY);
    }
}