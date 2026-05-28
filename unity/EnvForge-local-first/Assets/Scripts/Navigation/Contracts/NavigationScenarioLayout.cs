using System.Collections.Generic;
using UnityEngine;

namespace EnvForge.Navigation.Contracts
{
    public readonly struct NavigationScenarioWallSpec
    {
        public NavigationScenarioWallSpec(string id, string displayName, Vector3 center, Vector3 size, float rotationYDegrees = 0f)
        {
            Id = id;
            DisplayName = displayName;
            Center = center;
            Size = size;
            RotationYDegrees = rotationYDegrees;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public Vector3 Center { get; }

        public Vector3 Size { get; }

        public float RotationYDegrees { get; }
    }

    public static class NavigationScenarioLayout
    {
        public static IReadOnlyList<NavigationScenarioWallSpec> CreateBoundaryWalls(Vector2 floorSize, float wallHeight, float wallThickness)
        {
            float halfWidth = floorSize.x * 0.5f;
            float halfDepth = floorSize.y * 0.5f;

            return new[]
            {
                new NavigationScenarioWallSpec("wall_north", "Navigation Wall North", new Vector3(0f, wallHeight * 0.5f, halfDepth), new Vector3(floorSize.x, wallHeight, wallThickness)),
                new NavigationScenarioWallSpec("wall_south", "Navigation Wall South", new Vector3(0f, wallHeight * 0.5f, -halfDepth), new Vector3(floorSize.x, wallHeight, wallThickness)),
                new NavigationScenarioWallSpec("wall_east", "Navigation Wall East", new Vector3(halfWidth, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, floorSize.y)),
                new NavigationScenarioWallSpec("wall_west", "Navigation Wall West", new Vector3(-halfWidth, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, floorSize.y)),
            };
        }

        public static IReadOnlyList<NavigationScenarioWallSpec> CreateInnerWalls(float wallHeight)
        {
            return new[]
            {
                new NavigationScenarioWallSpec("inner_wall_a", "Navigation Inner Wall A", new Vector3(-1.5f, wallHeight * 0.5f, -1.2f), new Vector3(0.35f, wallHeight, 4.2f)),
                new NavigationScenarioWallSpec("inner_wall_b", "Navigation Inner Wall B", new Vector3(2.8f, wallHeight * 0.5f, 1.6f), new Vector3(4.4f, wallHeight, 0.35f)),
                new NavigationScenarioWallSpec("inner_wall_c", "Navigation Inner Wall C", new Vector3(4.8f, wallHeight * 0.5f, -2.2f), new Vector3(0.35f, wallHeight, 2.5f)),
                new NavigationScenarioWallSpec("inner_wall_d", "Navigation Inner Wall D", new Vector3(-4.2f, wallHeight * 0.5f, 2.2f), new Vector3(2.4f, wallHeight, 0.35f)),
            };
        }
    }
}
