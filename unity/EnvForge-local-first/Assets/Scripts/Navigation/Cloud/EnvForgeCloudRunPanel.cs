using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Inference;
using EnvForge.Navigation.Replay;
using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    public sealed class EnvForgeCloudRunPanel : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float Width = 640f;
        private const float Height = 320f;
        private const float DetailsHeight = 560f;
        private const float ButtonHeight = 54f;
        private const float ButtonGap = 8f;
        private const float StatusHeight = 156f;
        private const float StatusLineHeight = 38f;
        private const float StatusLabelWidth = 124f;
        private const int FontSize = 26;
        private const int StatusFontSize = 30;
        private const int SettingsFontSize = 28;
        private const int ButtonFontSize = 26;
        private const int PrimaryButtonFontSize = 26;
        private const float SettingsWidth = 640f;
        private const float SettingsHeight = 780f;
        private const float SettingsButtonHeight = 58f;
        private const float SettingsFieldHeight = 50f;
        private const float SettingsLabelWidth = 245f;
        private const string BundledReplayResource = "EnvForge/navigation_default_replay";

        [SerializeField] private bool showPanel = true;
        [SerializeField] private string fallbackBaseUrl = "http://localhost:8000";

        private NavigationSceneBuilder sceneBuilder;
        private NavigationReplayPlayer replayPlayer;
        private NavigationModelInferenceController inferenceController;
        private EnvForgeApiClient apiClient;
        private EnvForgeArtifactDownloader artifactDownloader;
        private EnvForgeJobHistoryStore jobHistoryStore;
        private EnvForgeResultWebSocketClient resultWebSocketClient;
        private ResultDocumentDto latestResult;
        private string submissionId;
        private string webSocketUrlTemplate;
        private string activeScenarioId;
        private string activeJobSettingsSummary;
        private string activeJobTrainerSummary;
        private string loadedReplaySummary;
        private string resultStreamState = "not connected";
        private int resultStreamEventCount;
        private string lastResultStreamStatus;
        private string lastResultStreamReceivedAt;
        private bool resultStreamErrorReported;
        private string status = "Cloud: idle";
        private bool busy;
        private bool showTrainingSettings;
        private bool showRewardSettings;
        private bool showJobDetails;
        private Vector2 trainingSettingsScroll;
        private Vector2 jobDetailsScroll;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle statusStyle;
        private GUIStyle detailStyle;
        private GUIStyle settingsLabelStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle settingsTextFieldStyle;
        private GUIStyle boxStyle;
        private string timestepsText;
        private string maxEpisodeStepsText;
        private string seedText;
        private string nEnvsText;
        private string cpuCountText;
        private string torchNumThreadsText;
        private string nStepsText;
        private string batchSizeText;
        private string gammaText;
        private string learningRateText;
        private string entCoefText;
        private string evalEpisodesText;
        private string goalReachedRewardText;
        private string goalProgressRewardText;
        private string collisionPenaltyText;
        private string stepPenaltyText;
        private string wideAnglePenaltyText;
        private string rearAnglePenaltyText;
        private string inactivePenaltyText;
        private string movementThresholdText;

        public void Configure(
            NavigationSceneBuilder builder,
            NavigationReplayPlayer player,
            NavigationModelInferenceController inference,
            EnvForgeApiSettings settings,
            string baseUrl,
            string resultStreamUrlTemplate)
        {
            sceneBuilder = builder;
            replayPlayer = player;
            inferenceController = inference;
            fallbackBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? fallbackBaseUrl : baseUrl;
            apiClient = settings != null
                ? new EnvForgeApiClient(settings)
                : new EnvForgeApiClient(fallbackBaseUrl);
            webSocketUrlTemplate = settings != null && !string.IsNullOrWhiteSpace(settings.WebSocketUrlTemplate)
                ? settings.WebSocketUrlTemplate
                : resultStreamUrlTemplate;
            artifactDownloader = new EnvForgeArtifactDownloader();
            jobHistoryStore = new EnvForgeJobHistoryStore(Application.persistentDataPath);
            resultWebSocketClient = new EnvForgeResultWebSocketClient();
            RestoreLatestJob();
            SyncTextFromTrainingSettings();
        }

        private void Update()
        {
            ProcessResultStreamMessages();
        }

        private void OnDestroy()
        {
            resultWebSocketClient?.Dispose();
        }

        private void RestoreLatestJob()
        {
            EnvForgeJobRecordDto latestJob = jobHistoryStore.Latest;
            if (latestJob == null)
            {
                return;
            }

            submissionId = latestJob.submission_id;
            activeScenarioId = latestJob.scenario_id;
            activeJobTrainerSummary = latestJob.trainer_summary;
            activeJobSettingsSummary = string.IsNullOrWhiteSpace(latestJob.scenario_id)
                ? latestJob.trainer_summary
                : $"{latestJob.scenario_id} · {latestJob.trainer_summary}";
            if (!string.IsNullOrWhiteSpace(latestJob.local_replay_path))
            {
                loadedReplaySummary = $"Saved replay: {Shorten(latestJob.submission_id, 18)}";
            }

            status = $"Cloud: restored {Shorten(latestJob.submission_id, 18)}";
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            EnsureStyles();
            float boxWidth = Mathf.Min(Width, Screen.width - Padding * 2f);
            float boxHeight = Mathf.Min(Height, Screen.height - Padding * 2f);
            Rect boxRect = new(Screen.width - boxWidth - Padding, Padding, boxWidth, boxHeight);
            GUI.Box(boxRect, GUIContent.none, boxStyle);
            DrawMainPanel(boxRect);

            if (showTrainingSettings)
            {
                float settingsHeight = Mathf.Min(SettingsHeight, Screen.height - boxRect.yMax - Padding * 2f);
                float settingsWidth = Mathf.Min(SettingsWidth, Screen.width - Padding * 2f);
                DrawTrainingSettings(new Rect(Screen.width - settingsWidth - Padding, boxRect.yMax + Padding, settingsWidth, settingsHeight));
            }
            else if (showJobDetails)
            {
                float detailsHeight = Mathf.Min(DetailsHeight, Screen.height - boxRect.yMax - Padding * 2f);
                DrawJobDetails(new Rect(Screen.width - boxWidth - Padding, boxRect.yMax + Padding, boxWidth, detailsHeight));
            }
        }

        private void DrawMainPanel(Rect boxRect)
        {
            Rect contentRect = new(boxRect.x + Padding, boxRect.y + Padding, boxRect.width - Padding * 2f, boxRect.height - Padding * 2f);
            float y = contentRect.y;

            float topButtonWidth = (contentRect.width - ButtonGap * 2f) / 3f;
            Rect replayRect = new(contentRect.x, y, topButtonWidth, ButtonHeight);
            Rect cfgRect = new(replayRect.xMax + ButtonGap, y, topButtonWidth, ButtonHeight);
            Rect inferenceRect = new(cfgRect.xMax + ButtonGap, y, topButtonWidth, ButtonHeight);

            if (DrawButton(replayRect, new GUIContent("REPLAY", "Load replay"), primaryButtonStyle, !busy))
            {
                LoadLatestReplay();
            }

            GUIStyle cfgButtonStyle = showTrainingSettings ? selectedButtonStyle : buttonStyle;
            if (DrawButton(cfgRect, new GUIContent("CFG", "Training settings"), cfgButtonStyle, !busy))
            {
                showTrainingSettings = !showTrainingSettings;
                showJobDetails = false;
                SyncTextFromTrainingSettings();
            }

            GUIStyle inferenceButtonStyle = inferenceController != null && inferenceController.IsRunning
                ? selectedButtonStyle
                : buttonStyle;
            if (DrawButton(inferenceRect, new GUIContent("AI", "Run downloaded model"), inferenceButtonStyle, !busy))
            {
                ToggleInferenceMode();
            }

            y += ButtonHeight + ButtonGap;
            float secondaryButtonWidth = (contentRect.width - ButtonGap * 2f) / 3f;
            Rect submitRect = new(contentRect.x, y, secondaryButtonWidth, ButtonHeight);
            Rect detailsRect = new(submitRect.xMax + ButtonGap, y, secondaryButtonWidth, ButtonHeight);
            Rect downloadRect = new(detailsRect.xMax + ButtonGap, y, secondaryButtonWidth, ButtonHeight);

            if (DrawButton(submitRect, new GUIContent("SUBMIT", "Submit and train"), buttonStyle, !busy && apiClient != null && sceneBuilder != null))
            {
                StartCoroutine(SubmitAndTrain());
            }

            GUIStyle detailsButtonStyle = showJobDetails ? selectedButtonStyle : buttonStyle;
            if (DrawButton(detailsRect, new GUIContent("JOB", "Job details"), detailsButtonStyle, !busy))
            {
                ToggleJobDetails();
            }

            if (DrawButton(downloadRect, new GUIContent("GET", "Download artifacts"), buttonStyle, !busy))
            {
                StartCoroutine(DownloadAvailableArtifacts());
            }

            y += ButtonHeight + ButtonGap * 2f;
            DrawHudStatus(new Rect(contentRect.x, y, contentRect.width, StatusHeight));
        }

        private static bool DrawButton(Rect rect, GUIContent content, GUIStyle style, bool enabled)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, content, style);
            GUI.enabled = previousEnabled;
            return clicked;
        }

        private IEnumerator SubmitAndTrain()
        {
            busy = true;
            latestResult = null;
            resultWebSocketClient?.Stop();
            resultStreamState = "not connected";
            resultStreamEventCount = 0;
            lastResultStreamStatus = null;
            lastResultStreamReceivedAt = null;
            resultStreamErrorReported = false;
            status = "Cloud: submitting scenario";

            ScenarioBundleDto scenario = sceneBuilder.BuildScenarioBundle();
            activeScenarioId = scenario.scenario_id;
            activeJobSettingsSummary = FormatScenarioSummary(scenario);
            activeJobTrainerSummary = FormatScenarioTrainerSummary(scenario);
            bool failed = false;
            yield return apiClient.SubmitScenario(
                scenario,
                response =>
                {
                    submissionId = response.submission_id;
                    jobHistoryStore.UpsertSubmittedJob(submissionId, scenario, activeJobTrainerSummary);
                    status = $"Cloud: submitted {submissionId}";
                },
                error =>
                {
                    failed = true;
                    status = $"Cloud submit failed: {error}";
                });

            if (failed || string.IsNullOrEmpty(submissionId))
            {
                busy = false;
                yield break;
            }

            status = "Cloud: starting training";
            yield return apiClient.StartTraining(
                submissionId,
                _ => status = "Cloud: training queued",
                error =>
                {
                    failed = true;
                    status = $"Cloud train failed: {error}";
                });

            busy = false;
            if (!failed)
            {
                StartResultStream();
            }
        }

        private void StartResultStream()
        {
            string url = BuildResultWebSocketUrl(submissionId);
            if (string.IsNullOrWhiteSpace(url))
            {
                resultStreamState = "missing URL";
                status = "Cloud: result stream URL missing";
                Debug.LogWarning("Result stream URL is not configured. Set WebSocket Url Template on ApiSettings or NavigationSceneBuilder.");
                showTrainingSettings = false;
                showJobDetails = true;
                return;
            }

            resultStreamState = "connecting";
            resultWebSocketClient.Start(url);
            status = "Cloud: listening for result";
        }

        private void ProcessResultStreamMessages()
        {
            if (resultWebSocketClient == null)
            {
                return;
            }

            while (resultWebSocketClient.TryDequeue(out string message))
            {
                if (message.Contains("\"type\"", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("\"connected\"", StringComparison.OrdinalIgnoreCase))
                {
                    resultStreamState = "connected";
                    status = "Cloud: result stream connected";
                    continue;
                }

                ResultDocumentDto result = JsonUtilityBridge.FromJson<ResultDocumentDto>(message);
                if (string.IsNullOrWhiteSpace(result?.submission_id) ||
                    string.IsNullOrWhiteSpace(result.status))
                {
                    Debug.LogWarning($"Ignored result stream message: {message}");
                    continue;
                }

                ApplyResultUpdate(result);
            }

            string streamError = resultWebSocketClient.LastError;
            if (!resultStreamErrorReported && !string.IsNullOrWhiteSpace(streamError))
            {
                resultStreamErrorReported = true;
                resultStreamState = "failed";
                status = $"Result stream failed: {streamError}";
                Debug.LogWarning($"Result stream failed: {streamError}");
            }
        }

        private void ApplyResultUpdate(ResultDocumentDto result)
        {
            latestResult = result;
            resultStreamEventCount += 1;
            lastResultStreamStatus = result.status;
            lastResultStreamReceivedAt = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            string resultSubmissionId = string.IsNullOrWhiteSpace(result.submission_id)
                ? submissionId
                : result.submission_id;
            jobHistoryStore.UpsertResult(resultSubmissionId, result);
            status = $"Cloud: {result.status}";

            if (string.Equals(result.status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result.status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                resultStreamState = "closed";
                resultWebSocketClient?.Stop();
            }
            else
            {
                resultStreamState = "receiving";
            }
        }

        private IEnumerator DownloadReplay()
        {
            busy = true;
            status = "Cloud: downloading replay";
            string localPath = GetLocalReplayPath();
            yield return artifactDownloader.DownloadFile(
                GetResultArtifacts().replay_log,
                localPath,
                savedPath =>
                {
                    string jsonLines = File.ReadAllText(savedPath);
                    jobHistoryStore.SetLocalReplayPath(submissionId, savedPath);
                    LoadReplaySteps(ReplayLogSerializer.FromJsonLines(jsonLines), "Downloaded replay");
                    status = "Cloud: replay loaded";
                },
                error => status = $"Replay download failed: {error}");
            busy = false;
        }

        private IEnumerator DownloadAvailableArtifacts()
        {
            if (!IsCompletedResult())
            {
                status = "Cloud: wait for DONE before download";
                showTrainingSettings = false;
                showJobDetails = true;
                yield break;
            }

            ResultArtifactsDto artifacts = GetResultArtifacts();
            if (artifacts == null ||
                (artifacts.replay_log == null && artifacts.onnx_model == null))
            {
                status = "Cloud: no downloadable artifacts";
                showTrainingSettings = false;
                showJobDetails = true;
                yield break;
            }

            if (artifacts.replay_log != null)
            {
                yield return DownloadReplay();
            }

            if (artifacts.onnx_model != null)
            {
                yield return DownloadModelArtifacts();
            }
        }

        private IEnumerator DownloadModelArtifacts()
        {
            busy = true;
            string outputDir = Path.Combine(Application.persistentDataPath, "EnvForge", submissionId ?? "latest");
            ResultArtifactsDto artifacts = GetResultArtifacts();
            if (artifacts.onnx_model != null)
            {
                yield return DownloadArtifactFile(
                    artifacts.onnx_model,
                    Path.Combine(outputDir, "policy.onnx"),
                    savedPath => jobHistoryStore.SetLocalOnnxPath(submissionId, savedPath));
            }

            busy = false;
        }

        private ResultArtifactsDto GetResultArtifacts()
        {
            if (latestResult?.artifacts != null)
            {
                return latestResult.artifacts;
            }

            return latestResult?.result_bundle?.artifacts;
        }

        private bool IsCompletedResult()
        {
            return string.Equals(latestResult?.status, "completed", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawHudStatus(Rect rect)
        {
            string[] lines = FormatHudStatusLines();
            GUI.Label(new Rect(rect.x, rect.y, rect.width, StatusLineHeight), lines[0], statusStyle);

            Rect scenarioLabelRect = new(rect.x, rect.y + StatusLineHeight, StatusLabelWidth, StatusLineHeight);
            Rect scenarioValueRect = new(scenarioLabelRect.xMax, scenarioLabelRect.y, rect.width - StatusLabelWidth, StatusLineHeight);
            GUI.Label(scenarioLabelRect, "SCENE", labelStyle);
            GUI.Label(scenarioValueRect, lines[1], detailStyle);

            Rect trainerLabelRect = new(rect.x, rect.y + StatusLineHeight * 2f, StatusLabelWidth, StatusLineHeight);
            Rect trainerValueRect = new(trainerLabelRect.xMax, trainerLabelRect.y, rect.width - StatusLabelWidth, StatusLineHeight);
            GUI.Label(trainerLabelRect, "TRAIN", labelStyle);
            GUI.Label(trainerValueRect, lines[2], detailStyle);

            Rect streamLabelRect = new(rect.x, rect.y + StatusLineHeight * 3f, StatusLabelWidth, StatusLineHeight);
            Rect streamValueRect = new(streamLabelRect.xMax, streamLabelRect.y, rect.width - StatusLabelWidth, StatusLineHeight);
            GUI.Label(streamLabelRect, "STREAM", labelStyle);
            GUI.Label(streamValueRect, lines[3], detailStyle);
        }

        private string[] FormatHudStatusLines()
        {
            string resultStatus = latestResult?.status;
            string hudStatus;
            if (string.Equals(resultStatus, "completed", StringComparison.OrdinalIgnoreCase))
            {
                hudStatus = "DONE";
            }
            else if (string.Equals(resultStatus, "failed", StringComparison.OrdinalIgnoreCase))
            {
                hudStatus = "FAILED";
            }
            else if (string.Equals(resultStatus, "running", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultStatus, "starting", StringComparison.OrdinalIgnoreCase))
            {
                hudStatus = "RUNNING";
            }
            else if (string.Equals(resultStatus, "queued", StringComparison.OrdinalIgnoreCase))
            {
                hudStatus = "QUEUED";
            }
            else if (!string.IsNullOrEmpty(submissionId) && resultStreamEventCount == 0)
            {
                hudStatus = "NO EVENT";
            }
            else if (busy)
            {
                hudStatus = "WORKING";
            }
            else
            {
                hudStatus = string.IsNullOrEmpty(submissionId) ? "IDLE" : "WAITING";
            }

            string scenario = !string.IsNullOrEmpty(activeScenarioId)
                ? activeScenarioId
                : "none";
            return new[]
            {
                hudStatus,
                scenario,
                GetVisibleTrainerSummary(),
                FormatResultStreamSummary(),
            };
        }

        private string GetVisibleSettingsSummary()
        {
            if (!string.IsNullOrEmpty(activeJobSettingsSummary))
            {
                return activeJobSettingsSummary;
            }

            return FormatTrainingSummary(sceneBuilder?.TrainingSettings);
        }

        private static string FormatTrainingSummary(NavigationTrainingSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            return $"{settings.PresetName} · {FormatSteps(settings.Timesteps)} · seed {settings.Seed}";
        }

        private static string FormatSteps(int timesteps)
        {
            if (timesteps >= 1000000)
            {
                return (timesteps / 1000000f).ToString("0.#M", CultureInfo.InvariantCulture);
            }

            if (timesteps >= 1000)
            {
                return (timesteps / 1000f).ToString("0.#k", CultureInfo.InvariantCulture);
            }

            return timesteps.ToString(CultureInfo.InvariantCulture);
        }

        private string GetLocalReplayPath()
        {
            string outputDir = Path.Combine(Application.persistentDataPath, "EnvForge", submissionId ?? "latest", "replay");
            return Path.Combine(outputDir, "replay.jsonl");
        }

        private IEnumerator DownloadArtifactFile(ArtifactLocationDto artifact, string localPath, Action<string> onSaved = null)
        {
            status = $"Cloud: downloading {Path.GetFileName(localPath)}";
            yield return artifactDownloader.DownloadFile(
                artifact,
                localPath,
                savedPath =>
                {
                    onSaved?.Invoke(savedPath);
                    status = $"Cloud: saved {savedPath}";
                },
                error => status = $"Model download failed: {error}");
        }

        private void DrawJobDetails(Rect panelRect)
        {
            GUI.Box(panelRect, GUIContent.none, boxStyle);
            GUILayout.BeginArea(new Rect(panelRect.x + Padding, panelRect.y + Padding, panelRect.width - Padding * 2f, panelRect.height - Padding * 2f));
            jobDetailsScroll = GUILayout.BeginScrollView(jobDetailsScroll);

            GUILayout.Label("Result", statusStyle);
            GUILayout.Label($"Status: {latestResult?.status ?? "none"}", detailStyle);
            GUILayout.Label(FormatResultStreamSummary(), detailStyle);

            GUILayout.Space(8f);
            GUILayout.Label("Submitted Job", labelStyle);
            GUILayout.Label($"Job: {Shorten(submissionId, 18)}", detailStyle);
            GUILayout.Label($"Scenario: {activeScenarioId ?? "none"}", detailStyle);
            GUILayout.Label($"Trainer: {GetVisibleTrainerSummary()}", detailStyle);
            GUILayout.Label(FormatProgressSummary(), detailStyle);

            GUILayout.Space(8f);
            GUILayout.Label("Loaded Replay", labelStyle);
            GUILayout.Label(loadedReplaySummary ?? "Replay: none", detailStyle);
            GUILayout.Label($"Artifacts: replay {FormatArtifactState(GetResultArtifacts()?.replay_log)} · model {FormatModelArtifactState(GetResultArtifacts())}", detailStyle);
            GUILayout.Label(FormatHistorySummary(), detailStyle);
            GUILayout.Label(FormatInferenceSummary(), detailStyle);
            if (!string.IsNullOrWhiteSpace(inferenceController?.LastObservationSummary))
            {
                GUILayout.Label(inferenceController.LastObservationSummary, detailStyle);
            }

            if (!string.IsNullOrWhiteSpace(inferenceController?.LastErrorDetails))
            {
                GUILayout.Label($"Inference error: {inferenceController.LastErrorDetails}", detailStyle);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                GUILayout.Space(8f);
                GUILayout.Label(status, detailStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ToggleJobDetails()
        {
            if (string.IsNullOrEmpty(submissionId) && latestResult == null)
            {
                status = "Cloud: no job yet";
            }

            showTrainingSettings = false;
            showJobDetails = !showJobDetails;
        }

        private static string FormatArtifactState(ArtifactLocationDto artifact)
        {
            return artifact == null ? "not ready" : "ready";
        }

        private static string FormatModelArtifactState(ResultArtifactsDto artifacts)
        {
            return artifacts?.onnx_model == null ? "not ready" : "ready";
        }

        private string FormatHistorySummary()
        {
            if (jobHistoryStore == null || jobHistoryStore.Jobs.Count == 0)
            {
                return "History: none";
            }

            EnvForgeJobRecordDto latestJob = jobHistoryStore.Latest;
            string replay = string.IsNullOrWhiteSpace(latestJob.local_replay_path) ? "replay -" : "replay saved";
            string model = string.IsNullOrWhiteSpace(latestJob.local_onnx_path)
                ? "model -"
                : "model saved";
            return $"History: {jobHistoryStore.Jobs.Count} jobs · latest {Shorten(latestJob.submission_id, 18)} · {replay} · {model}";
        }

        private string FormatInferenceSummary()
        {
            if (inferenceController == null)
            {
                return "Inference: unavailable";
            }

            return $"{inferenceController.StatusSummary} · {inferenceController.LastActionSummary}";
        }

        private void ToggleInferenceMode()
        {
            if (inferenceController == null)
            {
                status = "Inference: controller unavailable";
                return;
            }

            if (inferenceController.IsRunning)
            {
                inferenceController.StopInference();
                status = inferenceController.StatusSummary;
                return;
            }

            string modelPath = GetLatestLocalOnnxModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                status = "Inference: download ONNX model first";
                showTrainingSettings = false;
                showJobDetails = true;
                return;
            }

            replayPlayer?.ReleaseControl();
            if (inferenceController.StartInference(modelPath, out string error))
            {
                status = inferenceController.StatusSummary;
                return;
            }

            status = "Inference failed: see Console / Job details";
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogError($"Inference failed: {error}");
            }

            showTrainingSettings = false;
            showJobDetails = true;
        }

        private string GetLatestLocalOnnxModelPath()
        {
            EnvForgeJobRecordDto latestJob = jobHistoryStore?.Latest;
            if (latestJob == null || string.IsNullOrWhiteSpace(latestJob.local_onnx_path))
            {
                return string.Empty;
            }

            return latestJob.local_onnx_path;
        }

        private string FormatProgressSummary()
        {
            ProgressDto progress = latestResult?.progress;
            if (progress == null)
            {
                return "Progress: none";
            }

            string steps = progress.total_steps > 0
                ? $"{FormatSteps(progress.current_step)}/{FormatSteps(progress.total_steps)}"
                : FormatSteps(progress.current_step);
            string phase = string.IsNullOrWhiteSpace(progress.phase) ? latestResult?.status : progress.phase;
            return $"Progress: {phase ?? "unknown"} · {steps}";
        }

        private string FormatResultStreamSummary()
        {
            string summary = $"Stream: {resultStreamState} · events {resultStreamEventCount}";
            if (!string.IsNullOrWhiteSpace(lastResultStreamStatus))
            {
                summary += $" · last {lastResultStreamStatus}";
            }

            if (!string.IsNullOrWhiteSpace(lastResultStreamReceivedAt))
            {
                summary += $" @ {lastResultStreamReceivedAt}";
            }

            return summary;
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "none";
            }

            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        private string BuildResultWebSocketUrl(string resultSubmissionId)
        {
            if (string.IsNullOrWhiteSpace(resultSubmissionId) ||
                string.IsNullOrWhiteSpace(webSocketUrlTemplate))
            {
                return string.Empty;
            }

            return webSocketUrlTemplate.Replace(
                "{submission_id}",
                Uri.EscapeDataString(resultSubmissionId));
        }

        private void DrawTrainingSettings(Rect panelRect)
        {
            GUI.Box(panelRect, GUIContent.none, boxStyle);
            GUILayout.BeginArea(new Rect(panelRect.x + Padding, panelRect.y + Padding, panelRect.width - Padding * 2f, panelRect.height - Padding * 2f));
            trainingSettingsScroll = GUILayout.BeginScrollView(trainingSettingsScroll);
            GUILayout.Label($"CFG: {sceneBuilder.TrainingSettings.PresetName} · {(showRewardSettings ? "Reward" : "Training")}", statusStyle);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Training", showRewardSettings ? buttonStyle : selectedButtonStyle, GUILayout.Height(SettingsButtonHeight)))
            {
                showRewardSettings = false;
            }

            if (GUILayout.Button("Reward", showRewardSettings ? selectedButtonStyle : buttonStyle, GUILayout.Height(SettingsButtonHeight)))
            {
                showRewardSettings = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Smoke", GetPresetButtonStyle("Smoke"), GUILayout.Height(SettingsButtonHeight)))
            {
                sceneBuilder.TrainingSettings.ApplySmokePreset();
                SyncTextFromTrainingSettings();
            }

            if (GUILayout.Button("MVP", GetPresetButtonStyle("MVP"), GUILayout.Height(SettingsButtonHeight)))
            {
                sceneBuilder.TrainingSettings.ApplyMvpPreset();
                SyncTextFromTrainingSettings();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            if (showRewardSettings)
            {
                DrawRewardSettings();
            }
            else
            {
                DrawTrainerSettings();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTrainerSettings()
        {
            GUILayout.Label("Training", settingsLabelStyle);
            DrawIntField("timesteps", ref timestepsText, value => sceneBuilder.TrainingSettings.Timesteps = value);
            DrawIntField("max episode", ref maxEpisodeStepsText, value => sceneBuilder.TrainingSettings.MaxEpisodeSteps = value);
            DrawIntField("seed", ref seedText, value => sceneBuilder.TrainingSettings.Seed = value);
            DrawIntField("n envs", ref nEnvsText, value => sceneBuilder.TrainingSettings.NEnvs = value);
            DrawIntField("cpu", ref cpuCountText, value => sceneBuilder.TrainingSettings.CpuCount = value);
            DrawIntField("torch th.", ref torchNumThreadsText, value => sceneBuilder.TrainingSettings.TorchNumThreads = value);
            DrawIntField("n steps", ref nStepsText, value => sceneBuilder.TrainingSettings.NSteps = value);
            DrawIntField("batch", ref batchSizeText, value => sceneBuilder.TrainingSettings.BatchSize = value);
            DrawFloatField("gamma", ref gammaText, value => sceneBuilder.TrainingSettings.Gamma = value);
            DrawFloatField("learn rate", ref learningRateText, value => sceneBuilder.TrainingSettings.LearningRate = value);
            DrawFloatField("entropy", ref entCoefText, value => sceneBuilder.TrainingSettings.EntCoef = value);
            DrawIntField("eval eps", ref evalEpisodesText, value => sceneBuilder.TrainingSettings.EvalEpisodes = value);
        }

        private void DrawRewardSettings()
        {
            GUILayout.Label("Reward", settingsLabelStyle);
            DrawFloatField("goal", ref goalReachedRewardText, value => sceneBuilder.TrainingSettings.GoalReachedReward = value);
            DrawFloatField("progress", ref goalProgressRewardText, value => sceneBuilder.TrainingSettings.GoalProgressReward = value);
            DrawFloatField("collision", ref collisionPenaltyText, value => sceneBuilder.TrainingSettings.CollisionPenalty = value);
            DrawFloatField("step", ref stepPenaltyText, value => sceneBuilder.TrainingSettings.StepPenalty = value);
            DrawFloatField("wide angle", ref wideAnglePenaltyText, value => sceneBuilder.TrainingSettings.WideAnglePenalty = value);
            DrawFloatField("rear angle", ref rearAnglePenaltyText, value => sceneBuilder.TrainingSettings.RearAnglePenalty = value);
            DrawFloatField("inactive", ref inactivePenaltyText, value => sceneBuilder.TrainingSettings.InactivePenalty = value);
            DrawFloatField("move th.", ref movementThresholdText, value => sceneBuilder.TrainingSettings.MovementThreshold = value);
        }

        private void DrawIntField(string label, ref string text, Action<int> applyValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, settingsLabelStyle, GUILayout.Width(SettingsLabelWidth), GUILayout.Height(SettingsFieldHeight));
            text = GUILayout.TextField(text, settingsTextFieldStyle, GUILayout.Height(SettingsFieldHeight));
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                applyValue(value);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawFloatField(string label, ref string text, Action<float> applyValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, settingsLabelStyle, GUILayout.Width(SettingsLabelWidth), GUILayout.Height(SettingsFieldHeight));
            text = GUILayout.TextField(text, settingsTextFieldStyle, GUILayout.Height(SettingsFieldHeight));
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                applyValue(value);
            }

            GUILayout.EndHorizontal();
        }

        private void SyncTextFromTrainingSettings()
        {
            NavigationTrainingSettings settings = sceneBuilder?.TrainingSettings;
            if (settings == null)
            {
                return;
            }

            timestepsText = settings.Timesteps.ToString(CultureInfo.InvariantCulture);
            maxEpisodeStepsText = settings.MaxEpisodeSteps.ToString(CultureInfo.InvariantCulture);
            seedText = settings.Seed.ToString(CultureInfo.InvariantCulture);
            nEnvsText = settings.NEnvs.ToString(CultureInfo.InvariantCulture);
            cpuCountText = settings.CpuCount.ToString(CultureInfo.InvariantCulture);
            torchNumThreadsText = settings.TorchNumThreads.ToString(CultureInfo.InvariantCulture);
            nStepsText = settings.NSteps.ToString(CultureInfo.InvariantCulture);
            batchSizeText = settings.BatchSize.ToString(CultureInfo.InvariantCulture);
            gammaText = settings.Gamma.ToString("0.####", CultureInfo.InvariantCulture);
            learningRateText = settings.LearningRate.ToString("0.######", CultureInfo.InvariantCulture);
            entCoefText = settings.EntCoef.ToString("0.######", CultureInfo.InvariantCulture);
            evalEpisodesText = settings.EvalEpisodes.ToString(CultureInfo.InvariantCulture);
            goalReachedRewardText = settings.GoalReachedReward.ToString("0.######", CultureInfo.InvariantCulture);
            goalProgressRewardText = settings.GoalProgressReward.ToString("0.######", CultureInfo.InvariantCulture);
            collisionPenaltyText = settings.CollisionPenalty.ToString("0.######", CultureInfo.InvariantCulture);
            stepPenaltyText = settings.StepPenalty.ToString("0.######", CultureInfo.InvariantCulture);
            wideAnglePenaltyText = settings.WideAnglePenalty.ToString("0.######", CultureInfo.InvariantCulture);
            rearAnglePenaltyText = settings.RearAnglePenalty.ToString("0.######", CultureInfo.InvariantCulture);
            inactivePenaltyText = settings.InactivePenalty.ToString("0.######", CultureInfo.InvariantCulture);
            movementThresholdText = settings.MovementThreshold.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private void LoadBundledReplay()
        {
            TextAsset replayAsset = Resources.Load<TextAsset>(BundledReplayResource);
            if (replayAsset == null)
            {
                status = $"Demo replay not found: Resources/{BundledReplayResource}";
                return;
            }

            LoadReplaySteps(ReplayLogSerializer.FromJsonLines(replayAsset.text), "Bundled demo replay");
            status = "Cloud: demo replay loaded";
        }

        private void LoadLatestReplay()
        {
            string replayPath = GetLatestLocalReplayPath();
            if (string.IsNullOrWhiteSpace(replayPath))
            {
                LoadBundledReplay();
                return;
            }

            try
            {
                string jsonLines = File.ReadAllText(replayPath);
                LoadReplaySteps(ReplayLogSerializer.FromJsonLines(jsonLines), "Latest downloaded replay");
                status = "Cloud: latest replay loaded";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Latest replay load failed: {ex}");
                status = "Latest replay load failed; loading demo";
                LoadBundledReplay();
            }
        }

        private string GetLatestLocalReplayPath()
        {
            EnvForgeJobRecordDto latestJob = jobHistoryStore?.Latest;
            if (latestJob == null ||
                string.IsNullOrWhiteSpace(latestJob.local_replay_path) ||
                !File.Exists(latestJob.local_replay_path))
            {
                return string.Empty;
            }

            return latestJob.local_replay_path;
        }

        private void LoadReplaySteps(IReadOnlyList<ReplayLogStepDto> steps, string source)
        {
            replayPlayer.LoadSteps(steps);
            loadedReplaySummary = FormatReplaySummary(steps, source);
        }

        private string FormatReplaySummary(IReadOnlyList<ReplayLogStepDto> steps, string source)
        {
            if (steps == null || steps.Count == 0)
            {
                return $"{source}: no steps";
            }

            ReplayLogStepDto first = steps[0];
            ReplayLogStepDto last = steps[steps.Count - 1];
            string episode = string.IsNullOrWhiteSpace(first.episode_id) ? "episode unknown" : first.episode_id;
            return $"{source}: job {Shorten(first.job_id, 18)} · scenario {first.scenario_id ?? "unknown"} · " +
                   $"{episode} · {steps.Count} steps · {last.time_seconds:0.0}s";
        }

        private static string FormatScenarioSummary(ScenarioBundleDto scenario)
        {
            TrainingDto training = scenario?.training;
            if (training == null)
            {
                return string.Empty;
            }

            return $"{scenario.scenario_id} · {training.algorithm} · {FormatSteps(training.timesteps)} · seed {training.seed} · envs {training.n_envs}";
        }

        private string GetVisibleTrainerSummary()
        {
            if (!string.IsNullOrEmpty(activeJobTrainerSummary))
            {
                return activeJobTrainerSummary;
            }

            NavigationTrainingSettings settings = sceneBuilder?.TrainingSettings;
            return settings == null
                ? "none"
                : $"ppo · {FormatSteps(settings.Timesteps)} · seed {settings.Seed} · envs {settings.NEnvs} · cpu {settings.CpuCount} · th {settings.TorchNumThreads}";
        }

        private static string FormatScenarioTrainerSummary(ScenarioBundleDto scenario)
        {
            TrainingDto training = scenario?.training;
            if (training == null)
            {
                return "none";
            }

            string cpuSummary = training.cpu_count > 0
                ? training.cpu_count.ToString(CultureInfo.InvariantCulture)
                : "job";
            return $"{training.algorithm} · {FormatSteps(training.timesteps)} · seed {training.seed} · envs {training.n_envs} · cpu {cpuSummary} · th {training.torch_num_threads}";
        }

        private GUIStyle GetPresetButtonStyle(string presetName)
        {
            return string.Equals(sceneBuilder.TrainingSettings.PresetName, presetName, StringComparison.OrdinalIgnoreCase)
                ? selectedButtonStyle
                : buttonStyle;
        }

        private void EnsureStyles()
        {
            if (buttonStyle != null &&
                selectedButtonStyle != null &&
                primaryButtonStyle != null &&
                labelStyle != null &&
                statusStyle != null &&
                detailStyle != null &&
                settingsLabelStyle != null &&
                textFieldStyle != null &&
                settingsTextFieldStyle != null &&
                boxStyle != null)
            {
                return;
            }

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ButtonFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            Texture2D buttonBackground = CreateTexture(new Color(0.08f, 0.1f, 0.12f, 0.92f));
            Texture2D buttonHoverBackground = CreateTexture(new Color(0.14f, 0.17f, 0.2f, 0.96f));
            Texture2D buttonActiveBackground = CreateTexture(new Color(0.2f, 0.28f, 0.34f, 0.98f));
            buttonStyle.normal.background = buttonBackground;
            buttonStyle.hover.background = buttonHoverBackground;
            buttonStyle.active.background = buttonActiveBackground;
            buttonStyle.focused.background = buttonHoverBackground;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.focused.textColor = Color.white;

            selectedButtonStyle = new GUIStyle(buttonStyle);
            Texture2D selectedButtonBackground = CreateTexture(new Color(1f, 0.72f, 0.12f, 1f));
            Texture2D selectedButtonHoverBackground = CreateTexture(new Color(1f, 0.82f, 0.24f, 1f));
            selectedButtonStyle.normal.background = selectedButtonBackground;
            selectedButtonStyle.hover.background = selectedButtonHoverBackground;
            selectedButtonStyle.active.background = selectedButtonHoverBackground;
            selectedButtonStyle.focused.background = selectedButtonHoverBackground;
            selectedButtonStyle.normal.textColor = Color.black;
            selectedButtonStyle.hover.textColor = Color.black;
            selectedButtonStyle.active.textColor = Color.black;
            selectedButtonStyle.focused.textColor = Color.black;

            primaryButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = PrimaryButtonFontSize,
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
            };

            statusStyle = new GUIStyle(labelStyle)
            {
                fontSize = StatusFontSize,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
            };

            detailStyle = new GUIStyle(labelStyle)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
            };

            settingsLabelStyle = new GUIStyle(labelStyle)
            {
                fontSize = SettingsFontSize,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = FontSize,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white },
                focused = { textColor = Color.white },
            };

            settingsTextFieldStyle = new GUIStyle(textFieldStyle)
            {
                fontSize = SettingsFontSize,
            };

            Texture2D background = CreateTexture(new Color(0f, 0f, 0f, 0.72f));

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = background;
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
