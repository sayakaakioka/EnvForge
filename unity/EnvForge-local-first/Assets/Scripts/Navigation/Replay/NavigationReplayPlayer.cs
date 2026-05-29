using System;
using System.Collections.Generic;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation;
using Unity.MLAgents;
using UnityEngine;

namespace EnvForge.Navigation.Replay
{
    public sealed class NavigationReplayPlayer : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float Width = 520f;
        private const float Height = 210f;
        private const int FontSize = 18;

        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool showOverlay = true;

        private readonly List<ReplayLogStepDto> steps = new();

        private Transform replayTarget;
        private Rigidbody targetBody;
        private Agent targetAgent;
        private DecisionRequester decisionRequester;
        private AgentMotor targetMotor;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;
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
            targetAgent = replayTarget.GetComponent<Agent>();
            decisionRequester = replayTarget.GetComponent<DecisionRequester>();
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
            isPlaying = steps.Count > 1;
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
            status = "Replay: playing";
        }

        public void Pause()
        {
            isPlaying = false;
            status = "Replay: paused";
        }

        public void Stop()
        {
            isPlaying = false;
            currentStepIndex = 0;
            replayClock = 0f;
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
            Rect boxRect = new(Padding, Screen.height - Height - Padding, Width, Height);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            GUILayout.BeginArea(new Rect(boxRect.x + Padding, boxRect.y + Padding, Width - Padding * 2f, Height - Padding * 2f));
            GUILayout.Label("EnvForge Replay", labelStyle);
            GUILayout.Label(status, labelStyle);
            GUILayout.Label(FormatCurrentStep(), labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play"))
            {
                Play();
            }

            if (GUILayout.Button("Pause"))
            {
                Pause();
            }

            if (GUILayout.Button("Stop"))
            {
                Stop();
            }

            if (GUILayout.Button("<"))
            {
                StepBackward();
            }

            if (GUILayout.Button(">"))
            {
                StepForward();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DisableLiveControl()
        {
            targetMotor?.Stop();
            if (targetAgent != null)
            {
                targetAgent.enabled = false;
            }

            if (decisionRequester != null)
            {
                decisionRequester.enabled = false;
            }

            if (targetMotor != null)
            {
                targetMotor.enabled = false;
            }

            if (targetBody != null)
            {
                targetBody.isKinematic = true;
                targetBody.linearVelocity = Vector3.zero;
                targetBody.angularVelocity = Vector3.zero;
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

            if (targetBody != null)
            {
                targetBody.position = position;
                targetBody.rotation = rotation;
            }

            replayTarget.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
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
            if (labelStyle != null && boxStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                normal = { textColor = Color.white },
                wordWrap = true,
            };

            Texture2D background = new(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            background.Apply();

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = background;
        }
    }
}
