using System;
using System.Collections;
using System.IO;
using EnvForge.Navigation.Inference;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvForge.Navigation.Automation
{
    public sealed class NavigationModelEvaluationAutomation : MonoBehaviour, INavigationEpisodeEvents
    {
        private const string ModelPathArgument = "-envforgeEvaluateModel";
        private const string OutputArgument = "-envforgeEvaluationOutput";
        private const string EpisodesArgument = "-envforgeEvaluationEpisodes";
        private const string MaxStepsArgument = "-envforgeEvaluationMaxSteps";
        private const string StepSecondsArgument = "-envforgeEvaluationStepSeconds";
        private const string GoalRadiusArgument = "-envforgeEvaluationGoalRadius";
        private const string SeedArgument = "-envforgeEvaluationSeed";
        private const string StartXArgument = "-envforgeEvaluationStartX";
        private const string StartZArgument = "-envforgeEvaluationStartZ";
        private const string StartYawArgument = "-envforgeEvaluationStartYaw";
        private const string RandomStartArgument = "-envforgeEvaluationRandomStart";
        private const string CameraHeightArgument = "-envforgeEvaluationCameraHeight";
        private const string VariantArgument = "-envforgeWorldVariant";

        private string modelPath;
        private string outputPath;
        private int episodeCount = 20;
        private int maxEpisodeSteps = 1000;
        private float fixedStepSeconds = 0.1f;
        private float goalRadius = 1.2f;
        private int evaluationSeed = 10;
        private bool hasFixedStartPose;
        private bool useRandomStartPose;
        private Vector3 fixedStartPosition;
        private Quaternion fixedStartRotation;
        private bool hasFixedCameraHeight;
        private float fixedCameraHeightMeters;
        private string worldVariant = "default";
        private EpisodeOutcome pendingOutcome = EpisodeOutcome.None;
        private string pendingCollisionWallId = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartFromCommandLine()
        {
            string model = NavigationAutomationArguments.GetFullPath(ModelPathArgument);
            string output = NavigationAutomationArguments.GetFullPath(OutputArgument);
            if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            Debug.Log($"EnvForge model evaluation requested: model={model}, output={output}");
            GameObject runner = new("EnvForge Model Evaluation Automation");
            DontDestroyOnLoad(runner);
            NavigationModelEvaluationAutomation automation = runner.AddComponent<NavigationModelEvaluationAutomation>();
            automation.modelPath = model;
            automation.outputPath = output;
            automation.episodeCount = NavigationAutomationArguments.GetInt(EpisodesArgument, 20, 1);
            automation.maxEpisodeSteps = NavigationAutomationArguments.GetInt(MaxStepsArgument, 1000, 1);
            automation.fixedStepSeconds = NavigationAutomationArguments.GetFloat(StepSecondsArgument, 0.1f, 0.001f);
            automation.goalRadius = NavigationAutomationArguments.GetFloat(GoalRadiusArgument, 1.2f, 0.001f);
            automation.evaluationSeed = NavigationAutomationArguments.GetInt(SeedArgument, 10, int.MinValue);
            automation.worldVariant = NavigationAutomationArguments.GetValue(VariantArgument) ?? "default";
            automation.TryConfigureFixedStartPose();
            automation.useRandomStartPose = NavigationAutomationArguments.HasFlag(RandomStartArgument);
            automation.TryConfigureFixedCameraHeight();
        }

        private void Start()
        {
            StartCoroutine(RunEvaluation());
        }

        public void ReportGoalReached()
        {
            if (pendingOutcome == EpisodeOutcome.None)
            {
                pendingOutcome = EpisodeOutcome.Goal;
            }
        }

        public void ReportWallCollision()
        {
            ReportWallCollision(string.Empty);
        }

        public void ReportWallCollision(string wallId)
        {
            if (pendingOutcome == EpisodeOutcome.None)
            {
                pendingOutcome = EpisodeOutcome.Collision;
                pendingCollisionWallId = wallId;
            }
        }

        private IEnumerator RunEvaluation()
        {
            Time.fixedDeltaTime = fixedStepSeconds;
            yield return null;
            yield return new WaitUntil(() => SceneManager.GetActiveScene().isLoaded);
            yield return new WaitForSeconds(0.5f);

            NavigationModelInferenceController inferenceController = FindFirstObjectByType<NavigationModelInferenceController>();
            NavigationLiveController liveController = FindFirstObjectByType<NavigationLiveController>();
            AgentMotor motor = FindFirstObjectByType<AgentMotor>();
            NavigationEpisodeEventHub eventHub = FindFirstObjectByType<NavigationEpisodeEventHub>();
            NavigationMetrics metrics = FindFirstObjectByType<NavigationMetrics>();
            NavigationSceneBuilder sceneBuilder = FindFirstObjectByType<NavigationSceneBuilder>();
            NavigationWorldVariantAutomation.Apply(sceneBuilder, worldVariant);

            EvaluationResult result = new()
            {
                model_path = modelPath,
                world_variant = worldVariant,
                requested_episodes = episodeCount,
                max_episode_steps = maxEpisodeSteps,
                fixed_step_seconds = fixedStepSeconds,
                seed = evaluationSeed,
                episode_summaries = new string[episodeCount],
            };

            if (inferenceController == null || liveController == null || motor == null || eventHub == null || metrics == null)
            {
                result.status = "failed";
                result.error = "Evaluation dependencies were not found in the active scene.";
                WriteResult(result);
                Application.Quit(1);
                yield break;
            }

            eventHub.SetOverrideSink(this);
            System.Random rng = new(evaluationSeed);
            ApplyEpisodePose(liveController, metrics, sceneBuilder, rng);
            if (!inferenceController.StartInference(modelPath, out string error))
            {
                eventHub.ClearOverrideSink(this);
                result.status = "failed";
                result.error = error;
                WriteResult(result);
                Application.Quit(1);
                yield break;
            }

            motor.SetAutomationStepping(true);
            for (int episodeIndex = 0; episodeIndex < episodeCount; episodeIndex++)
            {
                pendingOutcome = EpisodeOutcome.None;
                pendingCollisionWallId = string.Empty;
                ApplyEpisodeCameraHeight(inferenceController, sceneBuilder, rng);
                AccumulateCameraHeight(result, inferenceController.CameraMountHeightMeters);
                ApplyEpisodePose(liveController, metrics, sceneBuilder, rng);
                string episodeStartSummary =
                    $"start pos ({liveController.transform.position.x:0.00}, {liveController.transform.position.z:0.00}) " +
                    $"rot {liveController.transform.eulerAngles.y:0.0} camera {inferenceController.CameraMountHeightMeters:0.00}";
                yield return new WaitForFixedUpdate();

                int stepCount = 0;
                while (pendingOutcome == EpisodeOutcome.None && stepCount < maxEpisodeSteps)
                {
                    stepCount++;
                    inferenceController.StepInferenceForAutomation();
                    if (stepCount == 1)
                    {
                        result.first_action_summary = inferenceController.LastActionSummary;
                        result.first_observation_summary = inferenceController.LastObservationSummary;
                        result.first_image_observation_summary = inferenceController.LastImageObservationSummary;
                        result.first_camera_height_meters = inferenceController.CameraMountHeightMeters;
                        result.first_image_observation_png = SaveFirstImageObservation(inferenceController);
                    }

                    motor.StepMotionForAutomation(fixedStepSeconds);
                    if (metrics.DistanceToGoal <= goalRadius)
                    {
                        pendingOutcome = EpisodeOutcome.Goal;
                    }

                    yield return new WaitForFixedUpdate();
                }

                EpisodeOutcome outcome = pendingOutcome == EpisodeOutcome.None ? EpisodeOutcome.Timeout : pendingOutcome;
                if (episodeIndex == 0)
                {
                    result.first_outcome_summary =
                        $"{outcome} {FormatWallId(pendingCollisionWallId)}at step {stepCount} pos ({liveController.transform.position.x:0.00}, {liveController.transform.position.z:0.00}) " +
                        $"rot {liveController.transform.eulerAngles.y:0.0} dist {metrics.DistanceToGoal:0.00}";
                }

                result.episode_summaries[episodeIndex] =
                    $"{episodeIndex + 1}: {episodeStartSummary} -> {outcome} {FormatWallId(pendingCollisionWallId)}" +
                    $"step {stepCount} pos ({liveController.transform.position.x:0.00}, {liveController.transform.position.z:0.00}) " +
                    $"rot {liveController.transform.eulerAngles.y:0.0} dist {metrics.DistanceToGoal:0.00}";
                RecordEpisode(result, outcome, stepCount, metrics.DistanceToGoal);
            }

            motor.SetAutomationStepping(false);
            inferenceController.StopInference();
            eventHub.ClearOverrideSink(this);
            result.status = "completed";
            result.last_action_summary = inferenceController.LastActionSummary;
            result.last_observation_summary = inferenceController.LastObservationSummary;
            result.last_image_observation_summary = inferenceController.LastImageObservationSummary;
            result.success_rate = result.completed_episodes > 0
                ? (float)result.goal_reached_episodes / result.completed_episodes
                : 0f;
            result.average_steps = result.completed_episodes > 0
                ? (float)result.total_steps / result.completed_episodes
                : 0f;
            result.average_camera_height_meters = result.camera_height_samples > 0
                ? result.camera_height_sum_meters / result.camera_height_samples
                : 0f;
            WriteResult(result);
            Application.Quit(result.success_rate >= 0.95f ? 0 : 2);
        }

        private static void AccumulateCameraHeight(EvaluationResult result, float cameraHeightMeters)
        {
            result.camera_height_samples++;
            result.camera_height_sum_meters += cameraHeightMeters;
            if (result.camera_height_samples == 1)
            {
                result.min_camera_height_meters = cameraHeightMeters;
                result.max_camera_height_meters = cameraHeightMeters;
                return;
            }

            result.min_camera_height_meters = Mathf.Min(result.min_camera_height_meters, cameraHeightMeters);
            result.max_camera_height_meters = Mathf.Max(result.max_camera_height_meters, cameraHeightMeters);
        }

        private void TryConfigureFixedStartPose()
        {
            string startXText = NavigationAutomationArguments.GetValue(StartXArgument);
            string startZText = NavigationAutomationArguments.GetValue(StartZArgument);
            string startYawText = NavigationAutomationArguments.GetValue(StartYawArgument);
            if (!float.TryParse(startXText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float startX) ||
                !float.TryParse(startZText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float startZ) ||
                !float.TryParse(startYawText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float startYaw))
            {
                return;
            }

            hasFixedStartPose = true;
            fixedStartPosition = new Vector3(startX, Navigation.Contracts.NavigationScenarioBundleDefaults.AgentStartPosition.y, startZ);
            fixedStartRotation = Quaternion.Euler(0f, startYaw, 0f);
        }

        private void TryConfigureFixedCameraHeight()
        {
            string heightText = NavigationAutomationArguments.GetValue(CameraHeightArgument);
            if (!float.TryParse(heightText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float cameraHeight))
            {
                return;
            }

            hasFixedCameraHeight = true;
            fixedCameraHeightMeters = Mathf.Max(0.001f, cameraHeight);
        }

        private void ApplyEpisodeCameraHeight(
            NavigationModelInferenceController inferenceController,
            NavigationSceneBuilder sceneBuilder,
            System.Random rng)
        {
            if (inferenceController == null)
            {
                return;
            }

            if (hasFixedCameraHeight)
            {
                inferenceController.SetCameraMountHeightMeters(fixedCameraHeightMeters);
                return;
            }

            if (sceneBuilder == null || rng == null || sceneBuilder.TrainingSettings == null)
            {
                return;
            }

            float minHeight = Mathf.Min(
                sceneBuilder.TrainingSettings.CameraMountHeightMinMeters,
                sceneBuilder.TrainingSettings.CameraMountHeightMaxMeters);
            float maxHeight = Mathf.Max(
                sceneBuilder.TrainingSettings.CameraMountHeightMinMeters,
                sceneBuilder.TrainingSettings.CameraMountHeightMaxMeters);
            float sampledHeight = Lerp(minHeight, maxHeight, (float)rng.NextDouble());
            inferenceController.SetCameraMountHeightMeters(sampledHeight);
        }

        private void ApplyEpisodePose(
            NavigationLiveController liveController,
            NavigationMetrics metrics,
            NavigationSceneBuilder sceneBuilder,
            System.Random rng)
        {
            if (hasFixedStartPose)
            {
                liveController?.SetResetPose(fixedStartPosition, fixedStartRotation);
                return;
            }

            if (useRandomStartPose)
            {
                ApplyRandomEpisodePose(liveController, metrics, sceneBuilder, rng);
                return;
            }

            liveController?.SetResetPose(
                Navigation.Contracts.NavigationScenarioBundleDefaults.AgentStartPosition,
                Navigation.Contracts.NavigationScenarioBundleDefaults.AgentStartRotation);
        }

        private void ApplyRandomEpisodePose(
            NavigationLiveController liveController,
            NavigationMetrics metrics,
            NavigationSceneBuilder sceneBuilder,
            System.Random rng)
        {
            if (liveController == null || sceneBuilder == null || rng == null)
            {
                liveController?.ResetPose();
                return;
            }

            Vector2 floorSize = sceneBuilder.FloorSize;
            float halfWidth = Mathf.Max(0.5f, floorSize.x * 0.5f - sceneBuilder.WallThickness - 0.75f);
            float halfDepth = Mathf.Max(0.5f, floorSize.y * 0.5f - sceneBuilder.WallThickness - 0.75f);
            Vector3 currentPosition = liveController.transform.position;
            Quaternion currentRotation = liveController.transform.rotation;
            Collider agentCollider = liveController.GetComponent<Collider>();

            for (int attempt = 0; attempt < 80; attempt++)
            {
                float x = Lerp(-halfWidth, halfWidth, (float)rng.NextDouble());
                float z = Lerp(-halfDepth, halfDepth, (float)rng.NextDouble());
                float yaw = Lerp(-180f, 180f, (float)rng.NextDouble());
                Vector3 candidatePosition = new(x, currentPosition.y, z);
                if (OverlapsBlockingGeometry(candidatePosition, 0.45f, agentCollider))
                {
                    continue;
                }

                Quaternion candidateRotation = Quaternion.Euler(0f, yaw, 0f);
                liveController.SetResetPose(candidatePosition, candidateRotation);

                if (metrics == null || metrics.DistanceToGoal > sceneBuilder.GoalReachRadius * 1.75f)
                {
                    return;
                }
            }

            liveController.SetResetPose(currentPosition, currentRotation);
        }

        private static bool OverlapsBlockingGeometry(Vector3 position, float radius, Collider ignoredCollider)
        {
            Collider[] overlaps = Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < overlaps.Length; index++)
            {
                Collider overlap = overlaps[index];
                if (overlap != null && overlap != ignoredCollider)
                {
                    return true;
                }
            }

            return false;
        }

        private static float Lerp(float min, float max, float t)
        {
            return min + (max - min) * Mathf.Clamp01(t);
        }

        private static string FormatWallId(string wallId)
        {
            return string.IsNullOrWhiteSpace(wallId) ? string.Empty : $"wall {wallId} ";
        }

        private static void RecordEpisode(EvaluationResult result, EpisodeOutcome outcome, int steps, float finalDistance)
        {
            result.completed_episodes++;
            result.total_steps += steps;
            result.final_distance_sum += finalDistance;
            switch (outcome)
            {
                case EpisodeOutcome.Goal:
                    result.goal_reached_episodes++;
                    break;
                case EpisodeOutcome.Collision:
                    result.collision_episodes++;
                    break;
                case EpisodeOutcome.Timeout:
                    result.timeout_episodes++;
                    break;
            }
        }

        private void WriteResult(EvaluationResult result)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonUtility.ToJson(result, prettyPrint: true));
            Debug.Log($"EnvForge evaluation result saved: {outputPath}");
        }

        private string SaveFirstImageObservation(NavigationModelInferenceController inferenceController)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            string fileName = Path.GetFileNameWithoutExtension(outputPath) + "-first-observation.png";
            string imagePath = Path.Combine(directory, fileName);
            return inferenceController.SaveLastImageObservationPng(imagePath) ? imagePath : string.Empty;
        }

        private enum EpisodeOutcome
        {
            None,
            Goal,
            Collision,
            Timeout,
        }

        [Serializable]
        private sealed class EvaluationResult
        {
            public string status;
            public string error;
            public string model_path;
            public string world_variant;
            public int requested_episodes;
            public int completed_episodes;
            public int goal_reached_episodes;
            public int collision_episodes;
            public int timeout_episodes;
            public int max_episode_steps;
            public int total_steps;
            public int seed;
            public float fixed_step_seconds;
            public float success_rate;
            public float average_steps;
            public float final_distance_sum;
            public int camera_height_samples;
            public float camera_height_sum_meters;
            public float average_camera_height_meters;
            public float min_camera_height_meters;
            public float max_camera_height_meters;
            public float first_camera_height_meters;
            public string first_action_summary;
            public string first_observation_summary;
            public string first_image_observation_summary;
            public string first_image_observation_png;
            public string first_outcome_summary;
            public string[] episode_summaries;
            public string last_action_summary;
            public string last_observation_summary;
            public string last_image_observation_summary;
        }
    }
}
