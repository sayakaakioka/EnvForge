using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using UnityEngine;

namespace EnvForge.Navigation.Inference
{
    [RequireComponent(typeof(AgentMotor))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NavigationModelInferenceController : MonoBehaviour
    {
        [SerializeField] private int decisionIntervalFrames = 5;

        private const int ImageObservationChannels = 3;
        private const int ImageObservationHeight = 84;
        private const int ImageObservationWidth = 112;
        private const int ImageObservationValueCount = ImageObservationChannels * ImageObservationHeight * ImageObservationWidth;
        private const int NumericObservationValueCount = 2;
        private const int FlatNavigationFinalObservationValueCount = ImageObservationValueCount + NumericObservationValueCount;
        private const float TrainingForwardStepMeters = 0.2f;
        private const float TrainingTurnDegreesPerStep = 15f;
        private const float SemanticRayNearMeters = 0.05f;
        private const float SemanticRayRangeMeters = 5f;
        private const float SemanticRayFovDegrees = 70f;
        private const float SemanticRayOriginHeightMeters = 0.15f;

        private readonly float[] observationBuffer = new float[NavigationGoalObservation.ValueCount];
        private readonly float[] robotBuffer = new float[NavigationGoalObservation.RobotValueCount];
        private readonly float[] goalBuffer = new float[NavigationGoalObservation.GoalValueCount];
        private readonly float[] frontDistanceBuffer = new float[NavigationGoalObservation.FrontDistanceValueCount];
        private readonly float[] imageObservationBuffer = new float[ImageObservationValueCount];
        private readonly float[] numericObservationBuffer = new float[NumericObservationValueCount];
        private readonly float[] flatNavigationFinalObservationBuffer = new float[FlatNavigationFinalObservationValueCount];

        private AgentMotor motor;
        private Rigidbody body;
        private NavigationLiveController liveController;
        private NavigationGoalObservationProvider observationProvider;
        private InferenceSession session;
        private string outputName;
        private IReadOnlyList<ModelInputBinding> inputBindings = Array.Empty<ModelInputBinding>();
        private bool isRunning;
        private string modelPath;
        private string statusSummary = "Inference: off";
        private string lastActionSummary = "action none";
        private string lastObservationSummary = "obs none";
        private string lastErrorDetails = string.Empty;

        public bool IsRunning => isRunning;

        public string StatusSummary => statusSummary;

        public string LastActionSummary => lastActionSummary;

        public string LastObservationSummary => lastObservationSummary;

        public string LastErrorDetails => lastErrorDetails;

        public void Configure(
            AgentMotor agentMotor,
            Rigidbody agentBody,
            NavigationLiveController controller,
            NavigationGoalObservationProvider observations)
        {
            motor = agentMotor;
            body = agentBody;
            liveController = controller;
            observationProvider = observations;
        }

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            body = GetComponent<Rigidbody>();
            liveController = GetComponent<NavigationLiveController>();
        }

        private void OnDisable()
        {
            StopInference();
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
            StopInference();
            error = string.Empty;

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

            statusSummary = $"Inference: running {Path.GetFileName(localModelPath)}";
            return true;
        }

        public void StopInference()
        {
            isRunning = false;
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
                !observationProvider.TryGetObservation(out NavigationGoalObservation observation) ||
                !observation.TryWriteTo(observationBuffer))
            {
                motor?.Stop();
                statusSummary = "Inference: observation unavailable";
                return;
            }

            observation.TryWriteRobotTo(robotBuffer);
            observation.TryWriteGoalTo(goalBuffer);
            observation.TryWriteFrontDistanceTo(frontDistanceBuffer);
            WriteNumericObservation(observation, numericObservationBuffer);
            lastObservationSummary = observation.FormatSummary();

            bool needsImageObservation =
                RequiresInputSource(ModelInputSource.ImageObservation) ||
                RequiresInputSource(ModelInputSource.FlatNavigationFinalObservation);
            if (needsImageObservation &&
                !TryCaptureImageObservation(observation, imageObservationBuffer))
            {
                motor?.Stop();
                statusSummary = "Inference: image observation unavailable";
                return;
            }

            if (RequiresInputSource(ModelInputSource.FlatNavigationFinalObservation))
            {
                WriteFlatNavigationFinalObservation();
            }

            try
            {
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(CreateInputs());

                DisposableNamedOnnxValue output = results.FirstOrDefault(result => result.Name == outputName) ??
                                                  results.FirstOrDefault();
                Tensor<float> actionTensor = output?.AsTensor<float>();
                if (actionTensor == null || actionTensor.Length < 2)
                {
                    StopWithError("Inference: invalid action output");
                    return;
                }

                float rawForward = actionTensor.GetValue(0);
                float rawTurn = actionTensor.GetValue(1);
                float forward = Mathf.Clamp01(rawForward);
                float turn = Mathf.Clamp(rawTurn, -1f, 1f);
                motor.SetInput(forward, turn);
                lastActionSummary = FormatActionSummary(rawForward, rawTurn, forward, turn);
                statusSummary = rawForward < -0.0001f
                    ? "Inference: contract violation"
                    : $"Inference: running {Path.GetFileName(modelPath)}";
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
            string warning = rawForward < -0.0001f ? " · CONTRACT VIOLATION forward<0" : string.Empty;
            return $"action raw f {rawForward:0.00} t {rawTurn:0.00} · applied f {forward:0.00} t {turn:0.00}{warning}";
        }

        private void StopWithError(string summary, string details = "")
        {
            statusSummary = summary;
            lastErrorDetails = string.IsNullOrWhiteSpace(details) ? summary : details;
            LogInferenceError(lastErrorDetails);
            isRunning = false;
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
                float[] values = binding.Source switch
                {
                    ModelInputSource.Robot => robotBuffer,
                    ModelInputSource.Goal => goalBuffer,
                    ModelInputSource.FrontDistance => frontDistanceBuffer,
                    ModelInputSource.CompactObservation => observationBuffer,
                    ModelInputSource.ImageObservation => imageObservationBuffer,
                    ModelInputSource.NumericObservation => numericObservationBuffer,
                    ModelInputSource.FlatNavigationFinalObservation => flatNavigationFinalObservationBuffer,
                    _ => observationBuffer,
                };

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

        private static bool TryResolveInputs(
            InferenceSession modelSession,
            out IReadOnlyList<ModelInputBinding> resolvedInputs,
            out string error)
        {
            List<ModelInputBinding> bindings = new();
            foreach (KeyValuePair<string, NodeMetadata> input in modelSession.InputMetadata)
            {
                if (input.Value.ElementDataType != TensorElementType.Float)
                {
                    continue;
                }

                if (TryResolveInputSource(input.Key, input.Value.Dimensions, out ModelInputSource source, out int valueCount) &&
                    TryResolveInputDimensions(input.Value.Dimensions, valueCount, out int[] dimensions))
                {
                    bindings.Add(new ModelInputBinding(input.Key, dimensions, valueCount, source));
                }
            }

            if (bindings.Count == 0)
            {
                resolvedInputs = Array.Empty<ModelInputBinding>();
                error = "Expected obs_0/obs_1, robot/goal/front_distance, or one compact float observation input.";
                return false;
            }

            resolvedInputs = bindings;
            error = string.Empty;
            return true;
        }

        private static bool TryResolveInputSource(
            string name,
            int[] dimensions,
            out ModelInputSource source,
            out int valueCount)
        {
            string normalizedName = name?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalizedName == "robot")
            {
                source = ModelInputSource.Robot;
                valueCount = NavigationGoalObservation.RobotValueCount;
                return true;
            }

            if (normalizedName == "goal")
            {
                source = ModelInputSource.Goal;
                valueCount = NavigationGoalObservation.GoalValueCount;
                return true;
            }

            if (normalizedName == "front_distance")
            {
                source = ModelInputSource.FrontDistance;
                valueCount = NavigationGoalObservation.FrontDistanceValueCount;
                return true;
            }

            if (normalizedName == "obs_0")
            {
                source = ModelInputSource.ImageObservation;
                valueCount = ImageObservationValueCount;
                return true;
            }

            if (normalizedName == "obs_1")
            {
                source = ModelInputSource.NumericObservation;
                valueCount = NumericObservationValueCount;
                return true;
            }

            int product = ProductWithDynamicBatchAsOne(dimensions);
            if (normalizedName == "observation" && product == FlatNavigationFinalObservationValueCount)
            {
                source = ModelInputSource.FlatNavigationFinalObservation;
                valueCount = FlatNavigationFinalObservationValueCount;
                return true;
            }

            if (product == NavigationGoalObservation.ValueCount)
            {
                source = ModelInputSource.CompactObservation;
                valueCount = NavigationGoalObservation.ValueCount;
                return true;
            }

            source = ModelInputSource.Unknown;
            valueCount = 0;
            return false;
        }

        private static bool TryResolveInputDimensions(int[] modelDimensions, int valueCount, out int[] resolvedDimensions)
        {
            if (modelDimensions == null || modelDimensions.Length == 0)
            {
                resolvedDimensions = new[] { valueCount };
                return true;
            }

            resolvedDimensions = (int[])modelDimensions.Clone();
            int dynamicIndex = -1;
            int knownProduct = 1;
            for (int i = 0; i < resolvedDimensions.Length; i++)
            {
                int dimension = resolvedDimensions[i];
                if (dimension <= 0)
                {
                    if (dynamicIndex < 0)
                    {
                        dynamicIndex = i;
                    }

                    resolvedDimensions[i] = 1;
                    continue;
                }

                knownProduct *= dimension;
            }

            int product = Product(resolvedDimensions);
            if (product == valueCount)
            {
                return true;
            }

            if (dynamicIndex >= 0 && knownProduct > 0 && valueCount % knownProduct == 0)
            {
                resolvedDimensions[dynamicIndex] = valueCount / knownProduct;
                return Product(resolvedDimensions) == valueCount;
            }

            return false;
        }

        private static bool TryResolveOutput(InferenceSession modelSession, out string resolvedName, out string error)
        {
            foreach (KeyValuePair<string, NodeMetadata> output in modelSession.OutputMetadata)
            {
                if (output.Value.ElementDataType == TensorElementType.Float)
                {
                    resolvedName = output.Key;
                    error = string.Empty;
                    return true;
                }
            }

            resolvedName = string.Empty;
            error = "Expected a float output with 2 continuous action values.";
            return false;
        }

        private static int Product(int[] values)
        {
            int product = 1;
            for (int i = 0; i < values.Length; i++)
            {
                product *= values[i];
            }

            return product;
        }

        private bool RequiresInputSource(ModelInputSource source)
        {
            for (int i = 0; i < inputBindings.Count; i++)
            {
                if (inputBindings[i].Source == source)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyTrainingMotionProfile()
        {
            if (motor == null)
            {
                return;
            }

            float decisionSeconds = Mathf.Max(1, decisionIntervalFrames) * Time.fixedDeltaTime;
            if (decisionSeconds <= 0f)
            {
                decisionSeconds = 0.1f;
            }

            motor.SetMotionProfile(
                TrainingForwardStepMeters / decisionSeconds,
                TrainingTurnDegreesPerStep / decisionSeconds);
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

        private bool TryCaptureImageObservation(NavigationGoalObservation observation, float[] values)
        {
            if (values == null || values.Length < ImageObservationValueCount)
            {
                return false;
            }

            Array.Clear(values, 0, ImageObservationValueCount);
            int planeSize = ImageObservationHeight * ImageObservationWidth;
            Vector3 origin = new(observation.RobotX, transform.position.y + SemanticRayOriginHeightMeters, observation.RobotZ);

            for (int row = 0; row < ImageObservationHeight; row++)
            {
                float rowRatio = 1f - row / Mathf.Max(1f, ImageObservationHeight - 1f);
                float sampleDistance = SemanticRayNearMeters + rowRatio * (SemanticRayRangeMeters - SemanticRayNearMeters);
                for (int column = 0; column < ImageObservationWidth; column++)
                {
                    float columnRatio = column / Mathf.Max(1f, ImageObservationWidth - 1f) - 0.5f;
                    float rayDegrees = observation.RobotRotationYDegrees + columnRatio * SemanticRayFovDegrees;
                    Vector3 direction = new(Mathf.Sin(rayDegrees * Mathf.Deg2Rad), 0f, Mathf.Cos(rayDegrees * Mathf.Deg2Rad));
                    bool blocked = IsSemanticRayBlocked(origin, direction, sampleDistance);
                    int pixelIndex = row * ImageObservationWidth + column;
                    values[planeSize + pixelIndex] = blocked ? 0f : 1f;
                    values[planeSize * 2 + pixelIndex] = blocked ? 1f : 0f;
                }
            }

            return true;
        }

        private void WriteFlatNavigationFinalObservation()
        {
            Array.Copy(
                imageObservationBuffer,
                0,
                flatNavigationFinalObservationBuffer,
                0,
                ImageObservationValueCount);
            Array.Copy(
                numericObservationBuffer,
                0,
                flatNavigationFinalObservationBuffer,
                ImageObservationValueCount,
                NumericObservationValueCount);
        }

        private bool IsSemanticRayBlocked(Vector3 origin, Vector3 direction, float distance)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.rigidbody == body || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static int ProductWithDynamicBatchAsOne(int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return 0;
            }

            int product = 1;
            for (int i = 0; i < values.Length; i++)
            {
                product *= Mathf.Max(1, values[i]);
            }

            return product;
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

        private enum ModelInputSource
        {
            Unknown,
            CompactObservation,
            Robot,
            Goal,
            FrontDistance,
            ImageObservation,
            NumericObservation,
            FlatNavigationFinalObservation,
        }

        private readonly struct ModelInputBinding
        {
            public ModelInputBinding(string name, int[] dimensions, int valueCount, ModelInputSource source)
            {
                Name = name;
                Dimensions = dimensions;
                ValueCount = valueCount;
                Source = source;
            }

            public string Name { get; }

            public int[] Dimensions { get; }

            public int ValueCount { get; }

            public ModelInputSource Source { get; }
        }
    }
}
