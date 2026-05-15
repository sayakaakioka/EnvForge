using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using UnityEngine;

public class RobotPolicyPlayer : MonoBehaviour
{
    public enum ObservationMode
    {
        NormalizedCoordinates,
        RawCoordinates
    }

    public enum ActionMapping
    {
        UpRightDownLeft,
        UpDownLeftRight
    }

    [SerializeField] private EnvironmentManager environmentManager = null;
    [SerializeField] private GridView gridView = null;
    [SerializeField] private GameObject robotPrefab = null;
    [SerializeField] private ObservationMode observationMode = ObservationMode.RawCoordinates;
    [SerializeField] private ActionMapping actionMapping = ActionMapping.UpRightDownLeft;
    [SerializeField] private bool invertYAxis = true;
    [SerializeField] private string preferredInputName = "observation";
    [SerializeField] private string preferredOutputName = "action_logits";
    [SerializeField] private float stepIntervalSeconds = 0.35f;
    [SerializeField] private float robotYOffset = 0.45f;
    [SerializeField] private int maxSimulationSteps = 128;
    [SerializeField] private bool logSimulationSteps = true;

    private GameObject robotObject;
    private InferenceSession session;
    private Coroutine playCoroutine;
    private GridPosition currentPosition;
    private string inputName;
    private string outputName;

    public void Initialize(EnvironmentManager environmentManager, GridView gridView)
    {
        this.environmentManager = environmentManager;
        this.gridView = gridView;
    }

    public void Play(string onnxModelPath)
    {
        if (!TryValidateSceneReferences())
        {
            return;
        }

        Stop();

        if (!TryLoadModel(onnxModelPath))
        {
            return;
        }

        currentPosition = new GridPosition(environmentManager.RobotStart.X, environmentManager.RobotStart.Y);
        EnsureRobotObject();
        MoveRobotVisual(currentPosition);
        playCoroutine = StartCoroutine(PlayPolicy());
    }

    public void Stop()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (session != null)
        {
            session.Dispose();
            session = null;
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private IEnumerator PlayPolicy()
    {
        for (int step = 0; step < maxSimulationSteps; step++)
        {
            if (IsAtGoal(currentPosition))
            {
                Debug.Log(
                    "RobotPolicyPlayer: goal reached at (" +
                    currentPosition.X + "," + currentPosition.Y +
                    ") after " + step + " steps."
                );
                yield break;
            }

            int action;
            try
            {
                action = PredictAction(BuildObservation());
            }
            catch (Exception ex)
            {
                Debug.LogError("RobotPolicyPlayer: inference failed: " + ex.Message);
                yield break;
            }

            GridPosition previousPosition = currentPosition;
            string blockedReason;
            currentPosition = ResolveNextPosition(currentPosition, action, out blockedReason);
            MoveRobotVisual(currentPosition);
            LogSimulationStep(step, previousPosition, currentPosition, action, blockedReason);

            yield return new WaitForSeconds(stepIntervalSeconds);
        }

        Debug.LogWarning(
            "RobotPolicyPlayer: max simulation steps reached before goal. " +
            "final=(" + currentPosition.X + "," + currentPosition.Y + "), " +
            "goal=(" + environmentManager.Goal.X + "," + environmentManager.Goal.Y + ")"
        );
    }

    private bool TryValidateSceneReferences()
    {
        if (environmentManager == null)
        {
            environmentManager = FindFirstObjectByType<EnvironmentManager>();
        }

        if (gridView == null)
        {
            gridView = FindFirstObjectByType<GridView>();
        }

        if (environmentManager == null)
        {
            Debug.LogError("RobotPolicyPlayer: EnvironmentManager is not assigned.");
            return false;
        }

        if (gridView == null)
        {
            Debug.LogError("RobotPolicyPlayer: GridView is not assigned.");
            return false;
        }

        return true;
    }

    private bool TryLoadModel(string onnxModelPath)
    {
        if (string.IsNullOrEmpty(onnxModelPath))
        {
            Debug.LogError("RobotPolicyPlayer: ONNX model path is empty.");
            return false;
        }

        if (!Path.GetExtension(onnxModelPath).Equals(".onnx", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("RobotPolicyPlayer: expected an ONNX model path, got: " + onnxModelPath.Replace('\\', '/'));
            return false;
        }

        try
        {
            session = new InferenceSession(onnxModelPath);
            inputName = ResolveInputName();
            outputName = ResolveOutputName();
            Debug.Log(
                "RobotPolicyPlayer: loaded ONNX model: " + onnxModelPath.Replace('\\', '/') + "\n" +
                "Input: " + inputName + "\n" +
                "Output: " + outputName
            );
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("RobotPolicyPlayer: failed to load ONNX model: " + ex.Message);
            return false;
        }
    }

    private string ResolveInputName()
    {
        if (!string.IsNullOrEmpty(preferredInputName) && session.InputMetadata.ContainsKey(preferredInputName))
        {
            return preferredInputName;
        }

        return session.InputMetadata.Keys.First();
    }

    private string ResolveOutputName()
    {
        if (!string.IsNullOrEmpty(preferredOutputName) && session.OutputMetadata.ContainsKey(preferredOutputName))
        {
            return preferredOutputName;
        }

        return session.OutputMetadata.Keys.First();
    }

    private void EnsureRobotObject()
    {
        if (robotObject != null)
        {
            return;
        }

        robotObject = robotPrefab != null
            ? Instantiate(robotPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);

        robotObject.name = "PolicyRobot";
        robotObject.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);

        var renderer = robotObject.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.2f, 0.45f, 1.0f);
        }
    }

    private void MoveRobotVisual(GridPosition position)
    {
        if (robotObject == null)
        {
            return;
        }

        robotObject.transform.position = gridView.GridToWorldPosition(position.X, position.Y, robotYOffset);
    }

    private float[] BuildObservation()
    {
        if (observationMode == ObservationMode.NormalizedCoordinates)
        {
            float widthScale = Mathf.Max(1, environmentManager.Width - 1);
            float heightScale = Mathf.Max(1, environmentManager.Height - 1);

            return new[]
            {
                currentPosition.X / widthScale,
                currentPosition.Y / heightScale,
                environmentManager.Goal.X / widthScale,
                environmentManager.Goal.Y / heightScale
            };
        }

        return new[]
        {
            (float)currentPosition.X,
            (float)currentPosition.Y,
            (float)environmentManager.Goal.X,
            (float)environmentManager.Goal.Y
        };
    }

    private int PredictAction(float[] observation)
    {
        var inputTensor = new DenseTensor<float>(observation, new[] { 1, observation.Length });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

        using var results = session.Run(inputs);
        DisposableNamedOnnxValue output = FindOutput(results);

        if (output.Value is Tensor<float> floatTensor)
        {
            return ArgMax(floatTensor.ToArray());
        }

        if (output.Value is Tensor<long> longTensor)
        {
            long[] values = longTensor.ToArray();
            return values.Length == 1 ? (int)values[0] : ArgMax(values);
        }

        if (output.Value is Tensor<int> intTensor)
        {
            int[] values = intTensor.ToArray();
            return values.Length == 1 ? values[0] : ArgMax(values);
        }

        throw new InvalidOperationException("Unsupported ONNX output type: " + output.Value.GetType().Name);
    }

    private DisposableNamedOnnxValue FindOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        DisposableNamedOnnxValue first = null;

        foreach (DisposableNamedOnnxValue result in results)
        {
            if (first == null)
            {
                first = result;
            }

            if (result.Name == outputName)
            {
                return result;
            }
        }

        if (first != null)
        {
            return first;
        }

        throw new InvalidOperationException("ONNX model returned no outputs.");
    }

    private int ArgMax(float[] values)
    {
        if (values.Length == 0)
        {
            throw new InvalidOperationException("ONNX output tensor is empty.");
        }

        int bestIndex = 0;
        float bestValue = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int ArgMax(long[] values)
    {
        if (values.Length == 0)
        {
            throw new InvalidOperationException("ONNX output tensor is empty.");
        }

        int bestIndex = 0;
        long bestValue = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int ArgMax(int[] values)
    {
        if (values.Length == 0)
        {
            throw new InvalidOperationException("ONNX output tensor is empty.");
        }

        int bestIndex = 0;
        int bestValue = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private GridPosition ResolveNextPosition(GridPosition position, int action, out string blockedReason)
    {
        Vector2Int delta = GetActionDelta(action);
        int nextX = position.X + delta.x;
        int nextY = position.Y + delta.y;

        if (nextX < 0 ||
            nextX >= environmentManager.Width ||
            nextY < 0 ||
            nextY >= environmentManager.Height)
        {
            blockedReason = "out of bounds";
            return position;
        }

        if (environmentManager.HasObstacle(nextX, nextY))
        {
            blockedReason = "obstacle";
            return position;
        }

        blockedReason = "";
        return new GridPosition(nextX, nextY);
    }

    private void LogSimulationStep(
        int step,
        GridPosition previousPosition,
        GridPosition nextPosition,
        int action,
        string blockedReason)
    {
        if (!logSimulationSteps)
        {
            return;
        }

        string actionName = GetActionName(action);
        bool stayed = previousPosition.X == nextPosition.X && previousPosition.Y == nextPosition.Y;

        string message =
            "RobotPolicyPlayer step " + step +
            ": pos=(" + previousPosition.X + "," + previousPosition.Y + ")" +
            ", action=" + action + " " + actionName +
            ", next=(" + nextPosition.X + "," + nextPosition.Y + ")";

        if (!string.IsNullOrEmpty(blockedReason))
        {
            message += ", blocked=" + blockedReason;
        }
        else if (stayed)
        {
            message += ", stayed=true";
        }

        Debug.Log(message);
    }

    private string GetActionName(int action)
    {
        if (actionMapping == ActionMapping.UpDownLeftRight)
        {
            switch (action)
            {
                case 0:
                    return "up";
                case 1:
                    return "down";
                case 2:
                    return "left";
                case 3:
                    return "right";
            }
        }

        switch (action)
        {
            case 0:
                return "up";
            case 1:
                return "right";
            case 2:
                return "down";
            case 3:
                return "left";
            default:
                return "unknown";
        }
    }

    private Vector2Int GetActionDelta(int action)
    {
        if (actionMapping == ActionMapping.UpDownLeftRight)
        {
            switch (action)
            {
                case 0:
                    return GetVerticalDelta(up: true);
                case 1:
                    return GetVerticalDelta(up: false);
                case 2:
                    return Vector2Int.left;
                case 3:
                    return Vector2Int.right;
            }
        }

        switch (action)
        {
            case 0:
                return GetVerticalDelta(up: true);
            case 1:
                return Vector2Int.right;
            case 2:
                return GetVerticalDelta(up: false);
            case 3:
                return Vector2Int.left;
            default:
                return Vector2Int.zero;
        }
    }

    private Vector2Int GetVerticalDelta(bool up)
    {
        if (invertYAxis)
        {
            return up ? Vector2Int.down : Vector2Int.up;
        }

        return up ? Vector2Int.up : Vector2Int.down;
    }

    private bool IsAtGoal(GridPosition position)
    {
        return position.X == environmentManager.Goal.X && position.Y == environmentManager.Goal.Y;
    }
}
