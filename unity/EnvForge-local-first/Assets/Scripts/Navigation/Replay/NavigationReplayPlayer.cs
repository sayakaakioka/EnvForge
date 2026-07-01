using System;
using System.Collections.Generic;
using EnvForge.Navigation.Cloud;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Navigation.Replay
{
    public sealed class NavigationReplayPlayer : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float Width = 680f;
        private const float Height = 320f;
        private const float CompactWidth = 520f;
        private const float CompactHeight = 118f;
        private const float CompactButtonHeight = 42f;
        private const float CompactIconButtonWidth = 48f;
        private const float BottomMargin = 32f;
        private const float ButtonHeight = 52f;
        private const float ButtonWidth = 70f;
        private const float ButtonGap = 8f;
        private const float EpisodeButtonWidth = 108f;
        private const float EpisodeGapSeconds = 0.35f;
        private const string CameraMountHeightSensorId = "camera_mount_height";
        private const int FontSize = 26;
        private const int TitleFontSize = 30;
        private const int ButtonFontSize = 26;
        private static readonly Color IconColor = new(0.72f, 0.95f, 1f, 1f);

        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool showOverlay = true;

        private readonly List<ReplayLogStepDto> steps = new();
        private readonly List<int> episodeStartIndices = new();

        private Transform replayTarget;
        private Transform segmentationCameraTransform;
        private Rigidbody targetBody;
        private AgentMotor targetMotor;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle boxStyle;
        private EnvForgeCloudRunPanel cloudRunPanel;
        private NavigationWorldEditorPanel worldEditorPanel;
        private Texture2D previousIcon;
        private Texture2D playIcon;
        private Texture2D pauseIcon;
        private Texture2D stopIcon;
        private Texture2D nextIcon;
        private float replayClock;
        private int currentStepIndex;
        private int windowStartStepIndex;
        private int windowTotalStepCount;
        private int windowIndex;
        private int windowCount = 1;
        private string windowLabel = "Replay";
        private bool isPlaying;
        private bool showDetails;
        private string status = "Replay: no log loaded";

        public event Action<int, bool> WindowBoundaryRequested;

        public bool HasReplay => steps.Count > 0;

        public bool IsPlaying => isPlaying;

        public bool IsAtWindowStart => HasReplay && currentStepIndex <= 0;

        public bool IsAtWindowEnd => HasReplay && currentStepIndex >= steps.Count - 1;

        public bool IsExpandedPanelOpen => showOverlay && showDetails;

        public ReplayLogStepDto CurrentStep =>
            HasReplay ? steps[Mathf.Clamp(currentStepIndex, 0, steps.Count - 1)] : null;

        public void Configure(Transform target)
        {
            replayTarget = target;
            if (replayTarget == null)
            {
                return;
            }

            targetBody = replayTarget.GetComponent<Rigidbody>();
            targetMotor = replayTarget.GetComponent<AgentMotor>();
            Camera segmentationCamera = replayTarget.GetComponentInChildren<Camera>(true);
            segmentationCameraTransform = segmentationCamera != null ? segmentationCamera.transform : null;
        }

        public void LoadSteps(IReadOnlyList<ReplayLogStepDto> replaySteps)
        {
            LoadWindow(replaySteps, 0, 1, 0, replaySteps?.Count ?? 0, "Replay", false);
        }

        public void LoadWindow(
            IReadOnlyList<ReplayLogStepDto> replaySteps,
            int replayWindowIndex,
            int replayWindowCount,
            int globalStartStepIndex,
            int globalTotalStepCount,
            string label,
            bool autoPlay,
            bool startAtEnd = false)
        {
            steps.Clear();
            if (replaySteps != null)
            {
                steps.AddRange(replaySteps);
            }

            RebuildEpisodeIndex();
            currentStepIndex = startAtEnd && steps.Count > 0 ? steps.Count - 1 : 0;
            replayClock = steps.Count > 0 ? steps[currentStepIndex].time_seconds : 0f;
            windowStartStepIndex = Mathf.Max(0, globalStartStepIndex);
            windowTotalStepCount = Mathf.Max(steps.Count, globalTotalStepCount);
            windowIndex = Mathf.Max(0, replayWindowIndex);
            windowCount = Mathf.Max(1, replayWindowCount);
            windowLabel = string.IsNullOrWhiteSpace(label) ? "Replay" : label;
            isPlaying = autoPlay && steps.Count > 0;
            DisableLiveControl();
            ApplyCurrentStep();
            status = steps.Count > 0
                ? $"Replay: loaded {windowLabel}"
                : "Replay: no log loaded";
        }

        public void Play()
        {
            if (!HasReplay)
            {
                return;
            }

            isPlaying = true;
            if (currentStepIndex >= steps.Count - 1)
            {
                if (windowIndex < windowCount - 1)
                {
                    isPlaying = false;
                    status = "Replay: loading next";
                    WindowBoundaryRequested?.Invoke(1, true);
                    return;
                }

                currentStepIndex = 0;
                replayClock = 0f;
            }

            DisableLiveControl();
            ApplyCurrentStep();
            status = "Replay: playing";
        }

        public void Pause()
        {
            isPlaying = false;
            DisableLiveControl();
            status = "Replay: paused";
        }

        public void Stop()
        {
            isPlaying = false;
            currentStepIndex = 0;
            replayClock = 0f;
            DisableLiveControl();
            ApplyCurrentStep();
            status = HasReplay ? "Replay: stopped" : "Replay: no log loaded";
        }

        public void ReleaseControl()
        {
            isPlaying = false;
            if (targetMotor != null)
            {
                targetMotor.enabled = true;
                targetMotor.Stop();
            }

            if (targetBody != null)
            {
                targetBody.isKinematic = false;
            }

            status = HasReplay ? "Replay: released" : "Replay: no log loaded";
        }

        public void StepForward()
        {
            if (!HasReplay)
            {
                return;
            }

            isPlaying = false;
            DisableLiveControl();
            if (currentStepIndex >= steps.Count - 1)
            {
                status = "Replay: loading next";
                WindowBoundaryRequested?.Invoke(1, false);
                return;
            }

            currentStepIndex = Mathf.Min(currentStepIndex + 1, steps.Count - 1);
            replayClock = steps[currentStepIndex].time_seconds;
            ApplyCurrentStep();
            status = "Replay: stepped";
        }

        public void StepToNextEpisode()
        {
            if (!HasReplay)
            {
                return;
            }

            isPlaying = false;
            DisableLiveControl();
            int episodeIndex = GetCurrentEpisodeIndex();
            if (episodeIndex >= episodeStartIndices.Count - 1)
            {
                status = "Replay: loading next";
                WindowBoundaryRequested?.Invoke(1, false);
                return;
            }

            int nextEpisodeIndex = Mathf.Min(episodeIndex + 1, episodeStartIndices.Count - 1);
            currentStepIndex = episodeStartIndices[nextEpisodeIndex];
            replayClock = steps[currentStepIndex].time_seconds;
            ApplyCurrentStep();
            status = "Replay: episode";
        }

        public void StepToPreviousEpisode()
        {
            if (!HasReplay)
            {
                return;
            }

            isPlaying = false;
            DisableLiveControl();
            int episodeIndex = GetCurrentEpisodeIndex();
            if (episodeIndex <= 0)
            {
                status = "Replay: loading previous";
                WindowBoundaryRequested?.Invoke(-1, true);
                return;
            }

            int previousEpisodeIndex = Mathf.Max(episodeIndex - 1, 0);
            currentStepIndex = episodeStartIndices[previousEpisodeIndex];
            replayClock = steps[currentStepIndex].time_seconds;
            ApplyCurrentStep();
            status = "Replay: episode";
        }

        public void StepBackward()
        {
            if (!HasReplay)
            {
                return;
            }

            isPlaying = false;
            DisableLiveControl();
            if (currentStepIndex <= 0)
            {
                status = "Replay: loading previous";
                WindowBoundaryRequested?.Invoke(-1, true);
                return;
            }

            currentStepIndex = Mathf.Max(currentStepIndex - 1, 0);
            replayClock = steps[currentStepIndex].time_seconds;
            ApplyCurrentStep();
            status = "Replay: stepped";
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                showOverlay = !showOverlay;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && showDetails)
            {
                showDetails = false;
            }

            if (!isPlaying || steps.Count < 2)
            {
                return;
            }

            replayClock += Time.deltaTime * Mathf.Max(0.01f, playbackSpeed);
            while (currentStepIndex + 1 < steps.Count &&
                   IsSameEpisode(currentStepIndex, currentStepIndex + 1) &&
                   steps[currentStepIndex + 1].time_seconds <= replayClock)
            {
                currentStepIndex++;
            }

            if (currentStepIndex + 1 < steps.Count &&
                !IsSameEpisode(currentStepIndex, currentStepIndex + 1) &&
                replayClock >= steps[currentStepIndex].time_seconds + EpisodeGapSeconds)
            {
                currentStepIndex++;
                replayClock = steps[currentStepIndex].time_seconds;
                ApplyCurrentStep();
                return;
            }

            if (currentStepIndex >= steps.Count - 1)
            {
                currentStepIndex = steps.Count - 1;
                isPlaying = false;
                status = "Replay: loading next";
                WindowBoundaryRequested?.Invoke(1, true);
                return;
            }

            ApplyInterpolatedStep();
        }

        public void ShowDetailsForAutomation()
        {
            showOverlay = true;
            showDetails = true;
        }

        private void OnGUI()
        {
            if (!showOverlay || !HasReplay || IsCloudPanelExpanded() || IsWorldPanelExpanded())
            {
                return;
            }

            EnsureStyles();
            if (!showDetails)
            {
                DrawCompactOverlay();
                return;
            }

            DrawDetailsOverlay();
        }

        private void DrawCompactOverlay()
        {
            float boxWidth = Mathf.Min(CompactWidth, Screen.width - Padding * 2f);
            float boxHeight = Mathf.Min(CompactHeight, Screen.height - Padding * 2f);
            Rect boxRect = new(Padding, Screen.height - boxHeight - BottomMargin, boxWidth, boxHeight);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            Rect contentRect = new(boxRect.x + Padding, boxRect.y + Padding, boxWidth - Padding * 2f, boxHeight - Padding * 2f);
            GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 32f), FormatCompactStatus(), labelStyle);

            float buttonTop = contentRect.y + 40f;
            float buttonLeft = contentRect.x;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, CompactIconButtonWidth, CompactButtonHeight), previousIcon, "Previous step"))
            {
                StepBackward();
            }

            buttonLeft += CompactIconButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, CompactIconButtonWidth, CompactButtonHeight), playIcon, "Play"))
            {
                Play();
            }

            buttonLeft += CompactIconButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, CompactIconButtonWidth, CompactButtonHeight), pauseIcon, "Pause"))
            {
                Pause();
            }

            buttonLeft += CompactIconButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, CompactIconButtonWidth, CompactButtonHeight), stopIcon, "Stop"))
            {
                Stop();
            }

            buttonLeft += CompactIconButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, CompactIconButtonWidth, CompactButtonHeight), nextIcon, "Next step"))
            {
                StepForward();
            }

            float detailsLeft = buttonLeft + CompactIconButtonWidth + ButtonGap;
            if (GUI.Button(new Rect(detailsLeft, buttonTop, contentRect.xMax - detailsLeft, CompactButtonHeight), "Details", buttonStyle))
            {
                showDetails = true;
            }
        }

        private void DrawDetailsOverlay()
        {
            float boxWidth = Mathf.Min(Width, Screen.width - Padding * 2f);
            float boxHeight = Mathf.Min(Height, Screen.height - Padding * 2f);
            Rect boxRect = new(Padding, Screen.height - boxHeight - BottomMargin, boxWidth, boxHeight);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            Rect contentRect = new(boxRect.x + Padding, boxRect.y + Padding, boxWidth - Padding * 2f, boxHeight - Padding * 2f);
            GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 36f), "Replay", titleStyle);
            if (GUI.Button(new Rect(contentRect.xMax - 128f, contentRect.y, 128f, 42f), "Compact", buttonStyle))
            {
                showDetails = false;
            }

            float buttonTop = contentRect.y + 44f;
            float buttonLeft = contentRect.x;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, ButtonWidth, ButtonHeight), previousIcon, "Previous step"))
            {
                StepBackward();
            }

            buttonLeft += ButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, ButtonWidth, ButtonHeight), playIcon, "Play"))
            {
                Play();
            }

            buttonLeft += ButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, ButtonWidth, ButtonHeight), pauseIcon, "Pause"))
            {
                Pause();
            }

            buttonLeft += ButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, ButtonWidth, ButtonHeight), stopIcon, "Stop"))
            {
                Stop();
            }

            buttonLeft += ButtonWidth + ButtonGap;
            if (DrawIconButton(new Rect(buttonLeft, buttonTop, ButtonWidth, ButtonHeight), nextIcon, "Next step"))
            {
                StepForward();
            }

            buttonLeft += ButtonWidth + ButtonGap;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && CanStepToPreviousEpisode();
            if (GUI.Button(new Rect(buttonLeft, buttonTop, EpisodeButtonWidth, ButtonHeight), "Prev Ep", buttonStyle))
            {
                StepToPreviousEpisode();
            }

            buttonLeft += EpisodeButtonWidth + ButtonGap;
            GUI.enabled = wasEnabled && CanStepToNextEpisode();
            if (GUI.Button(new Rect(buttonLeft, buttonTop, EpisodeButtonWidth, ButtonHeight), "Next Ep", buttonStyle))
            {
                StepToNextEpisode();
            }
            GUI.enabled = wasEnabled;

            float detailTop = buttonTop + ButtonHeight + 12f;
            GUI.Label(new Rect(contentRect.x, detailTop, contentRect.width, 36f), FormatHudStatus(), labelStyle);
            GUI.Label(new Rect(contentRect.x, detailTop + 40f, contentRect.width, contentRect.yMax - detailTop - 40f), FormatCurrentStep(), labelStyle);
        }

        private string FormatCompactStatus()
        {
            ReplayLogStepDto step = CurrentStep;
            if (step == null)
            {
                return status;
            }

            return $"{status} · {FormatCompactEpisodeStatus()} · reward {step.reward?.total:0.###}";
        }

        private bool IsCloudPanelExpanded()
        {
            if (cloudRunPanel == null)
            {
                cloudRunPanel = FindFirstObjectByType<EnvForgeCloudRunPanel>();
            }

            return cloudRunPanel != null && cloudRunPanel.IsExpandedPanelOpen;
        }

        private bool IsWorldPanelExpanded()
        {
            if (worldEditorPanel == null)
            {
                worldEditorPanel = FindFirstObjectByType<NavigationWorldEditorPanel>();
            }

            return worldEditorPanel != null && worldEditorPanel.IsExpandedPanelOpen;
        }

        private void DisableLiveControl()
        {
            targetMotor?.Stop();
            if (targetMotor != null)
            {
                targetMotor.enabled = false;
            }

            if (targetBody != null)
            {
                StopDynamicBodyBeforeKinematic(targetBody);
            }
        }

        private void ApplyCurrentStep()
        {
            if (!HasReplay)
            {
                return;
            }

            ApplyStep(steps[currentStepIndex]);
        }

        private void ApplyInterpolatedStep()
        {
            if (!HasReplay)
            {
                return;
            }

            ReplayLogStepDto current = steps[currentStepIndex];
            if (currentStepIndex + 1 >= steps.Count)
            {
                ApplyStep(current);
                return;
            }

            ReplayLogStepDto next = steps[currentStepIndex + 1];
            if (!IsSameEpisode(currentStepIndex, currentStepIndex + 1))
            {
                ApplyStep(current);
                return;
            }

            float duration = Mathf.Max(0.0001f, next.time_seconds - current.time_seconds);
            float t = Mathf.Clamp01((replayClock - current.time_seconds) / duration);

            Vector3 position = Vector3.Lerp(ToWorldPosition(current), ToWorldPosition(next), t);
            Quaternion rotation = Quaternion.Slerp(ToWorldRotation(current), ToWorldRotation(next), t);
            ApplyPose(position, rotation);
            ApplyCameraMountHeight(current);
        }

        private void ApplyStep(ReplayLogStepDto step)
        {
            ApplyPose(ToWorldPosition(step), ToWorldRotation(step));
            ApplyCameraMountHeight(step);
        }

        private void ApplyPose(Vector3 position, Quaternion rotation)
        {
            if (replayTarget == null)
            {
                return;
            }

            targetMotor?.Stop();
            if (targetBody != null)
            {
                StopDynamicBodyBeforeKinematic(targetBody);
                targetBody.position = position;
                targetBody.rotation = rotation;
            }

            replayTarget.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }

        private static void StopDynamicBodyBeforeKinematic(Rigidbody body)
        {
            if (body.isKinematic)
            {
                return;
            }

#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        private Vector3 ToWorldPosition(ReplayLogStepDto step)
        {
            float y = replayTarget != null ? replayTarget.position.y : 0.6f;
            return new Vector3(step.robot.position.x, y, step.robot.position.z);
        }

        private void ApplyCameraMountHeight(ReplayLogStepDto step)
        {
            if (segmentationCameraTransform == null ||
                replayTarget == null ||
                !TryGetSensorValue(step, CameraMountHeightSensorId, out float cameraMountHeightMeters))
            {
                return;
            }

            Vector3 localPosition = segmentationCameraTransform.localPosition;
            localPosition.y = cameraMountHeightMeters - replayTarget.position.y;
            segmentationCameraTransform.localPosition = localPosition;
        }

        private static Quaternion ToWorldRotation(ReplayLogStepDto step)
        {
            return Quaternion.Euler(0f, step.robot.rotation_y_degrees, 0f);
        }

        private string FormatCurrentStep()
        {
            ReplayLogStepDto step = CurrentStep;
            if (step == null)
            {
                return "step: -";
            }

            string action = FormatNamedValues(step.action?.values);
            string reward = FormatNamedValues(step.reward?.components);
            string camera = TryGetSensorValue(step, CameraMountHeightSensorId, out float cameraMountHeightMeters)
                ? $"{cameraMountHeightMeters:0.00}m"
                : "-";
            return $"{FormatEpisodeStatus(step)}  t={step.time_seconds:0.00}s  job={Shorten(step.job_id, 12)}\n" +
                   $"reward {step.reward?.total:0.000}  {Shorten(reward, 48)}\n" +
                   $"action {Shorten(action, 44)}  camera {camera}\n" +
                   $"end {step.termination_reason ?? "-"}";
        }

        private string FormatHudStatus()
        {
            ReplayLogStepDto step = CurrentStep;
            if (step == null)
            {
                return status;
            }

            return $"{status}  {FormatEpisodeStatus(step)}  {step.time_seconds:0.0}s";
        }

        private void RebuildEpisodeIndex()
        {
            episodeStartIndices.Clear();
            string previousEpisodeId = null;
            for (int i = 0; i < steps.Count; i++)
            {
                string episodeId = NormalizeEpisodeId(steps[i]);
                if (i == 0 || episodeId != previousEpisodeId)
                {
                    episodeStartIndices.Add(i);
                    previousEpisodeId = episodeId;
                }
            }
        }

        private int GetCurrentEpisodeIndex()
        {
            if (episodeStartIndices.Count == 0)
            {
                return 0;
            }

            int result = 0;
            for (int i = 0; i < episodeStartIndices.Count; i++)
            {
                if (episodeStartIndices[i] > currentStepIndex)
                {
                    break;
                }

                result = i;
            }

            return result;
        }

        private int GetCurrentEpisodeStepCount()
        {
            int episodeIndex = GetCurrentEpisodeIndex();
            if (episodeStartIndices.Count == 0)
            {
                return steps.Count;
            }

            int start = episodeStartIndices[episodeIndex];
            int end = episodeIndex + 1 < episodeStartIndices.Count
                ? episodeStartIndices[episodeIndex + 1]
                : steps.Count;
            return Mathf.Max(0, end - start);
        }

        private int GetCurrentEpisodeStepNumber()
        {
            int episodeIndex = GetCurrentEpisodeIndex();
            int start = episodeStartIndices.Count == 0 ? 0 : episodeStartIndices[episodeIndex];
            return currentStepIndex - start + 1;
        }

        private bool IsSameEpisode(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || firstIndex >= steps.Count || secondIndex < 0 || secondIndex >= steps.Count)
            {
                return false;
            }

            return NormalizeEpisodeId(steps[firstIndex]) == NormalizeEpisodeId(steps[secondIndex]);
        }

        private bool CanStepToPreviousEpisode()
        {
            return HasReplay && (GetCurrentEpisodeIndex() > 0 || windowIndex > 0);
        }

        private bool CanStepToNextEpisode()
        {
            return HasReplay && (GetCurrentEpisodeIndex() < episodeStartIndices.Count - 1 || windowIndex < windowCount - 1);
        }

        private string FormatEpisodeStatus(ReplayLogStepDto step)
        {
            int episodeIndex = GetCurrentEpisodeIndex() + 1;
            int episodeCount = Mathf.Max(1, episodeStartIndices.Count);
            int globalStep = windowStartStepIndex + currentStepIndex + 1;
            return $"chunk {windowIndex + 1}/{windowCount} ep {episodeIndex}/{episodeCount} {NormalizeEpisodeId(step)} step {GetCurrentEpisodeStepNumber()}/{GetCurrentEpisodeStepCount()} total {globalStep}/{windowTotalStepCount}";
        }

        private string FormatCompactEpisodeStatus()
        {
            int episodeIndex = GetCurrentEpisodeIndex() + 1;
            int episodeCount = Mathf.Max(1, episodeStartIndices.Count);
            return $"chunk {windowIndex + 1}/{windowCount} ep {episodeIndex}/{episodeCount} step {GetCurrentEpisodeStepNumber()}/{GetCurrentEpisodeStepCount()}";
        }

        private static string NormalizeEpisodeId(ReplayLogStepDto step)
        {
            return string.IsNullOrWhiteSpace(step?.episode_id) ? "episode_unknown" : step.episode_id;
        }

        private static bool TryGetSensorValue(ReplayLogStepDto step, string sensorId, out float value)
        {
            value = 0f;
            if (step?.sensors == null || string.IsNullOrWhiteSpace(sensorId))
            {
                return false;
            }

            for (int i = 0; i < step.sensors.Count; i++)
            {
                ReplaySensorSummaryDto sensor = step.sensors[i];
                if (sensor != null && sensor.id == sensorId)
                {
                    value = sensor.value;
                    return true;
                }
            }

            return false;
        }

        private static string FormatNamedValues(List<ReplayNamedValueDto> values)
        {
            if (values == null || values.Count == 0)
            {
                return "-";
            }

            string[] parts = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                parts[i] = $"{values[i].name}:{values[i].value:0.00}";
            }

            return string.Join(", ", parts);
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "-";
            }

            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && labelStyle != null && buttonStyle != null && boxStyle != null && previousIcon != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = TitleFontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                wordWrap = true,
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                normal = { textColor = Color.white },
                wordWrap = true,
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ButtonFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            Texture2D buttonBackground = CreateTexture(new Color(0.06f, 0.09f, 0.12f, 0.92f));
            Texture2D buttonHoverBackground = CreateTexture(new Color(0.08f, 0.16f, 0.2f, 0.96f));
            Texture2D buttonActiveBackground = CreateTexture(new Color(0.1f, 0.28f, 0.34f, 0.98f));
            buttonStyle.normal.background = buttonBackground;
            buttonStyle.hover.background = buttonHoverBackground;
            buttonStyle.active.background = buttonActiveBackground;
            buttonStyle.focused.background = buttonHoverBackground;
            buttonStyle.normal.textColor = new Color(0.72f, 0.95f, 1f);
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.focused.textColor = Color.white;

            Texture2D background = new(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            background.Apply();

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = background;

            previousIcon = CreateIconTexture(ReplayIcon.Previous);
            playIcon = CreateIconTexture(ReplayIcon.Play);
            pauseIcon = CreateIconTexture(ReplayIcon.Pause);
            stopIcon = CreateIconTexture(ReplayIcon.Stop);
            nextIcon = CreateIconTexture(ReplayIcon.Next);
        }

        private bool DrawIconButton(Rect rect, Texture2D icon, string tooltip)
        {
            bool pressed = GUI.Button(rect, new GUIContent(string.Empty, tooltip), buttonStyle);
            if (Event.current.type == EventType.Repaint && icon != null)
            {
                float horizontalPadding = Mathf.Min(18f, rect.width * 0.26f);
                float verticalPadding = Mathf.Min(12f, rect.height * 0.24f);
                Rect iconRect = new(
                    rect.x + horizontalPadding,
                    rect.y + verticalPadding,
                    rect.width - horizontalPadding * 2f,
                    rect.height - verticalPadding * 2f);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            return pressed;
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateIconTexture(ReplayIcon icon)
        {
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool filled = icon switch
                    {
                        ReplayIcon.Previous => InLeftTriangle(x, y, 16, 32, 34, 16, 34, 48) ||
                                               InLeftTriangle(x, y, 34, 32, 52, 16, 52, 48),
                        ReplayIcon.Play => InRightTriangle(x, y, 18, 14, 18, 50, 50, 32),
                        ReplayIcon.Pause => InRect(x, y, 19, 14, 27, 50) || InRect(x, y, 37, 14, 45, 50),
                        ReplayIcon.Stop => InRect(x, y, 18, 18, 46, 46),
                        ReplayIcon.Next => InRightTriangle(x, y, 12, 16, 12, 48, 30, 32) ||
                                           InRightTriangle(x, y, 30, 16, 30, 48, 48, 32),
                        _ => false,
                    };
                    pixels[y * size + x] = filled ? IconColor : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static bool InRect(int x, int y, int left, int top, int right, int bottom)
        {
            return x >= left && x <= right && y >= top && y <= bottom;
        }

        private static bool InRightTriangle(int x, int y, int leftTopX, int leftTopY, int leftBottomX, int leftBottomY, int tipX, int tipY)
        {
            return InTriangle(x, y, leftTopX, leftTopY, leftBottomX, leftBottomY, tipX, tipY);
        }

        private static bool InLeftTriangle(int x, int y, int tipX, int tipY, int rightTopX, int rightTopY, int rightBottomX, int rightBottomY)
        {
            return InTriangle(x, y, tipX, tipY, rightTopX, rightTopY, rightBottomX, rightBottomY);
        }

        private static bool InTriangle(int x, int y, int ax, int ay, int bx, int by, int cx, int cy)
        {
            int denominator = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
            int alphaNumerator = (by - cy) * (x - cx) + (cx - bx) * (y - cy);
            int betaNumerator = (cy - ay) * (x - cx) + (ax - cx) * (y - cy);

            if (denominator < 0)
            {
                denominator = -denominator;
                alphaNumerator = -alphaNumerator;
                betaNumerator = -betaNumerator;
            }

            int gammaNumerator = denominator - alphaNumerator - betaNumerator;
            return alphaNumerator >= 0 && betaNumerator >= 0 && gammaNumerator >= 0;
        }

        private enum ReplayIcon
        {
            Previous,
            Play,
            Pause,
            Stop,
            Next,
        }
    }
}
