using System.Collections.Generic;
using UnityEngine;
public class EnvironmentManager : MonoBehaviour
{
    [SerializeField] private EnvironmentData data = new();

    [SerializeField] private int initialWidth = 10;

    [SerializeField] private int initialHeight = 10;

    public int Width => data.Width;
    public int Height => data.Height;
    public IReadOnlyList<GridPosition> Obstacles => data.Obstacles;
    public GridPosition Goal => data.Goal;
    public GridPosition RobotStart => data.RobotStart;

    private void Awake()
    {
        Initialize();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (data == null)
        {
            data = new EnvironmentData();
        }

        initialWidth = data.EnsureMin(initialWidth);
        initialHeight = data.EnsureMin(initialHeight);
    }
#endif

    public void Initialize()
    {
        // Build fresh runtime state from inspector-defined initial configuration.
        data.SetSize(initialWidth, initialHeight);
        data.ClearObstacles();
        data.SetGoal(initialWidth - 1, initialHeight - 1);
        data.SetRobotStart(0, 0);
        data.NormalizeForEditor();
    }

    public void ResizeGrid(int newWidth, int newHeight)
    {
        Debug.Log($"ResizeGrid: {data.Width}x{data.Height} -> {newWidth}x{newHeight}");
        data.ResizePreservingState(newWidth, newHeight);
    }

    public void SetGoal(int x, int y)
    {
        data.SetGoal(x, y);
    }

    public void SetRobotStart(int x, int y)
    {
        data.SetRobotStart(x, y);
    }

    public void AddObstacle(int x, int y)
    {
        if (!data.HasObstacleAt(x, y))
        {
            data.AddObstacleAt(x, y);
        }
    }

    public void RemoveObstacle(int x, int y)
    {
        data.RemoveObstacleAt(x, y);
    }

    public void ToggleObstacle(int x, int y)
    {
        if (data.HasObstacleAt(x, y))
        {
            data.RemoveObstacleAt(x, y);
        }
        else
        {
            data.AddObstacleAt(x, y);
        }
    }

    public bool HasObstacle(int x, int y)
    {
        return data.HasObstacleAt(x, y);

    }
}