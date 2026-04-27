using System.Collections.Generic;
using UnityEngine;

public class GridView : MonoBehaviour
{
    [SerializeField] private EnvironmentManager environmentManager = null;
    [SerializeField] private GameObject cellPrefab = null;
    [SerializeField] private Transform gridRoot = null;

    private readonly List<CellView> cellViews = new();

    private class CellView
    {
        public GameObject GameObject { get; }
        public GridCell GridCell { get; }
        public Renderer Renderer { get; }

        public CellView(GameObject gameObject, GridCell gridCell, Renderer renderer)
        {
            GameObject = gameObject;
            GridCell = gridCell;
            Renderer = renderer;
        }
    }

    public void RebuildGrid()
    {
        ClearGrid();

        if (environmentManager == null)
        {
            Debug.LogError("GridView: EnvironmentManager is not assigned.");
            return;
        }

        if (cellPrefab == null)
        {
            Debug.LogError("GridView: cellPrefab is not assigned.");
            return;
        }
        
        if(gridRoot == null)
        {
            Debug.LogError("GridView: gridRoot is not assigned.");
            return;
        }

        for (int y = 0; y < environmentManager.Height; y++)
        {
            for (int x = 0; x < environmentManager.Width; x++)
            {
                var cellObject = Instantiate(cellPrefab, gridRoot);
                cellObject.name = $"Cell_{x}_{y}";
                cellObject.transform.localPosition = new Vector3(x, 0f, y);

                var gridCell = cellObject.GetComponent<GridCell>();
                if (gridCell == null)
                {
                    Debug.LogError($"GridView: GridCell component is missing on instantiated cell '{cellObject.name}'.");
                    Destroy(cellObject);
                    continue;
                }

                // NOTE: GetComponentInChildren<Renderer>() is acceptable,
                // but GetComponent<Renderer>() might be better.
                var cellRenderer = cellObject.GetComponentInChildren<Renderer>();
                if(cellRenderer == null)
                {
                    Debug.LogError($"GridView: Renderer component is missing on instantiated cell '{cellObject.name}'.");
                    Destroy(cellObject);
                    continue;
                }

                gridCell.Initialize(x, y);
                cellViews.Add(new CellView(cellObject, gridCell, cellRenderer));
            }
        }

        RefreshGridVisuals();
    }

    public void RefreshGridVisuals()
    {
        if (environmentManager == null)
        {
            Debug.LogError("GridView: EnvironmentManager is not assigned.");
            return;
        }

        foreach (var cellView in cellViews)
        {
            var x = cellView.GridCell.X;
            var y = cellView.GridCell.Y;

            Color color;
            if (environmentManager.HasObstacle(x, y))
            {
                color = Color.black;
            }
            else if (environmentManager.Goal.X == x && environmentManager.Goal.Y == y)
            {
                color = Color.green;
            }
            else if (environmentManager.RobotStart.X == x && environmentManager.RobotStart.Y == y)
            {
                color = Color.blue;
            }
            else
            {
                color = Color.white;
            }

            // NOTE: renderer.material is acceptable for now,
            // but should be replaced with a more efficient visual update strategy
            // if grid size grows.
            cellView.Renderer.material.color = color;
        }

    }

    public Vector3 GridToWorldPosition(int x, int y, float yOffset = 0.35f)
    {
        if (gridRoot == null)
        {
            return new Vector3(x, yOffset, y);
        }

        return gridRoot.TransformPoint(new Vector3(x, yOffset, y));
    }

    private void ClearGrid()
    {
        foreach (var cellView in cellViews)
        {
            if (cellView.GameObject != null)
            {
                Destroy(cellView.GameObject);
            }
        }
        cellViews.Clear();
    }
}
