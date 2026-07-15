using System;
using System.Collections;
using System.IO;
using EmbodiedLab.Contracts;
using EmbodiedLab.Unity;
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
        private const string WebSocketBaseUrlArgument = "-envforgeWebSocketBaseUrl";
        private const string ScenarioIdArgument = "-envforgeScenarioId";
        private const string PresetArgument = "-envforgeTrainingPreset";
        private const string VariantArgument = "-envforgeWorldVariant";
        private const string DefaultApiBaseUrl =
            "https://embodiedlab-api-886092613885.asia-northeast1.run.app";
        private const string DefaultWebSocketBaseUrl =
            "wss://embodiedlab-notification-e4dlxes4aq-an.a.run.app";

        private string outputPath;
        private string apiBaseUrl;
        private string webSocketBaseUrl;
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
            automation.webSocketBaseUrl = NavigationAutomationArguments.GetValue(
                WebSocketBaseUrlArgument);
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

            ScenarioBundle scenario = sceneBuilder.BuildScenarioBundle(scenarioId);
            result.scenario_json = ScenarioBundleJson.Serialize(scenario, indented: true);
            result.training_timesteps = scenario.Training?.Timesteps ?? 0;
            result.eval_episodes = scenario.Training?.EvalEpisodes ?? 0;
            result.floor_width = sceneBuilder.FloorSize.x;
            result.floor_depth = sceneBuilder.FloorSize.y;
            result.user_wall_count = sceneBuilder.UserWallCount;

            string resolvedApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
                ? DefaultApiBaseUrl
                : apiBaseUrl;
            string resolvedWebSocketBaseUrl = string.IsNullOrWhiteSpace(webSocketBaseUrl)
                ? DefaultWebSocketBaseUrl
                : webSocketBaseUrl;
            _ = SubmitJobAsync(
                scenario,
                result,
                resolvedApiBaseUrl,
                resolvedWebSocketBaseUrl);
        }

        private async Awaitable SubmitJobAsync(
            ScenarioBundle scenario,
            CloudJobSubmissionResult result,
            string resolvedApiBaseUrl,
            string resolvedWebSocketBaseUrl)
        {
            try
            {
                EmbodiedLabEndpoints endpoints = new(
                    resolvedApiBaseUrl,
                    resolvedWebSocketBaseUrl);
                EnvForgeEndpointSecurity.Validate(endpoints);
                result.api_base_url = endpoints.ApiBaseUri.AbsoluteUri;
                result.websocket_base_url = endpoints.ResultWebSocketBaseUri.AbsoluteUri;
                using EmbodiedLabJob job = await EmbodiedLabJob.SubmitAsync(
                    endpoints,
                    scenario);
                result.submission_id = job.SubmissionId;
                result.submit_response_status = "accepted";
                result.train_response_status = "accepted";
                result.status = "submitted";
                WriteResult(result);
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                result.status = "failed";
                result.error = $"Submit failed: {exception.Message}";
                WriteResult(result);
                Debug.LogError($"EnvForge cloud job submission failed: {exception}");
                Application.Quit(1);
            }
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
            public string websocket_base_url;
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
