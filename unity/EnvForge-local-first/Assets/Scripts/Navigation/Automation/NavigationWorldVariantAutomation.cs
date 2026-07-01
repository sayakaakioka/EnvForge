using System;
using UnityEngine;

namespace EnvForge.Navigation.Automation
{
    internal static class NavigationWorldVariantAutomation
    {
        public static void Apply(NavigationSceneBuilder sceneBuilder, string worldVariant)
        {
            if (sceneBuilder == null)
            {
                return;
            }

            sceneBuilder.ClearUserWalls();
            if (string.Equals(worldVariant, "open-small", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(12f, 9f));
                return;
            }

            if (string.Equals(worldVariant, "open-medium", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(18f, 14f));
                return;
            }

            if (string.Equals(worldVariant, "open-large", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(24f, 18f));
                return;
            }

            if (string.Equals(worldVariant, "random-small", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(12f, 9f));
                sceneBuilder.AddUserWall(new Vector2(-1.5f, -0.4f), 3.2f, 0.35f, 28f);
                sceneBuilder.AddUserWall(new Vector2(2.0f, 1.3f), 2.4f, 0.3f, -35f);
                return;
            }

            if (string.Equals(worldVariant, "random-medium", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(18f, 14f));
                sceneBuilder.AddUserWall(new Vector2(-2.8f, -1.2f), 5.2f, 0.35f, 18f);
                sceneBuilder.AddUserWall(new Vector2(1.4f, 2.1f), 4.0f, 0.4f, -42f);
                sceneBuilder.AddUserWall(new Vector2(3.6f, -2.4f), 2.8f, 0.35f, 72f);
                return;
            }

            if (string.Equals(worldVariant, "random-large", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(24f, 18f));
                sceneBuilder.AddUserWall(new Vector2(-4.5f, -2.0f), 6.5f, 0.45f, 12f);
                sceneBuilder.AddUserWall(new Vector2(0.5f, 2.8f), 5.0f, 0.35f, -30f);
                sceneBuilder.AddUserWall(new Vector2(5.2f, -1.8f), 4.5f, 0.4f, 65f);
                sceneBuilder.AddUserWall(new Vector2(-1.8f, 5.0f), 3.2f, 0.3f, 88f);
                return;
            }

            if (string.Equals(worldVariant, "corridor-medium", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(18f, 14f));
                sceneBuilder.AddUserWall(new Vector2(-2.9f, 0.2f), 8.0f, 0.35f, 0f);
                sceneBuilder.AddUserWall(new Vector2(2.7f, -0.8f), 7.2f, 0.35f, 0f);
                sceneBuilder.AddUserWall(new Vector2(0.0f, 3.6f), 4.5f, 0.35f, 90f);
                return;
            }

            if (string.Equals(worldVariant, "maze-medium", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(18f, 14f));
                sceneBuilder.AddUserWall(new Vector2(-4.2f, -1.4f), 4.5f, 0.35f, 90f);
                sceneBuilder.AddUserWall(new Vector2(-1.0f, 1.8f), 5.0f, 0.35f, 0f);
                sceneBuilder.AddUserWall(new Vector2(2.7f, -1.6f), 5.0f, 0.35f, 90f);
                sceneBuilder.AddUserWall(new Vector2(4.0f, 2.8f), 3.5f, 0.35f, -30f);
                return;
            }

            if (string.Equals(worldVariant, "clutter-large", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.SetFloorSize(new Vector2(24f, 18f));
                sceneBuilder.AddUserWall(new Vector2(-6.2f, -3.4f), 5.0f, 0.35f, 20f);
                sceneBuilder.AddUserWall(new Vector2(-2.6f, 2.8f), 5.5f, 0.35f, -45f);
                sceneBuilder.AddUserWall(new Vector2(2.4f, -2.2f), 5.0f, 0.4f, 70f);
                sceneBuilder.AddUserWall(new Vector2(6.0f, 1.8f), 4.2f, 0.35f, 10f);
                sceneBuilder.AddUserWall(new Vector2(0.6f, 5.4f), 3.8f, 0.3f, 88f);
                return;
            }

            sceneBuilder.SetFloorSize(Contracts.NavigationScenarioBundleDefaults.FloorSize);
        }
    }
}
