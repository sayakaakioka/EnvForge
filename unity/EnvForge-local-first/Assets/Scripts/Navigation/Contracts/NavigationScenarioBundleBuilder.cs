using System.Collections.Generic;
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

        public Vector3 GoalStartPosition { get; set; }

        public float GoalReachRadius { get; set; }

        public int SegmentationImageWidth { get; set; }

        public int SegmentationImageHeight { get; set; }

        public int MaxEpisodeSteps { get; set; }

        public int TrainingTimesteps { get; set; } = 5000;

        public int Seed { get; set; } = 10;

        public int NEnvs { get; set; } = 4;

        public int CpuCount { get; set; } = 4;

        public int TorchNumThreads { get; set; } = 2;

        public int NSteps { get; set; } = 32;

        public int BatchSize { get; set; } = 32;

        public float Gamma { get; set; } = 0.99f;

        public float LearningRate { get; set; } = 0.0003f;

        public float EntCoef { get; set; }

        public int EvalEpisodes { get; set; } = 20;

        public float GoalReachedReward { get; set; } = 100.0f;

        public float GoalProgressReward { get; set; } = 0.1f;

        public float CollisionPenalty { get; set; } = -50.0f;

        public float StepPenalty { get; set; } = -0.01f;

        public float WideAnglePenalty { get; set; } = -0.1f;

        public float RearAnglePenalty { get; set; } = -5.0f;

        public float InactivePenalty { get; set; } = -0.1f;

        public float MovementThreshold { get; set; } = 0.001f;
    }

    public static class NavigationScenarioBundleBuilder
    {
        public const string SchemaVersion = "scenario-bundle.v0";
        public const string CoordinateSystem = "envforge_xz_meters";

        public static ScenarioBundleDto Build(NavigationScenarioBundleSource source)
        {
            IReadOnlyList<NavigationScenarioWallSpec> boundaryWalls = NavigationScenarioLayout.CreateBoundaryWalls(source.FloorSize, source.WallHeight, source.WallThickness);
            IReadOnlyList<NavigationScenarioWallSpec> innerWalls = NavigationScenarioLayout.CreateInnerWalls(source.WallHeight);

            return new ScenarioBundleDto
            {
                schema_version = SchemaVersion,
                scenario_id = source.ScenarioId,
                created_by = new CreatedByDto
                {
                    tool = "EnvForge",
                    version = source.EnvForgeVersion,
                },
                compatibility = new CompatibilityDto
                {
                    envforge_min_version = source.EnvForgeVersion,
                    robot_version = "simple_robot.v0",
                    sensor_version = "basic_sensors.v0",
                },
                world = new WorldDto
                {
                    coordinate_system = CoordinateSystem,
                    bounds = BuildCenteredBounds(source.FloorSize),
                    static_walls = BuildStaticWalls(boundaryWalls),
                    static_obstacles = BuildStaticObstacles(innerWalls),
                    goal = new GoalDto
                    {
                        id = "goal_001",
                        position = ToVector2Dto(source.GoalStartPosition),
                        radius = source.GoalReachRadius,
                    },
                },
                robot = new RobotDto
                {
                    type = "simple_robot",
                    start_pose = new Pose2DDto
                    {
                        position = ToVector2Dto(source.AgentStartPosition),
                        rotation_y_degrees = source.AgentStartRotation.eulerAngles.y,
                    },
                    action_space = new ActionSpaceDto
                    {
                        type = "continuous",
                        layout = new List<string> { "forward", "turn" },
                    },
                },
                sensors = new List<SensorDto>
                {
                    new SensorDto
                    {
                        id = "front_camera",
                        type = "forward_camera",
                        width = source.SegmentationImageWidth,
                        height = source.SegmentationImageHeight,
                        semantic_mode = "traversable_vs_blocked",
                    },
                    new SensorDto
                    {
                        id = "front_distance",
                        type = "distance_sensor",
                        range_meters = 5.0f,
                        direction = "forward",
                    },
                },
                reward = new RewardDto
                {
                    components = new List<RewardComponentDto>
                    {
                        new RewardComponentDto { name = "goal_reached", type = "terminal_reward", weight = source.GoalReachedReward },
                        new RewardComponentDto { name = "goal_progress", type = "distance_delta", target = "goal_001", weight = source.GoalProgressReward },
                        new RewardComponentDto { name = "collision_penalty", type = "collision", weight = source.CollisionPenalty },
                        new RewardComponentDto { name = "step_penalty", type = "per_step", weight = source.StepPenalty },
                        new RewardComponentDto { name = "wide_angle_penalty", type = "per_step", weight = source.WideAnglePenalty },
                        new RewardComponentDto { name = "rear_angle_penalty", type = "per_step", weight = source.RearAnglePenalty },
                        new RewardComponentDto { name = "inactive_penalty", type = "per_step", weight = source.InactivePenalty },
                        new RewardComponentDto { name = "movement_threshold", type = "per_step", weight = source.MovementThreshold },
                    },
                },
                training = new TrainingDto
                {
                    algorithm = "ppo",
                    timesteps = source.TrainingTimesteps,
                    seed = source.Seed,
                    max_episode_steps = source.MaxEpisodeSteps,
                    n_envs = source.NEnvs,
                    cpu_count = source.CpuCount,
                    torch_num_threads = source.TorchNumThreads,
                    n_steps = source.NSteps,
                    batch_size = source.BatchSize,
                    gamma = source.Gamma,
                    learning_rate = source.LearningRate,
                    ent_coef = source.EntCoef,
                    eval_episodes = source.EvalEpisodes,
                },
            };
        }

        private static Bounds2DDto BuildCenteredBounds(Vector2 floorSize)
        {
            float halfWidth = floorSize.x * 0.5f;
            float halfDepth = floorSize.y * 0.5f;

            return new Bounds2DDto
            {
                min = new Vector2Dto { x = -halfWidth, z = -halfDepth },
                max = new Vector2Dto { x = halfWidth, z = halfDepth },
            };
        }

        private static List<StaticWallDto> BuildStaticWalls(IReadOnlyList<NavigationScenarioWallSpec> wallSpecs)
        {
            List<StaticWallDto> walls = new(wallSpecs.Count);
            foreach (NavigationScenarioWallSpec wallSpec in wallSpecs)
            {
                walls.Add(new StaticWallDto
                {
                    id = wallSpec.Id,
                    center = ToVector2Dto(wallSpec.Center),
                    size = ToSize2DDto(wallSpec.Size),
                    rotation_y_degrees = wallSpec.RotationYDegrees,
                });
            }

            return walls;
        }

        private static List<StaticObstacleDto> BuildStaticObstacles(IReadOnlyList<NavigationScenarioWallSpec> wallSpecs)
        {
            List<StaticObstacleDto> obstacles = new(wallSpecs.Count);
            foreach (NavigationScenarioWallSpec wallSpec in wallSpecs)
            {
                obstacles.Add(new StaticObstacleDto
                {
                    id = wallSpec.Id,
                    shape = "box",
                    center = ToVector2Dto(wallSpec.Center),
                    size = ToSize2DDto(wallSpec.Size),
                    rotation_y_degrees = wallSpec.RotationYDegrees,
                });
            }

            return obstacles;
        }

        private static Vector2Dto ToVector2Dto(Vector3 value)
        {
            return new Vector2Dto { x = value.x, z = value.z };
        }

        private static Vector2Dto ToSize2DDto(Vector3 value)
        {
            return new Vector2Dto { x = value.x, z = value.z };
        }
    }
}
