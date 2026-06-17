using System;
using System.Collections;
using System.IO;
using EnvForge.Navigation.Cloud;
using EnvForge.Navigation.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvForge.Navigation.Automation
{
    public sealed class NavigationCloudJobAutomation : MonoBehaviour
    {
        private const string SubmitArgument = "-envforgeSubmitJob";
        private const string ApiBaseUrlArgument = "-envforgeApiBaseUrl";
        private const string ScenarioIdArgument = "-envforgeScenarioId";
        private const string PresetArgument = "-envforgeTrainingPreset";
        private const string VariantArgument = "-envforgeWorldVariant";

        private string outputPath;
        private string apiBaseUrl;
        private string scenarioId = NavigationScenarioBundleDefaults.ScenarioId;
        private string trainingPreset = "mvp";
        private string worldVariant = "default";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromCommandLine()
        {
            string output = NavigationAutomationArguments.GetFullPath(SubmitArgument);
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            GameObject runner = new("EnvForge Cloud Job Automation");
            DontDestroyOnLoad(runner);
            NavigationCloudJobAutomation automation = runner.AddComponent<NavigationCloudJobAutomation>();
            automation.outputPath = output;
            automation.apiBaseUrl = NavigationAutomationArguments.GetValue(ApiBaseUrlArgument);
            automation.scenarioId = NavigationAutomationArguments.GetValue(ScenarioIdArgument) ?? NavigationScenarioBundleDefaults.ScenarioId;
            automation.trainingPreset = NavigationAutomationArguments.GetValue(PresetArgument) ?? "mvp";
            automation.worldVariant = NavigationAutomationArguments.GetValue(VariantArgument) ?? "default";
        }

        private void Start()
        {
            StartCoroutine(SubmitJob());
        }

        private IEnumerator SubmitJob()
        {
            yield return null;
            yield return new WaitUntil(() => SceneManager.GetActiveScene().isLoaded);
            yield return new WaitForSeconds(0.5f);

            NavigationSceneBuilder sceneBuilder = FindFirstObjectByType<NavigationSceneBuilder>();
            CloudJobSubmissionResult result = new()
            {
                status = "failed",
                api_base_url = apiBaseUrl,
                scenario_id = scenarioId,
                training_preset = trainingPreset,
                world_variant = worldVariant,
            };

            if (sceneBuilder == null)
            {
                result.error = "NavigationSceneBuilder was not found.";
                WriteResult(result);
                Application.Quit(1);
                yield break;
            }

            ApplyTrainingPreset(sceneBuilder);
            NavigationWorldVariantAutomation.Apply(sceneBuilder, worldVariant);

            ScenarioBundleDto scenario = sceneBuilder.BuildScenarioBundle(scenarioId);
            result.scenario_json = ScenarioBundleSerializer.ToJson(scenario, prettyPrint: true);
            result.training_timesteps = scenario.training?.timesteps ?? 0;
            result.eval_episodes = scenario.training?.eval_episodes ?? 0;
            result.floor_width = sceneBuilder.FloorSize.x;
            result.floor_depth = sceneBuilder.FloorSize.y;
            result.user_wall_count = sceneBuilder.UserWallCount;

            string resolvedApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
                ? "https://embodiedlab-api-886092613885.asia-northeast1.run.app"
                : apiBaseUrl;
            EnvForgeApiClient apiClient = new(resolvedApiBaseUrl);
            bool failed = false;

            yield return apiClient.SubmitScenario(
                scenario,
                response =>
                {
                    result.submission_id = response.submission_id;
                    result.submit_response_status = response.status;
                },
                error =>
                {
                    failed = true;
                    result.error = $"Submit failed: {error}";
                });

            if (!failed && !string.IsNullOrWhiteSpace(result.submission_id))
            {
                yield return apiClient.StartTraining(
                    result.submission_id,
                    response => result.train_response_status = response.status,
                    error =>
                    {
                        failed = true;
                        result.error = $"Training start failed: {error}";
                    });
            }

            result.status = failed ? "failed" : "submitted";
            result.api_base_url = resolvedApiBaseUrl;
            WriteResult(result);
            Application.Quit(failed ? 1 : 0);
        }

        private void ApplyTrainingPreset(NavigationSceneBuilder sceneBuilder)
        {
            if (string.Equals(trainingPreset, "mvp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trainingPreset, "production", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.TrainingSettings.ApplyMvpPreset();
                return;
            }

            if (string.Equals(trainingPreset, "smoke", StringComparison.OrdinalIgnoreCase))
            {
                sceneBuilder.TrainingSettings.ApplySmokePreset();
            }
        }

        private void WriteResult(CloudJobSubmissionResult result)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonUtility.ToJson(result, prettyPrint: true));
            Debug.Log($"EnvForge cloud job submission result saved: {outputPath}");
        }

        [Serializable]
        private sealed class CloudJobSubmissionResult
        {
            public string status;
            public string error;
            public string api_base_url;
            public string scenario_id;
            public string submission_id;
            public string submit_response_status;
            public string train_response_status;
            public string training_preset;
            public string world_variant;
            public int training_timesteps;
            public int eval_episodes;
            public float floor_width;
            public float floor_depth;
            public int user_wall_count;
            public string scenario_json;
        }
    }
}
