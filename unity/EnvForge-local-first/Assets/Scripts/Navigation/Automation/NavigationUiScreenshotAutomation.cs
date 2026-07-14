using System;
using System.Collections;
using System.IO;
using EmbodiedLab.Unity;
using EnvForge.Navigation;
using EnvForge.Navigation.Cloud;
using EnvForge.Navigation.Inference;
using EnvForge.Navigation.Replay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnvForge.Navigation.Automation
{
    public sealed class NavigationUiScreenshotAutomation : MonoBehaviour
    {
        private const string ScreenshotArgument = "-envforgeScreenshot";
        private const string ModeArgument = "-envforgeScreenshotMode";
        private const string DelayArgument = "-envforgeScreenshotDelay";
        private const string VariantArgument = "-envforgeWorldVariant";
        private const string ReplayFileArgument = "-envforgeReplayFile";
        private const string InferenceModelArgument = "-envforgeInferenceModel";

        private string screenshotPath;
        private string screenshotMode;
        private float screenshotDelay = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromCommandLine()
        {
            string path = GetArgumentValue(ScreenshotArgument);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            GameObject runner = new("EnvForge UI Screenshot Automation");
            DontDestroyOnLoad(runner);
            NavigationUiScreenshotAutomation automation = runner.AddComponent<NavigationUiScreenshotAutomation>();
            automation.screenshotPath = Path.GetFullPath(path);
            automation.screenshotMode = GetArgumentValue(ModeArgument) ?? "compact";
            if (float.TryParse(GetArgumentValue(DelayArgument), out float delaySeconds))
            {
                automation.screenshotDelay = Mathf.Max(0.1f, delaySeconds);
            }
        }

        private void Start()
        {
            StartCoroutine(CaptureWhenReady());
        }

        private IEnumerator CaptureWhenReady()
        {
            yield return null;
            yield return new WaitUntil(() => SceneManager.GetActiveScene().isLoaded);
            yield return new WaitForSeconds(screenshotDelay);

            EnvForgeCloudRunPanel panel = FindFirstObjectByType<EnvForgeCloudRunPanel>();
            NavigationSceneBuilder sceneBuilder = FindFirstObjectByType<NavigationSceneBuilder>();
            string worldVariant = GetArgumentValue(VariantArgument);
            if (!string.IsNullOrWhiteSpace(worldVariant))
            {
                NavigationWorldVariantAutomation.Apply(sceneBuilder, worldVariant);
            }

            if (panel != null)
            {
                if (string.Equals(screenshotMode, "compact", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(screenshotMode, "replay", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(screenshotMode, "world", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(screenshotMode, "inference", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep the default compact HUD state.
                }
                else if (string.Equals(screenshotMode, "reward", StringComparison.OrdinalIgnoreCase))
                {
                    panel.ShowTrainingSettingsForAutomation(true);
                }
                else if (string.Equals(screenshotMode, "job", StringComparison.OrdinalIgnoreCase))
                {
                    panel.ShowJobDetailsForAutomation();
                }
                else
                {
                    panel.ShowTrainingSettingsForAutomation(false);
                }
            }

            NavigationReplayPlayer replayPlayer = FindFirstObjectByType<NavigationReplayPlayer>();
            string replayPath = GetArgumentValue(ReplayFileArgument);
            if (replayPlayer != null && !string.IsNullOrWhiteSpace(replayPath) && File.Exists(replayPath))
            {
                replayPlayer.LoadSteps(EmbodiedLabReplay.ReadSteps(replayPath));
                replayPlayer.Play();
            }

            if (replayPlayer != null &&
                string.Equals(screenshotMode, "replay", StringComparison.OrdinalIgnoreCase))
            {
                replayPlayer.ShowDetailsForAutomation();
            }

            string modelPath = GetArgumentValue(InferenceModelArgument);
            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                replayPlayer?.ReleaseControl();
                NavigationModelInferenceController inferenceController = FindFirstObjectByType<NavigationModelInferenceController>();
                if (inferenceController != null && !inferenceController.StartInference(modelPath, out string inferenceError))
                {
                    Debug.LogError($"EnvForge screenshot inference failed: {inferenceError}");
                }
            }

            NavigationWorldEditorPanel worldEditorPanel = FindFirstObjectByType<NavigationWorldEditorPanel>();
            if (worldEditorPanel != null &&
                string.Equals(screenshotMode, "world", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(worldVariant))
                {
                    sceneBuilder?.AddUserWall(new Vector2(0f, 0f), 3.5f, 0.35f, 25f);
                }

                worldEditorPanel.ShowDetailsForAutomation();
            }

            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath) ?? ".");
            ScreenCapture.CaptureScreenshot(screenshotPath);
            Debug.Log($"EnvForge UI screenshot saved: {screenshotPath}");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(0);
        }

        private static string GetArgumentValue(string argumentName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

    }
}
