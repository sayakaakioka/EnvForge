using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmbodiedLab.Contracts;
using EnvForge.Navigation.Contracts;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvForge.Navigation.Inference
{
    [RequireComponent(typeof(AgentMotor))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NavigationModelInferenceController : MonoBehaviour, INavigationEpisodeEvents
    {
        [SerializeField] private int decisionIntervalFrames = 1;

        private const int ImageObservationChannels = 3;
        private const int NumericObservationValueCount = 2;
        private const string ActionOutputName = "action";

        private int imageObservationHeight;
        private int imageObservationWidth;
        private int imageObservationValueCount;
        private string imageObservationInputName;
        private string numericObservationInputName;
        private float forwardStepMeters;
        private float turnDegreesPerStep;
        private float stepDurationSeconds;
        private float[] imageObservationBuffer = Array.Empty<float>();
        private readonly float[] numericObservationBuffer = new float[NumericObservationValueCount];

        private AgentMotor motor;
        private Rigidbody body;
        private NavigationLiveController liveController;
        private NavigationEpisodeEventHub episodeEventHub;
        private NavigationGoalObservationProvider observationProvider;
        private Camera segmentationCamera;
        private RenderTexture imageObservationTexture;
        private Texture2D imageObservationReadback;
        private InferenceSession session;
        private string outputName;
        private IReadOnlyList<ModelInputBinding> inputBindings = Array.Empty<ModelInputBinding>();
        private float desiredCameraMountHeightMeters;
        private bool isRunning;
        private string modelPath;
        private string statusSummary = "Inference: off";
        private string lastActionSummary = "action none";
        private string lastObservationSummary = "obs none";
        private string lastImageObservationSummary = "image obs none";
        private string lastErrorDetails = string.Empty;
        private bool scenarioConfigured;

        public bool IsRunning => isRunning;

        public event Action InferenceGoalReached;

        public event Action InferenceWallCollision;

        public string StatusSummary => statusSummary;

        public string LastActionSummary => lastActionSummary;

        public string LastObservationSummary => lastObservationSummary;

        public string LastImageObservationSummary => lastImageObservationSummary;

        public string LastErrorDetails => lastErrorDetails;

        public string LastRuntimePoseSummary { get; private set; } = "pose -";

        public float CameraMountHeightMeters
        {
            get
            {
                return desiredCameraMountHeightMeters;
            }
        }

        public void StepInferenceForAutomation()
        {
            if (isRunning)
            {
                StepInference();
            }
        }

        public void Configure(
            AgentMotor agentMotor,
            Rigidbody agentBody,
            NavigationLiveController controller,
            NavigationEpisodeEventHub eventHub,
            NavigationGoalObservationProvider observations)
        {
            motor = agentMotor;
            body = agentBody;
            liveController = controller;
            episodeEventHub = eventHub;
            observationProvider = observations;
        }

        internal bool ConfigureScenario(ScenarioBundle scenario, out string error)
        {
            error = string.Empty;
            if (scenario?.Robot?.ActionSpace == null || scenario.Sensors == null)
            {
                error = "Scenario is missing the action or sensor contract.";
                return false;
            }

            ForwardCameraSensor[] cameras = scenario.Sensors.OfType<ForwardCameraSensor>().ToArray();
            GoalVectorSensor[] goals = scenario.Sensors.OfType<GoalVectorSensor>().ToArray();
            ActionSpace action = scenario.Robot.ActionSpace;
            if (cameras.Length != 1 || goals.Length != 1)
            {
                error = "Scenario must define exactly one forward camera and one goal-vector observation.";
                return false;
            }

            ForwardCameraSensor camera = cameras[0];
            GoalVectorSensor goal = goals[0];
            if (
                camera.Width <= 0 || camera.Height <= 0 ||
                string.IsNullOrWhiteSpace(camera.ObservationName) ||
                string.IsNullOrWhiteSpace(goal.ObservationName) ||
                string.Equals(camera.ObservationName, goal.ObservationName, StringComparison.Ordinal) ||
                camera.SemanticMode != SemanticMode.TraversableVsBlocked ||
                !string.Equals(goal.Target, scenario.World?.Goal?.Id, StringComparison.Ordinal) ||
                goal.Values == null ||
                !goal.Values.SequenceEqual(new[] { Values.GoalAngleDegrees, Values.GoalDistanceMeters }) ||
                action.Layout == null ||
                !action.Layout.SequenceEqual(new[] { Layout.Forward, Layout.Turn }) ||
                action.ForwardStepMeters <= 0d ||
                action.TurnDegreesPerStep <= 0d ||
                action.StepDurationSeconds <= 0d)
            {
                error = "Scenario does not match the supported navigation policy contract.";
                return false;
            }

            ReleaseImageObservationResources();
            imageObservationWidth = camera.Width;
            imageObservationHeight = camera.Height;
            imageObservationValueCount = ImageObservationChannels * imageObservationWidth * imageObservationHeight;
            imageObservationInputName = camera.ObservationName;
            numericObservationInputName = goal.ObservationName;
            forwardStepMeters = (float)action.ForwardStepMeters;
            turnDegreesPerStep = (float)action.TurnDegreesPerStep;
            stepDurationSeconds = (float)action.StepDurationSeconds;
            imageObservationBuffer = new float[imageObservationValueCount];
            scenarioConfigured = true;
            return true;
        }

        public void SetCameraMountHeightMeters(float mountHeightMeters)
        {
            if (segmentationCamera == null)
            {
                segmentationCamera = GetComponentInChildren<Camera>(includeInactive: true);
            }

            if (segmentationCamera == null)
            {
                return;
            }

            desiredCameraMountHeightMeters = Mathf.Max(0.001f, mountHeightMeters);
            ApplyCameraMountHeightLocalPosition();
        }

        private void ApplyCameraMountHeightLocalPosition()
        {
            if (segmentationCamera == null)
            {
                return;
            }

            Vector3 localPosition = segmentationCamera.transform.localPosition;
            segmentationCamera.transform.localPosition = new Vector3(
                localPosition.x,
                WorldCameraHeightToLocalY(desiredCameraMountHeightMeters),
                localPosition.z);
        }

        private float WorldCameraHeightToLocalY(float mountHeightMeters)
        {
            return mountHeightMeters - transform.position.y;
        }

        public bool SaveLastImageObservationPng(string outputPath)
        {
            if (imageObservationReadback == null || string.IsNullOrWhiteSpace(outputPath))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(outputPath, imageObservationReadback.EncodeToPNG());
            return true;
        }

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            body = GetComponent<Rigidbody>();
            liveController = GetComponent<NavigationLiveController>();
            segmentationCamera = GetComponentInChildren<Camera>(includeInactive: true);
            desiredCameraMountHeightMeters = segmentationCamera == null ? 0f : segmentationCamera.transform.position.y;
        }

        private void OnDisable()
        {
            StopInference();
            ReleaseImageObservationResources();
        }

        private void Update()
        {
            if (!isRunning || Time.frameCount % Mathf.Max(1, decisionIntervalFrames) != 0)
            {
                return;
            }

            StepInference();
        }

        public bool StartInference(string localModelPath, out string error)
        {
            return StartInference(localModelPath, false, default, default, out error);
        }

        public bool StartInference(string localModelPath, Vector3 runtimeStartPosition, Quaternion runtimeStartRotation, out string error)
        {
            return StartInference(localModelPath, true, runtimeStartPosition, runtimeStartRotation, out error);
        }

        private bool StartInference(
            string localModelPath,
            bool hasRuntimeStartPose,
            Vector3 runtimeStartPosition,
            Quaternion runtimeStartRotation,
            out string error)
        {
            StopInference();
            error = string.Empty;

            if (!scenarioConfigured)
            {
                error = "The current Scenario policy contract has not been configured.";
                statusSummary = "Inference: Scenario contract missing";
                return false;
            }

            if (string.IsNullOrWhiteSpace(localModelPath))
            {
                error = "No local ONNX model path is saved.";
                statusSummary = "Inference: no model";
                return false;
            }

            if (!File.Exists(localModelPath))
            {
                error = $"Model file not found: {localModelPath}";
                statusSummary = "Inference: missing model";
                return false;
            }

            modelPath = localModelPath;
            try
            {
                session = new InferenceSession(localModelPath, CreateSessionOptions());
                if (!TryResolveInputs(session, out inputBindings, out error) ||
                    !TryResolveOutput(session, out outputName, out error))
                {
                    DisposeSession();
                    statusSummary = "Inference: unsupported model";
                    LogInferenceError(error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                DisposeSession();
                error = ex.Message;
                statusSummary = "Inference: load failed";
                LogInferenceError(ex.ToString());
                return false;
            }

            if (hasRuntimeStartPose)
            {
                ApplyRuntimeStartPose(runtimeStartPosition, runtimeStartRotation);
            }
            else
            {
                LastRuntimePoseSummary = $"pose x={transform.position.x:0.00} z={transform.position.z:0.00} yaw={transform.rotation.eulerAngles.y:0.#}";
            }

            isRunning = true;
            lastActionSummary = "action waiting";
            lastObservationSummary = "obs waiting";
            lastErrorDetails = string.Empty;

            if (motor != null)
            {
                motor.enabled = true;
                ApplyTrainingMotionProfile();
                motor.Stop();
            }

            if (liveController != null)
            {
                liveController.enabled = false;
            }

            if (body != null)
            {
                body.isKinematic = false;
            }

            episodeEventHub?.SetOverrideSink(this);
            statusSummary = $"Inference: running {Path.GetFileName(localModelPath)}";
            return true;
        }

        private void ApplyRuntimeStartPose(Vector3 position, Quaternion rotation)
        {
            motor?.Stop();
            if (body != null)
            {
                body.isKinematic = false;
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = Vector3.zero;
#else
                body.velocity = Vector3.zero;
#endif
                body.angularVelocity = Vector3.zero;
                body.position = position;
                body.rotation = rotation;
            }

            transform.SetPositionAndRotation(position, rotation);
            liveController?.SetResetPose(position, rotation, applyImmediately: false);
            LastRuntimePoseSummary = $"pose x={position.x:0.00} z={position.z:0.00} yaw={rotation.eulerAngles.y:0.#}";
            Physics.SyncTransforms();
        }

        public void StopInference()
        {
            isRunning = false;
            episodeEventHub?.ClearOverrideSink(this);
            motor?.Stop();
            motor?.ResetMotionProfile();
            DisposeSession();

            if (liveController != null)
            {
                liveController.enabled = true;
            }

            if (string.IsNullOrEmpty(modelPath))
            {
                statusSummary = "Inference: off";
                lastActionSummary = "action none";
                lastObservationSummary = "obs none";
                lastImageObservationSummary = "image obs none";
                lastErrorDetails = string.Empty;
            }
            else
            {
                statusSummary = $"Inference: stopped {Path.GetFileName(modelPath)}";
            }
        }

        private void StepInference()
        {
            if (observationProvider == null ||
                !observationProvider.TryGetObservation(out NavigationGoalObservation observation))
            {
                motor?.Stop();
                statusSummary = "Inference: observation unavailable";
                return;
            }

            WriteNumericObservation(observation, numericObservationBuffer);
            lastObservationSummary = observation.FormatSummary();

            if (!TryCaptureImageObservation(imageObservationBuffer))
            {
                motor?.Stop();
                statusSummary = "Inference: image observation unavailable";
                return;
            }

            try
            {
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(CreateInputs());

                DisposableNamedOnnxValue output = results.FirstOrDefault(result => result.Name == outputName);
                Tensor<float> actionTensor = output?.AsTensor<float>();
                if (actionTensor == null || actionTensor.Length != 2)
                {
                    StopWithError("Inference: invalid action output");
                    return;
                }

                float rawForward = actionTensor.GetValue(0);
                float rawTurn = actionTensor.GetValue(1);
                if (!float.IsFinite(rawForward) || !float.IsFinite(rawTurn))
                {
                    StopWithError("Inference: non-finite action output");
                    return;
                }

                if (rawForward < 0f || rawForward > 1f || rawTurn < -1f || rawTurn > 1f)
                {
                    StopWithError(
                        "Inference: action output outside contract range",
                        $"forward={rawForward:R}, turn={rawTurn:R}");
                    return;
                }

                float forward = rawForward;
                float turn = rawTurn;
                motor.SetInput(forward, turn);
                lastActionSummary = FormatActionSummary(rawForward, rawTurn, forward, turn);
                statusSummary = $"Inference: running {Path.GetFileName(modelPath)}";
            }
            catch (Exception ex)
            {
                StopWithError("Inference: run failed", ex.ToString());
            }
        }

        private static string FormatActionSummary(
            float rawForward,
            float rawTurn,
            float forward,
            float turn)
        {
            return $"action policy f {rawForward:0.00} t {rawTurn:0.00} · applied f {forward:0.00} t {turn:0.00}";
        }

        private void StopWithError(string summary, string details = "")
        {
            statusSummary = summary;
            lastErrorDetails = string.IsNullOrWhiteSpace(details) ? summary : details;
            LogInferenceError(lastErrorDetails);
            isRunning = false;
            episodeEventHub?.ClearOverrideSink(this);
            motor?.Stop();
            motor?.ResetMotionProfile();
            DisposeSession();
            if (liveController != null)
            {
                liveController.enabled = true;
            }
        }

        public void ReportGoalReached()
        {
            StopForTerminalEvent("Inference: goal reached");
            InferenceGoalReached?.Invoke();
        }

        public void ReportWallCollision()
        {
            ReportInferenceWallCollision("Inference: wall collision");
        }

        public void ReportWallCollision(string wallId)
        {
            string suffix = string.IsNullOrWhiteSpace(wallId) ? string.Empty : $" {wallId}";
            ReportInferenceWallCollision($"Inference: wall collision{suffix}");
        }

        private void ReportInferenceWallCollision(string summary)
        {
            StopForTerminalEvent(summary);
            InferenceWallCollision?.Invoke();
        }

        private void StopForTerminalEvent(string summary)
        {
            statusSummary = summary;
            isRunning = false;
            episodeEventHub?.ClearOverrideSink(this);
            motor?.Stop();
            motor?.ResetMotionProfile();
            DisposeSession();
            if (liveController != null)
            {
                liveController.enabled = true;
            }
        }

        private List<NamedOnnxValue> CreateInputs()
        {
            List<NamedOnnxValue> inputs = new(inputBindings.Count);
            foreach (ModelInputBinding binding in inputBindings)
            {
                float[] values = binding.Name == imageObservationInputName
                    ? imageObservationBuffer
                    : numericObservationBuffer;

                DenseTensor<float> tensor = new(binding.Dimensions);
                for (int i = 0; i < binding.ValueCount; i++)
                {
                    tensor.SetValue(i, values[i]);
                }

                inputs.Add(NamedOnnxValue.CreateFromTensor(binding.Name, tensor));
            }

            return inputs;
        }

        private static SessionOptions CreateSessionOptions()
        {
            return new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = true,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                IntraOpNumThreads = 1,
                InterOpNumThreads = 1,
            };
        }

        private bool TryResolveInputs(
            InferenceSession modelSession,
            out IReadOnlyList<ModelInputBinding> resolvedInputs,
            out string error)
        {
            List<ModelInputBinding> bindings = new();
            bool hasImageObservation = false;
            bool hasNumericObservation = false;
            if (modelSession.InputMetadata.Count != 2)
            {
                resolvedInputs = Array.Empty<ModelInputBinding>();
                error = "Expected exactly two ONNX inputs from the current Scenario.";
                return false;
            }

            foreach (KeyValuePair<string, NodeMetadata> input in modelSession.InputMetadata)
            {
                if (input.Value.ElementDataType != TensorElementType.Float)
                {
                    resolvedInputs = Array.Empty<ModelInputBinding>();
                    error = $"Input '{input.Key}' must use float32.";
                    return false;
                }

                string normalizedName = input.Key?.Trim() ?? string.Empty;
                int valueCount;
                int[] expectedDimensions;
                if (normalizedName == imageObservationInputName)
                {
                    valueCount = imageObservationValueCount;
                    expectedDimensions = new[]
                    {
                        ImageObservationChannels,
                        imageObservationHeight,
                        imageObservationWidth,
                    };
                    hasImageObservation = true;
                }
                else if (normalizedName == numericObservationInputName)
                {
                    valueCount = NumericObservationValueCount;
                    expectedDimensions = new[] { NumericObservationValueCount };
                    hasNumericObservation = true;
                }
                else
                {
                    resolvedInputs = Array.Empty<ModelInputBinding>();
                    error = $"Unsupported ONNX input '{input.Key}'. Expected only the current Scenario observations.";
                    return false;
                }

                if (!TryResolveInputDimensions(
                        input.Value.Dimensions,
                        expectedDimensions,
                        out int[] dimensions))
                {
                    resolvedInputs = Array.Empty<ModelInputBinding>();
                    error = $"Input '{input.Key}' dimensions do not match the current EnvForge policy contract.";
                    return false;
                }

                bindings.Add(new ModelInputBinding(normalizedName, dimensions, valueCount));
            }

            if (!hasImageObservation || !hasNumericObservation)
            {
                resolvedInputs = Array.Empty<ModelInputBinding>();
                error = "The model inputs do not match both Scenario observations.";
                return false;
            }

            resolvedInputs = bindings;
            error = string.Empty;
            return true;
        }

        private static bool TryResolveInputDimensions(
            int[] modelDimensions,
            int[] expectedDimensions,
            out int[] resolvedDimensions)
        {
            if (modelDimensions == null || expectedDimensions == null)
            {
                resolvedDimensions = Array.Empty<int>();
                return false;
            }

            resolvedDimensions = (int[])modelDimensions.Clone();
            int offset;
            if (resolvedDimensions.Length == expectedDimensions.Length)
            {
                offset = 0;
            }
            else if (resolvedDimensions.Length == expectedDimensions.Length + 1 &&
                     (resolvedDimensions[0] <= 0 || resolvedDimensions[0] == 1))
            {
                resolvedDimensions[0] = 1;
                offset = 1;
            }
            else
            {
                return false;
            }

            for (int index = 0; index < expectedDimensions.Length; index++)
            {
                if (resolvedDimensions[index + offset] != expectedDimensions[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveOutput(InferenceSession modelSession, out string resolvedName, out string error)
        {
            if (modelSession.OutputMetadata.Count == 1 &&
                modelSession.OutputMetadata.TryGetValue(ActionOutputName, out NodeMetadata output) &&
                output.ElementDataType == TensorElementType.Float &&
                IsActionOutputShape(output.Dimensions))
            {
                resolvedName = ActionOutputName;
                error = string.Empty;
                return true;
            }

            resolvedName = string.Empty;
            error = "Expected only float output 'action' with shape [2], [1,2], or [-1,2].";
            return false;
        }

        private static bool IsActionOutputShape(int[] dimensions)
        {
            return dimensions != null &&
                ((dimensions.Length == 1 && dimensions[0] == 2) ||
                 (dimensions.Length == 2 &&
                  (dimensions[0] <= 0 || dimensions[0] == 1) &&
                  dimensions[1] == 2));
        }

        private void ApplyTrainingMotionProfile()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMotionProfile(
                forwardStepMeters / stepDurationSeconds,
                turnDegreesPerStep / stepDurationSeconds);
        }

        private static void WriteNumericObservation(NavigationGoalObservation observation, float[] values)
        {
            float deltaX = observation.GoalX - observation.RobotX;
            float deltaZ = observation.GoalZ - observation.RobotZ;
            float distance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            float targetDegrees = Mathf.Atan2(deltaX, deltaZ) * Mathf.Rad2Deg;
            values[0] = Mathf.Repeat(targetDegrees - observation.RobotRotationYDegrees + 180f, 360f) - 180f;
            values[1] = distance;
        }

        private bool TryCaptureImageObservation(float[] values)
        {
            if (values == null || values.Length != imageObservationValueCount)
            {
                return false;
            }

            return TryCaptureCameraImageObservation(values);
        }

        private bool TryCaptureCameraImageObservation(float[] values)
        {
            if (segmentationCamera == null ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return false;
            }

            ApplyCameraMountHeightLocalPosition();
            EnsureImageObservationResources();
            RenderTexture previousTargetTexture = segmentationCamera.targetTexture;
            RenderTexture previousActiveTexture = RenderTexture.active;
            try
            {
                segmentationCamera.targetTexture = imageObservationTexture;
                segmentationCamera.Render();
                RenderTexture.active = imageObservationTexture;
                imageObservationReadback.ReadPixels(new Rect(0, 0, imageObservationWidth, imageObservationHeight), 0, 0);
                imageObservationReadback.Apply();

                Color32[] pixels = imageObservationReadback.GetPixels32();
                int planeSize = imageObservationHeight * imageObservationWidth;
                for (int row = 0; row < imageObservationHeight; row++)
                {
                    int flippedRow = imageObservationHeight - 1 - row;
                    for (int column = 0; column < imageObservationWidth; column++)
                    {
                        int sourceIndex = flippedRow * imageObservationWidth + column;
                        int targetIndex = row * imageObservationWidth + column;
                        Color32 pixel = pixels[sourceIndex];
                        values[targetIndex] = pixel.r / 255f;
                        values[planeSize + targetIndex] = pixel.g / 255f;
                        values[planeSize * 2 + targetIndex] = pixel.b / 255f;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Camera image observation capture failed. {ex.Message}");
                return false;
            }
            finally
            {
                segmentationCamera.targetTexture = previousTargetTexture;
                RenderTexture.active = previousActiveTexture;
            }

            UpdateImageObservationSummary(values, "unity-camera");
            return true;
        }

        private void UpdateImageObservationSummary(float[] values, string source)
        {
            int planeSize = imageObservationHeight * imageObservationWidth;
            double red = 0d;
            double green = 0d;
            double blue = 0d;
            for (int i = 0; i < planeSize; i++)
            {
                red += values[i];
                green += values[planeSize + i];
                blue += values[planeSize * 2 + i];
            }

            lastImageObservationSummary =
                $"image {source} mean r {red / planeSize:0.000} g {green / planeSize:0.000} b {blue / planeSize:0.000}";
        }

        private void EnsureImageObservationResources()
        {
            if (imageObservationTexture == null)
            {
                imageObservationTexture = new RenderTexture(imageObservationWidth, imageObservationHeight, 16, RenderTextureFormat.ARGB32)
                {
                    name = "EnvForge Image Observation",
                };
                imageObservationTexture.Create();
            }

            if (imageObservationReadback == null)
            {
                imageObservationReadback = new Texture2D(imageObservationWidth, imageObservationHeight, TextureFormat.RGB24, mipChain: false)
                {
                    name = "EnvForge Image Observation Readback",
                };
            }
        }

        private void ReleaseImageObservationResources()
        {
            if (imageObservationTexture != null)
            {
                imageObservationTexture.Release();
                Destroy(imageObservationTexture);
                imageObservationTexture = null;
            }

            if (imageObservationReadback != null)
            {
                Destroy(imageObservationReadback);
                imageObservationReadback = null;
            }
        }

        private void LogInferenceError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Debug.LogError(message);
            }
        }

        private void DisposeSession()
        {
            session?.Dispose();
            session = null;
            outputName = string.Empty;
            inputBindings = Array.Empty<ModelInputBinding>();
        }

        private readonly struct ModelInputBinding
        {
            public ModelInputBinding(string name, int[] dimensions, int valueCount)
            {
                Name = name;
                Dimensions = dimensions;
                ValueCount = valueCount;
            }

            public string Name { get; }

            public int[] Dimensions { get; }

            public int ValueCount { get; }
        }
    }
}
