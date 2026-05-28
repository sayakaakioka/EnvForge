using UnityEngine;

namespace EnvForge.Navigation.Contracts
{
    public static class NavigationScenarioBundleDefaults
    {
        public const string ScenarioId = "navigation_default";
        public const int SegmentationImageHeight = 84;
        public const int SegmentationImageWidth = 112;
        public const int MaxEpisodeSteps = 1000;

        public static readonly Vector2 FloorSize = new(16f, 12f);
        public static readonly Vector3 AgentStartPosition = new(-6f, 0.6f, -4f);
        public static readonly Quaternion AgentStartRotation = Quaternion.Euler(0f, 45f, 0f);
        public static readonly Vector3 GoalStartPosition = new(6f, 1.2f, 4f);

        public static NavigationScenarioBundleSource CreateSource()
        {
            return new NavigationScenarioBundleSource
            {
                ScenarioId = ScenarioId,
                FloorSize = FloorSize,
                WallHeight = 1.8f,
                WallThickness = 0.35f,
                AgentStartPosition = AgentStartPosition,
                AgentStartRotation = AgentStartRotation,
                GoalStartPosition = GoalStartPosition,
                GoalReachRadius = 1.2f,
                SegmentationImageWidth = SegmentationImageWidth,
                SegmentationImageHeight = SegmentationImageHeight,
                MaxEpisodeSteps = MaxEpisodeSteps,
            };
        }
    }
}
