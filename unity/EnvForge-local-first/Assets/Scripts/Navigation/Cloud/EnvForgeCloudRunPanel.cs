using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Inference;
using EnvForge.Navigation.Replay;
using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    public sealed class EnvForgeCloudRunPanel : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float CompactWidth = 620f;
        private const float CompactHeight = 186f;
        private const float CompactButtonHeight = 42f;
        private const float CompactTopMargin = 24f;
        private const float DetailsHeight = 620f;
        private const float ButtonHeight = 54f;
        private const float ButtonGap = 8f;
        private const int FontSize = 24;
        private const int StatusFontSize = 28;
        private const int SettingsFontSize = 22;
        private const int ButtonFontSize = 24;
        private const int ReplayDisplayEnvIndex = 0;
        private const float SettingsWidth = 760f;
        private const float SettingsHeight = 680f;
        private const float SettingsButtonHeight = 50f;
        private const float SettingsFieldHeight = 34f;
        private const float SettingsLabelWidth = 300f;
        private const float SettingsColumnLabelWidth = 178f;
        private const float ResultPollIntervalSeconds = 30f;
        private const string SettingsTextFieldFocusPrefix = "CloudSettingsTextField_";
        private const float DebugOverlayMaxHeight = 420f;

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
        private string loadedReplaySummary;
        private string resultStreamState = "not connected";
        private int resultStreamEventCount;
        private string lastResultStreamStatus;
        private string lastResultStreamReceivedAt;
        private string lastResultStreamError;
        private string lastResultFetchError;
        private int consecutiveResultFetchFailures;
        private bool resultStreamErrorReported;
        private bool restoredJobNeedsResume;
        private bool autoDownloadStarted;
        private bool resultFetchInFlight;
        private bool replayChunkLoadInFlight;
        private int activeReplayChunkIndex = -1;
        private int activeReplayTotalSteps;
        private string activeReplayManifestPath;
        private string activeReplayScenarioSource;
        private ArtifactLocationDto activeReplayManifestArtifact;
        private ReplayBundleManifestDto activeReplayManifest;
        private readonly List<ReplayBundleChunkDto> activeReplayChunks = new();
        private float nextResultPollAt;
        private string status = "Cloud: idle";
        private bool busy;
        private bool showTrainingSettings;
        private bool showRewardSettings;
        private bool showJobDetails;
        private bool showLibraryDetails;
        private Vector2 trainingSettingsScroll;
        private Vector2 jobDetailsScroll;
        private Vector2 libraryDetailsScroll;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle statusStyle;
        private GUIStyle detailStyle;
        private GUIStyle compactDetailStyle;
        private GUIStyle settingsLabelStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle settingsTextFieldStyle;
        private GUIStyle boxStyle;
        private GUIStyle debugBoxStyle;
        private GUIStyle debugTitleStyle;
        private GUIStyle debugInfoStyle;
        private GUIStyle debugUrlStyle;
        private GUIStyle debugErrorStyle;
        private string timestepsText;
        private string maxEpisodeStepsText;
        private string seedText;
        private string nEnvsText;
        private string cpuCountText;
        private string torchNumThreadsText;
        private string nStepsText;
        private string batchSizeText;
        private string cameraMountHeightText;
        private string cameraMountHeightMinText;
        private string cameraMountHeightMaxText;
        private string robotRadiusText;
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
        private string settingsNameText;
        private string pendingDeleteSubmissionId;
        private string libraryNameSubmissionId;
        private string libraryNameText;
        private bool libraryRefreshInFlight;
        private bool inferenceRestartInFlight;
        private string pendingResultFetchSource;

        public bool IsExpandedPanelOpen => showTrainingSettings || showJobDetails || showLibraryDetails;

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
            if (replayPlayer != null)
            {
                replayPlayer.WindowBoundaryRequested -= HandleReplayWindowBoundaryRequested;
                replayPlayer.WindowBoundaryRequested += HandleReplayWindowBoundaryRequested;
                replayPlayer.ReplayControlRequested -= HandleReplayControlRequested;
                replayPlayer.ReplayControlRequested += HandleReplayControlRequested;
            }

            inferenceController = inference;
            if (inferenceController != null)
            {
                inferenceController.InferenceGoalReached -= HandleInferenceGoalReached;
                inferenceController.InferenceGoalReached += HandleInferenceGoalReached;
                inferenceController.InferenceWallCollision -= HandleInferenceWallCollision;
                inferenceController.InferenceWallCollision += HandleInferenceWallCollision;
            }
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
            if (restoredJobNeedsResume)
            {
                StartCoroutine(ResumeLatestJob());
            }
        }

        private void Update()
        {
            ProcessResultStreamMessages();
            PollLatestResultIfNeeded();
        }

        private void OnDestroy()
        {
            if (replayPlayer != null)
            {
                replayPlayer.WindowBoundaryRequested -= HandleReplayWindowBoundaryRequested;
                replayPlayer.ReplayControlRequested -= HandleReplayControlRequested;
            }

            if (inferenceController != null)
            {
                inferenceController.InferenceGoalReached -= HandleInferenceGoalReached;
                inferenceController.InferenceWallCollision -= HandleInferenceWallCollision;
            }

            EnvForge.Navigation.NavigationInputBlocker.UnregisterPanel(nameof(EnvForgeCloudRunPanel));
            EnvForge.Navigation.NavigationInputBlocker.UnregisterPanel(nameof(EnvForgeCloudRunPanel) + "Debug");
            resultWebSocketClient?.Dispose();
        }

        private void RestoreLatestJob()
        {
            EnvForgeJobRecordDto recentJob = jobHistoryStore.MostRecentRecord;
            if (recentJob == null)
            {
                return;
            }

            submissionId = recentJob.submission_id;
            activeScenarioId = recentJob.scenario_id;
            if (!string.IsNullOrWhiteSpace(recentJob.settings_name))
            {
                settingsNameText = recentJob.settings_name;
            }

            if (!string.IsNullOrWhiteSpace(recentJob.local_replay_manifest_path))
            {
                loadedReplaySummary = $"Saved replay bundle: {Shorten(recentJob.submission_id, 18)}";
            }

            status = $"Cloud: restored {Shorten(recentJob.submission_id, 18)}";
            restoredJobNeedsResume = !string.IsNullOrWhiteSpace(submissionId);
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                EnvForge.Navigation.NavigationInputBlocker.UnregisterPanel(nameof(EnvForgeCloudRunPanel));
                EnvForge.Navigation.NavigationInputBlocker.UnregisterPanel(nameof(EnvForgeCloudRunPanel) + "Debug");
                return;
            }

            EnsureStyles();
            Rect boxRect = DrawCompactPanel();
            Rect expandedRect = Rect.zero;
            Rect debugRect = DrawDebugOverlayIfNeeded();
            if (!IsExpandedPanelOpen)
            {
                UpdatePointerOverDebugOverlay(debugRect);
                UpdatePointerOverPanel(boxRect, expandedRect);
                return;
            }

            if (showTrainingSettings)
            {
                float settingsWidth = Mathf.Min(SettingsWidth, Screen.width - Padding * 2f);
                Rect settingsRect = BuildExpandedPanelRect(boxRect, settingsWidth, SettingsHeight);
                DrawTrainingSettings(settingsRect);
                expandedRect = settingsRect;
            }
            else if (showJobDetails)
            {
                Rect detailsRect = BuildExpandedPanelRect(boxRect, boxRect.width, DetailsHeight);
                DrawJobDetails(detailsRect);
                expandedRect = detailsRect;
            }
            else if (showLibraryDetails)
            {
                Rect detailsRect = BuildExpandedPanelRect(boxRect, boxRect.width, DetailsHeight);
                DrawLibraryDetails(detailsRect);
                expandedRect = detailsRect;
            }

            UpdatePointerOverDebugOverlay(debugRect);
            UpdatePointerOverPanel(boxRect, expandedRect);
        }

        private Rect DrawCompactPanel()
        {
            float boxWidth = Mathf.Min(CompactWidth, Screen.width - Padding * 2f);
            float boxHeight = Mathf.Min(CompactHeight, Screen.height - Padding * 2f);
            Rect boxRect = new(Screen.width - boxWidth - Padding, CompactTopMargin, boxWidth, boxHeight);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            Rect contentRect = new(boxRect.x + Padding, boxRect.y + Padding, boxRect.width - Padding * 2f, boxRect.height - Padding * 2f);
            GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 34f), FormatCompactStatus(), compactDetailStyle);
            GUI.Label(new Rect(contentRect.x, contentRect.y + 34f, contentRect.width, 30f), FormatCompactJobSummary(), compactDetailStyle);

            DrawActionButtons(contentRect, contentRect.y + 70f, CompactButtonHeight);
            return boxRect;
        }

        private string FormatCompactStatus()
        {
            string state = latestResult?.status;
            if (string.IsNullOrWhiteSpace(state))
            {
                state = string.IsNullOrWhiteSpace(status) ? "Cloud: idle" : status;
            }

            return $"{Shorten(state, 22)} · stream {FormatCompactStreamState()} · events {resultStreamEventCount}";
        }

        private string FormatCompactJobSummary()
        {
            return $"{FormatTrainingCoreSummary(sceneBuilder?.TrainingSettings)} · {FormatSensorSummary(sceneBuilder?.TrainingSettings)}";
        }

        private string FormatCompactStreamState()
        {
            return resultStreamState switch
            {
                "not connected" => "off",
                "connecting" => "connecting",
                "connected" => "on",
                "receiving" => "receiving",
                "missing URL" => "no url",
                _ => resultStreamState,
            };
        }

        private static Rect BuildExpandedPanelRect(Rect anchorRect, float panelWidth, float preferredHeight)
        {
            float x = anchorRect.x;
            float y = anchorRect.yMax + Padding;
            float availableHeight = Screen.height - y - Padding;

            if (Screen.width >= anchorRect.width + panelWidth + Padding * 3f)
            {
                x = anchorRect.x - panelWidth - Padding;
                y = Mathf.Min(anchorRect.y + 210f, Screen.height - Padding - 320f);
                availableHeight = Screen.height - y - Padding;
            }

            return new Rect(
                Mathf.Max(Padding, x),
                Mathf.Max(Padding, y),
                panelWidth,
                Mathf.Min(preferredHeight, Mathf.Max(280f, availableHeight)));
        }

        public void ShowTrainingSettingsForAutomation(bool rewardSettings)
        {
            showPanel = true;
            showTrainingSettings = true;
            showRewardSettings = rewardSettings;
            showJobDetails = false;
            showLibraryDetails = false;
            SyncTextFromTrainingSettings();
        }

        public void ShowJobDetailsForAutomation()
        {
            showPanel = true;
            showTrainingSettings = false;
            showJobDetails = true;
            showLibraryDetails = false;
        }

        private void CollapseExpandedPanel()
        {
            showTrainingSettings = false;
            showJobDetails = false;
            showLibraryDetails = false;
        }

        private void DrawActionButtons(Rect contentRect, float top, float height)
        {
            float buttonWidth = (contentRect.width - ButtonGap * 3f) / 4f;
            Rect replayRect = new(contentRect.x, top, buttonWidth, height);
            Rect submitRect = new(replayRect.xMax + ButtonGap, top, buttonWidth, height);
            Rect downloadRect = new(submitRect.xMax + ButtonGap, top, buttonWidth, height);
            Rect aiRect = new(downloadRect.xMax + ButtonGap, top, buttonWidth, height);

            if (DrawButton(replayRect, new GUIContent("Replay", "Load replay"), buttonStyle, !busy))
            {
                StartCoroutine(LoadLatestReplay());
            }

            if (DrawButton(submitRect, new GUIContent("Submit", "Submit and train"), buttonStyle, !busy && apiClient != null && sceneBuilder != null))
            {
                StartCoroutine(SubmitAndTrain());
            }

            if (DrawButton(downloadRect, new GUIContent("Download", "Download artifacts"), buttonStyle, !busy))
            {
                StartCoroutine(DownloadAvailableArtifacts());
            }

            GUIStyle aiButtonStyle = inferenceController != null && inferenceController.IsRunning
                ? selectedButtonStyle
                : buttonStyle;
            if (DrawButton(aiRect, new GUIContent("Run AI", "Run downloaded model"), aiButtonStyle, !busy))
            {
                ToggleInferenceMode();
            }

            float secondTop = top + height + ButtonGap;
            float secondaryWidth = (contentRect.width - ButtonGap * 2f) / 3f;
            Rect jobRect = new(contentRect.x, secondTop, secondaryWidth, height);
            Rect libraryRect = new(jobRect.xMax + ButtonGap, secondTop, secondaryWidth, height);
            Rect settingsRect = new(libraryRect.xMax + ButtonGap, secondTop, secondaryWidth, height);

            GUIStyle jobStyle = showJobDetails ? selectedButtonStyle : buttonStyle;
            if (DrawButton(jobRect, new GUIContent("Job", "Job details"), jobStyle, !busy))
            {
                ToggleJobDetails();
            }

            GUIStyle libraryStyle = showLibraryDetails ? selectedButtonStyle : buttonStyle;
            if (DrawButton(libraryRect, new GUIContent("Library", "Maps, results, models"), libraryStyle, !busy))
            {
                ToggleLibraryDetails();
            }

            GUIStyle settingsStyle = showTrainingSettings ? selectedButtonStyle : buttonStyle;
            if (DrawButton(settingsRect, new GUIContent("Settings", "Training settings"), settingsStyle, !busy))
            {
                ToggleTrainingSettings();
            }
        }

        private static bool DrawButton(Rect rect, GUIContent content, GUIStyle style, bool enabled)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, content, style);
            GUI.enabled = previousEnabled;
            return clicked;
        }

        private static void UpdatePointerOverPanel(Rect compactRect, Rect expandedRect)
        {
            Rect panelRect = expandedRect == Rect.zero ? compactRect : Union(compactRect, expandedRect);
            EnvForge.Navigation.NavigationInputBlocker.RegisterPanel(nameof(EnvForgeCloudRunPanel), panelRect);
        }

        private static void UpdatePointerOverDebugOverlay(Rect debugRect)
        {
            string panelId = nameof(EnvForgeCloudRunPanel) + "Debug";
            if (debugRect == Rect.zero)
            {
                EnvForge.Navigation.NavigationInputBlocker.UnregisterPanel(panelId);
                return;
            }

            EnvForge.Navigation.NavigationInputBlocker.RegisterPanel(panelId, debugRect);
        }

        private Rect DrawDebugOverlayIfNeeded()
        {
#if !UNITY_EDITOR
            return Rect.zero;
#else
            if (!Application.isPlaying || !HasDebugOverlayInfo())
            {
                return Rect.zero;
            }

            float width = Mathf.Max(320f, Screen.width - Padding * 2f);
            float height = Mathf.Min(DebugOverlayMaxHeight, Screen.height - Padding * 2f);
            Rect rect = new(Padding, Padding, width, height);
            GUI.Box(rect, GUIContent.none, debugBoxStyle);
            Rect content = new(rect.x + Padding, rect.y + Padding, rect.width - Padding * 2f, rect.height - Padding * 2f);

            GUILayout.BeginArea(content);
            GUILayout.Label("Editor Debug", debugTitleStyle, GUILayout.Height(34f));
            GUILayout.Label($"Status: {FormatDebugValue(status)}", debugInfoStyle);
            GUILayout.Label($"Submission: {FormatDebugValue(Shorten(submissionId, 48))}", debugInfoStyle);
            GUILayout.Label($"API Base: {FormatDebugRawValue(apiClient?.BaseUrl)}", debugUrlStyle);
            GUILayout.Label($"Fetch URL: {FormatDebugRawValue(BuildCurrentResultFetchUrl())}", debugUrlStyle);
            GUILayout.Label($"Stream: {FormatResultStreamSummary()}", debugInfoStyle);
            GUILayout.Label($"Stream error: {FormatDebugValue(lastResultStreamError)}", debugErrorStyle);
            GUILayout.Label($"Fetch error: {FormatDebugValue(lastResultFetchError)}", debugErrorStyle);
            GUILayout.Label($"Fetch failures: {consecutiveResultFetchFailures} · retry {FormatNextResultRetry()}", debugInfoStyle);
            GUILayout.Label($"Runtime start: {FormatDebugValue(sceneBuilder?.LastRuntimeStartSummary)} · {FormatDebugValue(inferenceController?.LastRuntimePoseSummary)}", debugInfoStyle);
            GUILayout.Label($"Replay: {FormatDebugValue(loadedReplaySummary)} · AI: {(inferenceController != null && inferenceController.IsRunning ? "running" : "off")}", debugInfoStyle);
            GUILayout.EndArea();
            return rect;
#endif
        }

        private bool HasDebugOverlayInfo()
        {
            return !string.IsNullOrWhiteSpace(lastResultStreamError) ||
                !string.IsNullOrWhiteSpace(lastResultFetchError) ||
                consecutiveResultFetchFailures > 0;
        }

        private string BuildCurrentResultFetchUrl()
        {
            if (apiClient == null || string.IsNullOrWhiteSpace(submissionId))
            {
                return string.Empty;
            }

            return apiClient.BuildResultUrl(submissionId);
        }

        private string FormatNextResultRetry()
        {
            if (nextResultPollAt <= Time.unscaledTime)
            {
                return "now";
            }

            return $"in {Mathf.Max(0f, nextResultPollAt - Time.unscaledTime):0}s";
        }

        private static string FormatDebugValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : FormatUserFacingError(value);
        }

        private static string FormatDebugRawValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "none"
                : Shorten(value.Replace('\r', ' ').Replace('\n', ' ').Trim(), 220);
        }

        private static Rect Union(Rect first, Rect second)
        {
            float xMin = Mathf.Min(first.xMin, second.xMin);
            float yMin = Mathf.Min(first.yMin, second.yMin);
            float xMax = Mathf.Max(first.xMax, second.xMax);
            float yMax = Mathf.Max(first.yMax, second.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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
            lastResultStreamError = null;
            lastResultFetchError = null;
            consecutiveResultFetchFailures = 0;
            resultStreamErrorReported = false;
            autoDownloadStarted = false;
            nextResultPollAt = Time.unscaledTime + ResultPollIntervalSeconds;
            status = "Cloud: submitting scenario";

            ScenarioBundleDto scenario = sceneBuilder.BuildScenarioBundle();
            activeScenarioId = scenario.scenario_id;
            string trainerSummary = FormatScenarioTrainerSummary(scenario);
            string settingsName = GetCurrentSettingsName();
            bool failed = false;
            yield return apiClient.SubmitScenario(
                scenario,
                response =>
                {
                    submissionId = response.submission_id;
                    jobHistoryStore.UpsertSubmittedJob(submissionId, scenario, trainerSummary, settingsName);
                    status = $"Cloud: submitted {submissionId}";
                },
                error =>
                {
                    failed = true;
                    status = $"Cloud submit failed: {FormatUserFacingError(error)}";
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
                    status = $"Cloud train failed: {FormatUserFacingError(error)}";
                });

            busy = false;
            if (!failed)
            {
                StartResultStream();
                StartCoroutine(FetchLatestResult("submitted job"));
            }
        }

        private IEnumerator ResumeLatestJob()
        {
            restoredJobNeedsResume = false;
            if (string.IsNullOrWhiteSpace(submissionId) || apiClient == null)
            {
                yield break;
            }

            status = $"Cloud: resuming {Shorten(submissionId, 18)}";
            StartResultStream();
            yield return FetchLatestResult("restored job");
        }

        private IEnumerator FetchLatestResult(string source)
        {
            if (string.IsNullOrWhiteSpace(submissionId) || apiClient == null)
            {
                yield break;
            }

            if (resultFetchInFlight)
            {
                pendingResultFetchSource = source;
                yield break;
            }

            string requestedSubmissionId = submissionId;
            resultFetchInFlight = true;
            bool found = false;
            ResultDocumentDto fetchedResult = null;
            yield return apiClient.GetResult(
                requestedSubmissionId,
                result =>
                {
                    found = true;
                    fetchedResult = result;
                },
                error =>
                {
                    HandleResultFetchError(source, requestedSubmissionId, error);
                });
            resultFetchInFlight = false;
            nextResultPollAt = Time.unscaledTime + GetResultPollBackoffSeconds();

            if (!string.Equals(requestedSubmissionId, submissionId, StringComparison.Ordinal))
            {
                Debug.Log($"Ignored result fetch for inactive job {Shorten(requestedSubmissionId, 18)}");
                RestartPendingResultFetchIfNeeded();
                yield break;
            }

            if (!found || fetchedResult == null)
            {
                RestartPendingResultFetchIfNeeded();
                yield break;
            }

            lastResultFetchError = null;
            consecutiveResultFetchFailures = 0;
            if (!ApplyResultUpdate(fetchedResult, requestedSubmissionId, countStreamEvent: false))
            {
                RestartPendingResultFetchIfNeeded();
                yield break;
            }

            status = $"Cloud: fetched {fetchedResult.status}";
            TryAutoDownloadArtifacts();
            RestartPendingResultFetchIfNeeded();
        }

        private void RestartPendingResultFetchIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(pendingResultFetchSource) ||
                resultFetchInFlight ||
                string.IsNullOrWhiteSpace(submissionId) ||
                apiClient == null)
            {
                return;
            }

            string source = pendingResultFetchSource;
            pendingResultFetchSource = null;
            StartCoroutine(FetchLatestResult(source));
        }

        private void PollLatestResultIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(submissionId) ||
                apiClient == null ||
                resultFetchInFlight ||
                IsTerminalResultStatus(latestResult?.status) ||
                Time.unscaledTime < nextResultPollAt)
            {
                return;
            }

            StartCoroutine(FetchLatestResult("poll"));
        }

        private void StartResultStream()
        {
            StopResultStream("reconnecting");
            string url = BuildResultWebSocketUrl(submissionId);
            if (string.IsNullOrWhiteSpace(url))
            {
                resultStreamState = "missing URL";
                status = "Cloud: result stream URL missing";
                Debug.LogWarning("Result stream URL is not configured. Set WebSocket Url Template on ApiSettings or NavigationSceneBuilder.");
                showTrainingSettings = false;
                showJobDetails = true;
                showLibraryDetails = false;
                return;
            }

            resultStreamState = "connecting";
            lastResultStreamError = null;
            resultStreamErrorReported = false;
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
                    Debug.LogWarning($"Ignored result stream message: {Shorten(message, 160)}");
                    continue;
                }

                ApplyResultUpdate(result, submissionId);
            }

            string streamError = resultWebSocketClient.LastError;
            if (!resultStreamErrorReported && !string.IsNullOrWhiteSpace(streamError))
            {
                resultStreamErrorReported = true;
                lastResultStreamError = streamError;
                if (IsExpectedResultStreamClose(streamError))
                {
                    StopResultStream("closed");
                    status = "Cloud: result stream closed";
                    return;
                }

                resultStreamState = "failed";
                status = $"Result stream failed: {FormatUserFacingError(streamError)}";
                Debug.LogWarning($"Result stream failed: {streamError}");
            }
        }

        private bool ApplyResultUpdate(ResultDocumentDto result, string expectedSubmissionId = null, bool countStreamEvent = true)
        {
            string resultSubmissionId = string.IsNullOrWhiteSpace(result?.submission_id)
                ? expectedSubmissionId ?? submissionId
                : result.submission_id;
            string activeSubmissionId = expectedSubmissionId ?? submissionId;
            if (!string.IsNullOrWhiteSpace(activeSubmissionId) &&
                !string.Equals(resultSubmissionId, activeSubmissionId, StringComparison.Ordinal))
            {
                Debug.Log($"Ignored result for inactive job {Shorten(resultSubmissionId, 18)}; active {Shorten(activeSubmissionId, 18)}");
                return false;
            }

            latestResult = result;
            if (countStreamEvent)
            {
                resultStreamEventCount += 1;
            }

            lastResultStreamStatus = result.status;
            lastResultStreamReceivedAt = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            jobHistoryStore.UpsertResult(resultSubmissionId, result);
            status = $"Cloud: {result.status}";

            if (IsTerminalResultStatus(result.status))
            {
                StopResultStream("closed");
                TryAutoDownloadArtifacts();
            }
            else
            {
                resultStreamState = "receiving";
            }

            return true;
        }

        private void TryAutoDownloadArtifacts()
        {
            if (autoDownloadStarted || busy || !IsCompletedResult())
            {
                return;
            }

            ResultArtifactsDto artifacts = GetResultArtifacts();
            if (artifacts == null || (artifacts.replay_bundle == null && artifacts.onnx_model == null))
            {
                return;
            }

            EnvForgeJobRecordDto activeJob = GetActiveJobRecord();
            if (activeJob != null &&
                !string.IsNullOrWhiteSpace(activeJob.local_onnx_path) &&
                !string.IsNullOrWhiteSpace(activeJob.local_replay_manifest_path) &&
                File.Exists(activeJob.local_replay_manifest_path) &&
                File.Exists(activeJob.local_onnx_path))
            {
                return;
            }

            autoDownloadStarted = true;
            StartCoroutine(DownloadAvailableArtifacts());
        }

        private IEnumerator DownloadReplay()
        {
            busy = true;
            status = "Cloud: downloading replay manifest";
            ArtifactLocationDto replayBundle = GetResultArtifacts().replay_bundle;
            string manifestPath = GetLocalReplayManifestPath();
            ReplayBundleManifestDto manifest = null;
            yield return artifactDownloader.DownloadFile(
                replayBundle,
                manifestPath,
                savedPath =>
                {
                    string manifestJson = File.ReadAllText(savedPath);
                    manifest = ScenarioBundleSerializer.FromReplayBundleManifestJson(manifestJson);
                },
                error =>
                {
                    busy = false;
                    status = $"Replay download failed: {FormatUserFacingError(error)}";
                });
            if (manifest == null)
            {
                yield break;
            }

            ConfigureActiveReplayBundle(replayBundle, manifest, manifestPath);
            jobHistoryStore.SetLocalReplayManifestPath(submissionId, manifestPath);
            loadedReplaySummary = $"Replay manifest ready: {manifest.chunks?.Count ?? 0} chunks";
            status = "Cloud: replay manifest ready";
            busy = false;
        }

        private IEnumerator DownloadAvailableArtifacts()
        {
            if (!IsCompletedResult())
            {
                status = "Cloud: wait for DONE before download";
                showTrainingSettings = false;
                showJobDetails = true;
                showLibraryDetails = false;
                yield break;
            }

            ResultArtifactsDto artifacts = GetResultArtifacts();
            if (artifacts == null ||
                (artifacts.replay_bundle == null && artifacts.onnx_model == null))
            {
                status = "Cloud: no downloadable artifacts";
                showTrainingSettings = false;
                showJobDetails = true;
                showLibraryDetails = false;
                yield break;
            }

            if (artifacts.replay_bundle != null)
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
            string outputDir = Path.Combine(Application.persistentDataPath, "EnvForge", GetSafeSubmissionDirectoryName(submissionId));
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

        private void HandleResultFetchError(string source, string requestedSubmissionId, string error)
        {
            if (!string.Equals(requestedSubmissionId, submissionId, StringComparison.Ordinal))
            {
                Debug.Log($"Ignored result fetch error for inactive job {Shorten(requestedSubmissionId, 18)}: {FormatUserFacingError(error)}");
                return;
            }

            consecutiveResultFetchFailures += 1;
            status = $"Cloud: result unavailable ({source})";
            bool repeatedError = string.Equals(lastResultFetchError, error, StringComparison.Ordinal);
            lastResultFetchError = error;

            if (!repeatedError)
            {
                Debug.LogWarning($"Result fetch failed for {submissionId}: {error}");
            }

            if (IsMissingResultError(error))
            {
                latestResult = new ResultDocumentDto
                {
                    submission_id = requestedSubmissionId,
                    status = "missing",
                };
                jobHistoryStore?.UpsertResult(requestedSubmissionId, latestResult);
                StopResultStream("missing result");
            }
        }

        private float GetResultPollBackoffSeconds()
        {
            if (IsTerminalResultStatus(latestResult?.status))
            {
                return ResultPollIntervalSeconds;
            }

            if (IsNetworkDestinationError(lastResultFetchError))
            {
                return ResultPollIntervalSeconds * 4f;
            }

            if (consecutiveResultFetchFailures >= 3)
            {
                return ResultPollIntervalSeconds * 2f;
            }

            return ResultPollIntervalSeconds;
        }

        private void StopResultStream(string nextState)
        {
            resultWebSocketClient?.Stop();
            resultStreamState = string.IsNullOrWhiteSpace(nextState) ? "closed" : nextState;
        }

        private static bool IsTerminalResultStatus(string resultStatus)
        {
            return string.Equals(resultStatus, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultStatus, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultStatus, "cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultStatus, "canceled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultStatus, "deleted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultStatus, "missing", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExpectedResultStreamClose(string error)
        {
            return !string.IsNullOrWhiteSpace(error) &&
                error.Contains("closed the WebSocket connection without completing the close handshake", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMissingResultError(string error)
        {
            return !string.IsNullOrWhiteSpace(error) &&
                (error.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("410", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("result not found", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("result deleted", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNetworkDestinationError(string error)
        {
            return !string.IsNullOrWhiteSpace(error) &&
                (error.Contains("Cannot resolve destination host", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase) ||
                 error.Contains("No such host", StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatTrainingCoreSummary(NavigationTrainingSettings settings)
        {
            if (settings == null)
            {
                return "none";
            }

            return $"ppo · {FormatSteps(settings.Timesteps)} · seed {settings.Seed} · envs {settings.NEnvs}";
        }

        private static string FormatResourceSummary(NavigationTrainingSettings settings)
        {
            if (settings == null)
            {
                return "none";
            }

            return $"cpu {settings.CpuCount} · torch {settings.TorchNumThreads}";
        }

        private static string FormatSensorSummary(NavigationTrainingSettings settings)
        {
            if (settings == null)
            {
                return "camera none";
            }

            return $"camera h {settings.CameraMountHeightMeters:0.##}m · range {settings.CameraMountHeightMinMeters:0.##}-{settings.CameraMountHeightMaxMeters:0.##}m";
        }

        private static string FormatPpoSummary(NavigationTrainingSettings settings)
        {
            if (settings == null)
            {
                return "ppo none";
            }

            return $"n_steps {settings.NSteps} · batch {settings.BatchSize} · lr {settings.LearningRate:0.######}";
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

        private string GetLocalReplayManifestPath()
        {
            string outputDir = Path.Combine(Application.persistentDataPath, "EnvForge", GetSafeSubmissionDirectoryName(submissionId), "replay");
            return Path.Combine(outputDir, "manifest.json");
        }

        private void ConfigureActiveReplayBundle(
            ArtifactLocationDto manifestArtifact,
            ReplayBundleManifestDto manifest,
            string manifestPath)
        {
            activeReplayManifestArtifact = manifestArtifact;
            activeReplayManifest = manifest;
            activeReplayManifestPath = manifestPath;
            activeReplayChunks.Clear();
            activeReplayTotalSteps = 0;
            activeReplayChunkIndex = -1;

            if (manifest?.chunks == null)
            {
                return;
            }

            foreach (ReplayBundleChunkDto chunk in manifest.chunks)
            {
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.path))
                {
                    continue;
                }

                activeReplayChunks.Add(chunk);
                activeReplayTotalSteps += Mathf.Max(0, chunk.step_count);
            }
        }

        private IEnumerator LoadReplayChunkAt(int chunkIndex, bool autoPlay, bool startAtEnd)
        {
            if (replayChunkLoadInFlight)
            {
                yield break;
            }

            if (chunkIndex < 0 || chunkIndex >= activeReplayChunks.Count)
            {
                status = chunkIndex < 0 ? "Replay: first chunk" : "Replay: finished";
                yield break;
            }

            replayChunkLoadInFlight = true;
            busy = true;

            ReplayBundleChunkDto chunk = activeReplayChunks[chunkIndex];
            string localChunkPath = GetLocalReplayChunkPath(chunk);
            if (string.IsNullOrEmpty(localChunkPath))
            {
                status = "Replay output path is unavailable";
                FinishReplayChunkLoad();
                yield break;
            }

            string localChunkDir = Path.GetDirectoryName(localChunkPath);
            if (!string.IsNullOrEmpty(localChunkDir))
            {
                Directory.CreateDirectory(localChunkDir);
            }

            if (!File.Exists(localChunkPath))
            {
                if (activeReplayManifestArtifact == null)
                {
                    status = "Replay artifact metadata missing";
                    FinishReplayChunkLoad();
                    yield break;
                }

                ArtifactLocationDto chunkArtifact = BuildReplayChunkArtifact(activeReplayManifestArtifact, chunk.path, chunk.format);
                bool failed = false;
                status = $"Cloud: downloading replay {FormatReplayChunkLabel(chunk)}";
                yield return artifactDownloader.DownloadFile(
                    chunkArtifact,
                    localChunkPath,
                    _ => { },
                    error =>
                    {
                        failed = true;
                        status = $"Replay chunk download failed: {FormatUserFacingError(error)}";
                    });

                if (failed)
                {
                    FinishReplayChunkLoad();
                    yield break;
                }
            }

            List<ReplayLogStepDto> chunkSteps = new();
            foreach (string line in ReadReplayChunkLines(localChunkPath))
            {
                chunkSteps.Add(ReplayLogSerializer.FromReplayLogStepJson(line));
            }

            List<ReplayLogStepDto> displaySteps = BuildReplayDisplaySteps(chunkSteps);
            EnvForgeJobRecordDto job = jobHistoryStore.SetLocalReplayBundlePaths(submissionId, activeReplayManifestPath, localChunkPath);
            activeReplayScenarioSource = ApplyReplayScenario(job);
            replayPlayer.LoadWindow(
                displaySteps,
                chunkIndex,
                activeReplayChunks.Count,
                0,
                displaySteps.Count,
                $"{FormatReplayChunkLabel(chunk)} env {ReplayDisplayEnvIndex}",
                autoPlay,
                startAtEnd);
            activeReplayChunkIndex = chunkIndex;
            loadedReplaySummary = FormatReplaySummary(displaySteps, $"Replay {FormatReplayChunkLabel(chunk)} env {ReplayDisplayEnvIndex}", activeReplayScenarioSource);
            status = $"Cloud: replay {FormatReplayChunkLabel(chunk)} loaded";
            FinishReplayChunkLoad();
        }

        private void FinishReplayChunkLoad()
        {
            replayChunkLoadInFlight = false;
            busy = false;
        }

        private string GetLocalReplayChunkPath(ReplayBundleChunkDto chunk)
        {
            string manifestPath = activeReplayManifestPath;
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                manifestPath = GetLocalReplayManifestPath();
            }

            string outputDir = Path.GetDirectoryName(manifestPath);
            return string.IsNullOrEmpty(outputDir)
                ? string.Empty
                : TryBuildSafeChildPath(outputDir, chunk.path, out string localPath)
                    ? localPath
                    : string.Empty;
        }

        private int GetReplayChunkGlobalStartStepIndex(int chunkIndex)
        {
            int start = 0;
            for (int i = 0; i < chunkIndex && i < activeReplayChunks.Count; i++)
            {
                start += Mathf.Max(0, activeReplayChunks[i].step_count);
            }

            return start;
        }

        private void HandleReplayWindowBoundaryRequested(int direction, bool autoPlay)
        {
            if (replayChunkLoadInFlight || activeReplayChunks.Count == 0)
            {
                return;
            }

            int nextIndex = activeReplayChunkIndex + Math.Sign(direction);
            if (nextIndex < 0)
            {
                status = "Replay: first chunk";
                return;
            }

            if (nextIndex >= activeReplayChunks.Count)
            {
                status = "Replay: finished";
                return;
            }

            StartCoroutine(LoadReplayChunkAt(nextIndex, autoPlay, direction < 0));
        }

        private static string FormatReplayChunkLabel(ReplayBundleChunkDto chunk)
        {
            if (chunk == null)
            {
                return "chunk";
            }

            string phase = string.IsNullOrWhiteSpace(chunk.phase) ? "replay" : chunk.phase;
            return $"{phase} {FormatSteps(chunk.checkpoint_step)}";
        }

        private static ArtifactLocationDto BuildReplayChunkArtifact(
            ArtifactLocationDto manifestArtifact,
            string chunkPath,
            string format)
        {
            string basePath = manifestArtifact.path;
            int slashIndex = basePath.LastIndexOf('/');
            string replayRoot = slashIndex >= 0 ? basePath[..slashIndex] : string.Empty;
            string objectPath = string.IsNullOrWhiteSpace(replayRoot)
                ? chunkPath
                : $"{replayRoot}/{chunkPath}";

            return new ArtifactLocationDto
            {
                storage = manifestArtifact.storage,
                bucket = manifestArtifact.bucket,
                path = objectPath,
                format = string.IsNullOrWhiteSpace(format) ? "jsonl.gz" : format,
            };
        }

        private static IEnumerable<string> ReadReplayChunkLines(string path)
        {
            using (Stream source = File.OpenRead(path))
            {
                using Stream readable = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? new GZipStream(source, CompressionMode.Decompress)
                    : source;
                using StreamReader reader = new(readable);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        yield return line;
                    }
                }
            }
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
                    status = $"Cloud: saved {Path.GetFileName(savedPath)}";
                },
                error => status = $"Download failed: {FormatUserFacingError(error)}");
        }

        private void DrawJobDetails(Rect panelRect)
        {
            GUI.Box(panelRect, GUIContent.none, boxStyle);
            GUILayout.BeginArea(new Rect(panelRect.x + Padding, panelRect.y + Padding, panelRect.width - Padding * 2f, panelRect.height - Padding * 2f));
            jobDetailsScroll = GUILayout.BeginScrollView(jobDetailsScroll);

            GUILayout.Label("Result", statusStyle);
            GUILayout.Label($"Status: {latestResult?.status ?? "none"}", detailStyle);
            GUILayout.Label(FormatResultStreamSummary(), detailStyle);
            if (HasDebugOverlayInfo())
            {
                GUILayout.Label("Editor debug overlay available", detailStyle);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Submitted Job", labelStyle);
            GUILayout.Label($"Job: {Shorten(submissionId, 18)}", detailStyle);
            GUILayout.Label($"Scenario: {activeScenarioId ?? "none"}", detailStyle);
            GUILayout.Label($"Settings name: {GetSubmittedSettingsName()}", detailStyle);
            GUILayout.Label($"Training: {FormatTrainingCoreSummary(sceneBuilder?.TrainingSettings)}", detailStyle);
            GUILayout.Label($"Resources: {FormatResourceSummary(sceneBuilder?.TrainingSettings)}", detailStyle);
            GUILayout.Label($"PPO: {FormatPpoSummary(sceneBuilder?.TrainingSettings)}", detailStyle);
            GUILayout.Label($"Sensor: {FormatSensorSummary(sceneBuilder?.TrainingSettings)}", detailStyle);
            GUILayout.Label(FormatProgressSummary(), detailStyle);

            GUILayout.Space(8f);
            GUILayout.Label("Runtime", labelStyle);
            GUILayout.Label(FormatReplayLoadedSummary(), detailStyle);
            GUILayout.Label(FormatCloudArtifactSummary(), detailStyle);
            GUILayout.Label(FormatLocalArtifactSummary(), detailStyle);
            GUILayout.Label(FormatCurrentMapSummary(), detailStyle);
            GUILayout.Label(FormatCurrentModelSummary(), detailStyle);
            GUILayout.Label(FormatInferenceSummary(), detailStyle);
            if (!string.IsNullOrWhiteSpace(inferenceController?.LastObservationSummary))
            {
                GUILayout.Label(inferenceController.LastObservationSummary, detailStyle);
            }

            if (!string.IsNullOrWhiteSpace(inferenceController?.LastErrorDetails))
            {
                GUILayout.Label($"AI error: {FormatUserFacingError(inferenceController.LastErrorDetails)}", detailStyle);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                GUILayout.Space(8f);
                GUILayout.Label(status, detailStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLibraryDetails(Rect panelRect)
        {
            GUI.Box(panelRect, GUIContent.none, boxStyle);
            GUILayout.BeginArea(new Rect(panelRect.x + Padding, panelRect.y + Padding, panelRect.width - Padding * 2f, panelRect.height - Padding * 2f));
            libraryDetailsScroll = GUILayout.BeginScrollView(libraryDetailsScroll);

            GUILayout.Label("Library", statusStyle);
            DrawSelectedJobNameEditor();
            bool previousTopEnabled = GUI.enabled;
            GUI.enabled = previousTopEnabled && !libraryRefreshInFlight && jobHistoryStore != null && jobHistoryStore.Jobs.Count > 0;
            if (GUILayout.Button(libraryRefreshInFlight ? "Refreshing" : "Refresh History", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                StartCoroutine(RefreshLibraryResults());
            }

            GUI.enabled = previousTopEnabled;
            GUILayout.Space(8f);

            if (jobHistoryStore == null || jobHistoryStore.Jobs.Count == 0)
            {
                GUILayout.Label("No jobs yet", detailStyle);
            }
            else
            {
                List<EnvForgeJobRecordDto> visibleJobs = BuildLibraryVisibleJobs();

                foreach (EnvForgeJobRecordDto job in visibleJobs)
                {
                    if (job == null)
                    {
                        continue;
                    }

                    bool selected = string.Equals(job.submission_id, submissionId, StringComparison.OrdinalIgnoreCase);
                    GUILayout.Label(FormatLibraryJobSummary(job, selected), detailStyle);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select", selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(ButtonHeight)))
                    {
                        SelectLibraryJob(job, reconnect: false);
                    }

                    bool hasReplay = !string.IsNullOrWhiteSpace(job.local_replay_manifest_path) && File.Exists(job.local_replay_manifest_path);
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = previousEnabled && hasReplay;
                    if (GUILayout.Button("Replay", buttonStyle, GUILayout.Height(ButtonHeight)))
                    {
                        SelectLibraryJob(job, reconnect: false);
                        StartCoroutine(LoadLatestReplay());
                    }

                    GUI.enabled = previousEnabled && !string.IsNullOrWhiteSpace(job.submission_id);
                    if (GUILayout.Button("Fetch", buttonStyle, GUILayout.Height(ButtonHeight)))
                    {
                        SelectLibraryJob(job, reconnect: false);
                        StartCoroutine(FetchLatestResult("library"));
                    }

                    GUI.enabled = previousEnabled && !string.IsNullOrWhiteSpace(job.submission_id);
                    string deleteLabel = string.Equals(pendingDeleteSubmissionId, job.submission_id, StringComparison.Ordinal)
                        ? "Confirm"
                        : "Remove";
                    if (GUILayout.Button(deleteLabel, buttonStyle, GUILayout.Height(ButtonHeight)))
                    {
                        RemoveLibraryJob(job);
                        GUI.enabled = previousEnabled;
                        GUILayout.EndHorizontal();
                        GUILayout.EndScrollView();
                        GUILayout.EndArea();
                        return;
                    }

                    GUI.enabled = previousEnabled;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(6f);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSelectedJobNameEditor()
        {
            EnvForgeJobRecordDto activeJob = GetActiveJobRecord();
            if (activeJob == null)
            {
                GUILayout.Label("Select a job to rename it.", detailStyle);
                return;
            }

            EnsureLibraryNameText(activeJob);
            GUILayout.Label($"Selected: {Shorten(activeJob.submission_id, 18)}", detailStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", detailStyle, GUILayout.Width(72f), GUILayout.Height(ButtonHeight));
            GUI.SetNextControlName(SettingsTextFieldFocusPrefix + "library_name");
            libraryNameText = GUILayout.TextField(libraryNameText ?? string.Empty, textFieldStyle, GUILayout.Height(ButtonHeight));
            RegisterTextInputFocus();
            if (GUILayout.Button("Save", buttonStyle, GUILayout.Width(96f), GUILayout.Height(ButtonHeight)))
            {
                EnvForgeJobRecordDto renamed = jobHistoryStore.SetDisplayName(activeJob.submission_id, libraryNameText);
                libraryNameText = renamed?.display_name ?? libraryNameText;
                status = $"Library: renamed {Shorten(activeJob.submission_id, 18)}";
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private List<EnvForgeJobRecordDto> BuildLibraryVisibleJobs()
        {
            List<EnvForgeJobRecordDto> visibleJobs = new();
            if (jobHistoryStore == null)
            {
                return visibleJobs;
            }

            for (int i = 0; i < jobHistoryStore.Jobs.Count; i++)
            {
                EnvForgeJobRecordDto job = jobHistoryStore.Jobs[i];
                if (job == null)
                {
                    continue;
                }

                visibleJobs.Add(job);
            }

            visibleJobs.Sort(CompareLibraryJobsBySubmittedAtDescending);
            return visibleJobs;
        }

        private static int CompareLibraryJobsBySubmittedAtDescending(EnvForgeJobRecordDto left, EnvForgeJobRecordDto right)
        {
            DateTime leftSubmitted = ParseUtcDateTimeOrMin(left?.submitted_at_utc);
            DateTime rightSubmitted = ParseUtcDateTimeOrMin(right?.submitted_at_utc);
            int dateComparison = rightSubmitted.CompareTo(leftSubmitted);
            if (dateComparison != 0)
            {
                return dateComparison;
            }

            return string.Compare(right?.submission_id, left?.submission_id, StringComparison.Ordinal);
        }

        private void EnsureLibraryNameText(EnvForgeJobRecordDto job)
        {
            if (job == null)
            {
                return;
            }

            if (string.Equals(libraryNameSubmissionId, job.submission_id, StringComparison.Ordinal))
            {
                return;
            }

            libraryNameSubmissionId = job.submission_id;
            libraryNameText = GetJobDisplayName(job);
        }

        private void RestoreActiveJobAfterHistoryMutation()
        {
            if (jobHistoryStore == null || string.IsNullOrWhiteSpace(submissionId) || jobHistoryStore.FindJob(submissionId) != null)
            {
                return;
            }

            EnvForgeJobRecordDto recentJob = jobHistoryStore.MostRecentRecord;
            submissionId = recentJob?.submission_id;
            activeScenarioId = recentJob?.scenario_id;
            latestResult = null;
        }

        private void RemoveLibraryJob(EnvForgeJobRecordDto job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.submission_id))
            {
                return;
            }

            if (!string.Equals(pendingDeleteSubmissionId, job.submission_id, StringComparison.Ordinal))
            {
                pendingDeleteSubmissionId = job.submission_id;
                status = $"Library: confirm remove {Shorten(job.submission_id, 18)}";
                return;
            }

            DeleteLocalArtifactIfPresent(job.local_replay_manifest_path);
            DeleteLocalArtifactIfPresent(job.local_replay_chunk_path);
            DeleteLocalArtifactIfPresent(job.local_onnx_path);
            if (jobHistoryStore.RemoveJob(job.submission_id))
            {
                RestoreActiveJobAfterHistoryMutation();
                status = $"Library: removed {Shorten(job.submission_id, 18)}";
            }

            pendingDeleteSubmissionId = null;
        }

        private static void DeleteLocalArtifactIfPresent(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !IsSafeLocalArtifactPath(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"Local artifact delete failed: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning($"Local artifact delete failed: {ex.Message}");
            }
        }

        private static bool IsSafeLocalArtifactPath(string path)
        {
            try
            {
                string root = Path.GetFullPath(Application.persistentDataPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(path);
                return fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private void SelectLibraryJob(EnvForgeJobRecordDto job, bool reconnect)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.submission_id))
            {
                return;
            }

            EnvForgeJobRecordDto selectedJob = jobHistoryStore.FindJob(job.submission_id) ?? job;
            StopResultStream("selected job");
            submissionId = selectedJob.submission_id;
            activeScenarioId = selectedJob.scenario_id;
            loadedReplaySummary = !string.IsNullOrWhiteSpace(selectedJob.local_replay_manifest_path)
                ? $"manifest saved for {Shorten(selectedJob.submission_id, 18)}"
                : null;
            latestResult = null;
            autoDownloadStarted = false;
            lastResultFetchError = null;
            consecutiveResultFetchFailures = 0;
            resultStreamEventCount = 0;
            lastResultStreamStatus = null;
            lastResultStreamReceivedAt = null;
            lastResultStreamError = null;
            resultStreamErrorReported = false;
            pendingDeleteSubmissionId = null;
            libraryNameSubmissionId = null;
            libraryNameText = null;
            if (!string.IsNullOrWhiteSpace(selectedJob.settings_name))
            {
                settingsNameText = selectedJob.settings_name;
            }

            status = $"Cloud: selected {Shorten(submissionId, 18)}";

            if (reconnect)
            {
                StartResultStream();
                StartCoroutine(FetchLatestResult("library"));
            }
        }

        private IEnumerator RefreshLibraryResults()
        {
            if (libraryRefreshInFlight || jobHistoryStore == null || apiClient == null)
            {
                yield break;
            }

            libraryRefreshInFlight = true;
            status = "Library: refreshing history";
            List<EnvForgeJobRecordDto> jobs = new(jobHistoryStore.Jobs);
            foreach (EnvForgeJobRecordDto job in jobs)
            {
                if (job == null || string.IsNullOrWhiteSpace(job.submission_id))
                {
                    continue;
                }

                yield return RefreshLibraryJobResult(job.submission_id);
            }

            libraryRefreshInFlight = false;
            status = "Library: history refreshed";
        }

        private IEnumerator RefreshLibraryJobResult(string requestedSubmissionId)
        {
            bool found = false;
            ResultDocumentDto fetchedResult = null;
            string fetchError = null;
            yield return apiClient.GetResult(
                requestedSubmissionId,
                result =>
                {
                    found = true;
                    fetchedResult = result;
                },
                error => fetchError = error);

            if (found && fetchedResult != null)
            {
                try
                {
                    if (string.Equals(requestedSubmissionId, submissionId, StringComparison.Ordinal))
                    {
                        ApplyResultUpdate(fetchedResult, requestedSubmissionId, countStreamEvent: false);
                    }
                    else
                    {
                        jobHistoryStore.UpsertResult(requestedSubmissionId, fetchedResult);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Library refresh update failed for {Shorten(requestedSubmissionId, 18)}: {ex.Message}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(fetchError))
            {
                Debug.LogWarning($"Library refresh failed for {Shorten(requestedSubmissionId, 18)}: {fetchError}");
            }
        }

        private static string FormatLibraryJobSummary(EnvForgeJobRecordDto job, bool selected)
        {
            string marker = selected ? "Current" : "Job";
            string state = string.IsNullOrWhiteSpace(job.status) ? "unknown" : job.status;
            string replay = !string.IsNullOrWhiteSpace(job.local_replay_manifest_path) && File.Exists(job.local_replay_manifest_path)
                ? "manifest local"
                : string.IsNullOrWhiteSpace(job.replay_artifact_path) ? "replay missing" : "replay cloud";
            string model = !string.IsNullOrWhiteSpace(job.local_onnx_path) && File.Exists(job.local_onnx_path)
                ? "model local"
                : string.IsNullOrWhiteSpace(job.onnx_artifact_path) ? "model missing" : "model cloud";
            string progress = job.progress_total_steps > 0
                ? $"{FormatSteps(job.progress_current_step)}/{FormatSteps(job.progress_total_steps)}"
                : FormatSteps(job.training_timesteps);
            string updated = string.IsNullOrWhiteSpace(job.history_updated_at_utc)
                ? "stale"
                : $"updated {FormatDateTimeLabel(job.history_updated_at_utc)}";
            string settings = string.IsNullOrWhiteSpace(job.settings_name)
                ? string.Empty
                : $" · cfg {Shorten(job.settings_name, 18)}";
            return $"{marker}: {Shorten(GetJobDisplayName(job), 28)} · {Shorten(job.submission_id, 10)}{settings} · {state} · {progress} · {replay} · {model} · {updated}";
        }

        private void ToggleJobDetails()
        {
            if (string.IsNullOrEmpty(submissionId) && latestResult == null)
            {
                status = "Cloud: no job yet";
            }

            showTrainingSettings = false;
            showLibraryDetails = false;
            showJobDetails = !showJobDetails;
        }

        private void ToggleLibraryDetails()
        {
            showTrainingSettings = false;
            showJobDetails = false;
            showLibraryDetails = !showLibraryDetails;
        }

        private void ToggleTrainingSettings()
        {
            showTrainingSettings = !showTrainingSettings;
            showJobDetails = false;
            showLibraryDetails = false;
            SyncTextFromTrainingSettings();
        }

        private string FormatReplayLoadedSummary()
        {
            return string.IsNullOrWhiteSpace(loadedReplaySummary)
                ? "Replay: not loaded"
                : $"Replay: {loadedReplaySummary}";
        }

        private string FormatCloudArtifactSummary()
        {
            if (IsCompletedResult())
            {
                ResultArtifactsDto artifacts = GetResultArtifacts();
                string replay = artifacts?.replay_bundle == null ? "replay missing" : "replay available";
                string model = artifacts?.onnx_model == null ? "model missing" : "model available";
                return $"Cloud: {replay} · {model}";
            }

            if (IsTerminalResultStatus(latestResult?.status))
            {
                return $"Cloud: {latestResult.status} · artifacts unavailable";
            }

            return "Cloud: replay waiting · model waiting";
        }

        private string FormatLocalArtifactSummary()
        {
            EnvForgeJobRecordDto activeJob = GetActiveJobRecord();
            string replay = activeJob != null &&
                !string.IsNullOrWhiteSpace(activeJob.local_replay_manifest_path) &&
                File.Exists(activeJob.local_replay_manifest_path)
                    ? "replay downloaded"
                    : "replay missing";
            string model = activeJob != null &&
                !string.IsNullOrWhiteSpace(activeJob.local_onnx_path) &&
                File.Exists(activeJob.local_onnx_path)
                    ? "model downloaded"
                    : "model missing";
            return $"Local: {replay} · {model}";
        }

        private string FormatInferenceSummary()
        {
            if (inferenceController == null)
            {
                return "AI: unavailable";
            }

            string state = inferenceController.IsRunning ? "AI: running" : "AI: off";
            return $"{state} · {inferenceController.LastActionSummary}";
        }

        private string FormatCurrentMapSummary()
        {
            return sceneBuilder == null
                ? "Map: unavailable"
                : sceneBuilder.CurrentScenarioSourceSummary;
        }

        private string FormatCurrentModelSummary()
        {
            EnvForgeJobRecordDto activeJob = GetActiveJobRecord();
            if (activeJob == null)
            {
                return "Model: no job selected";
            }

            string modelState = !string.IsNullOrWhiteSpace(activeJob.local_onnx_path) && File.Exists(activeJob.local_onnx_path)
                ? "downloaded"
                : "missing";
            return $"Model: {Shorten(GetJobDisplayName(activeJob), 28)} · {Shorten(activeJob.submission_id, 10)} · {modelState}";
        }

        private void ToggleInferenceMode()
        {
            if (inferenceController == null)
            {
                status = "AI: controller unavailable";
                return;
            }

            if (inferenceController.IsRunning)
            {
                inferenceController.StopInference();
                status = "AI: off";
                return;
            }

            StartInferenceWithCurrentModel();
        }

        private bool StartInferenceWithCurrentModel()
        {
            string modelPath = GetCurrentLocalOnnxModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                status = "AI: download model first";
                showTrainingSettings = false;
                showJobDetails = true;
                showLibraryDetails = false;
                return false;
            }

            Vector3 runtimeStartPosition = default;
            Quaternion runtimeStartRotation = default;
            bool hasRuntimeStartPose = sceneBuilder != null;
            if (sceneBuilder != null && !sceneBuilder.TrySelectRandomAgentPose(out runtimeStartPosition, out runtimeStartRotation, out string randomStartError))
            {
                status = $"AI: random start failed ({randomStartError})";
                return false;
            }

            replayPlayer?.ReleaseControl();
            string error;
            bool started = hasRuntimeStartPose
                ? inferenceController.StartInference(modelPath, runtimeStartPosition, runtimeStartRotation, out error)
                : inferenceController.StartInference(modelPath, out error);
            if (started)
            {
                sceneBuilder?.RecordRuntimeStart(runtimeStartPosition, runtimeStartRotation);
                status = sceneBuilder == null
                    ? "AI: running"
                    : $"AI: running · {FormatCurrentModelSummary()} · {sceneBuilder.LastRuntimeStartSummary}";
                return true;
            }

            status = "AI failed: see Console / Job details";
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogError($"Inference failed: {error}");
            }

            showTrainingSettings = false;
            showJobDetails = true;
            showLibraryDetails = false;
            return false;
        }

        private void HandleInferenceGoalReached()
        {
            RestartInferenceAfterTerminalEvent("goal reached");
        }

        private void HandleInferenceWallCollision()
        {
            RestartInferenceAfterTerminalEvent("wall collision");
        }

        private void RestartInferenceAfterTerminalEvent(string reason)
        {
            if (inferenceRestartInFlight)
            {
                return;
            }

            inferenceRestartInFlight = true;
            StartCoroutine(RestartInferenceAfterTerminalEventCoroutine(reason));
        }

        private IEnumerator RestartInferenceAfterTerminalEventCoroutine(string reason)
        {
            yield return null;
            if (inferenceController != null && inferenceController.IsRunning)
            {
                inferenceController.StopInference();
            }

            if (StartInferenceWithCurrentModel() && !string.IsNullOrWhiteSpace(reason))
            {
                status = $"{status} · after {reason}";
            }

            inferenceRestartInFlight = false;
        }

        private void HandleReplayControlRequested()
        {
            if (inferenceController == null || !inferenceController.IsRunning)
            {
                return;
            }

            inferenceController.StopInference();
            status = "AI: stopped for replay";
        }

        private string GetCurrentLocalOnnxModelPath()
        {
            EnvForgeJobRecordDto activeJob = GetActiveJobRecord();
            if (activeJob == null || string.IsNullOrWhiteSpace(activeJob.local_onnx_path))
            {
                return string.Empty;
            }

            return activeJob.local_onnx_path;
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

        private static string GetJobDisplayName(EnvForgeJobRecordDto job)
        {
            if (job == null)
            {
                return "Job";
            }

            if (!string.IsNullOrWhiteSpace(job.display_name))
            {
                return job.display_name;
            }

            if (!string.IsNullOrWhiteSpace(job.submission_id))
            {
                return $"Job {Shorten(job.submission_id, 8)}";
            }

            return "Job";
        }

        private static string FormatDateTimeLabel(string value)
        {
            DateTime parsed = ParseUtcDateTimeOrMin(value);
            if (parsed != DateTime.MinValue)
            {
                return parsed.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
            }

            return Shorten(value, 12);
        }

        private static DateTime ParseUtcDateTimeOrMin(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.MinValue;
        }

        private EnvForgeJobRecordDto GetActiveJobRecord()
        {
            if (jobHistoryStore == null || string.IsNullOrWhiteSpace(submissionId))
            {
                return null;
            }

            return jobHistoryStore.FindJob(submissionId);
        }

        private static string FormatUserFacingError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "unknown";
            }

            string normalized = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
            normalized = RedactUrlLikeText(normalized);
            return Shorten(normalized, 180);
        }

        private static string RedactUrlLikeText(string value)
        {
            string[] tokens = value.Split(' ');
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    tokens[i].StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    tokens[i].StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
                {
                    tokens[i] = "[artifact-url]";
                }
            }

            return string.Join(" ", tokens);
        }

        private static string GetSafeSubmissionDirectoryName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "latest";
            }

            string trimmed = value.Trim();
            if (Guid.TryParseExact(trimmed, "D", out _))
            {
                return trimmed;
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_')
                {
                    chars[i] = '_';
                }
            }

            string sanitized = new string(chars);
            string safePrefix = string.IsNullOrWhiteSpace(sanitized.Trim('_')) ? "submission" : sanitized;
            return $"{safePrefix}-{ShortHash(value)}";
        }

        private static string ShortHash(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new(12);
            for (int i = 0; i < 6 && i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static bool TryBuildSafeChildPath(string rootDirectory, string relativePath, out string safePath)
        {
            safePath = string.Empty;
            if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string normalizedRelativePath = relativePath.Replace('\\', '/');
            if (normalizedRelativePath.StartsWith("/", StringComparison.Ordinal) ||
                normalizedRelativePath.Contains(":", StringComparison.Ordinal))
            {
                return false;
            }

            string combined = rootDirectory;
            string[] parts = normalizedRelativePath.Split('/');
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part) ||
                    string.Equals(part, ".", StringComparison.Ordinal) ||
                    string.Equals(part, "..", StringComparison.Ordinal) ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return false;
                }

                combined = Path.Combine(combined, part);
            }

            string fullRoot = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(combined);
            if (!fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            safePath = fullPath;
            return true;
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

            if (GUILayout.Button("Smoke", GetPresetButtonStyle("Smoke"), GUILayout.Height(SettingsButtonHeight)))
            {
                sceneBuilder.TrainingSettings.ApplySmokePreset();
                SyncTextFromTrainingSettings();
                settingsNameText = sceneBuilder.TrainingSettings.PresetName;
            }

            if (GUILayout.Button("MVP", GetPresetButtonStyle("MVP"), GUILayout.Height(SettingsButtonHeight)))
            {
                sceneBuilder.TrainingSettings.ApplyMvpPreset();
                SyncTextFromTrainingSettings();
                settingsNameText = sceneBuilder.TrainingSettings.PresetName;
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
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Training", settingsLabelStyle);
            DrawStringField("settings name", ref settingsNameText, SettingsColumnLabelWidth);
            DrawIntField("timesteps", ref timestepsText, value => sceneBuilder.TrainingSettings.Timesteps = value, SettingsColumnLabelWidth);
            DrawIntField("max steps/ep", ref maxEpisodeStepsText, value => sceneBuilder.TrainingSettings.MaxEpisodeSteps = value, SettingsColumnLabelWidth);
            DrawIntField("seed", ref seedText, value => sceneBuilder.TrainingSettings.Seed = value, SettingsColumnLabelWidth);
            GUILayout.Space(8f);
            GUILayout.Label("Camera", settingsLabelStyle);
            DrawFloatField("mount height m", ref cameraMountHeightText, value => sceneBuilder.TrainingSettings.CameraMountHeightMeters = value, SettingsColumnLabelWidth);
            DrawFloatField("height min m", ref cameraMountHeightMinText, value => sceneBuilder.TrainingSettings.CameraMountHeightMinMeters = value, SettingsColumnLabelWidth);
            DrawFloatField("height max m", ref cameraMountHeightMaxText, value => sceneBuilder.TrainingSettings.CameraMountHeightMaxMeters = value, SettingsColumnLabelWidth);
            GUILayout.Space(8f);
            GUILayout.Label("Robot", settingsLabelStyle);
            DrawFloatField("robot radius m", ref robotRadiusText, value => sceneBuilder.SetAgentCollisionRadius(value), SettingsColumnLabelWidth);
            GUILayout.EndVertical();

            GUILayout.Space(18f);
            GUILayout.BeginVertical();
            GUILayout.Label("Workers", settingsLabelStyle);
            DrawIntField("parallel envs", ref nEnvsText, value => sceneBuilder.TrainingSettings.NEnvs = value, SettingsColumnLabelWidth);
            DrawIntField("trainer CPUs", ref cpuCountText, value => sceneBuilder.TrainingSettings.CpuCount = value, SettingsColumnLabelWidth);
            DrawIntField("torch threads", ref torchNumThreadsText, value => sceneBuilder.TrainingSettings.TorchNumThreads = value, SettingsColumnLabelWidth);
            GUILayout.Space(8f);
            GUILayout.Label("PPO", settingsLabelStyle);
            DrawIntField("n steps", ref nStepsText, value => sceneBuilder.TrainingSettings.NSteps = value, SettingsColumnLabelWidth);
            DrawIntField("batch", ref batchSizeText, value => sceneBuilder.TrainingSettings.BatchSize = value, SettingsColumnLabelWidth);
            DrawFloatField("gamma", ref gammaText, value => sceneBuilder.TrainingSettings.Gamma = value, SettingsColumnLabelWidth);
            DrawFloatField("learning rate", ref learningRateText, value => sceneBuilder.TrainingSettings.LearningRate = value, SettingsColumnLabelWidth);
            DrawFloatField("entropy", ref entCoefText, value => sceneBuilder.TrainingSettings.EntCoef = value, SettingsColumnLabelWidth);
            DrawIntField("eval episodes", ref evalEpisodesText, value => sceneBuilder.TrainingSettings.EvalEpisodes = value, SettingsColumnLabelWidth);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawRewardSettings()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Goal", settingsLabelStyle);
            DrawFloatField("goal", ref goalReachedRewardText, value => sceneBuilder.TrainingSettings.GoalReachedReward = value, SettingsColumnLabelWidth);
            DrawFloatField("progress", ref goalProgressRewardText, value => sceneBuilder.TrainingSettings.GoalProgressReward = value, SettingsColumnLabelWidth);
            DrawFloatField("collision", ref collisionPenaltyText, value => sceneBuilder.TrainingSettings.CollisionPenalty = value, SettingsColumnLabelWidth);
            DrawFloatField("step", ref stepPenaltyText, value => sceneBuilder.TrainingSettings.StepPenalty = value, SettingsColumnLabelWidth);
            GUILayout.EndVertical();

            GUILayout.Space(18f);
            GUILayout.BeginVertical();
            GUILayout.Label("Shaping", settingsLabelStyle);
            DrawFloatField("wide angle", ref wideAnglePenaltyText, value => sceneBuilder.TrainingSettings.WideAnglePenalty = value, SettingsColumnLabelWidth);
            DrawFloatField("rear angle", ref rearAnglePenaltyText, value => sceneBuilder.TrainingSettings.RearAnglePenalty = value, SettingsColumnLabelWidth);
            DrawFloatField("inactive", ref inactivePenaltyText, value => sceneBuilder.TrainingSettings.InactivePenalty = value, SettingsColumnLabelWidth);
            DrawFloatField("move threshold", ref movementThresholdText, value => sceneBuilder.TrainingSettings.MovementThreshold = value, SettingsColumnLabelWidth);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawIntField(string label, ref string text, Action<int> applyValue, float labelWidth = SettingsLabelWidth)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, settingsLabelStyle, GUILayout.Width(labelWidth), GUILayout.Height(SettingsFieldHeight));
            GUI.SetNextControlName(SettingsTextFieldFocusPrefix + label);
            text = GUILayout.TextField(text, settingsTextFieldStyle, GUILayout.Height(SettingsFieldHeight));
            RegisterTextInputFocus();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                applyValue(value);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawStringField(string label, ref string text, float labelWidth = SettingsLabelWidth)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, settingsLabelStyle, GUILayout.Width(labelWidth), GUILayout.Height(SettingsFieldHeight));
            GUI.SetNextControlName(SettingsTextFieldFocusPrefix + label);
            text = GUILayout.TextField(text ?? string.Empty, settingsTextFieldStyle, GUILayout.Height(SettingsFieldHeight));
            RegisterTextInputFocus();
            GUILayout.EndHorizontal();
        }

        private void DrawFloatField(string label, ref string text, Action<float> applyValue, float labelWidth = SettingsLabelWidth)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, settingsLabelStyle, GUILayout.Width(labelWidth), GUILayout.Height(SettingsFieldHeight));
            GUI.SetNextControlName(SettingsTextFieldFocusPrefix + label);
            text = GUILayout.TextField(text, settingsTextFieldStyle, GUILayout.Height(SettingsFieldHeight));
            RegisterTextInputFocus();
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                applyValue(value);
            }

            GUILayout.EndHorizontal();
        }

        private static void RegisterTextInputFocus()
        {
            NavigationInputBlocker.RegisterTextInputFocus(SettingsTextFieldFocusPrefix);
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
            cameraMountHeightText = settings.CameraMountHeightMeters.ToString("0.######", CultureInfo.InvariantCulture);
            cameraMountHeightMinText = settings.CameraMountHeightMinMeters.ToString("0.######", CultureInfo.InvariantCulture);
            cameraMountHeightMaxText = settings.CameraMountHeightMaxMeters.ToString("0.######", CultureInfo.InvariantCulture);
            robotRadiusText = sceneBuilder.AgentCollisionRadius.ToString("0.######", CultureInfo.InvariantCulture);
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
            if (string.IsNullOrWhiteSpace(settingsNameText))
            {
                settingsNameText = settings.PresetName;
            }
        }

        private string GetCurrentSettingsName()
        {
            if (!string.IsNullOrWhiteSpace(settingsNameText))
            {
                return settingsNameText.Trim();
            }

            return sceneBuilder?.TrainingSettings?.PresetName ?? "Custom";
        }

        private string GetSubmittedSettingsName()
        {
            EnvForgeJobRecordDto activeJob = GetActiveJobRecord();
            if (!string.IsNullOrWhiteSpace(activeJob?.settings_name))
            {
                return activeJob.settings_name;
            }

            return GetCurrentSettingsName();
        }

        private IEnumerator LoadLatestReplay()
        {
            EnvForgeJobRecordDto latestJob = GetActiveLocalReplayJob();
            ArtifactLocationDto replayBundle = GetResultArtifacts()?.replay_bundle;
            if (replayBundle == null && !string.IsNullOrWhiteSpace(submissionId))
            {
                yield return FetchLatestResult("replay");
                replayBundle = GetResultArtifacts()?.replay_bundle;
            }

            string manifestPath = latestJob?.local_replay_manifest_path;
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                if (replayBundle == null || !IsCompletedResult())
                {
                    status = "Replay unavailable: fetch a completed replay first";
                    yield break;
                }

                yield return DownloadReplay();
                latestJob = GetActiveLocalReplayJob();
                manifestPath = latestJob?.local_replay_manifest_path;
                if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                {
                    status = "Replay download did not produce a local manifest";
                    yield break;
                }
            }

            bool manifestReady = false;
            try
            {
                string manifestJson = File.ReadAllText(manifestPath);
                ReplayBundleManifestDto manifest = ScenarioBundleSerializer.FromReplayBundleManifestJson(manifestJson);
                ConfigureActiveReplayBundle(replayBundle, manifest, manifestPath);
                activeReplayScenarioSource = ApplyReplayScenario(latestJob);
                manifestReady = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Latest replay load failed: {ex}");
                status = "Latest replay load failed";
            }

            if (!manifestReady)
            {
                yield break;
            }

            if (activeReplayChunks.Count == 0)
            {
                status = "Replay bundle has no chunks";
                yield break;
            }

            yield return LoadReplayChunkAt(0, false, false);
        }

        private EnvForgeJobRecordDto GetActiveLocalReplayJob()
        {
            EnvForgeJobRecordDto activeJob = GetActiveJobRecord();
            if (activeJob == null ||
                string.IsNullOrWhiteSpace(activeJob.local_replay_manifest_path) ||
                !File.Exists(activeJob.local_replay_manifest_path))
            {
                return null;
            }

            return activeJob;
        }

        private string ApplyReplayScenario(EnvForgeJobRecordDto job)
        {
            if (!string.IsNullOrWhiteSpace(job?.scenario_bundle_json))
            {
                try
                {
                    ScenarioBundleDto scenario = ScenarioBundleSerializer.FromScenarioBundleJson(job.scenario_bundle_json);
                    sceneBuilder?.ApplyScenarioBundle(scenario);
                    sceneBuilder?.RecordScenarioSource($"Map: job scenario {Shorten(job.submission_id, 10)}");
                    return "saved scenario";
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Replay scenario restore failed: {ex}");
                }
            }

            sceneBuilder?.ResetToDefaultScenario();
            sceneBuilder?.RecordScenarioSource("Map: default scenario");
            return "default scenario";
        }

        private static List<ReplayLogStepDto> BuildReplayDisplaySteps(IReadOnlyList<ReplayLogStepDto> rawSteps)
        {
            List<ReplayLogStepDto> displaySteps = new();
            if (rawSteps == null)
            {
                return displaySteps;
            }

            for (int i = 0; i < rawSteps.Count; i++)
            {
                ReplayLogStepDto step = rawSteps[i];
                if (step != null && step.env_index == ReplayDisplayEnvIndex)
                {
                    displaySteps.Add(step);
                }
            }

            return displaySteps;
        }

        private string FormatReplaySummary(IReadOnlyList<ReplayLogStepDto> steps, string source, string scenarioSource)
        {
            if (steps == null || steps.Count == 0)
            {
                return $"{source}: no steps";
            }

            ReplayLogStepDto first = steps[0];
            ReplayLogStepDto last = steps[steps.Count - 1];
            string episode = string.IsNullOrWhiteSpace(first.episode_id) ? "episode unknown" : first.episode_id;
            int episodeCount = CountEpisodeSegments(steps);
            string scenario = string.IsNullOrWhiteSpace(scenarioSource) ? string.Empty : $" · {scenarioSource}";
            return $"{source}: job {Shorten(first.job_id, 18)} · scenario {first.scenario_id ?? "unknown"} · " +
                   $"{episodeCount} ep · first {episode} · {steps.Count} steps · {last.time_seconds:0.0}s{scenario}";
        }

        private static int CountEpisodeSegments(IReadOnlyList<ReplayLogStepDto> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return 0;
            }

            int count = 0;
            string previousEpisodeId = null;
            for (int i = 0; i < steps.Count; i++)
            {
                string episodeId = string.IsNullOrWhiteSpace(steps[i]?.episode_id) ? "episode_unknown" : steps[i].episode_id;
                if (i == 0 || episodeId != previousEpisodeId)
                {
                    count++;
                    previousEpisodeId = episodeId;
                }
            }

            return count;
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
            return $"{training.algorithm} · {FormatSteps(training.timesteps)} · seed {training.seed} · envs {training.n_envs} · " +
                   $"cpu {cpuSummary} · th {training.torch_num_threads} · n_steps {training.n_steps} · batch {training.batch_size}";
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
                labelStyle != null &&
                statusStyle != null &&
                detailStyle != null &&
                compactDetailStyle != null &&
                settingsLabelStyle != null &&
                textFieldStyle != null &&
                settingsTextFieldStyle != null &&
                boxStyle != null &&
                debugBoxStyle != null)
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

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
            };
            FreezeReadOnlyStyle(labelStyle);

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

            compactDetailStyle = new GUIStyle(detailStyle)
            {
                wordWrap = false,
                clipping = TextClipping.Clip,
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

            Texture2D background = CreateTexture(new Color(0f, 0f, 0f, 0.9f));

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = background;

            Texture2D debugBackground = CreateTexture(new Color(0.02f, 0.03f, 0.06f, 0.28f));
            debugBoxStyle = new GUIStyle(GUI.skin.box);
            debugBoxStyle.normal.background = debugBackground;

            debugTitleStyle = new GUIStyle(statusStyle)
            {
                fontSize = FontSize,
                normal = { textColor = new Color(1f, 0.28f, 0.22f, 1f) },
                wordWrap = false,
            };
            FreezeReadOnlyStyle(debugTitleStyle);

            debugInfoStyle = new GUIStyle(detailStyle)
            {
                fontSize = SettingsFontSize,
                normal = { textColor = new Color(0.9f, 0.96f, 1f, 1f) },
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            FreezeReadOnlyStyle(debugInfoStyle);

            debugUrlStyle = new GUIStyle(debugInfoStyle)
            {
                normal = { textColor = new Color(0.25f, 0.78f, 1f, 1f) },
            };
            FreezeReadOnlyStyle(debugUrlStyle);

            debugErrorStyle = new GUIStyle(debugInfoStyle)
            {
                normal = { textColor = new Color(1f, 0.36f, 0.28f, 1f) },
            };
            FreezeReadOnlyStyle(debugErrorStyle);
        }

        private static void FreezeReadOnlyStyle(GUIStyle style)
        {
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            style.focused.textColor = style.normal.textColor;
            style.hover.background = style.normal.background;
            style.active.background = style.normal.background;
            style.focused.background = style.normal.background;
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
