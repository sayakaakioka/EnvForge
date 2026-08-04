using System;
using System.IO;
using System.Linq;
using EmbodiedLab.Contracts;
using EmbodiedLab.Unity;
using EnvForge.Navigation.Cloud;
using EnvForge.Navigation.Contracts;
using Newtonsoft.Json;
using UnityEngine;

namespace EnvForge.Editor
{
    public static class EnvForgeContractValidation
    {
        public static void Validate()
        {
            ScenarioBundle scenario = NavigationScenarioBundleBuilder.Build(
                NavigationScenarioBundleDefaults.CreateSource());
            string json = ScenarioBundleJson.Serialize(scenario, indented: true);
            ScenarioBundle roundTrip = ScenarioBundleJson.Deserialize(json);

            Require(
                roundTrip.World.CoordinateSystem == CoordinateSystem.LeftHandedYUpMeters,
                "Coordinate system is not the current contract value.");
            Require(roundTrip.World.StaticWalls.All(wall => NearlyEqual(wall.Height, 2d)),
                "Boundary wall height must be 2 meters.");
            Require(NearlyEqual(roundTrip.World.Goal.Radius, 0.45d), "Goal radius must be 0.45 meters.");
            Require(NearlyEqual(roundTrip.Robot.Radius, 0.45d), "Robot radius must be 0.45 meters.");
            Require(NearlyEqual(roundTrip.Robot.ActionSpace.ForwardStepMeters, 0.2d),
                "Forward step must come from the Scenario contract.");
            Require(NearlyEqual(roundTrip.Robot.ActionSpace.TurnDegreesPerStep, 15d),
                "Turn step must come from the Scenario contract.");
            Require(NearlyEqual(roundTrip.Robot.ActionSpace.StepDurationSeconds, 0.1d),
                "Step duration must come from the Scenario contract.");

            ForwardCameraSensor camera = roundTrip.Sensors.OfType<ForwardCameraSensor>().Single();
            GoalVectorSensor goal = roundTrip.Sensors.OfType<GoalVectorSensor>().Single();
            Require(camera.Width == 112 && camera.Height == 84 && camera.ObservationName == "obs_0",
                "Camera observation contract is inconsistent.");
            Require(goal.ObservationName == "obs_1" &&
                    goal.Values.SequenceEqual(new[] { Values.GoalAngleDegrees, Values.GoalDistanceMeters }),
                "Goal observation contract is inconsistent.");
            Require(roundTrip.Reward.Components.OfType<MinimumAbsoluteAngleRewardComponent>().Count() == 2,
                "Angle reward components must use their typed contract.");
            Require(roundTrip.Reward.Components.OfType<MaximumAbsoluteForwardRewardComponent>().Count() == 1,
                "Inactive reward must use its typed contract.");

            string[] explicitTrainingFields =
            {
                "\"device\"",
                "\"cpu_count\"",
                "\"torch_num_threads\"",
                "\"n_epochs\"",
                "\"gae_lambda\"",
                "\"clip_range\"",
                "\"clip_range_vf\"",
                "\"normalize_advantage\"",
                "\"vf_coef\"",
                "\"max_grad_norm\"",
                "\"use_sde\"",
                "\"sde_sample_freq\"",
                "\"target_kl\"",
                "\"stats_window_size\"",
                "\"replay_eval_interval_steps\"",
                "\"replay_train_chunk_steps\"",
                "\"randomize_start\"",
            };
            Require(explicitTrainingFields.All(json.Contains),
                "Scenario JSON omits one or more explicit training fields.");

            string repositoryRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            string fixtureRoot = Path.Combine(repositoryRoot, "fixtures");
            ScenarioBundle fixtureScenario = ScenarioBundleJson.Deserialize(File.ReadAllText(Path.Combine(
                fixtureRoot,
                "scenario-bundles",
                "navigation_default.json")));
            NavigationTrainingSettings settings = new();
            settings.ApplyFrom(fixtureScenario);
            NavigationScenarioBundleSource fixtureRoundTripSource = NavigationScenarioBundleDefaults.CreateSource();
            settings.ApplyTo(fixtureRoundTripSource);
            ScenarioBundle fixtureRoundTrip = NavigationScenarioBundleBuilder.Build(fixtureRoundTripSource);
            Require(fixtureRoundTrip.Training.CpuCount == fixtureScenario.Training.CpuCount,
                "CPU count must preserve the Scenario contract's explicit null or value.");
            Require(fixtureRoundTrip.Training.TorchNumThreads == fixtureScenario.Training.TorchNumThreads,
                "Torch thread count must preserve the Scenario contract's explicit null or value.");
            Require(fixtureRoundTrip.Training.ClipRangeVf == fixtureScenario.Training.ClipRangeVf,
                "PPO clip_range_vf must round-trip through EnvForge settings.");
            Require(fixtureRoundTrip.Training.TargetKl == fixtureScenario.Training.TargetKl,
                "PPO target_kl must round-trip through EnvForge settings.");

            settings.CpuCount = 3;
            settings.TorchNumThreads = 2;
            settings.ClipRangeVf = 0.15f;
            settings.TargetKl = 0.02f;
            settings.ApplyTo(fixtureRoundTripSource);
            ScenarioBundle explicitOptionalValues = NavigationScenarioBundleBuilder.Build(fixtureRoundTripSource);
            Require(explicitOptionalValues.Training.CpuCount == 3 &&
                    explicitOptionalValues.Training.TorchNumThreads == 2,
                "Explicit worker settings must flow into the Scenario contract.");
            Require(NearlyEqual(explicitOptionalValues.Training.ClipRangeVf ?? double.NaN, 0.15d) &&
                    NearlyEqual(explicitOptionalValues.Training.TargetKl ?? double.NaN, 0.02d),
                "Explicit optional PPO settings must flow into the Scenario contract.");

            settings.CpuCount = 99;
            settings.TorchNumThreads = 99;
            settings.GaeLambda = 0f;
            settings.ClipRange = 0f;
            settings.ClipRangeVf = -1f;
            settings.TargetKl = float.NaN;
            Require(settings.CpuCount == 32 && settings.TorchNumThreads == 32,
                "Worker settings must stay within the strict Scenario schema.");
            Require(settings.GaeLambda > 0f && settings.ClipRange > 0f &&
                    settings.ClipRangeVf > 0f && settings.TargetKl > 0f,
                "Positive PPO settings must stay valid for the strict Scenario schema.");
            Require(EmbodiedLabReplay.ReadSteps(Path.Combine(
                    fixtureRoot,
                    "replay-logs",
                    "navigation_default_replay.jsonl")).Count == 2,
                "Canonical Replay fixture did not contain two steps.");
            EmbodiedLabReplay.ReadManifest(
                Path.Combine(fixtureRoot, "replay-bundles", "navigation_default_manifest.json"),
                "submission-1",
                "navigation_default");
            bool rejectedWrongReplayIdentity = false;
            try
            {
                EmbodiedLabReplay.ReadManifest(
                    Path.Combine(fixtureRoot, "replay-bundles", "navigation_default_manifest.json"),
                    "wrong-submission",
                    "navigation_default");
            }
            catch (Exception)
            {
                rejectedWrongReplayIdentity = true;
            }

            Require(rejectedWrongReplayIdentity,
                "Replay manifest identity mismatch must be rejected.");

            ResultDocument result = JsonConvert.DeserializeObject<ResultDocument>(
                File.ReadAllText(Path.Combine(
                    fixtureRoot,
                    "result-documents",
                    "navigation_completed.json")),
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            Require(result?.ResultBundle?.Artifacts?.OnnxModel != null,
                "Canonical Result fixture is missing ONNX metadata.");

            Debug.Log("EnvForge contract validation passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static bool NearlyEqual(double actual, double expected)
        {
            return Math.Abs(actual - expected) <= 0.000001d;
        }
    }
}
