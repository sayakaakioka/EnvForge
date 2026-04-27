using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class TestRunner : MonoBehaviour
{
    public enum RunnerState
    {
        Idle,
        Submitting,
        TrainingRequest,
        GettingResult
    }

    [SerializeField] private ApiSettings apiSettings = null;
    [SerializeField] private string robotType = "simple";
    [SerializeField] private TrainingRequestData training = new TrainingRequestData();
    [SerializeField] private TMP_Text statusText = null;
    [SerializeField] private EnvironmentManager environmentManager = null;
    [SerializeField] private GridView gridView = null;
    [SerializeField] private RobotPolicyPlayer robotPolicyPlayer = null;
    [SerializeField] private bool autoPollResults = true;
    [SerializeField] private float resultPollIntervalSeconds = 60f;

    private TrainingApiClient apiClient;
    private TrainingWebSocketClient webSocketClient;
    private string lastSubmissionId = "";
    private bool trainingStarted = false;
    private bool modelDownloadStarted = false;
    private Coroutine autoPollCoroutine = null;
    private bool autoPollActive = false;
    private bool requestInFlight = false;
    private RunnerState currentState = RunnerState.Idle;

    public event Action<RunnerState> StateChanged;

    public RunnerState State => currentState;
    public bool IsBusy => requestInFlight || currentState != RunnerState.Idle;
    public bool HasSubmission => !string.IsNullOrEmpty(lastSubmissionId);
    public bool HasResult => trainingStarted && !string.IsNullOrEmpty(lastSubmissionId);

    private void Awake()
    {
        if (training == null)
        {
            training = new TrainingRequestData();
        }

    }

    private void Start()
    {
        SetStatus("Ready");
        NotifyStateChanged();
    }

    private void OnDisable()
    {
        StopAutoPolling();
        StopTrainingWebSocket();
        robotPolicyPlayer?.Stop();
    }

    public void OnClickSubmit()
    {
        StopAutoPolling();
        StopTrainingWebSocket();
        robotPolicyPlayer?.Stop();

        if (!TryCreateApiClient())
        {
            return;
        }

        if (!TryBeginRequest(RunnerState.Submitting))
        {
            return;
        }

        StartCoroutine(Submit());
    }

    public void OnClickTrain()
    {
        if (!TryCreateApiClient())
        {
            return;
        }

        if (string.IsNullOrEmpty(lastSubmissionId))
        {
            SetStatus("No submission ID available. Please submit first.");
            return;
        }

        if (!TryBeginRequest(RunnerState.TrainingRequest))
        {
            return;
        }

        StartCoroutine(Train());
    }

    public void OnClickGetResult()
    {
        if (!TryCreateApiClient())
        {
            return;
        }

        if (!trainingStarted || string.IsNullOrEmpty(lastSubmissionId))
        {
            SetStatus("No training result available. Please train first.");
            return;
        }

        if (!TryBeginRequest(RunnerState.GettingResult))
        {
            return;
        }

        StartCoroutine(GetResult());
    }

    private IEnumerator Submit()
    {
        SubmitRequestData payload = SubmitRequestBuilder.Build(environmentManager, robotType, training);
        if (payload == null)
        {
            SetStatus("Submit failed: environment is not available.");
            EndRequest();
            yield break;
        }

        SetStatus("Submitting...");

        ApiResult<SubmitResponseData> result = null;
        yield return apiClient.Submit(payload, response => result = response);

        if (!result.IsSuccess)
        {
            SetStatus("Submit failed: " + result.Error);
            Debug.LogError(result.RawBody);
            EndRequest();
            yield break;
        }

        if (string.IsNullOrEmpty(result.Data.submission_id))
        {
            SetStatus("Submit failed: missing submission ID.");
            Debug.LogError(result.RawBody);
            EndRequest();
            yield break;
        }

        lastSubmissionId = result.Data.submission_id;
        trainingStarted = false;
        modelDownloadStarted = false;
        SetStatus("Submit OK: " + lastSubmissionId);
        EndRequest();
    }

    private bool TryCreateApiClient()
    {
        if (apiSettings == null)
        {
            SetStatus("API settings are not assigned.");
            Debug.LogError("TestRunner: ApiSettings is not assigned.");
            return false;
        }

        if (string.IsNullOrEmpty(apiSettings.BaseUrl))
        {
            SetStatus("API base URL is empty.");
            Debug.LogError("TestRunner: API base URL is empty.");
            return false;
        }

        apiClient = new TrainingApiClient(apiSettings.BaseUrl);
        return true;
    }

    private IEnumerator Train()
    {
        SetStatus("Starting training...");

        ApiResult<TrainResponseData> result = null;
        yield return apiClient.Train(lastSubmissionId, response => result = response);

        if (!result.IsSuccess)
        {
            SetStatus("Train failed: " + result.Error);
            Debug.LogError(result.RawBody);
            EndRequest();
            yield break;
        }

        if (string.IsNullOrEmpty(result.Data.submission_id))
        {
            SetStatus("Train failed: missing submission ID.");
            Debug.LogError(result.RawBody);
            EndRequest();
            yield break;
        }

        lastSubmissionId = result.Data.submission_id;
        trainingStarted = true;
        modelDownloadStarted = false;
        SetStatus("Training started: " + lastSubmissionId);
        EndRequest();

        if (!StartTrainingWebSocket())
        {
            StartAutoPolling();
        }
    }

    private void Update()
    {
        DrainWebSocketEvents();
    }

    private IEnumerator GetResult()
    {
        yield return GetResult(lastSubmissionId);
    }

    private IEnumerator GetResult(string resultId)
    {
        SetStatus("Retrieving result...");

        ApiResult<ResultData> result = null;
        yield return apiClient.GetResult(resultId, response => result = response);

        if (!result.IsSuccess)
        {
            SetStatus("Result retrieve failed: " + result.Error);
            Debug.LogError(result.RawBody);
            EndRequest();
            yield break;
        }

        Debug.Log(result.RawBody);
        HandleResult(result.Data, result.RawBody);
        EndRequest();
    }

    private void HandleResult(ResultData data, string rawBody)
    {
        if (data == null || string.IsNullOrEmpty(data.submission_id))
        {
            SetStatus("Result failed: invalid response.");
            RequestStopAutoPolling();
            return;
        }

        switch (data.status)
        {
            case "queued":
                SetStatus(BuildProgressStatus(data, "Training queued."));
                break;

            case "starting":
                SetStatus(BuildProgressStatus(data, "Training starting."));
                break;

            case "running":
                SetStatus(BuildProgressStatus(data, "Training running."));
                break;

            case "failed":
                RequestStopAutoPolling();
                SetStatus("Failed: " + data.error);
                break;

            case "completed":
                RequestStopAutoPolling();
                ShowCompletedResult(data, rawBody);
                break;

            default:
                RequestStopAutoPolling();
                SetStatus("Unknown result status: " + data.status);
                break;
        }
    }

    private void ShowCompletedResult(ResultData data, string rawBody)
    {
        if (data.summary == null)
        {
            SetStatus("Result failed: missing summary.");
            return;
        }

        string modelPath = BuildModelPath(GetPlayableModelArtifact(data));

        SetStatus(
            "Done\n" +
            "Success: " + data.summary.success_rate + "\n" +
            "Avg Reward: " + data.summary.avg_reward + "\n" +
            "Avg Steps: " + data.summary.avg_steps + "\n" +
            "Seed: " + data.summary.training_seed + "\n" +
            "Model: " + modelPath
        );

        Debug.Log(
            "Result OK\n" +
            "Status: " + data.status + "\n" +
            "Policy: " + data.summary.policy + "\n" +
            "Score: " + data.summary.score + "\n" +
            "Grid: " + data.summary.grid_width + " x " + data.summary.grid_height + "\n" +
            "Episodes: " + data.summary.episodes + "\n" +
            "Obstacles: " + data.summary.obstacle_count + "\n" +
            "Success Rate: " + data.summary.success_rate + "\n" +
            "Avg Reward: " + data.summary.avg_reward + "\n" +
            "Avg Steps: " + data.summary.avg_steps + "\n" +
            "Training Timesteps: " + data.summary.training_timesteps + "\n" +
            "Training Seed: " + data.summary.training_seed + "\n" +
            "Updated At: " + data.updated_at + "\n" +
            "Model: " + modelPath
        );

        StartModelDownload(data);
    }

    private bool StartTrainingWebSocket()
    {
        StopTrainingWebSocket();

        string url = apiSettings.BuildWebSocketUrl(lastSubmissionId);
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        webSocketClient = new TrainingWebSocketClient();
        webSocketClient.Connect(url);
        Debug.Log("Training WebSocket connecting: " + url);
        return true;
    }

    private void StopTrainingWebSocket()
    {
        if (webSocketClient == null)
        {
            return;
        }

        webSocketClient.Dispose();
        webSocketClient = null;
    }

    private void DrainWebSocketEvents()
    {
        if (webSocketClient == null)
        {
            return;
        }

        while (webSocketClient.TryDequeueError(out string error))
        {
            Debug.LogError("Training WebSocket error: " + error);
            SetStatus("WebSocket error: " + error);

            StopTrainingWebSocket();
            if (trainingStarted)
            {
                StartAutoPolling();
            }

            return;
        }

        while (webSocketClient.TryDequeueMessage(out string message))
        {
            Debug.Log(message);

            ResultData data;
            try
            {
                data = JsonUtility.FromJson<ResultData>(message);
            }
            catch (Exception ex)
            {
                Debug.LogError("Training WebSocket received invalid JSON: " + ex.Message);
                continue;
            }

            if (data == null || string.IsNullOrEmpty(data.submission_id))
            {
                Debug.LogError("Training WebSocket received invalid result JSON.");
                continue;
            }

            HandleResult(data, message);

            if (data.status == "completed" || data.status == "failed")
            {
                StopTrainingWebSocket();
                return;
            }
        }
    }

    private void StartModelDownload(ResultData data)
    {
        ModelArtifactData model = GetPlayableModelArtifact(data);

        if (modelDownloadStarted ||
            model == null)
        {
            if (model == null)
            {
                SetStatus("No playable ONNX model artifact found.");
                Debug.LogError("No playable ONNX model artifact found in result artifacts.\n" + DescribeArtifacts(data));
            }

            return;
        }

        modelDownloadStarted = true;
        StartCoroutine(DownloadModel(model));
    }

    private ModelArtifactData GetPlayableModelArtifact(ResultData data)
    {
        if (data.artifacts == null)
        {
            return null;
        }

        if (IsOnnxArtifact(data.artifacts.sentis_model))
        {
            return data.artifacts.sentis_model;
        }

        if (IsOnnxArtifact(data.artifacts.onnx_model))
        {
            return data.artifacts.onnx_model;
        }

        if (IsOnnxArtifact(data.artifacts.model))
        {
            return data.artifacts.model;
        }

        return null;
    }

    private bool IsOnnxArtifact(ModelArtifactData model)
    {
        return model != null &&
               !string.IsNullOrEmpty(model.path) &&
               (model.path.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model.format, "onnx", StringComparison.OrdinalIgnoreCase));
    }

    private string DescribeArtifacts(ResultData data)
    {
        if (data.artifacts == null)
        {
            return "artifacts: null";
        }

        return "model: " + DescribeArtifact(data.artifacts.model) + "\n" +
               "onnx_model: " + DescribeArtifact(data.artifacts.onnx_model) + "\n" +
               "sentis_model: " + DescribeArtifact(data.artifacts.sentis_model);
    }

    private string DescribeArtifact(ModelArtifactData artifact)
    {
        if (artifact == null)
        {
            return "null";
        }

        return "path=" + artifact.path +
               ", format=" + artifact.format +
               ", target=" + artifact.target +
               ", opset_version=" + artifact.opset_version;
    }

    private IEnumerator DownloadModel(ModelArtifactData model)
    {
        SetStatus("Downloading ONNX model...");

        ApiResult<string> result = null;
        yield return ModelDownloadClient.Download(model, apiSettings.ModelDownloadDirectoryName, response => result = response);

        if (!result.IsSuccess)
        {
            SetStatus("ONNX model download failed: " + result.Error);
            Debug.LogError("ONNX model download failed: " + result.Error);
            yield break;
        }

        string displayPath = result.Data.Replace('\\', '/');
        SetStatus("ONNX model downloaded: " + displayPath);
        Debug.Log("ONNX model downloaded: " + displayPath);

        if (!result.Data.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Downloaded model is not an .onnx file.");
            Debug.LogError("Downloaded model is not an .onnx file: " + displayPath);
            yield break;
        }

        PlayDownloadedModel(result.Data);
    }

    private void PlayDownloadedModel(string modelPath)
    {
        if (robotPolicyPlayer == null)
        {
            robotPolicyPlayer = FindFirstObjectByType<RobotPolicyPlayer>();
        }

        if (robotPolicyPlayer == null)
        {
            robotPolicyPlayer = gameObject.AddComponent<RobotPolicyPlayer>();
        }

        robotPolicyPlayer.Initialize(environmentManager, gridView);
        robotPolicyPlayer.Play(modelPath);
    }

    private string BuildProgressStatus(ResultData data, string fallback)
    {
        if (data.progress == null)
        {
            return fallback;
        }

        string message = string.IsNullOrEmpty(data.progress.message)
            ? fallback
            : data.progress.message;

        return message;
    }

    private string BuildModelPath(ModelArtifactData model)
    {
        if (model == null)
        {
            return "";
        }

        if (string.IsNullOrEmpty(model.bucket))
        {
            return model.path;
        }

        return model.storage + "://" + model.bucket + "/" + model.path;
    }

    private void StartAutoPolling()
    {
        if (!autoPollResults || string.IsNullOrEmpty(lastSubmissionId))
        {
            return;
        }

        StopAutoPolling();
        autoPollActive = true;
        autoPollCoroutine = StartCoroutine(AutoPollResults(lastSubmissionId));
    }

    private void StopAutoPolling()
    {
        autoPollActive = false;

        if (autoPollCoroutine == null)
        {
            return;
        }

        StopCoroutine(autoPollCoroutine);
        autoPollCoroutine = null;
    }

    private void RequestStopAutoPolling()
    {
        autoPollActive = false;
    }

    private IEnumerator AutoPollResults(string resultId)
    {
        while (autoPollActive && resultId == lastSubmissionId)
        {
            yield return new WaitForSeconds(resultPollIntervalSeconds);

            if (!autoPollActive || !autoPollResults || resultId != lastSubmissionId)
            {
                break;
            }

            if (!TryBeginRequest(RunnerState.GettingResult))
            {
                continue;
            }

            yield return GetResult(resultId);
        }

        autoPollActive = false;
        autoPollCoroutine = null;
    }

    private bool TryBeginRequest(RunnerState newState)
    {
        if (IsBusy)
        {
            Debug.Log("TestRunner: ignored " + newState + " because another request is in progress.");
            return false;
        }

        requestInFlight = true;
        SetCurrentState(newState);
        return true;
    }

    private void EndRequest()
    {
        requestInFlight = false;
        SetCurrentState(RunnerState.Idle);
    }

    private void SetCurrentState(RunnerState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;
        Debug.Log("TestRunner: state changed to " + currentState);
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(currentState);
    }

    private void SetStatus(string message)
    {
        Debug.Log("TestRunner: " + message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
