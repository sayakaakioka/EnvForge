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
        [SerializeField] private int nSteps = 32;
        [SerializeField] private int batchSize = 32;
        [SerializeField] private float gamma = 0.99f;
        [SerializeField] private float learningRate = 0.0003f;
        [SerializeField] private float entCoef;
        [SerializeField] private int evalEpisodes = 20;
        [SerializeField] private float goalReachedReward = 10f;
        [SerializeField] private float goalProgressReward = 0.5f;
        [SerializeField] private float collisionPenalty = -5f;
        [SerializeField] private float stepPenalty = -0.01f;
        [SerializeField] private float movementReward;
        [SerializeField] private float wideAnglePenalty;
        [SerializeField] private float rearAnglePenalty;
        [SerializeField] private float inactivePenalty;
        [SerializeField] private float movementThreshold = 0.001f;
        [SerializeField] private float turnActivityThreshold = 0.3f;
        [SerializeField] private string presetName = "Smoke";

        private bool applyingPreset;

        public string PresetName => string.IsNullOrWhiteSpace(presetName) ? "Custom" : presetName;
        public int Timesteps { get => timesteps; set => SetValue(ref timesteps, Mathf.Max(1, value)); }
        public int MaxEpisodeSteps { get => maxEpisodeSteps; set => SetValue(ref maxEpisodeSteps, Mathf.Max(1, value)); }
        public int Seed { get => seed; set => SetValue(ref seed, value); }
        public int NSteps { get => nSteps; set => SetValue(ref nSteps, Mathf.Max(1, value)); }
        public int BatchSize { get => batchSize; set => SetValue(ref batchSize, Mathf.Max(1, value)); }
        public float Gamma { get => gamma; set => SetValue(ref gamma, Mathf.Clamp(value, 0.0001f, 1f)); }
        public float LearningRate { get => learningRate; set => SetValue(ref learningRate, Mathf.Max(0.000001f, value)); }
        public float EntCoef { get => entCoef; set => SetValue(ref entCoef, Mathf.Max(0f, value)); }
        public int EvalEpisodes { get => evalEpisodes; set => SetValue(ref evalEpisodes, Mathf.Max(1, value)); }
        public float GoalReachedReward { get => goalReachedReward; set => SetValue(ref goalReachedReward, value); }
        public float GoalProgressReward { get => goalProgressReward; set => SetValue(ref goalProgressReward, value); }
        public float CollisionPenalty { get => collisionPenalty; set => SetValue(ref collisionPenalty, value); }
        public float StepPenalty { get => stepPenalty; set => SetValue(ref stepPenalty, value); }
        public float MovementReward { get => movementReward; set => SetValue(ref movementReward, value); }
        public float WideAnglePenalty { get => wideAnglePenalty; set => SetValue(ref wideAnglePenalty, value); }
        public float RearAnglePenalty { get => rearAnglePenalty; set => SetValue(ref rearAnglePenalty, value); }
        public float InactivePenalty { get => inactivePenalty; set => SetValue(ref inactivePenalty, value); }
        public float MovementThreshold { get => movementThreshold; set => SetValue(ref movementThreshold, Mathf.Max(0f, value)); }
        public float TurnActivityThreshold { get => turnActivityThreshold; set => SetValue(ref turnActivityThreshold, Mathf.Max(0f, value)); }

        public void ApplySmokePreset()
        {
            applyingPreset = true;
            Timesteps = 5000;
            MaxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
            Seed = 10;
            NSteps = 32;
            BatchSize = 32;
            Gamma = 0.99f;
            LearningRate = 0.0003f;
            EntCoef = 0.0f;
            EvalEpisodes = 20;
            GoalReachedReward = 10.0f;
            GoalProgressReward = 0.5f;
            CollisionPenalty = -5.0f;
            StepPenalty = -0.01f;
            MovementReward = 0.0f;
            WideAnglePenalty = 0.0f;
            RearAnglePenalty = 0.0f;
            InactivePenalty = 0.0f;
            MovementThreshold = 0.001f;
            TurnActivityThreshold = 0.3f;
            applyingPreset = false;
            presetName = "Smoke";
        }

        public void ApplyMvpPreset()
        {
            applyingPreset = true;
            Timesteps = 1500000;
            MaxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
            Seed = 10;
            NSteps = 64;
            BatchSize = 64;
            Gamma = 0.99f;
            LearningRate = 0.0003f;
            EntCoef = 0.0005f;
            EvalEpisodes = 20;
            GoalReachedReward = 100.0f;
            GoalProgressReward = 0.1f;
            CollisionPenalty = -50.0f;
            StepPenalty = 0.0f;
            MovementReward = 0.01f;
            WideAnglePenalty = -0.1f;
            RearAnglePenalty = -5.0f;
            InactivePenalty = -0.1f;
            MovementThreshold = 0.001f;
            TurnActivityThreshold = 0.3f;
            applyingPreset = false;
            presetName = "MVP";
        }

        public void ApplyTo(NavigationScenarioBundleSource source)
        {
            source.TrainingTimesteps = Timesteps;
            source.MaxEpisodeSteps = MaxEpisodeSteps;
            source.Seed = Seed;
            source.NSteps = NSteps;
            source.BatchSize = BatchSize;
            source.Gamma = Gamma;
            source.LearningRate = LearningRate;
            source.EntCoef = EntCoef;
            source.EvalEpisodes = EvalEpisodes;
            source.GoalReachedReward = GoalReachedReward;
            source.GoalProgressReward = GoalProgressReward;
            source.CollisionPenalty = CollisionPenalty;
            source.StepPenalty = StepPenalty;
            source.MovementReward = MovementReward;
            source.WideAnglePenalty = WideAnglePenalty;
            source.RearAnglePenalty = RearAnglePenalty;
            source.InactivePenalty = InactivePenalty;
            source.MovementThreshold = MovementThreshold;
            source.TurnActivityThreshold = TurnActivityThreshold;
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
