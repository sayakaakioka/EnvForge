using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Navigation
{
    public sealed class NavigationCameraController : MonoBehaviour
    {
        private enum CameraViewMode
        {
            Angled,
            Top,
        }

        private const float AngledPitchDegrees = 55f;
        private const float TopPitchDegrees = 90f;
        private const float MinGroundDistance = 4f;
        private const float MaxGroundDistance = 360f;

        [SerializeField] private float dragPanSpeed = 0.025f;
        [SerializeField] private float keyboardPanSpeed = 10f;
        [SerializeField] private float zoomSpeed = 0.18f;
        [SerializeField] private float fitPaddingMeters = 4f;

        private Camera controlledCamera;
        private Vector3 focusPoint;
        private Vector2 currentFloorSize = new(16f, 12f);
        private float groundDistance = 10f;
        private Vector2 lastPointerPosition;
        private bool dragging;
        private CameraViewMode viewMode = CameraViewMode.Angled;

        public string NextViewModeLabel => viewMode == CameraViewMode.Angled ? "Top" : "Angle";

        public void Configure(Camera camera, Vector2 floorSize)
        {
            controlledCamera = camera;
            currentFloorSize = floorSize;
            FitToFloor(floorSize);
        }

        public void FitToFloor(Vector2 floorSize)
        {
            currentFloorSize = floorSize;
            float aspect = controlledCamera == null || controlledCamera.aspect <= 0f
                ? 16f / 9f
                : controlledCamera.aspect;
            float halfWidth = Mathf.Max(0.5f, floorSize.x * 0.5f);
            float halfDepth = Mathf.Max(0.5f, floorSize.y * 0.5f);
            float widthLimitedDistance = halfWidth / Mathf.Max(0.1f, aspect);
            groundDistance = Mathf.Clamp(
                Mathf.Max(halfDepth, widthLimitedDistance) + fitPaddingMeters,
                MinGroundDistance,
                MaxGroundDistance);
            focusPoint = Vector3.zero;
            ApplyCameraTransform();
        }

        public void ResetView(Vector2 floorSize)
        {
            FitToFloor(floorSize);
        }

        public void ToggleViewMode()
        {
            viewMode = viewMode == CameraViewMode.Angled ? CameraViewMode.Top : CameraViewMode.Angled;
            ApplyCameraTransform();
        }

        private void Update()
        {
            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
                if (controlledCamera == null)
                {
                    return;
                }
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                GUIUtility.keyboardControl == 0 &&
                !NavigationWorldEditorPanel.IsTextInputFocused &&
                !Cloud.EnvForgeCloudRunPanel.IsTextInputFocused)
            {
                if (keyboard.rKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
                {
                    ResetView(currentFloorSize);
                }

                Vector2 keyboardMove = Vector2.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    keyboardMove.x -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    keyboardMove.x += 1f;
                }

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    keyboardMove.y += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    keyboardMove.y -= 1f;
                }

                if (keyboardMove.sqrMagnitude > 0f)
                {
                    Pan(keyboardMove.normalized * (keyboardPanSpeed * Time.deltaTime));
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            if (NavigationWorldEditorPanel.IsPointerEditingWorld)
            {
                dragging = false;
                return;
            }

            bool wantsDrag = mouse.middleButton.isPressed || mouse.rightButton.isPressed;
            if (wantsDrag && !dragging)
            {
                dragging = true;
                lastPointerPosition = pointerPosition;
            }
            else if (!wantsDrag)
            {
                dragging = false;
            }

            if (dragging)
            {
                Vector2 delta = pointerPosition - lastPointerPosition;
                lastPointerPosition = pointerPosition;
                Pan(new Vector2(-delta.x, -delta.y) * (dragPanSpeed * Mathf.Max(1f, groundDistance * 0.1f)));
            }

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > Mathf.Epsilon)
            {
                groundDistance = Mathf.Clamp(
                    groundDistance * (1f - scrollY * zoomSpeed * 0.01f),
                    MinGroundDistance,
                    MaxGroundDistance);
                ApplyCameraTransform();
            }
        }

        private void Pan(Vector2 move)
        {
            Vector3 right = controlledCamera.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(controlledCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                forward = Vector3.forward;
            }

            focusPoint += right * move.x + forward * move.y;
            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            if (controlledCamera == null)
            {
                return;
            }

            if (viewMode == CameraViewMode.Top)
            {
                float halfFovRadians = controlledCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float height = Mathf.Max(2f, groundDistance / Mathf.Max(0.1f, Mathf.Tan(halfFovRadians)));
                controlledCamera.transform.SetPositionAndRotation(
                    new Vector3(focusPoint.x, height, focusPoint.z),
                    Quaternion.Euler(TopPitchDegrees, 0f, 0f));
                return;
            }

            float pitchRadians = AngledPitchDegrees * Mathf.Deg2Rad;
            Vector3 position = new(
                focusPoint.x,
                Mathf.Max(2f, groundDistance * Mathf.Tan(pitchRadians)),
                focusPoint.z - groundDistance);
            controlledCamera.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(AngledPitchDegrees, 0f, 0f));
        }
    }
}
