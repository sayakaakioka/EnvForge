using UnityEngine;

namespace EnvForge.Navigation
{
    public sealed class SegmentationPreviewOverlay : MonoBehaviour
    {
        private const float Padding = 12f;

        private bool showPreview;
        private Camera segmentationCamera;
        private Rect normalizedRect;
        private int textureWidth;
        private int textureHeight;
        private RenderTexture previewTexture;
        private RenderTexture originalTargetTexture;
        private bool originalCameraEnabled;

        public void Configure(bool enabled, Camera cameraToPreview, Rect screenRect, int width, int height)
        {
            showPreview = enabled;
            segmentationCamera = cameraToPreview;
            normalizedRect = screenRect;
            textureWidth = width;
            textureHeight = height;

            if (!showPreview || segmentationCamera == null)
            {
                return;
            }

            originalTargetTexture = segmentationCamera.targetTexture;
            originalCameraEnabled = segmentationCamera.enabled;
            EnsurePreviewTexture();
            AttachPreviewTexture();
        }

        private void LateUpdate()
        {
            if (!showPreview || segmentationCamera == null || previewTexture == null)
            {
                return;
            }

            AttachPreviewTexture();
        }

        private void OnGUI()
        {
            if (!showPreview || previewTexture == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            float pixelHeight = normalizedRect.height * Screen.height;
            float pixelWidth = pixelHeight * textureWidth / Mathf.Max(1f, textureHeight);
            Rect pixelRect = new(
                Screen.width - Padding - pixelWidth,
                Screen.height - Padding - pixelHeight,
                pixelWidth,
                pixelHeight);

            GUI.DrawTexture(pixelRect, previewTexture, ScaleMode.StretchToFill, false);
        }

        private void EnsurePreviewTexture()
        {
            if (previewTexture != null && previewTexture.width == textureWidth && previewTexture.height == textureHeight)
            {
                return;
            }

            ReleasePreviewTexture();
            previewTexture = new RenderTexture(textureWidth, textureHeight, 24)
            {
                name = "MVP 2 Segmentation Preview Texture",
            };
            previewTexture.Create();
        }

        private void AttachPreviewTexture()
        {
            segmentationCamera.targetTexture = previewTexture;
            segmentationCamera.rect = new Rect(0f, 0f, 1f, 1f);
            segmentationCamera.enabled = true;
        }

        private void OnDestroy()
        {
            if (segmentationCamera != null)
            {
                segmentationCamera.targetTexture = originalTargetTexture;
                segmentationCamera.enabled = originalCameraEnabled;
            }

            ReleasePreviewTexture();
        }

        private void ReleasePreviewTexture()
        {
            if (previewTexture == null)
            {
                return;
            }

            previewTexture.Release();
            Destroy(previewTexture);
            previewTexture = null;
        }
    }
}
