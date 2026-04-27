using UnityEngine;

[CreateAssetMenu(fileName = "ApiSettings", menuName = "EnvForge/API Settings")]
public class ApiSettings : ScriptableObject
{
    [SerializeField] private string baseUrl = "";
    [SerializeField] private string websocketUrlTemplate = "";
    [SerializeField] private string modelDownloadDirectoryName = "Models";

    public string BaseUrl => string.IsNullOrWhiteSpace(baseUrl) ? "" : baseUrl.TrimEnd('/');
    public string WebSocketUrlTemplate => string.IsNullOrWhiteSpace(websocketUrlTemplate) ? "" : websocketUrlTemplate.Trim();
    public string ModelDownloadDirectoryName => string.IsNullOrWhiteSpace(modelDownloadDirectoryName) ? "Models" : modelDownloadDirectoryName.Trim();

    public string BuildWebSocketUrl(string submissionId)
    {
        if (string.IsNullOrEmpty(WebSocketUrlTemplate))
        {
            return "";
        }

        return WebSocketUrlTemplate.Replace("{submission_id}", submissionId);
    }
}
