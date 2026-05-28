using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    [CreateAssetMenu(fileName = "ApiSettings", menuName = "EnvForge/API Settings")]
    public sealed class EnvForgeApiSettings : ScriptableObject
    {
        [SerializeField] private string baseUrl = "http://localhost:8000";

        public string BaseUrl => baseUrl;

        public string BuildUrl(string path)
        {
            string normalizedBaseUrl = baseUrl.TrimEnd('/');
            string normalizedPath = path.TrimStart('/');
            return $"{normalizedBaseUrl}/{normalizedPath}";
        }
    }
}
