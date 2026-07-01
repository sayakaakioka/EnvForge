using System;
using EnvForge.Navigation.Contracts;
using UnityEngine.Networking;

namespace EnvForge.Navigation.Cloud
{
    public static class ArtifactLocationUrl
    {
        private const string GcsStorage = "gcs";
        private const string PublicGcsBaseUrl = "https://storage.googleapis.com";

        public static bool TryBuildPublicUrl(ArtifactLocationDto artifact, out string url)
        {
            url = null;
            if (artifact == null ||
                !string.Equals(artifact.storage, GcsStorage, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(artifact.bucket) ||
                string.IsNullOrWhiteSpace(artifact.path))
            {
                return false;
            }

            url = $"{PublicGcsBaseUrl}/{UnityWebRequest.EscapeURL(artifact.bucket)}/{EscapePath(artifact.path)}";
            return true;
        }

        private static string EscapePath(string path)
        {
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = UnityWebRequest.EscapeURL(segments[i]);
            }

            return string.Join("/", segments);
        }
    }
}
