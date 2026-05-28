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

        private readonly EnvForgeApiSettings settings;

        public EnvForgeApiClient(EnvForgeApiSettings settings)
        {
            this.settings = settings;
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

        public IEnumerator GetResult(
            string submissionId,
            Action<ResultDocumentDto> onSuccess,
            Action<string> onError)
        {
            yield return SendJson(
                $"results/{submissionId}",
                UnityWebRequest.kHttpVerbGET,
                null,
                responseJson => onSuccess?.Invoke(JsonUtilityBridge.FromJson<ResultDocumentDto>(responseJson)),
                onError);
        }

        private IEnumerator SendJson(
            string path,
            string method,
            string requestJson,
            Action<string> onSuccess,
            Action<string> onError)
        {
            using UnityWebRequest request = new(settings.BuildUrl(path), method);
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
                onError?.Invoke(request.error);
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
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
