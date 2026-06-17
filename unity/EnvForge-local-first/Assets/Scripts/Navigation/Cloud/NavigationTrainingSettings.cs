using System;
using EnvForge.Navigation.Contracts;
using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    [Serializable]
    public sealed class NavigationTrainingSettings
    {
        [SerializeField] private int timesteps = 5000;
        [SerializeField] private int maxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
        [SerializeField] private int seed = 10;
        [SerializeField] private int nEnvs = 4;
        [SerializeField] private int cpuCount = 4;
        [SerializeField] private int torchNumThreads = 2;
        [SerializeField] private int nSteps = 32;
        [SerializeField] private int batchSize = 32;
        [SerializeField] private float cameraMountHeightMeters = NavigationScenarioBundleDefaults.CameraMountHeightMeters;
        [SerializeField] private float cameraMountHeightMinMeters = NavigationScenarioBundleDefaults.CameraMountHeightMinMeters;
        [SerializeField] private float cameraMountHeightMaxMeters = NavigationScenarioBundleDefaults.CameraMountHeightMaxMeters;
        [SerializeField] private float gamma = 0.99f;
        [SerializeField] private float learningRate = 0.0003f;
        [SerializeField] private float entCoef;
        [SerializeField] private int evalEpisodes = 20;
        [SerializeField] private float goalReachedReward = 100f;
        [SerializeField] private float goalProgressReward = 0.1f;
        [SerializeField] private float collisionPenalty = -50f;
        [SerializeField] private float stepPenalty = -0.01f;
        [SerializeField] private float wideAnglePenalty = -0.1f;
        [SerializeField] private float rearAnglePenalty = -5f;
        [SerializeField] private float inactivePenalty = -0.1f;
        [SerializeField] private float movementThreshold = 0.001f;
        [SerializeField] private string presetName = "Smoke";

        private bool applyingPreset;

        public string PresetName => string.IsNullOrWhiteSpace(presetName) ? "Custom" : presetName;
        public int Timesteps { get => timesteps; set => SetValue(ref timesteps, Mathf.Max(1, value)); }
        public int MaxEpisodeSteps { get => maxEpisodeSteps; set => SetValue(ref maxEpisodeSteps, Mathf.Max(1, value)); }
        public int Seed { get => seed; set => SetValue(ref seed, value); }
        public int NEnvs { get => nEnvs <= 0 ? 4 : nEnvs; set => SetValue(ref nEnvs, Mathf.Max(1, value)); }
        public int CpuCount { get => cpuCount <= 0 ? 4 : cpuCount; set => SetValue(ref cpuCount, Mathf.Max(1, value)); }
        public int TorchNumThreads { get => torchNumThreads <= 0 ? 2 : torchNumThreads; set => SetValue(ref torchNumThreads, Mathf.Max(1, value)); }
        public int NSteps { get => nSteps; set => SetValue(ref nSteps, Mathf.Max(1, value)); }
        public int BatchSize { get => batchSize; set => SetValue(ref batchSize, Mathf.Max(1, value)); }
        public float CameraMountHeightMeters { get => cameraMountHeightMeters; set => SetValue(ref cameraMountHeightMeters, Mathf.Max(0.001f, value)); }
        public float CameraMountHeightMinMeters { get => cameraMountHeightMinMeters; set => SetValue(ref cameraMountHeightMinMeters, Mathf.Max(0.001f, value)); }
        public float CameraMountHeightMaxMeters { get => cameraMountHeightMaxMeters; set => SetValue(ref cameraMountHeightMaxMeters, Mathf.Max(0.001f, value)); }
        public float Gamma { get => gamma; set => SetValue(ref gamma, Mathf.Clamp(value, 0.0001f, 1f)); }
        public float LearningRate { get => learningRate; set => SetValue(ref learningRate, Mathf.Max(0.000001f, value)); }
        public float EntCoef { get => entCoef; set => SetValue(ref entCoef, Mathf.Max(0f, value)); }
        public int EvalEpisodes { get => evalEpisodes; set => SetValue(ref evalEpisodes, Mathf.Max(1, value)); }
        public float GoalReachedReward { get => goalReachedReward; set => SetValue(ref goalReachedReward, value); }
        public float GoalProgressReward { get => goalProgressReward; set => SetValue(ref goalProgressReward, value); }
        public float CollisionPenalty { get => collisionPenalty; set => SetValue(ref collisionPenalty, value); }
        public float StepPenalty { get => stepPenalty; set => SetValue(ref stepPenalty, value); }
        public float WideAnglePenalty { get => wideAnglePenalty; set => SetValue(ref wideAnglePenalty, value); }
        public float RearAnglePenalty { get => rearAnglePenalty; set => SetValue(ref rearAnglePenalty, value); }
        public float InactivePenalty { get => inactivePenalty; set => SetValue(ref inactivePenalty, value); }
        public float MovementThreshold { get => movementThreshold; set => SetValue(ref movementThreshold, Mathf.Max(0f, value)); }

        public void ApplySmokePreset()
        {
            applyingPreset = true;
            Timesteps = 5000;
            MaxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
            Seed = 10;
            NEnvs = 4;
            CpuCount = 4;
            TorchNumThreads = 2;
            NSteps = 32;
            BatchSize = 32;
            CameraMountHeightMeters = NavigationScenarioBundleDefaults.CameraMountHeightMeters;
            CameraMountHeightMinMeters = NavigationScenarioBundleDefaults.CameraMountHeightMinMeters;
            CameraMountHeightMaxMeters = NavigationScenarioBundleDefaults.CameraMountHeightMaxMeters;
            Gamma = 0.99f;
            LearningRate = 0.0003f;
            EntCoef = 0.0f;
            EvalEpisodes = 20;
            GoalReachedReward = 100.0f;
            GoalProgressReward = 0.1f;
            CollisionPenalty = -50.0f;
            StepPenalty = -0.01f;
            WideAnglePenalty = -0.1f;
            RearAnglePenalty = -5.0f;
            InactivePenalty = -0.1f;
            MovementThreshold = 0.001f;
            applyingPreset = false;
            presetName = "Smoke";
        }

        public void ApplyMvpPreset()
        {
            applyingPreset = true;
            Timesteps = 1500000;
            MaxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
            Seed = 10;
            NEnvs = 4;
            CpuCount = 4;
            TorchNumThreads = 2;
            NSteps = 512;
            BatchSize = 64;
            CameraMountHeightMeters = NavigationScenarioBundleDefaults.CameraMountHeightMeters;
            CameraMountHeightMinMeters = NavigationScenarioBundleDefaults.CameraMountHeightMinMeters;
            CameraMountHeightMaxMeters = NavigationScenarioBundleDefaults.CameraMountHeightMaxMeters;
            Gamma = 0.99f;
            LearningRate = 0.0003f;
            EntCoef = 0.0005f;
            EvalEpisodes = 20;
            GoalReachedReward = 100.0f;
            GoalProgressReward = 0.1f;
            CollisionPenalty = -50.0f;
            StepPenalty = -0.01f;
            WideAnglePenalty = -0.1f;
            RearAnglePenalty = -5.0f;
            InactivePenalty = -0.1f;
            MovementThreshold = 0.001f;
            applyingPreset = false;
            presetName = "MVP";
        }

        public void ApplyTo(NavigationScenarioBundleSource source)
        {
            source.TrainingTimesteps = Timesteps;
            source.MaxEpisodeSteps = MaxEpisodeSteps;
            source.Seed = Seed;
            source.NEnvs = NEnvs;
            source.CpuCount = CpuCount;
            source.TorchNumThreads = TorchNumThreads;
            source.NSteps = NSteps;
            source.BatchSize = BatchSize;
            source.CameraMountHeightMeters = CameraMountHeightMeters;
            source.CameraMountHeightMinMeters = Mathf.Min(CameraMountHeightMinMeters, CameraMountHeightMaxMeters);
            source.CameraMountHeightMaxMeters = Mathf.Max(CameraMountHeightMinMeters, CameraMountHeightMaxMeters);
            source.Gamma = Gamma;
            source.LearningRate = LearningRate;
            source.EntCoef = EntCoef;
            source.EvalEpisodes = EvalEpisodes;
            source.GoalReachedReward = GoalReachedReward;
            source.GoalProgressReward = GoalProgressReward;
            source.CollisionPenalty = CollisionPenalty;
            source.StepPenalty = StepPenalty;
            source.WideAnglePenalty = WideAnglePenalty;
            source.RearAnglePenalty = RearAnglePenalty;
            source.InactivePenalty = InactivePenalty;
            source.MovementThreshold = MovementThreshold;
        }

        private void MarkCustom()
        {
            if (!applyingPreset)
            {
                presetName = "Custom";
            }
        }

        private void SetValue(ref int target, int value)
        {
            if (target == value)
            {
                return;
            }

            target = value;
            MarkCustom();
        }

        private void SetValue(ref float target, float value)
        {
            if (Mathf.Approximately(target, value))
            {
                return;
            }

            target = value;
            MarkCustom();
        }

    }
}
