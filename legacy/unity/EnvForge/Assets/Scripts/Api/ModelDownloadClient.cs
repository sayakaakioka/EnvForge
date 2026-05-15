using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class ModelDownloadClient
{
    public static IEnumerator Download(ModelArtifactData model, string directoryName, Action<ApiResult<string>> onComplete)
    {
        string url = BuildDownloadUrl(model);
        if (string.IsNullOrEmpty(url))
        {
            onComplete?.Invoke(ApiResult<string>.Failure("Model artifact is not downloadable.", ""));
            yield break;
        }

        string destinationPath = BuildDestinationPath(model, directoryName);
        string destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using var request = UnityWebRequest.Get(url);
        request.downloadHandler = new DownloadHandlerFile(destinationPath);

        Debug.Log("Model download request: " + url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onComplete?.Invoke(ApiResult<string>.Failure(request.error, ""));
            yield break;
        }

        onComplete?.Invoke(ApiResult<string>.Success(destinationPath, ""));
    }

    private static string BuildDownloadUrl(ModelArtifactData model)
    {
        if (model == null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(model.path) &&
            (model.path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             model.path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return model.path;
        }

        if (model.storage == "gcs" && !string.IsNullOrEmpty(model.bucket) && !string.IsNullOrEmpty(model.path))
        {
            return "https://storage.googleapis.com/" + model.bucket + "/" + model.path;
        }

        return "";
    }

    private static string BuildDestinationPath(ModelArtifactData model, string directoryName)
    {
        string directory = Path.Combine(Application.persistentDataPath, directoryName);

        if (model == null || string.IsNullOrEmpty(model.path))
        {
            return Path.Combine(directory, "policy.zip");
        }

        if (model.path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            model.path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(directory, Path.GetFileName(model.path));
        }

        string relativePath = model.path.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(directory, relativePath);
    }
}
