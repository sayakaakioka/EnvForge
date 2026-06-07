using System;
using System.Collections;
using System.Text;
using EnvForge.Navigation.Contracts;
using UnityEngine.Networking;

namespace EnvForge.Navigation.Cloud
{
    public sealed class EnvForgeApiClient
    {
        private const string JsonContentType = "application/json";

        private readonly string baseUrl;

        public EnvForgeApiClient(EnvForgeApiSettings settings)
            : this(settings != null ? settings.BaseUrl : "http://localhost:8000")
        {
        }

        public EnvForgeApiClient(string baseUrl)
        {
            this.baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:8000" : baseUrl;
        }

        public IEnumerator SubmitScenario(
            ScenarioBundleDto scenario,
            Action<SubmissionResponseDto> onSuccess,
            Action<string> onError)
        {
            string json = ScenarioBundleSerializer.ToJson(scenario, prettyPrint: false);
            yield return SendJson(
                "submissions",
                UnityWebRequest.kHttpVerbPOST,
                json,
                responseJson => onSuccess?.Invoke(JsonUtilityBridge.FromJson<SubmissionResponseDto>(responseJson)),
                onError);
        }

        public IEnumerator StartTraining(
            string submissionId,
            Action<SubmissionResponseDto> onSuccess,
            Action<string> onError)
        {
            yield return SendJson(
                $"submissions/{submissionId}/train",
                UnityWebRequest.kHttpVerbPOST,
                "{}",
                responseJson => onSuccess?.Invoke(JsonUtilityBridge.FromJson<SubmissionResponseDto>(responseJson)),
                onError);
        }

        private IEnumerator SendJson(
            string path,
            string method,
            string requestJson,
            Action<string> onSuccess,
            Action<string> onError)
        {
            using UnityWebRequest request = new(BuildUrl(path), method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", JsonContentType);

            if (requestJson != null)
            {
                byte[] body = Encoding.UTF8.GetBytes(requestJson);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.SetRequestHeader("Content-Type", JsonContentType);
            }

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{request.error}: {request.url}");
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
        }

        private string BuildUrl(string path)
        {
            string normalizedBaseUrl = baseUrl.TrimEnd('/');
            string normalizedPath = path.TrimStart('/');
            return $"{normalizedBaseUrl}/{normalizedPath}";
        }
    }

    internal static class JsonUtilityBridge
    {
        public static T FromJson<T>(string json)
        {
            return UnityEngine.JsonUtility.FromJson<T>(json);
        }
    }
}
