using System;
using System.Collections;
using System.IO;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Replay;
using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    public sealed class EnvForgeCloudRunPanel : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float Width = 560f;
        private const float Height = 260f;
        private const int FontSize = 18;
        private const string BundledReplayResource = "EnvForge/navigation_default_replay";

        [SerializeField] private bool showPanel = true;
        [SerializeField] private string fallbackBaseUrl = "http://localhost:8000";

        private NavigationSceneBuilder sceneBuilder;
        private NavigationReplayPlayer replayPlayer;
        private EnvForgeApiClient apiClient;
        private EnvForgeArtifactDownloader artifactDownloader;
        private ResultDocumentDto latestResult;
        private string submissionId;
        private string status = "Cloud: idle";
        private bool busy;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        public void Configure(
            NavigationSceneBuilder builder,
            NavigationReplayPlayer player,
            EnvForgeApiSettings settings,
            string baseUrl)
        {
            sceneBuilder = builder;
            replayPlayer = player;
            fallbackBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? fallbackBaseUrl : baseUrl;
            apiClient = settings != null
                ? new EnvForgeApiClient(settings)
                : new EnvForgeApiClient(fallbackBaseUrl);
            artifactDownloader = new EnvForgeArtifactDownloader();
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            EnsureStyles();
            Rect boxRect = new(Screen.width - Width - Padding, Padding, Width, Height);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            GUILayout.BeginArea(new Rect(boxRect.x + Padding, boxRect.y + Padding, Width - Padding * 2f, Height - Padding * 2f));
            GUILayout.Label("EnvForge Cloud", labelStyle);
            GUILayout.Label(status, labelStyle);
            GUILayout.Label($"submission: {submissionId ?? "-"}", labelStyle);
            GUILayout.Label($"result: {latestResult?.status ?? "-"}", labelStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = !busy && apiClient != null && sceneBuilder != null;
            if (GUILayout.Button("Submit + Train"))
            {
                StartCoroutine(SubmitAndTrain());
            }

            GUI.enabled = !busy && apiClient != null && !string.IsNullOrEmpty(submissionId);
            if (GUILayout.Button("Poll Result"))
            {
                StartCoroutine(PollResult());
            }

            ResultArtifactsDto artifacts = GetResultArtifacts();

            GUI.enabled = !busy && artifacts?.replay_log != null;
            if (GUILayout.Button("Download Replay"))
            {
                StartCoroutine(DownloadReplay());
            }

            GUI.enabled = !busy && artifacts?.sentis_model != null;
            if (GUILayout.Button("Download Model"))
            {
                StartCoroutine(DownloadModelArtifacts());
            }

            GUI.enabled = !busy;
            if (GUILayout.Button("Load Demo Replay"))
            {
                LoadBundledReplay();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private IEnumerator SubmitAndTrain()
        {
            busy = true;
            latestResult = null;
            status = "Cloud: submitting scenario";

            ScenarioBundleDto scenario = sceneBuilder.BuildScenarioBundle();
            bool failed = false;
            yield return apiClient.SubmitScenario(
                scenario,
                response =>
                {
                    submissionId = response.submission_id;
                    status = $"Cloud: submitted {submissionId}";
                },
                error =>
                {
                    failed = true;
                    status = $"Cloud submit failed: {error}";
                });

            if (failed || string.IsNullOrEmpty(submissionId))
            {
                busy = false;
                yield break;
            }

            status = "Cloud: starting training";
            yield return apiClient.StartTraining(
                submissionId,
                _ => status = "Cloud: training queued",
                error =>
                {
                    failed = true;
                    status = $"Cloud train failed: {error}";
                });

            busy = false;
            if (!failed)
            {
                StartCoroutine(PollUntilTerminal());
            }
        }

        private IEnumerator PollUntilTerminal()
        {
            while (!string.IsNullOrEmpty(submissionId))
            {
                yield return PollResult();
                string resultStatus = latestResult?.status;
                if (string.Equals(resultStatus, "completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(resultStatus, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    yield break;
                }

                yield return new WaitForSeconds(5f);
            }
        }

        private IEnumerator PollResult()
        {
            busy = true;
            status = "Cloud: polling result";
            yield return apiClient.GetResult(
                submissionId,
                result =>
                {
                    latestResult = result;
                    status = $"Cloud: {result.status}";
                },
                error => status = $"Cloud poll failed: {error}");
            busy = false;
        }

        private IEnumerator DownloadReplay()
        {
            busy = true;
            status = "Cloud: downloading replay";
            yield return artifactDownloader.DownloadText(
                GetResultArtifacts().replay_log,
                jsonLines =>
                {
                    replayPlayer.LoadSteps(ReplayLogSerializer.FromJsonLines(jsonLines));
                    status = "Cloud: replay loaded";
                },
                error => status = $"Replay download failed: {error}");
            busy = false;
        }

        private IEnumerator DownloadModelArtifacts()
        {
            busy = true;
            string outputDir = Path.Combine(Application.persistentDataPath, "EnvForge", submissionId ?? "latest");
            ResultArtifactsDto artifacts = GetResultArtifacts();
            if (artifacts.onnx_model != null)
            {
                yield return DownloadArtifactFile(artifacts.onnx_model, Path.Combine(outputDir, "policy.onnx"));
            }

            if (artifacts.sentis_model != null)
            {
                yield return DownloadArtifactFile(artifacts.sentis_model, Path.Combine(outputDir, "policy.sentis.onnx"));
            }

            busy = false;
        }

        private ResultArtifactsDto GetResultArtifacts()
        {
            if (latestResult?.artifacts != null)
            {
                return latestResult.artifacts;
            }

            return latestResult?.result_bundle?.artifacts;
        }

        private IEnumerator DownloadArtifactFile(ArtifactLocationDto artifact, string localPath)
        {
            status = $"Cloud: downloading {Path.GetFileName(localPath)}";
            yield return artifactDownloader.DownloadFile(
                artifact,
                localPath,
                savedPath => status = $"Cloud: saved {savedPath}",
                error => status = $"Model download failed: {error}");
        }

        private void LoadBundledReplay()
        {
            TextAsset replayAsset = Resources.Load<TextAsset>(BundledReplayResource);
            if (replayAsset == null)
            {
                status = $"Demo replay not found: Resources/{BundledReplayResource}";
                return;
            }

            replayPlayer.LoadSteps(ReplayLogSerializer.FromJsonLines(replayAsset.text));
            status = "Cloud: demo replay loaded";
        }

        private void EnsureStyles()
        {
            if (labelStyle != null && boxStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                normal = { textColor = Color.white },
                wordWrap = true,
            };

            Texture2D background = new(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            background.Apply();

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = background;
        }
    }
}
