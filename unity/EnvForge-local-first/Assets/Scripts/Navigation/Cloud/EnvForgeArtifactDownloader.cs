using System;
using System.Collections;
using System.IO;
using EnvForge.Navigation.Contracts;
using UnityEngine.Networking;

namespace EnvForge.Navigation.Cloud
{
    public sealed class EnvForgeArtifactDownloader
    {
        public IEnumerator DownloadText(
            ArtifactLocationDto artifact,
            Action<string> onSuccess,
            Action<string> onError)
        {
            if (!ArtifactLocationUrl.TryBuildPublicUrl(artifact, out string url))
            {
                onError?.Invoke("Artifact location is not a public GCS object.");
                yield break;
            }

            using UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{request.error}: {url}");
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
        }

        public IEnumerator DownloadFile(
            ArtifactLocationDto artifact,
            string localPath,
            Action<string> onSuccess,
            Action<string> onError)
        {
            if (!ArtifactLocationUrl.TryBuildPublicUrl(artifact, out string url))
            {
                onError?.Invoke("Artifact location is not a public GCS object.");
                yield break;
            }

            string directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(localPath);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{request.error}: {url}");
                yield break;
            }

            onSuccess?.Invoke(localPath);
        }
    }
}
