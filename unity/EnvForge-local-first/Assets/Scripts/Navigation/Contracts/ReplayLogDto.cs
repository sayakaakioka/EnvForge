using System;
using System.Collections.Generic;

namespace EnvForge.Navigation.Contracts
{
    [Serializable]
    public sealed class ReplayLogStepDto
    {
        public string schema_version;
        public string scenario_id;
        public string job_id;
        public string phase;
        public int checkpoint_step;
        public int env_index;
        public string policy_mode;
        public string episode_id;
        public int step_index;
        public float time_seconds;
        public ReplayRobotStateDto robot;
        public ReplayActionDto action;
        public ReplayRewardDto reward;
        public List<ReplayEventDto> events;
        public List<ReplaySensorSummaryDto> sensors;
        public bool terminated;
        public string termination_reason;
    }

    [Serializable]
    public sealed class ReplayRobotStateDto
    {
        public Vector2Dto position;
        public float rotation_y_degrees;
    }

    [Serializable]
    public sealed class ReplayActionDto
    {
        public List<ReplayNamedValueDto> values;
    }

    [Serializable]
    public sealed class ReplayRewardDto
    {
        public float total;
        public List<ReplayNamedValueDto> components;
    }

    [Serializable]
    public sealed class ReplayNamedValueDto
    {
        public string name;
        public float value;
    }

    [Serializable]
    public sealed class ReplayEventDto
    {
        public string type;
        public string object_id;
        public string message;
    }

    [Serializable]
    public sealed class ReplaySensorSummaryDto
    {
        public string id;
        public string type;
        public float value;
    }
}
