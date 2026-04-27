using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class TrainingApiClient
{
    private readonly string baseUrl;

    public TrainingApiClient(string baseUrl)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    public IEnumerator Submit(SubmitRequestData payload, Action<ApiResult<SubmitResponseData>> onComplete)
    {
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(baseUrl + "/submissions", UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        SetJsonHeaders(request);

        Debug.Log("Submit request: " + json);
        yield return request.SendWebRequest();

        CompleteJsonRequest(request, onComplete);
    }

    public IEnumerator Train(string submissionId, Action<ApiResult<TrainResponseData>> onComplete)
    {
        string url = $"{baseUrl}/submissions/{submissionId}/train";

        using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.downloadHandler = new DownloadHandlerBuffer();
        SetJsonHeaders(request);

        Debug.Log("Train request: " + url);
        yield return request.SendWebRequest();

        CompleteJsonRequest(request, onComplete);
    }

    public IEnumerator GetResult(string resultId, Action<ApiResult<ResultData>> onComplete)
    {
        string url = $"{baseUrl}/results/{resultId}";

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Accept", "application/json");

        Debug.Log("Result request: " + url);
        yield return request.SendWebRequest();

        CompleteJsonRequest(request, onComplete);
    }

    private static void SetJsonHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");
    }

    private static void CompleteJsonRequest<T>(UnityWebRequest request, Action<ApiResult<T>> onComplete)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : "";

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = ExtractErrorMessage(body, request.error);
            onComplete?.Invoke(ApiResult<T>.Failure(error, body));
            return;
        }

        T data = JsonUtility.FromJson<T>(body);
        if (data == null)
        {
            onComplete?.Invoke(ApiResult<T>.Failure("Invalid JSON response.", body));
            return;
        }

        onComplete?.Invoke(ApiResult<T>.Success(data, body));
    }

    private static string ExtractErrorMessage(string body, string fallback)
    {
        if (!string.IsNullOrEmpty(body))
        {
            ErrorResponseData error = JsonUtility.FromJson<ErrorResponseData>(body);
            if (error != null && !string.IsNullOrEmpty(error.detail))
            {
                return error.detail;
            }
        }

        return fallback;
    }
}
