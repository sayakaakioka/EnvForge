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
        public int n_steps;
        public int batch_size;
        public float gamma;
        public float learning_rate;
        public float ent_coef;
        public int eval_episodes;
    }

    [Serializable]
    public sealed class Vector2Dto
    {
        public float x;
        public float z;
    }

    [Serializable]
    public sealed class ResultBundleDto
    {
        public string schema_version;
        public string scenario_id;
        public string job_id;
        public string status;
        public ResultCompatibilityDto compatibility;
        public TrainingSummaryDto summary;
        public ResultArtifactsDto artifacts;
        public ErrorReportDto error;
    }

    [Serializable]
    public sealed class ResultDocumentDto
    {
        public string submission_id;
        public string status;
        public ProgressDto progress;
        public ResultArtifactsDto artifacts;
        public ResultBundleDto result_bundle;
        public string updated_at;
    }

    [Serializable]
    public sealed class ProgressDto
    {
        public string phase;
        public int current_step;
        public int total_steps;
        public string message;
    }

    [Serializable]
    public sealed class ResultCompatibilityDto
    {
        public string scenario_schema_version;
        public string envforge_min_version;
        public string robot_version;
        public string sensor_version;
        public List<string> action_layout;
        public List<string> observation_layout;
    }

    [Serializable]
    public sealed class TrainingSummaryDto
    {
        public int training_timesteps;
        public int training_seed;
        public float success_rate;
        public float average_episode_reward;
        public float average_episode_steps;
    }

    [Serializable]
    public sealed class ResultArtifactsDto
    {
        public ArtifactLocationDto model;
        public ArtifactLocationDto onnx_model;
        public ArtifactLocationDto replay_log;
    }

    [Serializable]
    public class ArtifactLocationDto
    {
        public string storage;
        public string bucket;
        public string path;
        public string format;
    }

    [Serializable]
    public sealed class ErrorReportDto
    {
        public string message;
        public string details;
    }
}
