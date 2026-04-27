using System;

[Serializable]
public class EnvironmentRequestData
{
    public int[] size;
    public ObstacleData[] obstacles;
    public PositionData goal;
    public PositionData robot_start;
}

[Serializable]
public class ObstacleData
{
    public int x;
    public int y;
}

[Serializable]
public class RobotData
{
    public string type;
}

[Serializable]
public class PositionData
{
    public int x;
    public int y;
}

[Serializable]
public class SubmitRequestData
{
    public EnvironmentRequestData environment;
    public RobotData robot;
    public TrainingRequestData training;
}

[Serializable]
public class TrainingRequestData
{
    public string algorithm = "ppo";
    public int timesteps = 5000;
    public int seed = 10;
    public int max_steps = 50;
    public int n_steps = 32;
    public int batch_size = 32;
    public float gamma = 0.99f;
    public float learning_rate = 0.0003f;
    public float ent_coef = 0.0f;
    public int eval_episodes = 20;
}

[Serializable]
public class SubmitResponseData
{
    public string status;
    public string submission_id;
}

[Serializable]
public class TrainResponseData
{
    public string status;
    public string submission_id;
}

[Serializable]
public class ErrorResponseData
{
    public string detail;
}

[Serializable]
public class ResultData
{
    public string submission_id;
    public string status;
    public ProgressData progress;
    public SummaryData summary;
    public string error;
    public ArtifactData artifacts;
    public string updated_at;
}

[Serializable]
public class ProgressData
{
    public string phase;
    public int current_step;
    public int total_steps;
    public string message;
}

[Serializable]
public class SummaryData
{
    public string policy;
    public float score;
    public int grid_width;
    public int grid_height;
    public int episodes;
    public int obstacle_count;
    public PositionData goal;
    public PositionData robot_start;
    public string robot_type;
    public float success_rate;
    public float avg_reward;
    public float avg_steps;
    public int training_timesteps;
    public int training_seed;
}

[Serializable]
public class ArtifactData
{
    public ModelArtifactData model;
}

[Serializable]
public class ModelArtifactData
{
    public string storage;
    public string bucket;
    public string path;
}
