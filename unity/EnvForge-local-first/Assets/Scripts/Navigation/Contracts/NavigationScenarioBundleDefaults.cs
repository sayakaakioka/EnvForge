using UnityEngine;

namespace EnvForge.Navigation.Contracts
{
    public static class NavigationScenarioBundleDefaults
    {
        public const string ScenarioId = "navigation_default";
        public const int SegmentationImageHeight = 84;
        public const int SegmentationImageWidth = 112;
        public const string GoalId = "goal_001";
        public const string CameraSensorId = "front_camera";
        public const string CameraObservationName = "obs_0";
        public const string GoalVectorSensorId = "goal_vector";
        public const string GoalVectorObservationName = "obs_1";
        public const int MaxEpisodeSteps = 1000;
        public const float CameraMountHeightMeters = 0.6f;
        public const float CameraMountHeightMinMeters = 0.1f;
        public const float CameraMountHeightMaxMeters = 1.0f;
        public const float CameraPitchDegrees = 0f;
        public const float CameraVerticalFovDegrees = 70f;
        public const float CameraNearClipMeters = 0.05f;
        public const float CameraFarClipMeters = 100f;
        public const float RobotRadiusMeters = 0.45f;
        public const float GoalRadiusMeters = 0.45f;
        public const int MinimumCameraDimensionPixels = 20;
        public const int MaximumCameraDimensionPixels = 512;
        public const float WallHeightMeters = 2f;
        public const float ForwardStepMeters = 0.2f;
        public const float TurnDegreesPerStep = 15f;
        public const float StepDurationSeconds = 0.1f;
        public const float GoalProgressMinimumDeltaMeters = 0.005f;
        public const float WideAngleMinimumDegrees = 90f;
        public const float RearAngleMinimumDegrees = 150f;

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
                WallHeight = WallHeightMeters,
                WallThickness = 0.35f,
                AgentStartPosition = AgentStartPosition,
                AgentStartRotation = AgentStartRotation,
                RobotRadiusMeters = RobotRadiusMeters,
                GoalStartPosition = GoalStartPosition,
                GoalReachRadius = GoalRadiusMeters,
                SegmentationImageWidth = SegmentationImageWidth,
                SegmentationImageHeight = SegmentationImageHeight,
                CameraMountHeightMeters = CameraMountHeightMeters,
                CameraMountHeightMinMeters = CameraMountHeightMinMeters,
                CameraMountHeightMaxMeters = CameraMountHeightMaxMeters,
                CameraPitchDegrees = CameraPitchDegrees,
                CameraVerticalFovDegrees = CameraVerticalFovDegrees,
                CameraNearClipMeters = CameraNearClipMeters,
                CameraFarClipMeters = CameraFarClipMeters,
                MaxEpisodeSteps = MaxEpisodeSteps,
            };
        }
    }
}
