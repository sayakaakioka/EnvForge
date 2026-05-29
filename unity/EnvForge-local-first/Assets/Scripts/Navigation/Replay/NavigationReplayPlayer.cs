using System;
using System.Collections.Generic;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation;
using UnityEngine;

namespace EnvForge.Navigation.Replay
{
    public sealed class NavigationReplayPlayer : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float Width = 500f;
        private const float Height = 260f;
        private const float ButtonHeight = 74f;
        private const float ButtonWidth = 88f;
        private const float ButtonGap = 7f;
        private const int FontSize = 22;
        private const int TitleFontSize = 28;
        private const int ButtonFontSize = 42;
        private static readonly Color IconColor = new(0.72f, 0.95f, 1f, 1f);

        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool showOverlay = true;

        private readonly List<ReplayLogStepDto> steps = new();

        private Transform replayTarget;
        private Rigidbody targetBody;
        private AgentMotor targetMotor;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle boxStyle;
        private Texture2D previousIcon;
        private Texture2D playIcon;
        private Texture2D pauseIcon;
        private Texture2D stopIcon;
        private Texture2D nextIcon;
        private float replayClock;
        private int currentStepIndex;
        private bool isPlaying;
        private string status = "Replay: no log loaded";

        public bool HasReplay => steps.Count > 0;

        public bool IsPlaying => isPlaying;

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
        }

        public void LoadSteps(IReadOnlyList<ReplayLogStepDto> replaySteps)
        {
            steps.Clear();
            if (replaySteps != null)
            {
                steps.AddRange(replaySteps);
            }

            currentStepIndex = 0;
            replayClock = 0f;
            isPlaying = false;
            DisableLiveControl();
            ApplyCurrentStep();
            status = steps.Count > 0
                ? $"Replay: loaded {steps.Count} steps"
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

        public void StepForward()
        {
            if (!HasReplay)
            {
                return;
            }

            isPlaying = false;
            DisableLiveControl();
            currentStepIndex = Mathf.Min(currentStepIndex + 1, steps.Count - 1);
            replayClock = steps[currentStepIndex].time_seconds;
            ApplyCurrentStep();
            status = "Replay: stepped";
        }

        public void StepBackward()
        {
            if (!HasReplay)
            {
                return;
            }

            isPlaying = false;
            DisableLiveControl();
            currentStepIndex = Mathf.Max(currentStepIndex - 1, 0);
            replayClock = steps[currentStepIndex].time_seconds;
            ApplyCurrentStep();
            status = "Replay: stepped";
        }

        private void Update()
        {
            if (!isPlaying || steps.Count < 2)
            {
                return;
            }

            replayClock += Time.deltaTime * Mathf.Max(0.01f, playbackSpeed);
            while (currentStepIndex + 1 < steps.Count &&
                   steps[currentStepIndex + 1].time_seconds <= replayClock)
            {
                currentStepIndex++;
            }

            if (currentStepIndex >= steps.Count - 1)
            {
                currentStepIndex = steps.Count - 1;
                isPlaying = false;
                status = "Replay: finished";
            }

            ApplyInterpolatedStep();
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            EnsureStyles();
            float boxWidth = Mathf.Min(Width, Screen.width - Padding * 2f);
            float boxHeight = Mathf.Min(Height, Screen.height - Padding * 2f);
            Rect boxRect = new(Padding, Screen.height - boxHeight - Padding, boxWidth, boxHeight);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            Rect contentRect = new(boxRect.x + Padding, boxRect.y + Padding, boxWidth - Padding * 2f, boxHeight - Padding * 2f);
            GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 34f), "Replay", titleStyle);

            float buttonTop = contentRect.y + 46f;
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

            float detailTop = buttonTop + ButtonHeight + 10f;
            GUI.Label(new Rect(contentRect.x, detailTop, contentRect.width, 32f), FormatHudStatus(), labelStyle);
            GUI.Label(new Rect(contentRect.x, detailTop + 34f, contentRect.width, contentRect.yMax - detailTop - 34f), FormatCurrentStep(), labelStyle);
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
            float duration = Mathf.Max(0.0001f, next.time_seconds - current.time_seconds);
            float t = Mathf.Clamp01((replayClock - current.time_seconds) / duration);

            Vector3 position = Vector3.Lerp(ToWorldPosition(current), ToWorldPosition(next), t);
            Quaternion rotation = Quaternion.Slerp(ToWorldRotation(current), ToWorldRotation(next), t);
            ApplyPose(position, rotation);
        }

        private void ApplyStep(ReplayLogStepDto step)
        {
            ApplyPose(ToWorldPosition(step), ToWorldRotation(step));
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
            return $"step {step.step_index + 1}/{steps.Count}  t={step.time_seconds:0.00}s\n" +
                   $"reward={step.reward?.total:0.000} [{reward}]\n" +
                   $"action [{action}]  end={step.termination_reason ?? "-"}";
        }

        private string FormatHudStatus()
        {
            ReplayLogStepDto step = CurrentStep;
            if (step == null)
            {
                return status;
            }

            return $"{status}  {step.step_index + 1}/{steps.Count}  {step.time_seconds:0.0}s";
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
                Rect iconRect = new(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 28f);
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
                        ReplayIcon.Previous => InRect(x, y, 10, 16, 14, 48) ||
                                               InLeftTriangle(x, y, 16, 32, 34, 16, 34, 48) ||
                                               InLeftTriangle(x, y, 34, 32, 52, 16, 52, 48),
                        ReplayIcon.Play => InRightTriangle(x, y, 18, 14, 18, 50, 50, 32),
                        ReplayIcon.Pause => InRect(x, y, 19, 14, 27, 50) || InRect(x, y, 37, 14, 45, 50),
                        ReplayIcon.Stop => InRect(x, y, 18, 18, 46, 46),
                        ReplayIcon.Next => InRect(x, y, 50, 16, 54, 48) ||
                                           InRightTriangle(x, y, 12, 16, 12, 48, 30, 32) ||
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
