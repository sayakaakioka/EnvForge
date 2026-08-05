using System;
using System.Linq;
using EmbodiedLab.Contracts;
using EnvForge.Navigation.Contracts;
using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    [Serializable]
    public sealed class NavigationTrainingSettings
    {
        private const int MaxWorkerCount = 32;
        private const float MinimumPositiveValue = 0.000001f;

        [SerializeField] private int timesteps = 5000;
        [SerializeField] private int maxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
        [SerializeField] private long seed = 10;
        [SerializeField] private int nEnvs = 4;
        [SerializeField] private int cpuCount = 4;
        [SerializeField] private bool automaticCpuCount;
        [SerializeField] private int torchNumThreads = 2;
        [SerializeField] private bool automaticTorchNumThreads;
        [SerializeField] private int nSteps = 32;
        [SerializeField] private int batchSize = 32;
        [SerializeField] private int nEpochs = 3;
        [SerializeField] private float cameraMountHeightMeters = NavigationScenarioBundleDefaults.CameraMountHeightMeters;
        [SerializeField] private float cameraMountHeightMinMeters = NavigationScenarioBundleDefaults.CameraMountHeightMinMeters;
        [SerializeField] private float cameraMountHeightMaxMeters = NavigationScenarioBundleDefaults.CameraMountHeightMaxMeters;
        [SerializeField] private float cameraPitchDegrees = NavigationScenarioBundleDefaults.CameraPitchDegrees;
        [SerializeField] private float cameraVerticalFovDegrees = NavigationScenarioBundleDefaults.CameraVerticalFovDegrees;
        [SerializeField] private float cameraNearClipMeters = NavigationScenarioBundleDefaults.CameraNearClipMeters;
        [SerializeField] private float cameraFarClipMeters = NavigationScenarioBundleDefaults.CameraFarClipMeters;
        [SerializeField] private float gamma = 0.99f;
        [SerializeField] private float gaeLambda = 0.95f;
        [SerializeField] private float learningRate = 0.0003f;
        [SerializeField] private float clipRange = 0.2f;
        [SerializeField] private bool useClipRangeVf;
        [SerializeField] private float clipRangeVf;
        [SerializeField] private bool normalizeAdvantage = true;
        [SerializeField] private float entCoef;
        [SerializeField] private float vfCoef = 0.5f;
        [SerializeField] private float maxGradNorm = 0.5f;
        [SerializeField] private bool useSde;
        [SerializeField] private int sdeSampleFreq = -1;
        [SerializeField] private bool useTargetKl;
        [SerializeField] private float targetKl;
        [SerializeField] private int statsWindowSize = 100;
        [SerializeField] private int evalEpisodes = 20;
        [SerializeField] private int replayEvalIntervalSteps = 1000000;
        [SerializeField] private int replayTrainChunkSteps = 10000;
        [SerializeField] private bool randomizeStart = true;
        [SerializeField] private float goalReachedReward = 100f;
        [SerializeField] private float goalProgressReward = 0.1f;
        [SerializeField] private float goalProgressMinimumDeltaMeters = NavigationScenarioBundleDefaults.GoalProgressMinimumDeltaMeters;
        [SerializeField] private float collisionPenalty = -50f;
        [SerializeField] private float stepPenalty = -0.01f;
        [SerializeField] private float wideAnglePenalty = -0.1f;
        [SerializeField] private float wideAngleMinimumDegrees = NavigationScenarioBundleDefaults.WideAngleMinimumDegrees;
        [SerializeField] private float rearAnglePenalty = -5f;
        [SerializeField] private float rearAngleMinimumDegrees = NavigationScenarioBundleDefaults.RearAngleMinimumDegrees;
        [SerializeField] private float inactivePenalty = -0.1f;
        [SerializeField] private float movementThreshold = 0.001f;
        [SerializeField] private string presetName = "Smoke";

        private bool applyingPreset;

        public string PresetName => string.IsNullOrWhiteSpace(presetName) ? "Custom" : presetName;
        public int Timesteps { get => timesteps; set => SetValue(ref timesteps, Mathf.Clamp(value, 1, 10000000)); }
        public int MaxEpisodeSteps { get => maxEpisodeSteps; set => SetValue(ref maxEpisodeSteps, Mathf.Clamp(value, 1, 100000)); }
        public long Seed { get => seed; set => SetValue(ref seed, Math.Max(0L, Math.Min(4294967295L, value))); }
        public int NEnvs { get => nEnvs <= 0 ? 4 : nEnvs; set => SetValue(ref nEnvs, Mathf.Clamp(value, 1, MaxWorkerCount)); }
        public int? CpuCount
        {
            get => automaticCpuCount ? null : (cpuCount <= 0 ? 4 : cpuCount);
            set => SetOptionalPositiveInt(ref automaticCpuCount, ref cpuCount, value, 4);
        }

        public int? TorchNumThreads
        {
            get => automaticTorchNumThreads ? null : (torchNumThreads <= 0 ? 2 : torchNumThreads);
            set => SetOptionalPositiveInt(ref automaticTorchNumThreads, ref torchNumThreads, value, 2);
        }
        public int NSteps { get => nSteps; set => SetValue(ref nSteps, Mathf.Clamp(value, 1, 65536)); }
        public int BatchSize { get => batchSize; set => SetValue(ref batchSize, Mathf.Clamp(value, 1, 65536)); }
        public int NEpochs { get => nEpochs; set => SetValue(ref nEpochs, Mathf.Clamp(value, 1, 100)); }
        public float CameraMountHeightMeters { get => cameraMountHeightMeters; set => SetValue(ref cameraMountHeightMeters, Mathf.Max(0.001f, value)); }
        public float CameraMountHeightMinMeters { get => cameraMountHeightMinMeters; set => SetValue(ref cameraMountHeightMinMeters, Mathf.Max(0.001f, value)); }
        public float CameraMountHeightMaxMeters { get => cameraMountHeightMaxMeters; set => SetValue(ref cameraMountHeightMaxMeters, Mathf.Max(0.001f, value)); }
        public float CameraPitchDegrees { get => cameraPitchDegrees; set => SetValue(ref cameraPitchDegrees, value); }
        public float CameraVerticalFovDegrees { get => cameraVerticalFovDegrees; set => SetValue(ref cameraVerticalFovDegrees, Mathf.Clamp(value, 1f, 179f)); }
        public float CameraNearClipMeters { get => cameraNearClipMeters; set => SetValue(ref cameraNearClipMeters, Mathf.Max(0.0001f, value)); }
        public float CameraFarClipMeters { get => cameraFarClipMeters; set => SetValue(ref cameraFarClipMeters, Mathf.Max(CameraNearClipMeters, value)); }
        public float Gamma { get => gamma; set => SetValue(ref gamma, ClampFinite(value, MinimumPositiveValue, 1f, 0.99f)); }
        public float GaeLambda { get => gaeLambda; set => SetValue(ref gaeLambda, ClampFinite(value, MinimumPositiveValue, 1f, 0.95f)); }
        public float LearningRate { get => learningRate; set => SetValue(ref learningRate, AtLeastFinite(value, MinimumPositiveValue, 0.0003f)); }
        public float ClipRange { get => clipRange; set => SetValue(ref clipRange, AtLeastFinite(value, MinimumPositiveValue, 0.2f)); }
        public float? ClipRangeVf
        {
            get => useClipRangeVf ? clipRangeVf : null;
            set => SetOptionalPositiveFloat(ref useClipRangeVf, ref clipRangeVf, value);
        }
        public bool NormalizeAdvantage { get => normalizeAdvantage; set => SetValue(ref normalizeAdvantage, value); }
        public float EntCoef { get => entCoef; set => SetValue(ref entCoef, Mathf.Max(0f, value)); }
        public float VfCoef { get => vfCoef; set => SetValue(ref vfCoef, Mathf.Max(0f, value)); }
        public float MaxGradNorm { get => maxGradNorm; set => SetValue(ref maxGradNorm, Mathf.Max(0f, value)); }
        public bool UseSde { get => useSde; set => SetValue(ref useSde, value); }
        public int SdeSampleFreq { get => sdeSampleFreq; set => SetValue(ref sdeSampleFreq, Mathf.Max(-1, value)); }
        public float? TargetKl
        {
            get => useTargetKl ? targetKl : null;
            set => SetOptionalPositiveFloat(ref useTargetKl, ref targetKl, value);
        }
        public int StatsWindowSize { get => statsWindowSize; set => SetValue(ref statsWindowSize, Mathf.Clamp(value, 1, 100000)); }
        public int EvalEpisodes { get => evalEpisodes; set => SetValue(ref evalEpisodes, Mathf.Clamp(value, 1, 100000)); }
        public int ReplayEvalIntervalSteps { get => replayEvalIntervalSteps; set => SetValue(ref replayEvalIntervalSteps, Mathf.Max(0, value)); }
        public int ReplayTrainChunkSteps { get => replayTrainChunkSteps; set => SetValue(ref replayTrainChunkSteps, Mathf.Clamp(value, 1, 100000)); }
        public bool RandomizeStart { get => randomizeStart; set => SetValue(ref randomizeStart, value); }
        public float GoalReachedReward { get => goalReachedReward; set => SetValue(ref goalReachedReward, value); }
        public float GoalProgressReward { get => goalProgressReward; set => SetValue(ref goalProgressReward, value); }
        public float GoalProgressMinimumDeltaMeters { get => goalProgressMinimumDeltaMeters; set => SetValue(ref goalProgressMinimumDeltaMeters, Mathf.Max(0f, value)); }
        public float CollisionPenalty { get => collisionPenalty; set => SetValue(ref collisionPenalty, value); }
        public float StepPenalty { get => stepPenalty; set => SetValue(ref stepPenalty, value); }
        public float WideAnglePenalty { get => wideAnglePenalty; set => SetValue(ref wideAnglePenalty, value); }
        public float WideAngleMinimumDegrees { get => wideAngleMinimumDegrees; set => SetValue(ref wideAngleMinimumDegrees, Mathf.Clamp(value, 0f, 180f)); }
        public float RearAnglePenalty { get => rearAnglePenalty; set => SetValue(ref rearAnglePenalty, value); }
        public float RearAngleMinimumDegrees { get => rearAngleMinimumDegrees; set => SetValue(ref rearAngleMinimumDegrees, Mathf.Clamp(value, 0f, 180f)); }
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
            NEpochs = 3;
            CameraMountHeightMeters = NavigationScenarioBundleDefaults.CameraMountHeightMeters;
            CameraMountHeightMinMeters = NavigationScenarioBundleDefaults.CameraMountHeightMinMeters;
            CameraMountHeightMaxMeters = NavigationScenarioBundleDefaults.CameraMountHeightMaxMeters;
            Gamma = 0.99f;
            GaeLambda = 0.95f;
            LearningRate = 0.0003f;
            ClipRange = 0.2f;
            ClipRangeVf = null;
            NormalizeAdvantage = true;
            EntCoef = 0.0f;
            VfCoef = 0.5f;
            MaxGradNorm = 0.5f;
            UseSde = false;
            SdeSampleFreq = -1;
            TargetKl = null;
            StatsWindowSize = 100;
            EvalEpisodes = 20;
            ReplayEvalIntervalSteps = 1000000;
            ReplayTrainChunkSteps = 10000;
            RandomizeStart = true;
            GoalReachedReward = 100.0f;
            GoalProgressReward = 0.1f;
            GoalProgressMinimumDeltaMeters = NavigationScenarioBundleDefaults.GoalProgressMinimumDeltaMeters;
            CollisionPenalty = -50.0f;
            StepPenalty = -0.01f;
            WideAnglePenalty = -0.1f;
            WideAngleMinimumDegrees = NavigationScenarioBundleDefaults.WideAngleMinimumDegrees;
            RearAnglePenalty = -5.0f;
            RearAngleMinimumDegrees = NavigationScenarioBundleDefaults.RearAngleMinimumDegrees;
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
            NEpochs = 3;
            CameraMountHeightMeters = NavigationScenarioBundleDefaults.CameraMountHeightMeters;
            CameraMountHeightMinMeters = NavigationScenarioBundleDefaults.CameraMountHeightMinMeters;
            CameraMountHeightMaxMeters = NavigationScenarioBundleDefaults.CameraMountHeightMaxMeters;
            Gamma = 0.99f;
            GaeLambda = 0.95f;
            LearningRate = 0.0003f;
            ClipRange = 0.2f;
            ClipRangeVf = null;
            NormalizeAdvantage = true;
            EntCoef = 0.0005f;
            VfCoef = 0.5f;
            MaxGradNorm = 0.5f;
            UseSde = false;
            SdeSampleFreq = -1;
            TargetKl = null;
            StatsWindowSize = 100;
            EvalEpisodes = 20;
            ReplayEvalIntervalSteps = 1000000;
            ReplayTrainChunkSteps = 10000;
            RandomizeStart = true;
            GoalReachedReward = 100.0f;
            GoalProgressReward = 0.1f;
            GoalProgressMinimumDeltaMeters = NavigationScenarioBundleDefaults.GoalProgressMinimumDeltaMeters;
            CollisionPenalty = -50.0f;
            StepPenalty = -0.01f;
            WideAnglePenalty = -0.1f;
            WideAngleMinimumDegrees = NavigationScenarioBundleDefaults.WideAngleMinimumDegrees;
            RearAnglePenalty = -5.0f;
            RearAngleMinimumDegrees = NavigationScenarioBundleDefaults.RearAngleMinimumDegrees;
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
            source.NEpochs = NEpochs;
            source.CameraMountHeightMeters = CameraMountHeightMeters;
            source.CameraMountHeightMinMeters = Mathf.Min(CameraMountHeightMinMeters, CameraMountHeightMaxMeters);
            source.CameraMountHeightMaxMeters = Mathf.Max(CameraMountHeightMinMeters, CameraMountHeightMaxMeters);
            source.CameraPitchDegrees = CameraPitchDegrees;
            source.CameraVerticalFovDegrees = CameraVerticalFovDegrees;
            source.CameraNearClipMeters = CameraNearClipMeters;
            source.CameraFarClipMeters = CameraFarClipMeters;
            source.Gamma = Gamma;
            source.GaeLambda = GaeLambda;
            source.LearningRate = LearningRate;
            source.ClipRange = ClipRange;
            source.ClipRangeVf = ClipRangeVf;
            source.NormalizeAdvantage = NormalizeAdvantage;
            source.EntCoef = EntCoef;
            source.VfCoef = VfCoef;
            source.MaxGradNorm = MaxGradNorm;
            source.UseSde = UseSde;
            source.SdeSampleFreq = SdeSampleFreq;
            source.TargetKl = TargetKl;
            source.StatsWindowSize = StatsWindowSize;
            source.EvalEpisodes = EvalEpisodes;
            source.ReplayEvalIntervalSteps = ReplayEvalIntervalSteps;
            source.ReplayTrainChunkSteps = ReplayTrainChunkSteps;
            source.RandomizeStart = RandomizeStart;
            source.GoalReachedReward = GoalReachedReward;
            source.GoalProgressReward = GoalProgressReward;
            source.GoalProgressMinimumDeltaMeters = GoalProgressMinimumDeltaMeters;
            source.CollisionPenalty = CollisionPenalty;
            source.StepPenalty = StepPenalty;
            source.WideAnglePenalty = WideAnglePenalty;
            source.WideAngleMinimumDegrees = WideAngleMinimumDegrees;
            source.RearAnglePenalty = RearAnglePenalty;
            source.RearAngleMinimumDegrees = RearAngleMinimumDegrees;
            source.InactivePenalty = InactivePenalty;
            source.MovementThreshold = MovementThreshold;
        }

        public void ApplyFrom(ScenarioBundle scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            applyingPreset = true;
            try
            {
                TrainingSpec training = scenario.Training;
                if (training != null)
                {
                    Timesteps = training.Timesteps;
                    MaxEpisodeSteps = training.MaxEpisodeSteps;
                    Seed = training.Seed;
                    NEnvs = training.NEnvs;
                    CpuCount = training.CpuCount;
                    TorchNumThreads = training.TorchNumThreads;
                    NSteps = training.NSteps;
                    BatchSize = training.BatchSize;
                    NEpochs = training.NEpochs;
                    Gamma = (float)training.Gamma;
                    GaeLambda = (float)training.GaeLambda;
                    LearningRate = (float)training.LearningRate;
                    ClipRange = (float)training.ClipRange;
                    ClipRangeVf = training.ClipRangeVf.HasValue ? (float?)training.ClipRangeVf.Value : null;
                    NormalizeAdvantage = training.NormalizeAdvantage;
                    EntCoef = (float)training.EntCoef;
                    VfCoef = (float)training.VfCoef;
                    MaxGradNorm = (float)training.MaxGradNorm;
                    UseSde = training.UseSde;
                    SdeSampleFreq = training.SdeSampleFreq;
                    TargetKl = training.TargetKl.HasValue ? (float?)training.TargetKl.Value : null;
                    StatsWindowSize = training.StatsWindowSize;
                    EvalEpisodes = training.EvalEpisodes;
                    ReplayEvalIntervalSteps = training.ReplayEvalIntervalSteps;
                    ReplayTrainChunkSteps = training.ReplayTrainChunkSteps;
                    RandomizeStart = training.RandomizeStart;
                }

                ForwardCameraSensor camera = scenario.Sensors?.OfType<ForwardCameraSensor>().SingleOrDefault();
                if (camera != null)
                {
                    CameraMountHeightMeters = (float)camera.MountHeightMeters;
                    CameraMountHeightMinMeters = (float)(camera.MountHeightMinMeters ?? camera.MountHeightMeters);
                    CameraMountHeightMaxMeters = (float)(camera.MountHeightMaxMeters ?? camera.MountHeightMeters);
                    CameraPitchDegrees = (float)camera.PitchDegrees;
                    CameraVerticalFovDegrees = (float)camera.VerticalFovDegrees;
                    CameraNearClipMeters = (float)camera.NearClipMeters;
                    CameraFarClipMeters = (float)camera.FarClipMeters;
                }

                if (scenario.Reward?.Components != null)
                {
                    foreach (RewardComponent component in scenario.Reward.Components)
                    {
                        switch (component)
                        {
                            case TerminalRewardComponent terminal when terminal.Name == "goal_reached":
                                GoalReachedReward = (float)terminal.Weight;
                                break;
                            case DistanceDeltaRewardComponent progress when progress.Name == "goal_progress":
                                GoalProgressReward = (float)progress.Weight;
                                GoalProgressMinimumDeltaMeters = (float)progress.MinimumDeltaMeters;
                                break;
                            case CollisionRewardComponent collision when collision.Name == "collision_penalty":
                                CollisionPenalty = (float)collision.Weight;
                                break;
                            case PerStepRewardComponent perStep when perStep.Name == "step_penalty":
                                StepPenalty = (float)perStep.Weight;
                                break;
                            case MinimumAbsoluteAngleRewardComponent wide when wide.Name == "wide_angle_penalty":
                                WideAnglePenalty = (float)wide.Weight;
                                WideAngleMinimumDegrees = (float)wide.MinimumAbsoluteAngleDegrees;
                                break;
                            case MinimumAbsoluteAngleRewardComponent rear when rear.Name == "rear_angle_penalty":
                                RearAnglePenalty = (float)rear.Weight;
                                RearAngleMinimumDegrees = (float)rear.MinimumAbsoluteAngleDegrees;
                                break;
                            case MaximumAbsoluteForwardRewardComponent inactive when inactive.Name == "inactive_penalty":
                                InactivePenalty = (float)inactive.Weight;
                                MovementThreshold = (float)inactive.MaximumAbsoluteForward;
                                break;
                        }
                    }
                }

                presetName = "Scenario";
            }
            finally
            {
                applyingPreset = false;
            }
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

        private void SetValue(ref long target, long value)
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

        private void SetValue(ref bool target, bool value)
        {
            if (target == value)
            {
                return;
            }

            target = value;
            MarkCustom();
        }

        private void SetOptionalPositiveInt(ref bool automatic, ref int target, int? value, int fallback)
        {
            bool nextAutomatic = !value.HasValue;
            int nextValue = value.HasValue
                ? Mathf.Clamp(value.Value, 1, MaxWorkerCount)
                : (target <= 0 ? fallback : target);
            if (automatic == nextAutomatic && target == nextValue)
            {
                return;
            }

            automatic = nextAutomatic;
            target = nextValue;
            MarkCustom();
        }

        private void SetOptionalPositiveFloat(ref bool enabled, ref float target, float? value)
        {
            bool nextEnabled = value.HasValue;
            float nextValue = value.HasValue
                ? AtLeastFinite(value.Value, MinimumPositiveValue, MinimumPositiveValue)
                : target;
            if (enabled == nextEnabled && Mathf.Approximately(target, nextValue))
            {
                return;
            }

            enabled = nextEnabled;
            target = nextValue;
            MarkCustom();
        }

        private static float ClampFinite(float value, float minimum, float maximum, float fallback)
        {
            return IsFinite(value) ? Mathf.Clamp(value, minimum, maximum) : fallback;
        }

        private static float AtLeastFinite(float value, float minimum, float fallback)
        {
            return IsFinite(value) ? Mathf.Max(minimum, value) : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

    }
}
