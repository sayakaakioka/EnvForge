using System.Collections.Generic;
using EmbodiedLab.Contracts;
using UnityEngine;

namespace EnvForge.Navigation.Contracts
{
    public sealed class NavigationScenarioBundleSource
    {
        public string ScenarioId { get; set; } = "navigation_default";

        public string EnvForgeVersion { get; set; } = "0.1.0";

        public Vector2 FloorSize { get; set; }

        public float WallHeight { get; set; }

        public float WallThickness { get; set; }

        public Vector3 AgentStartPosition { get; set; }

        public Quaternion AgentStartRotation { get; set; }

        public float RobotRadiusMeters { get; set; } = NavigationScenarioBundleDefaults.RobotRadiusMeters;

        public float ForwardStepMeters { get; set; } = NavigationScenarioBundleDefaults.ForwardStepMeters;

        public float TurnDegreesPerStep { get; set; } = NavigationScenarioBundleDefaults.TurnDegreesPerStep;

        public float StepDurationSeconds { get; set; } = NavigationScenarioBundleDefaults.StepDurationSeconds;

        public Vector3 GoalStartPosition { get; set; }

        public string GoalId { get; set; } = NavigationScenarioBundleDefaults.GoalId;

        public float GoalReachRadius { get; set; }

        public int SegmentationImageWidth { get; set; }

        public int SegmentationImageHeight { get; set; }

        public string CameraSensorId { get; set; } = NavigationScenarioBundleDefaults.CameraSensorId;

        public string CameraObservationName { get; set; } = NavigationScenarioBundleDefaults.CameraObservationName;

        public SemanticMode CameraSemanticMode { get; set; } = SemanticMode.TraversableVsBlocked;

        public string GoalVectorSensorId { get; set; } = NavigationScenarioBundleDefaults.GoalVectorSensorId;

        public string GoalVectorObservationName { get; set; } = NavigationScenarioBundleDefaults.GoalVectorObservationName;

        public ICollection<Values> GoalVectorValues { get; set; } = new List<Values>
        {
            Values.GoalAngleDegrees,
            Values.GoalDistanceMeters,
        };

        public float CameraMountHeightMeters { get; set; } = 0.6f;

        public float CameraMountHeightMinMeters { get; set; } = 0.6f;

        public float CameraMountHeightMaxMeters { get; set; } = 0.6f;

        public float CameraPitchDegrees { get; set; } = NavigationScenarioBundleDefaults.CameraPitchDegrees;

        public float CameraVerticalFovDegrees { get; set; } = NavigationScenarioBundleDefaults.CameraVerticalFovDegrees;

        public float CameraNearClipMeters { get; set; } = NavigationScenarioBundleDefaults.CameraNearClipMeters;

        public float CameraFarClipMeters { get; set; } = NavigationScenarioBundleDefaults.CameraFarClipMeters;

        public int MaxEpisodeSteps { get; set; }

        public int TrainingTimesteps { get; set; } = 5000;

        public long Seed { get; set; } = 10;

        public int NEnvs { get; set; } = 4;

        public int? CpuCount { get; set; } = 4;

        public int? TorchNumThreads { get; set; } = 2;

        public int NSteps { get; set; } = 32;

        public int BatchSize { get; set; } = 32;

        public int NEpochs { get; set; } = 3;

        public float Gamma { get; set; } = 0.99f;

        public float GaeLambda { get; set; } = 0.95f;

        public float LearningRate { get; set; } = 0.0003f;

        public float ClipRange { get; set; } = 0.2f;

        public float? ClipRangeVf { get; set; }

        public bool NormalizeAdvantage { get; set; } = true;

        public float EntCoef { get; set; }

        public float VfCoef { get; set; } = 0.5f;

        public float MaxGradNorm { get; set; } = 0.5f;

        public bool UseSde { get; set; }

        public int SdeSampleFreq { get; set; } = -1;

        public float? TargetKl { get; set; }

        public int StatsWindowSize { get; set; } = 100;

        public int EvalEpisodes { get; set; } = 20;

        public int ReplayEvalIntervalSteps { get; set; } = 1000000;

        public int ReplayTrainChunkSteps { get; set; } = 10000;

        public bool RandomizeStart { get; set; } = true;

        public float GoalReachedReward { get; set; } = 100.0f;

        public float GoalProgressReward { get; set; } = 0.1f;

        public float GoalProgressMinimumDeltaMeters { get; set; } = NavigationScenarioBundleDefaults.GoalProgressMinimumDeltaMeters;

        public float CollisionPenalty { get; set; } = -50.0f;

        public float StepPenalty { get; set; } = -0.01f;

        public float WideAnglePenalty { get; set; } = -0.1f;

        public float WideAngleMinimumDegrees { get; set; } = NavigationScenarioBundleDefaults.WideAngleMinimumDegrees;

        public float RearAnglePenalty { get; set; } = -5.0f;

        public float RearAngleMinimumDegrees { get; set; } = NavigationScenarioBundleDefaults.RearAngleMinimumDegrees;

        public float InactivePenalty { get; set; } = -0.1f;

        public float MovementThreshold { get; set; } = 0.001f;

        public IReadOnlyList<NavigationScenarioWallSpec> UserWalls { get; set; } = new List<NavigationScenarioWallSpec>();
    }

    public static class NavigationScenarioBundleBuilder
    {
        public static ScenarioBundle Build(NavigationScenarioBundleSource source)
        {
            IReadOnlyList<NavigationScenarioWallSpec> boundaryWalls = NavigationScenarioLayout.CreateBoundaryWalls(source.FloorSize, source.WallHeight, source.WallThickness);
            List<NavigationScenarioWallSpec> staticWalls = new(boundaryWalls);
            if (source.UserWalls != null)
            {
                staticWalls.AddRange(source.UserWalls);
            }

            return new ScenarioBundle
            {
                SchemaVersion = ScenarioBundleSchemaVersion.ScenarioBundleV0,
                ScenarioId = source.ScenarioId,
                CreatedBy = new CreatedBy
                {
                    Tool = "EnvForge",
                    Version = source.EnvForgeVersion,
                },
                Compatibility = new Compatibility
                {
                    RobotVersion = "simple_robot.v1",
                    SensorVersion = "basic_sensors.v0",
                },
                World = new WorldSpec
                {
                    CoordinateSystem = CoordinateSystem.LeftHandedYUpMeters,
                    Bounds = BuildCenteredBounds(source.FloorSize),
                    StaticWalls = BuildStaticWalls(staticWalls),
                    StaticObstacles = new List<StaticObstacle>(),
                    Goal = new GoalSpec
                    {
                        Id = source.GoalId,
                        Position = ToPosition2D(source.GoalStartPosition),
                        Radius = source.GoalReachRadius,
                    },
                },
                Robot = new RobotSpec
                {
                    Type = RobotType.SimpleRobot,
                    Radius = source.RobotRadiusMeters,
                    StartPose = new Pose2D
                    {
                        Position = ToPosition2D(source.AgentStartPosition),
                        RotationYDegrees = source.AgentStartRotation.eulerAngles.y,
                    },
                    ActionSpace = new ActionSpace
                    {
                        Type = ActionSpaceType.Continuous,
                        Layout = new List<Layout> { Layout.Forward, Layout.Turn },
                        ForwardStepMeters = source.ForwardStepMeters,
                        TurnDegreesPerStep = source.TurnDegreesPerStep,
                        StepDurationSeconds = source.StepDurationSeconds,
                    },
                },
                Sensors = new List<SensorSpec>
                {
                    new ForwardCameraSensor
                    {
                        Id = source.CameraSensorId,
                        Width = source.SegmentationImageWidth,
                        Height = source.SegmentationImageHeight,
                        SemanticMode = source.CameraSemanticMode,
                        MountHeightMeters = source.CameraMountHeightMeters,
                        MountHeightMinMeters = source.CameraMountHeightMinMeters,
                        MountHeightMaxMeters = source.CameraMountHeightMaxMeters,
                        PitchDegrees = source.CameraPitchDegrees,
                        VerticalFovDegrees = source.CameraVerticalFovDegrees,
                        NearClipMeters = source.CameraNearClipMeters,
                        FarClipMeters = source.CameraFarClipMeters,
                        ObservationName = source.CameraObservationName,
                    },
                    new GoalVectorSensor
                    {
                        Id = source.GoalVectorSensorId,
                        Target = source.GoalId,
                        ObservationName = source.GoalVectorObservationName,
                        Values = new List<Values>(source.GoalVectorValues),
                    },
                },
                Reward = new RewardSpec
                {
                    Components = new List<RewardComponent>
                    {
                        new TerminalRewardComponent { Name = "goal_reached", Weight = source.GoalReachedReward },
                        new DistanceDeltaRewardComponent
                        {
                            Name = "goal_progress",
                            Target = source.GoalId,
                            Weight = source.GoalProgressReward,
                            MinimumDeltaMeters = source.GoalProgressMinimumDeltaMeters,
                        },
                        new CollisionRewardComponent { Name = "collision_penalty", Weight = source.CollisionPenalty },
                        new PerStepRewardComponent { Name = "step_penalty", Weight = source.StepPenalty },
                        new MinimumAbsoluteAngleRewardComponent
                        {
                            Name = "wide_angle_penalty",
                            Weight = source.WideAnglePenalty,
                            MinimumAbsoluteAngleDegrees = source.WideAngleMinimumDegrees,
                        },
                        new MinimumAbsoluteAngleRewardComponent
                        {
                            Name = "rear_angle_penalty",
                            Weight = source.RearAnglePenalty,
                            MinimumAbsoluteAngleDegrees = source.RearAngleMinimumDegrees,
                        },
                        new MaximumAbsoluteForwardRewardComponent
                        {
                            Name = "inactive_penalty",
                            Weight = source.InactivePenalty,
                            MaximumAbsoluteForward = source.MovementThreshold,
                        },
                    },
                },
                Training = new TrainingSpec
                {
                    Algorithm = TrainingAlgorithm.Ppo,
                    Device = TrainingDevice.Cpu,
                    Timesteps = source.TrainingTimesteps,
                    Seed = source.Seed,
                    MaxEpisodeSteps = source.MaxEpisodeSteps,
                    NEnvs = source.NEnvs,
                    CpuCount = source.CpuCount,
                    TorchNumThreads = source.TorchNumThreads,
                    NSteps = source.NSteps,
                    BatchSize = source.BatchSize,
                    NEpochs = source.NEpochs,
                    Gamma = source.Gamma,
                    GaeLambda = source.GaeLambda,
                    LearningRate = source.LearningRate,
                    ClipRange = source.ClipRange,
                    ClipRangeVf = source.ClipRangeVf,
                    NormalizeAdvantage = source.NormalizeAdvantage,
                    EntCoef = source.EntCoef,
                    VfCoef = source.VfCoef,
                    MaxGradNorm = source.MaxGradNorm,
                    UseSde = source.UseSde,
                    SdeSampleFreq = source.SdeSampleFreq,
                    TargetKl = source.TargetKl,
                    StatsWindowSize = source.StatsWindowSize,
                    EvalEpisodes = source.EvalEpisodes,
                    ReplayEvalIntervalSteps = source.ReplayEvalIntervalSteps,
                    ReplayTrainChunkSteps = source.ReplayTrainChunkSteps,
                    RandomizeStart = source.RandomizeStart,
                },
            };
        }

        private static Bounds2D BuildCenteredBounds(Vector2 floorSize)
        {
            float halfWidth = floorSize.x * 0.5f;
            float halfDepth = floorSize.y * 0.5f;

            return new Bounds2D
            {
                Min = new Position2D { X = -halfWidth, Z = -halfDepth },
                Max = new Position2D { X = halfWidth, Z = halfDepth },
            };
        }

        private static List<StaticWall> BuildStaticWalls(IReadOnlyList<NavigationScenarioWallSpec> wallSpecs)
        {
            List<StaticWall> walls = new(wallSpecs.Count);
            foreach (NavigationScenarioWallSpec wallSpec in wallSpecs)
            {
                walls.Add(new StaticWall
                {
                    Id = wallSpec.Id,
                    Center = ToPosition2D(wallSpec.Center),
                    Size = ToSize2D(wallSpec.Size),
                    Height = wallSpec.Height,
                    RotationYDegrees = wallSpec.RotationYDegrees,
                });
            }

            return walls;
        }

        private static Position2D ToPosition2D(Vector3 value)
        {
            return new Position2D { X = value.x, Z = value.z };
        }

        private static Size2D ToSize2D(Vector3 value)
        {
            return new Size2D { X = value.x, Z = value.z };
        }
    }
}
