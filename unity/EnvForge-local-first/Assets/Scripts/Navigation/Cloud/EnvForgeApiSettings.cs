using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    [CreateAssetMenu(fileName = "ApiSettings", menuName = "EnvForge/API Settings")]
    public sealed class EnvForgeApiSettings : ScriptableObject
    {
        [SerializeField] private string baseUrl = "http://localhost:8000";
        [SerializeField] private string webSocketBaseUrl = "";

        public string BaseUrl => baseUrl;

        public string WebSocketBaseUrl => webSocketBaseUrl;
    }
}
