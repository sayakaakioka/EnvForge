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
                        new RewardComponentDto { name = "goal_reached", type = "terminal_reward", weight = 10.0f },
                        new RewardComponentDto { name = "goal_progress", type = "distance_delta", target = "goal_001", weight = 0.5f },
                        new RewardComponentDto { name = "collision_penalty", type = "collision", weight = -5.0f },
                        new RewardComponentDto { name = "step_penalty", type = "per_step", weight = -0.01f },
                    },
                },
                training = new TrainingDto
                {
                    algorithm = "ppo",
                    timesteps = source.TrainingTimesteps,
                    seed = source.Seed,
                    max_episode_steps = source.MaxEpisodeSteps,
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
