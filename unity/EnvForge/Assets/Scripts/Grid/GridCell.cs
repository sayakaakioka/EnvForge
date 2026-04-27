using UnityEngine;

// Lightweight component representing a grid coordinate (no logic).
public class GridCell : MonoBehaviour
{
    private int x;
    public int X => x;

    private int y;
    public int Y => y;

    private bool initialized = false;


    public void Initialize(int x, int y)
    {
        if (initialized)
        {
            Debug.LogWarning("GridCell is already initialized.");
            return;
        }

        this.x = x;
        this.y = y;
        initialized = true;
    }
}