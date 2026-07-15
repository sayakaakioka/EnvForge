using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using EmbodiedLab.Contracts;
using EmbodiedLab.Unity;
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
        private const string SettingsTextFieldFocusPrefix = "CloudSettingsTextField_";
        private const float DebugOverlayMaxHeight = 420f;

        [SerializeField] private bool showPanel = true;
        [SerializeField] private string fallbackBaseUrl = "http://localhost:8000";

        private NavigationSceneBuilder sceneBuilder;
        private NavigationReplayPlayer replayPlayer;
        private NavigationModelInferenceController inferenceController;
        private EmbodiedLabEndpoints endpoints;
        private EmbodiedLabJob activeJob;
        private EnvForgeJobHistoryStore jobHistoryStore;
        private readonly CancellationTokenSource lifetimeCancellation = new();
        private ResultDocument latestResult;
        private string submissionId;
        private string activeScenarioId;
        private string loadedReplaySummary;
        private string resultStreamState = "not connected";
        private int resultStreamEventCount;
        private string lastResultStreamStatus;
        private string lastResultStreamReceivedAt;
        private string lastResultStreamError;
        private string lastResultFetchError;
        private int consecutiveResultFetchFailures;
        private bool autoDownloadStarted;
        private bool resultFetchInFlight;
        private bool replayChunkLoadInFlight;
        private int activeReplaySessionVersion;
        private int activeReplayChunkIndex = -1;
        private string activeReplayManifestPath;
        private string activeReplayScenarioSource;
        private readonly List<ReplayBundleChunk> activeReplayChunks = new();
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

        public bool IsExpandedPanelOpen => showTrainingSettings || showJobDetails || showLibraryDetails;

        public void Configure(
            NavigationSceneBuilder builder,
            NavigationReplayPlayer player,
            NavigationModelInferenceController inference,
            EnvForgeApiSettings settings,
            string baseUrl,
            string resultWebSocketBaseUrl)
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
            string resolvedApiBaseUrl = settings != null
                ? settings.BaseUrl
                : fallbackBaseUrl;
            string resolvedWebSocketBaseUrl = settings != null &&
                !string.IsNullOrWhiteSpace(settings.WebSocketBaseUrl)
                    ? settings.WebSocketBaseUrl
                    : resultWebSocketBaseUrl;
            try
            {
                endpoints = null;
                EmbodiedLabEndpoints configuredEndpoints = new(
                    resolvedApiBaseUrl,
                    resolvedWebSocketBaseUrl);
                EnvForgeEndpointSecurity.Validate(configuredEndpoints);
                endpoints = configuredEndpoints;
            }
            catch (ArgumentException exception)
            {
                status = $"Cloud configuration failed: {FormatUserFacingError(exception.Message)}";
                showJobDetails = true;
            }

            jobHistoryStore = new EnvForgeJobHistoryStore(Application.persistentDataPath);
            if (endpoints != null)
            {
                RestoreLatestJob();
            }

            SyncTextFromTrainingSettings();
            if (activeJob != null)
            {
                _ = ResumeActiveJobAsync("restored job");
            }
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
            lifetimeCancellation.Cancel();
            SetActiveJob(null);
            lifetimeCancellation.Dispose();
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

            if (endpoints != null && !string.IsNullOrWhiteSpace(submissionId))
            {
                SetActiveJob(EmbodiedLabJob.Restore(
                    endpoints,
                    submissionId,
                    recentJob.cancel_token));
            }

            status = $"Cloud: restored {Shorten(recentJob.submission_id, 18)}";
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
            string state = latestResult == null ? null : FormatStatus(latestResult.Status);
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

            bool cloudActionEnabled = !busy && !resultFetchInFlight;
            if (DrawButton(replayRect, new GUIContent("Replay", "Load replay"), buttonStyle, cloudActionEnabled))
            {
                _ = LoadLatestReplayAsync();
            }

            if (DrawButton(submitRect, new GUIContent("Submit", "Submit and train"), buttonStyle, cloudActionEnabled && endpoints != null && sceneBuilder != null))
            {
                _ = SubmitAndTrainAsync();
            }

            if (DrawButton(downloadRect, new GUIContent("Download", "Download artifacts"), buttonStyle, cloudActionEnabled))
            {
                _ = DownloadAvailableArtifactsAsync();
            }

            GUIStyle aiButtonStyle = inferenceController != null && inferenceController.IsRunning
                ? selectedButtonStyle
                : buttonStyle;
            if (DrawButton(aiRect, new GUIContent("Run AI", "Run downloaded model"), aiButtonStyle, !busy))
            {
                ToggleInferenceMode();
            }

            float secondTop = top + height + ButtonGap;
            float secondaryWidth = (contentRect.width - ButtonGap * 3f) / 4f;
            Rect jobRect = new(contentRect.x, secondTop, secondaryWidth, height);
            Rect libraryRect = new(jobRect.xMax + ButtonGap, secondTop, secondaryWidth, height);
            Rect settingsRect = new(libraryRect.xMax + ButtonGap, secondTop, secondaryWidth, height);
            Rect cancelRect = new(settingsRect.xMax + ButtonGap, secondTop, secondaryWidth, height);

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

            bool canCancel = activeJob?.CanCancel == true &&
                (latestResult == null || IsCancellableResultStatus(latestResult.Status));
            if (DrawButton(
                cancelRect,
                new GUIContent("Cancel", "Cancel cloud job"),
                buttonStyle,
                !busy && !resultFetchInFlight && canCancel))
            {
                _ = CancelActiveJobAsync();
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
            GUILayout.Label($"API Base: {FormatDebugRawValue(endpoints?.ApiBaseUri?.AbsoluteUri)}", debugUrlStyle);
            GUILayout.Label($"Fetch URL: {FormatDebugRawValue(BuildCurrentResultFetchUrl())}", debugUrlStyle);
            GUILayout.Label($"Stream: {FormatResultStreamSummary()}", debugInfoStyle);
            GUILayout.Label($"Stream error: {FormatDebugValue(lastResultStreamError)}", debugErrorStyle);
            GUILayout.Label($"Fetch error: {FormatDebugValue(lastResultFetchError)}", debugErrorStyle);
            GUILayout.Label($"Fetch failures: {consecutiveResultFetchFailures}", debugInfoStyle);
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
            if (endpoints == null || string.IsNullOrWhiteSpace(submissionId))
            {
                return string.Empty;
            }

            return new Uri(
                endpoints.ApiBaseUri,
                $"results/{Uri.EscapeDataString(submissionId)}").AbsoluteUri;
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

        private void SetActiveJob(EmbodiedLabJob job)
        {
            if (!string.Equals(submissionId, job?.SubmissionId, StringComparison.Ordinal))
            {
                ResetActiveReplaySession();
            }

            if (activeJob != null)
            {
                activeJob.ResultUpdated -= OnActiveJobResultUpdated;
                activeJob.Dispose();
            }

            activeJob = job;
            if (activeJob == null)
            {
                return;
            }

            submissionId = activeJob.SubmissionId;
            activeJob.ResultUpdated += OnActiveJobResultUpdated;
        }

        private async Awaitable SubmitAndTrainAsync()
        {
            busy = true;
            SetActiveJob(null);
            submissionId = null;
            latestResult = null;
            ResetResultPresentation();
            status = "Cloud: submitting scenario";

            try
            {
                ScenarioBundle scenario = sceneBuilder.BuildScenarioBundle();
                activeScenarioId = scenario.ScenarioId;
                string trainerSummary = FormatScenarioTrainerSummary(scenario);
                EmbodiedLabJob job = await EmbodiedLabJob.SubmitAsync(
                    endpoints,
                    scenario,
                    lifetimeCancellation.Token);
                SetActiveJob(job);
                try
                {
                    jobHistoryStore.UpsertSubmittedJob(
                        job.SubmissionId,
                        job.CancelToken,
                        scenario,
                        trainerSummary,
                        GetCurrentSettingsName());
                    status = $"Cloud: submitted {Shorten(job.SubmissionId, 18)}";
                }
                catch (Exception exception)
                {
                    status = $"Cloud: submitted {Shorten(job.SubmissionId, 18)}; history save failed";
                    Debug.LogWarning($"Submitted job history save failed for {job.SubmissionId}: {exception.Message}");
                }
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                status = $"Cloud submit failed: {FormatUserFacingError(exception.Message)}";
                Debug.LogError($"Cloud submit failed: {exception}");
                return;
            }
            finally
            {
                busy = false;
            }

            _ = MonitorActiveJobAsync(activeJob);
        }

        private async Awaitable ResumeActiveJobAsync(string source)
        {
            EmbodiedLabJob job = activeJob;
            if (job == null)
            {
                return;
            }

            status = $"Cloud: resuming {Shorten(job.SubmissionId, 18)}";
            await FetchLatestResultAsync(source);
            if (ReferenceEquals(job, activeJob) && !job.IsTerminal)
            {
                _ = MonitorActiveJobAsync(job);
            }
        }

        private async Awaitable FetchLatestResultAsync(string source)
        {
            EmbodiedLabJob job = activeJob;
            if (resultFetchInFlight || job == null)
            {
                return;
            }

            resultFetchInFlight = true;
            try
            {
                ResultDocument result = await job.RefreshAsync(lifetimeCancellation.Token);
                if (!ReferenceEquals(job, activeJob))
                {
                    return;
                }

                lastResultFetchError = null;
                consecutiveResultFetchFailures = 0;
                status = $"Cloud: fetched {FormatStatus(result.Status)}";
                TryAutoDownloadArtifacts();
            }
            catch (OperationCanceledException)
            {
                if (ReferenceEquals(job, activeJob) && !lifetimeCancellation.IsCancellationRequested)
                {
                    status = $"Cloud: result fetch cancelled ({source})";
                }
            }
            catch (Exception exception)
            {
                if (!ReferenceEquals(job, activeJob))
                {
                    return;
                }

                lastResultFetchError = exception.Message;
                consecutiveResultFetchFailures += 1;
                status = $"Cloud: result unavailable ({source})";
                Debug.LogWarning($"Result fetch failed for {job.SubmissionId}: {exception}");
            }
            finally
            {
                resultFetchInFlight = false;
            }
        }

        private async Awaitable MonitorActiveJobAsync(EmbodiedLabJob job)
        {
            if (job == null || !ReferenceEquals(job, activeJob) || job.IsTerminal)
            {
                return;
            }

            resultStreamState = "connecting";
            lastResultStreamError = null;
            status = "Cloud: listening for result";
            try
            {
                await job.WaitForCompletionAsync(lifetimeCancellation.Token);
                if (ReferenceEquals(job, activeJob))
                {
                    resultStreamState = "closed";
                    TryAutoDownloadArtifacts();
                }
            }
            catch (OperationCanceledException)
            {
                if (ReferenceEquals(job, activeJob) && !lifetimeCancellation.IsCancellationRequested)
                {
                    resultStreamState = "stopped";
                }
            }
            catch (Exception exception)
            {
                if (!ReferenceEquals(job, activeJob))
                {
                    return;
                }

                lastResultStreamError = exception.Message;
                resultStreamState = "failed";
                status = $"Result stream failed: {FormatUserFacingError(exception.Message)}";
                Debug.LogWarning($"Result stream failed for {job.SubmissionId}: {exception}");
            }
        }

        private void OnActiveJobResultUpdated(ResultDocument result)
        {
            ApplyResultUpdate(
                result,
                submissionId,
                countStreamEvent: !resultFetchInFlight);
        }

        private bool ApplyResultUpdate(
            ResultDocument result,
            string expectedSubmissionId = null,
            bool countStreamEvent = true)
        {
            string resultSubmissionId = result?.SubmissionId;
            string activeSubmissionId = expectedSubmissionId ?? submissionId;
            if (result == null ||
                string.IsNullOrWhiteSpace(resultSubmissionId) ||
                (!string.IsNullOrWhiteSpace(activeSubmissionId) &&
                 !string.Equals(resultSubmissionId, activeSubmissionId, StringComparison.Ordinal)))
            {
                return false;
            }

            latestResult = result;
            if (countStreamEvent)
            {
                resultStreamEventCount += 1;
            }

            string resultStatus = FormatStatus(result.Status);
            lastResultStreamStatus = resultStatus;
            lastResultStreamReceivedAt = DateTime.Now.ToString(
                "HH:mm:ss",
                CultureInfo.InvariantCulture);
            try
            {
                jobHistoryStore.UpsertResult(resultSubmissionId, result);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Job history update failed for {resultSubmissionId}: {exception.Message}");
            }

            status = $"Cloud: {resultStatus}";

            if (IsTerminalResultStatus(result.Status))
            {
                resultStreamState = "closed";
                TryAutoDownloadArtifacts();
            }
            else
            {
                resultStreamState = "receiving";
            }

            return true;
        }

        private async Awaitable CancelActiveJobAsync()
        {
            EmbodiedLabJob job = activeJob;
            if (job?.CanCancel != true)
            {
                status = "Cloud: cancellation capability unavailable";
                return;
            }

            busy = true;
            status = "Cloud: requesting cancellation";
            try
            {
                ResultDocument result = await job.CancelAsync(lifetimeCancellation.Token);
                if (ReferenceEquals(job, activeJob))
                {
                    status = $"Cloud: {FormatStatus(result.Status)}";
                }
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                status = $"Cloud cancel failed: {FormatUserFacingError(exception.Message)}";
                Debug.LogError($"Cloud cancel failed for {job.SubmissionId}: {exception}");
            }
            finally
            {
                busy = false;
            }
        }

        private void ResetResultPresentation()
        {
            resultStreamState = "not connected";
            resultStreamEventCount = 0;
            lastResultStreamStatus = null;
            lastResultStreamReceivedAt = null;
            lastResultStreamError = null;
            lastResultFetchError = null;
            consecutiveResultFetchFailures = 0;
            autoDownloadStarted = false;
        }

        private void TryAutoDownloadArtifacts()
        {
            if (autoDownloadStarted || busy || !IsCompletedResult())
            {
                return;
            }

            ResultArtifacts artifacts = GetResultArtifacts();
            if (artifacts == null ||
                (artifacts.ReplayBundle == null &&
                 artifacts.OnnxModel == null &&
                 artifacts.SentisModel == null &&
                 artifacts.Model == null))
            {
                return;
            }

            EnvForgeJobRecordDto record = GetActiveJobRecord();
            if (record != null &&
                !string.IsNullOrWhiteSpace(record.local_onnx_path) &&
                !string.IsNullOrWhiteSpace(record.local_replay_manifest_path) &&
                File.Exists(record.local_replay_manifest_path) &&
                File.Exists(record.local_onnx_path))
            {
                return;
            }

            autoDownloadStarted = true;
            _ = DownloadAvailableArtifactsAsync();
        }

        private async Awaitable DownloadReplayAsync()
        {
            EmbodiedLabJob job = activeJob;
            if (job == null || GetResultArtifacts()?.ReplayBundle == null)
            {
                status = "Cloud: replay artifact unavailable";
                return;
            }

            busy = true;
            status = "Cloud: downloading replay manifest";
            string manifestPath = GetLocalReplayManifestPath();
            try
            {
                await job.DownloadReplayBundleAsync(
                    manifestPath,
                    lifetimeCancellation.Token);
                if (!ReferenceEquals(job, activeJob))
                {
                    return;
                }

                ReplayBundleManifest manifest = EmbodiedLabReplay.ReadManifest(manifestPath);
                ConfigureActiveReplayBundle(manifest, manifestPath);
                jobHistoryStore.SetLocalReplayManifestPath(submissionId, manifestPath);
                status = "Cloud: replay manifest ready";
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                status = $"Replay download failed: {FormatUserFacingError(exception.Message)}";
                Debug.LogError($"Replay download failed: {exception}");
            }
            finally
            {
                busy = false;
            }
        }

        private async Awaitable DownloadAvailableArtifactsAsync()
        {
            if (!IsCompletedResult())
            {
                status = "Cloud: wait for completed before download";
                showTrainingSettings = false;
                showJobDetails = true;
                showLibraryDetails = false;
                return;
            }

            ResultArtifacts artifacts = GetResultArtifacts();
            if (artifacts == null)
            {
                status = "Cloud: no downloadable artifacts";
                showTrainingSettings = false;
                showJobDetails = true;
                showLibraryDetails = false;
                return;
            }

            if (artifacts.ReplayBundle != null)
            {
                await DownloadReplayAsync();
            }

            if (artifacts.OnnxModel != null ||
                artifacts.SentisModel != null ||
                artifacts.Model != null)
            {
                await DownloadModelArtifactsAsync();
            }
        }

        private async Awaitable DownloadModelArtifactsAsync()
        {
            EmbodiedLabJob job = activeJob;
            if (job == null)
            {
                return;
            }

            busy = true;
            string outputDir = Path.Combine(
                Application.persistentDataPath,
                "EnvForge",
                GetSafeSubmissionDirectoryName(submissionId));
            string localPath = Path.Combine(outputDir, "policy.onnx");
            status = "Cloud: downloading policy.onnx";
            try
            {
                await job.DownloadModelAsync(localPath, lifetimeCancellation.Token);
                if (ReferenceEquals(job, activeJob))
                {
                    jobHistoryStore.SetLocalOnnxPath(submissionId, localPath);
                    status = $"Cloud: saved {localPath}";
                }
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                status = $"Model download failed: {FormatUserFacingError(exception.Message)}";
                Debug.LogError($"Model download failed: {exception}");
            }
            finally
            {
                busy = false;
            }
        }

        private ResultArtifacts GetResultArtifacts()
        {
            return latestResult?.ResultBundle?.Artifacts;
        }

        private bool IsCompletedResult()
        {
            return latestResult?.Status == ResultStatus.Completed;
        }

        private static bool IsTerminalResultStatus(ResultStatus? resultStatus)
        {
            return resultStatus == ResultStatus.Completed ||
                resultStatus == ResultStatus.Failed ||
                resultStatus == ResultStatus.Cancelled;
        }

        private static bool IsCancellableResultStatus(ResultStatus resultStatus)
        {
            return resultStatus == ResultStatus.Queued ||
                resultStatus == ResultStatus.Starting ||
                resultStatus == ResultStatus.Running;
        }

        private static string FormatStatus(ResultStatus resultStatus)
        {
            return resultStatus.ToString().ToLowerInvariant();
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
            ReplayBundleManifest manifest,
            string manifestPath)
        {
            activeReplayManifestPath = manifestPath;
            activeReplayChunks.Clear();
            activeReplayChunkIndex = -1;

            if (manifest?.Chunks == null)
            {
                return;
            }

            foreach (ReplayBundleChunk chunk in manifest.Chunks)
            {
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.Path))
                {
                    continue;
                }

                activeReplayChunks.Add(chunk);
            }
        }

        private async Awaitable LoadReplayChunkAtAsync(
            int chunkIndex,
            bool autoPlay,
            bool startAtEnd,
            bool startAtLastEpisode = false)
        {
            if (replayChunkLoadInFlight)
            {
                return;
            }

            if (chunkIndex < 0 || chunkIndex >= activeReplayChunks.Count)
            {
                status = chunkIndex < 0 ? "Replay: first chunk" : "Replay: finished";
                return;
            }

            replayChunkLoadInFlight = true;
            busy = true;
            int replaySessionVersion = activeReplaySessionVersion;
            string replaySubmissionId = submissionId;
            try
            {
                int searchDirection = startAtEnd ? -1 : 1;
                int candidateChunkIndex = chunkIndex;
                while (candidateChunkIndex >= 0 && candidateChunkIndex < activeReplayChunks.Count)
                {
                    ReplayBundleChunk chunk = activeReplayChunks[candidateChunkIndex];
                    string localChunkPath = GetLocalReplayChunkPath(chunk);
                    if (string.IsNullOrEmpty(localChunkPath))
                    {
                        status = "Replay output path is unavailable";
                        return;
                    }

                    if (!File.Exists(localChunkPath))
                    {
                        EmbodiedLabJob job = activeJob;
                        if (job == null)
                        {
                            status = "Replay job handle is unavailable";
                            return;
                        }

                        status = $"Cloud: downloading replay {FormatReplayChunkLabel(chunk)}";
                        await job.DownloadReplayChunkAsync(
                            chunk,
                            localChunkPath,
                            lifetimeCancellation.Token);
                        if (!ReferenceEquals(job, activeJob) || replaySessionVersion != activeReplaySessionVersion)
                        {
                            return;
                        }
                    }

                    if (replaySessionVersion != activeReplaySessionVersion ||
                        !string.Equals(replaySubmissionId, submissionId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    IReadOnlyList<ReplayLogStep> chunkSteps =
                        EmbodiedLabReplay.ReadSteps(localChunkPath);
                    List<ReplayLogStep> displaySteps = EnvForgeReplayDisplayBuilder.BuildDisplaySteps(
                        chunkSteps,
                        ReplayDisplayEnvIndex);
                    if (displaySteps.Count == 0)
                    {
                        candidateChunkIndex += searchDirection;
                        continue;
                    }

                    EnvForgeJobRecordDto record = jobHistoryStore.SetLocalReplayBundlePaths(
                        replaySubmissionId,
                        activeReplayManifestPath,
                        localChunkPath);
                    activeReplayScenarioSource = ApplyReplayScenario(record);
                    replayPlayer.LoadWindow(
                        displaySteps,
                        candidateChunkIndex,
                        activeReplayChunks.Count,
                        $"{FormatReplayChunkLabel(chunk)} env {ReplayDisplayEnvIndex}",
                        autoPlay,
                        startAtEnd,
                        startAtLastEpisode);
                    activeReplayChunkIndex = candidateChunkIndex;
                    loadedReplaySummary = EnvForgeReplayDisplayBuilder.FormatSummary(
                        displaySteps,
                        $"Replay {FormatReplayChunkLabel(chunk)} env {ReplayDisplayEnvIndex}",
                        activeReplayScenarioSource,
                        Shorten);
                    status = $"Cloud: replay {FormatReplayChunkLabel(chunk)} loaded";
                    return;
                }

                replayPlayer.Clear();
                loadedReplaySummary = null;
                status = $"Replay has no steps for env {ReplayDisplayEnvIndex}";
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (replaySessionVersion == activeReplaySessionVersion)
                {
                    status = $"Replay chunk load failed: {FormatUserFacingError(exception.Message)}";
                    Debug.LogError($"Replay chunk load failed: {exception}");
                }
            }
            finally
            {
                FinishReplayChunkLoad(replaySessionVersion);
            }
        }

        private void FinishReplayChunkLoad(int replaySessionVersion)
        {
            replayChunkLoadInFlight = false;
            if (replaySessionVersion == activeReplaySessionVersion)
            {
                busy = false;
            }
        }

        private string GetLocalReplayChunkPath(ReplayBundleChunk chunk)
        {
            string manifestPath = activeReplayManifestPath;
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                manifestPath = GetLocalReplayManifestPath();
            }

            string outputDir = Path.GetDirectoryName(manifestPath);
            return string.IsNullOrEmpty(outputDir)
                ? string.Empty
                : TryBuildSafeChildPath(outputDir, chunk.Path, out string localPath)
                    ? localPath
                    : string.Empty;
        }

        private void HandleReplayWindowBoundaryRequested(
            int direction,
            bool autoPlay,
            bool startAtLastEpisode)
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

            _ = LoadReplayChunkAtAsync(
                nextIndex,
                autoPlay,
                direction < 0,
                startAtLastEpisode);
        }

        private static string FormatReplayChunkLabel(ReplayBundleChunk chunk)
        {
            if (chunk == null)
            {
                return "chunk";
            }

            string phase = chunk.Phase.ToString().ToLowerInvariant();
            return $"{phase} {FormatSteps(chunk.CheckpointStep)}";
        }

        private void DrawJobDetails(Rect panelRect)
        {
            GUI.Box(panelRect, GUIContent.none, boxStyle);
            GUILayout.BeginArea(new Rect(panelRect.x + Padding, panelRect.y + Padding, panelRect.width - Padding * 2f, panelRect.height - Padding * 2f));
            jobDetailsScroll = GUILayout.BeginScrollView(jobDetailsScroll);

            GUILayout.Label("Result", statusStyle);
            GUILayout.Label(
                $"Status: {(latestResult == null ? "none" : FormatStatus(latestResult.Status))}",
                detailStyle);
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
            GUILayout.Label(FormatRuntimeCameraSummary(), detailStyle);
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
                _ = RefreshLibraryResultsAsync();
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
                    bool previousEnabled = GUI.enabled;
                    bool actionsEnabled = previousEnabled &&
                        !libraryRefreshInFlight &&
                        !busy &&
                        !resultFetchInFlight;
                    GUI.enabled = actionsEnabled;
                    if (GUILayout.Button("Select", selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(ButtonHeight)))
                    {
                        SelectLibraryJob(job, reconnect: false);
                    }

                    bool hasReplay = !string.IsNullOrWhiteSpace(job.local_replay_manifest_path) && File.Exists(job.local_replay_manifest_path);
                    GUI.enabled = actionsEnabled && hasReplay;
                    if (GUILayout.Button("Replay", buttonStyle, GUILayout.Height(ButtonHeight)))
                    {
                        SelectLibraryJob(job, reconnect: false);
                        _ = LoadLatestReplayAsync();
                    }

                    GUI.enabled = actionsEnabled && !string.IsNullOrWhiteSpace(job.submission_id);
                    if (GUILayout.Button("Fetch", buttonStyle, GUILayout.Height(ButtonHeight)))
                    {
                        SelectLibraryJob(job, reconnect: false);
                        _ = FetchLatestResultAsync("library");
                    }

                    GUI.enabled = actionsEnabled && !string.IsNullOrWhiteSpace(job.submission_id);
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
            SetActiveJob(null);
            submissionId = null;
            activeScenarioId = recentJob?.scenario_id;
            latestResult = null;
            ResetResultPresentation();
            if (recentJob != null && endpoints != null)
            {
                SetActiveJob(EmbodiedLabJob.Restore(
                    endpoints,
                    recentJob.submission_id,
                    recentJob.cancel_token));
            }
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

            DeleteLocalArtifactIfPresent(job.local_replay_manifest_path, job.submission_id);
            DeleteLocalArtifactIfPresent(job.local_replay_chunk_path, job.submission_id);
            DeleteLocalArtifactIfPresent(job.local_onnx_path, job.submission_id);
            if (jobHistoryStore.RemoveJob(job.submission_id))
            {
                RestoreActiveJobAfterHistoryMutation();
                status = $"Library: removed {Shorten(job.submission_id, 18)}";
            }

            pendingDeleteSubmissionId = null;
        }

        private static void DeleteLocalArtifactIfPresent(string path, string submissionId)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path) ||
                !IsSafeLocalArtifactPath(path, submissionId))
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

        private static bool IsSafeLocalArtifactPath(string path, string submissionId)
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(
                        Application.persistentDataPath,
                        "EnvForge",
                        GetSafeSubmissionDirectoryName(submissionId)))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(path);
                string rootPrefix = root + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string relativePath = fullPath.Substring(rootPrefix.Length);
                return string.Equals(relativePath, "policy.onnx", StringComparison.OrdinalIgnoreCase) ||
                    relativePath.StartsWith("replay" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    relativePath.StartsWith("replay" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (IOException)
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

            if (endpoints == null)
            {
                status = "Cloud: endpoints are not configured";
                return;
            }

            EnvForgeJobRecordDto selectedJob = jobHistoryStore.FindJob(job.submission_id) ?? job;
            SetActiveJob(EmbodiedLabJob.Restore(
                endpoints,
                selectedJob.submission_id,
                selectedJob.cancel_token));
            activeScenarioId = selectedJob.scenario_id;
            latestResult = null;
            ResetResultPresentation();
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
                _ = ResumeActiveJobAsync("library");
            }
        }

        private async Awaitable RefreshLibraryResultsAsync()
        {
            if (libraryRefreshInFlight || jobHistoryStore == null || endpoints == null)
            {
                return;
            }

            libraryRefreshInFlight = true;
            status = "Library: refreshing history";
            try
            {
                List<EnvForgeJobRecordDto> jobs = new(jobHistoryStore.Jobs);
                int failureCount = 0;
                foreach (EnvForgeJobRecordDto job in jobs)
                {
                    if (job == null || string.IsNullOrWhiteSpace(job.submission_id))
                    {
                        continue;
                    }

                    if (!await RefreshLibraryJobResultAsync(job))
                    {
                        failureCount += 1;
                    }
                }

                status = failureCount == 0
                    ? "Library: history refreshed"
                    : $"Library: refresh completed with {failureCount} failed";
            }
            finally
            {
                libraryRefreshInFlight = false;
            }
        }

        private async Awaitable<bool> RefreshLibraryJobResultAsync(EnvForgeJobRecordDto record)
        {
            string requestedSubmissionId = record?.submission_id;
            if (string.IsNullOrWhiteSpace(requestedSubmissionId))
            {
                return false;
            }

            try
            {
                using EmbodiedLabJob job = EmbodiedLabJob.Restore(
                    endpoints,
                    requestedSubmissionId,
                    record.cancel_token);
                ResultDocument fetchedResult = await job.RefreshAsync(lifetimeCancellation.Token);
                if (jobHistoryStore.FindJob(requestedSubmissionId) == null)
                {
                    return true;
                }

                if (string.Equals(requestedSubmissionId, submissionId, StringComparison.Ordinal))
                {
                    ApplyResultUpdate(fetchedResult, requestedSubmissionId, countStreamEvent: false);
                }
                else
                {
                    jobHistoryStore.UpsertResult(requestedSubmissionId, fetchedResult);
                }

                return true;
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Library refresh failed for {Shorten(requestedSubmissionId, 18)}: {exception.Message}");
                return false;
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
                ResultArtifacts artifacts = GetResultArtifacts();
                string replay = artifacts?.ReplayBundle == null ? "replay missing" : "replay available";
                string model = artifacts?.OnnxModel == null &&
                    artifacts?.SentisModel == null &&
                    artifacts?.Model == null
                        ? "model missing"
                        : "model available";
                return $"Cloud: {replay} · {model}";
            }

            if (IsTerminalResultStatus(latestResult?.Status))
            {
                return $"Cloud: {FormatStatus(latestResult.Status)} · artifacts unavailable";
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

        private string FormatRuntimeCameraSummary()
        {
            if (inferenceController == null)
            {
                return "Camera: unavailable";
            }

            NavigationTrainingSettings settings = sceneBuilder?.TrainingSettings;
            if (settings == null)
            {
                return $"Camera: current {inferenceController.CameraMountHeightMeters:0.00}m";
            }

            return $"Camera: current {inferenceController.CameraMountHeightMeters:0.00}m · train range {settings.CameraMountHeightMinMeters:0.00}-{settings.CameraMountHeightMaxMeters:0.00}m";
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

            float runtimeCameraHeightMeters = SelectRuntimeCameraMountHeight();
            inferenceController.SetCameraMountHeightMeters(runtimeCameraHeightMeters);

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
                    : $"AI: running · {FormatCurrentModelSummary()} · {sceneBuilder.LastRuntimeStartSummary} · camera {runtimeCameraHeightMeters:0.00}m";
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

        private float SelectRuntimeCameraMountHeight()
        {
            NavigationTrainingSettings settings = sceneBuilder?.TrainingSettings;
            if (settings == null)
            {
                return Mathf.Max(0.001f, inferenceController.CameraMountHeightMeters);
            }

            float min = Mathf.Min(settings.CameraMountHeightMinMeters, settings.CameraMountHeightMaxMeters);
            float max = Mathf.Max(settings.CameraMountHeightMinMeters, settings.CameraMountHeightMaxMeters);
            if (Mathf.Approximately(min, max))
            {
                return Mathf.Max(0.001f, min);
            }

            return Mathf.Max(0.001f, UnityEngine.Random.Range(min, max));
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
            Progress progress = latestResult?.Progress;
            if (progress == null)
            {
                return "Progress: none";
            }

            string steps = progress.TotalSteps > 0
                ? $"{FormatSteps(progress.CurrentStep)}/{FormatSteps(progress.TotalSteps)}"
                : FormatSteps(progress.CurrentStep);
            string phase = FormatStatus(progress.Phase);
            return $"Progress: {phase} · {steps}";
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

        private void ResetActiveReplaySession()
        {
            activeReplaySessionVersion += 1;
            activeReplayChunkIndex = -1;
            activeReplayManifestPath = null;
            activeReplayScenarioSource = null;
            activeReplayChunks.Clear();
            loadedReplaySummary = null;
            replayPlayer?.Clear();
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

        private async Awaitable LoadLatestReplayAsync()
        {
            if (resultFetchInFlight)
            {
                return;
            }

            int replaySessionVersion = activeReplaySessionVersion;
            string replaySubmissionId = submissionId;
            EnvForgeJobRecordDto latestJob = GetActiveLocalReplayJob();
            ArtifactLocation replayBundle = GetResultArtifacts()?.ReplayBundle;
            if (replayBundle == null && !string.IsNullOrWhiteSpace(submissionId))
            {
                await FetchLatestResultAsync("replay");
                if (!IsCurrentReplaySession(replaySessionVersion, replaySubmissionId))
                {
                    return;
                }

                replayBundle = GetResultArtifacts()?.ReplayBundle;
            }

            string manifestPath = latestJob?.local_replay_manifest_path;
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                if (replayBundle == null || !IsCompletedResult())
                {
                    status = "Replay unavailable: fetch a completed replay first";
                    return;
                }

                await DownloadReplayAsync();
                if (!IsCurrentReplaySession(replaySessionVersion, replaySubmissionId))
                {
                    return;
                }

                latestJob = GetActiveLocalReplayJob();
                manifestPath = latestJob?.local_replay_manifest_path;
                if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                {
                    status = "Replay download did not produce a local manifest";
                    return;
                }
            }

            bool manifestReady = false;
            try
            {
                if (!IsCurrentReplaySession(replaySessionVersion, replaySubmissionId))
                {
                    return;
                }

                ReplayBundleManifest manifest = EmbodiedLabReplay.ReadManifest(manifestPath);
                ConfigureActiveReplayBundle(manifest, manifestPath);
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
                return;
            }

            if (activeReplayChunks.Count == 0)
            {
                status = "Replay bundle has no chunks";
                return;
            }

            await LoadReplayChunkAtAsync(0, false, false);
        }

        private bool IsCurrentReplaySession(int replaySessionVersion, string replaySubmissionId)
        {
            return replaySessionVersion == activeReplaySessionVersion &&
                string.Equals(replaySubmissionId, submissionId, StringComparison.Ordinal);
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
                    ScenarioBundle scenario = ScenarioBundleJson.Deserialize(job.scenario_bundle_json);
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

        private static string FormatScenarioTrainerSummary(ScenarioBundle scenario)
        {
            TrainingSpec training = scenario?.Training;
            if (training == null)
            {
                return "none";
            }

            string cpuSummary = training.CpuCount.HasValue && training.CpuCount.Value > 0
                ? training.CpuCount.Value.ToString(CultureInfo.InvariantCulture)
                : "job";
            string algorithm = training.Algorithm.ToString().ToLowerInvariant();
            return $"{algorithm} · {FormatSteps(training.Timesteps)} · seed {training.Seed} · envs {training.NEnvs} · " +
                   $"cpu {cpuSummary} · th {training.TorchNumThreads ?? 0} · n_steps {training.NSteps} · batch {training.BatchSize}";
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
