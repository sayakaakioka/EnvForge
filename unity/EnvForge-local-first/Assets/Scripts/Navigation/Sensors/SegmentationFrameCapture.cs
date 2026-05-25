using System.IO;
using UnityEngine;

namespace EnvForge.Navigation
{
    [RequireComponent(typeof(Camera))]
    public sealed class SegmentationFrameCapture : MonoBehaviour
    {
        [SerializeField] private bool saveFrames;
        [SerializeField] private int saveEverySteps = 100;
        [SerializeField] private int width = 84;
        [SerializeField] private int height = 84;
        [SerializeField] private string directoryName = "SegmentationCaptures";

        private Camera captureCamera;
        private int savedFrameCount;

        public void Configure(bool shouldSaveFrames, int stepInterval, int captureWidth, int captureHeight)
        {
            saveFrames = shouldSaveFrames;
            saveEverySteps = Mathf.Max(1, stepInterval);
            width = Mathf.Max(1, captureWidth);
            height = Mathf.Max(1, captureHeight);
        }

        private void Awake()
        {
            captureCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (!saveFrames || captureCamera == null || Time.frameCount % saveEverySteps != 0)
            {
                return;
            }

            SaveFrame();
        }

        private void SaveFrame()
        {
            RenderTexture previousRenderTexture = RenderTexture.active;
            RenderTexture previousCameraTarget = captureCamera.targetTexture;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            try
            {
                captureCamera.targetTexture = renderTexture;
                captureCamera.Render();
                RenderTexture.active = renderTexture;

                Texture2D texture = new(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                string outputDirectory = Path.Combine(Application.persistentDataPath, directoryName);
                Directory.CreateDirectory(outputDirectory);
                string fileName = $"segmentation_{Time.frameCount:D08}_{savedFrameCount:D04}.png";
                File.WriteAllBytes(Path.Combine(outputDirectory, fileName), texture.EncodeToPNG());
                savedFrameCount++;

                Destroy(texture);
            }
            finally
            {
                captureCamera.targetTexture = previousCameraTarget;
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
