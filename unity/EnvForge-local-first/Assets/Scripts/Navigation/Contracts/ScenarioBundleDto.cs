using System;
using System.Collections.Generic;

namespace EnvForge.Navigation.Contracts
{
    [Serializable]
    public sealed class ScenarioBundleDto
    {
        public string schema_version;
        public string scenario_id;
        public CreatedByDto created_by;
        public CompatibilityDto compatibility;
        public WorldDto world;
        public RobotDto robot;
        public List<SensorDto> sensors;
        public RewardDto reward;
        public TrainingDto training;
    }

    [Serializable]
    public sealed class CreatedByDto
    {
        public string tool;
        public string version;
    }

    [Serializable]
    public sealed class CompatibilityDto
    {
        public string envforge_min_version;
        public string robot_version;
        public string sensor_version;
    }

    [Serializable]
    public sealed class WorldDto
    {
        public string coordinate_system;
        public Bounds2DDto bounds;
        public List<StaticWallDto> static_walls;
        public List<StaticObstacleDto> static_obstacles;
        public GoalDto goal;
    }

    [Serializable]
    public sealed class Bounds2DDto
    {
        public Vector2Dto min;
        public Vector2Dto max;
    }

    [Serializable]
    public sealed class StaticWallDto
    {
        public string id;
        public Vector2Dto center;
        public Vector2Dto size;
        public float rotation_y_degrees;
    }

    [Serializable]
    public sealed class StaticObstacleDto
    {
        public string id;
        public string shape;
        public Vector2Dto center;
        public Vector2Dto size;
        public float rotation_y_degrees;
    }

    [Serializable]
    public sealed class GoalDto
    {
        public string id;
        public Vector2Dto position;
        public float radius;
    }

    [Serializable]
    public sealed class RobotDto
    {
        public string type;
        public Pose2DDto start_pose;
        public ActionSpaceDto action_space;
    }

    [Serializable]
    public sealed class Pose2DDto
    {
        public Vector2Dto position;
        public float rotation_y_degrees;
    }

    [Serializable]
    public sealed class ActionSpaceDto
    {
        public string type;
        public List<string> layout;
    }

    [Serializable]
    public sealed class SensorDto
    {
        public string id;
        public string type;
        public int width;
        public int height;
        public string semantic_mode;
        public float range_meters;
        public string direction;
    }

    [Serializable]
    public sealed class RewardDto
    {
        public List<RewardComponentDto> components;
    }

    [Serializable]
    public sealed class RewardComponentDto
    {
        public string name;
        public string type;
        public string target;
        public float weight;
    }

    [Serializable]
    public sealed class TrainingDto
    {
        public string algorithm;
        public int timesteps;
        public int seed;
        public int max_episode_steps;
    }

    [Serializable]
    public sealed class Vector2Dto
    {
        public float x;
        public float z;
    }
}
