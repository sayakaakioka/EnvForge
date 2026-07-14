using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EmbodiedLab.Contracts;
using EmbodiedLab.Unity;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Replay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Navigation
{
    public sealed class NavigationWorldEditorPanel : MonoBehaviour
    {
        private enum WallDragMode
        {
            None,
            Move,
            ResizeNegative,
            ResizePositive,
        }

        private const float Padding = 12f;
        private const float CompactWidth = 640f;
        private const float CompactHeight = 112f;
        private const float DetailsWidth = 620f;
        private const float DetailsHeight = 540f;
        private const float PanelTop = Padding;
        private const float ButtonHeight = 42f;
        private const float FieldHeight = 34f;
        private const float LabelWidth = 180f;
        private const int FontSize = 22;
        private const int TitleFontSize = 26;
        private const float HandleSize = 18f;
        private const float HandlePickRadius = 22f;
        private const float BodyPickRadius = 28f;
        private const float HandleInsetMeters = 0.35f;
        private const float OutlineThicknessPixels = 4f;
        private const float NotificationDurationSeconds = 1.8f;
        private const string TextFieldFocusPrefix = "WorldEditorTextField_";
        private const string MapDirectoryName = "maps";
        private const string MapFileName = "latest-map.json";
        private const string MapHistoryFileName = "map-history.json";
        private const string SavedMapScenarioId = "navigation_saved_map";
        private const float RotationStepDegrees = 15f;

        private NavigationSceneBuilder sceneBuilder;
        private bool showPanel = true;
        private bool showDetails;
        private GUIStyle buttonStyle;
        private GUIStyle compactButtonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle titleStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle boxStyle;
        private GUIStyle notificationStyle;
        private Texture2D wallGuideTexture;
        private NavigationReplayPlayer replayPlayer;
        private Vector2 detailsScroll;
        private Rect lastPanelRect;
        private int selectedWallIndex = -1;
        private WallDragMode dragMode;
        private Vector2 dragOffset;
        private Vector2 fixedResizeEndpoint;
        private string notificationMessage;
        private float notificationUntil;
        private string pendingDeleteMapPath;

        private string floorWidthText;
        private string floorDepthText;
        private string wallXText = "0";
        private string wallZText = "0";
        private string startXText = "0";
        private string startZText = "0";
        private string goalXText = "0";
        private string goalZText = "0";
        private string wallAngleText = "0";
        private string mapNameText = "Map";
        private string mapStatus = "Map: not saved";

        public static bool IsPointerEditingWorld { get; private set; }

        public bool IsExpandedPanelOpen => showPanel && showDetails;

        public void Configure(NavigationSceneBuilder builder)
        {
            sceneBuilder = builder;
            SyncTextFromScene();
        }

        public void ShowDetailsForAutomation()
        {
            showPanel = true;
            showDetails = true;
            SyncTextFromScene();
        }

        private void Update()
        {
            HandleWorldMouseEditing();
        }

        private void OnDestroy()
        {
            NavigationInputBlocker.UnregisterPanel(nameof(NavigationWorldEditorPanel));
        }

        private void OnGUI()
        {
            if (!showPanel || sceneBuilder == null || IsReplayPanelExpanded())
            {
                lastPanelRect = Rect.zero;
                NavigationInputBlocker.UnregisterPanel(nameof(NavigationWorldEditorPanel));
                return;
            }

            EnsureStyles();
            if (showDetails)
            {
                DrawDetails();
                DrawSelectedWallHandles();
                UpdatePointerOverPanel();
                DrawNotificationOverlay();
                return;
            }

            DrawCompact();
            DrawSelectedWallHandles();
            UpdatePointerOverPanel();
            DrawNotificationOverlay();
        }

        private bool IsReplayPanelExpanded()
        {
            if (replayPlayer == null)
            {
                replayPlayer = FindFirstObjectByType<NavigationReplayPlayer>();
            }

            return replayPlayer != null && replayPlayer.IsExpandedPanelOpen;
        }

        private void DrawCompact()
        {
            float width = Mathf.Min(CompactWidth, Screen.width - Padding * 2f);
            Rect rect = new(Padding, PanelTop, width, CompactHeight);
            lastPanelRect = rect;
            GUI.Box(rect, GUIContent.none, boxStyle);

            Rect content = new(rect.x + Padding, rect.y + Padding, rect.width - Padding * 2f, rect.height - Padding * 2f);
            GUI.Label(new Rect(content.x, content.y, content.width, 32f), FormatWorldSummary(), labelStyle);

            float buttonTop = content.y + 42f;
            float buttonWidth = (content.width - Padding * 5f) / 6f;
            if (GUI.Button(new Rect(content.x, buttonTop, buttonWidth, ButtonHeight), "Edit", compactButtonStyle))
            {
                showDetails = true;
                SyncTextFromScene();
            }

            if (GUI.Button(new Rect(content.x + buttonWidth + Padding, buttonTop, buttonWidth, ButtonHeight), "Wall", compactButtonStyle))
            {
                AddWallFromFields();
            }

            if (GUI.Button(new Rect(content.x + (buttonWidth + Padding) * 2f, buttonTop, buttonWidth, ButtonHeight), "Undo", compactButtonStyle))
            {
                RemoveLastWall();
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && selectedWallIndex >= 0;
            if (GUI.Button(new Rect(content.x + (buttonWidth + Padding) * 3f, buttonTop, buttonWidth, ButtonHeight), "Delete", compactButtonStyle))
            {
                DeleteSelectedWall();
            }

            if (GUI.Button(new Rect(content.x + (buttonWidth + Padding) * 4f, buttonTop, buttonWidth, ButtonHeight), "Rot", compactButtonStyle))
            {
                RotateSelectedWallBy(RotationStepDegrees);
            }

            GUI.enabled = previousEnabled;
            if (GUI.Button(new Rect(content.x + (buttonWidth + Padding) * 5f, buttonTop, buttonWidth, ButtonHeight), sceneBuilder.NextCameraViewLabel, compactButtonStyle))
            {
                sceneBuilder.ToggleCameraView();
            }
        }

        private void DrawDetails()
        {
            float width = Mathf.Min(DetailsWidth, Screen.width - Padding * 2f);
            Rect rect = new(Padding, PanelTop, width, DetailsHeight);
            lastPanelRect = rect;
            GUI.Box(rect, GUIContent.none, boxStyle);

            Rect content = new(rect.x + Padding, rect.y + Padding, rect.width - Padding * 2f, rect.height - Padding * 2f);
            GUI.Label(new Rect(content.x, content.y, content.width, 36f), "World", titleStyle);
            if (GUI.Button(new Rect(content.xMax - 132f, content.y, 132f, ButtonHeight), "Compact", buttonStyle))
            {
                showDetails = false;
            }

            GUILayout.BeginArea(new Rect(content.x, content.y + 48f, content.width, content.height - 48f));
            detailsScroll = GUILayout.BeginScrollView(detailsScroll);
            GUILayout.Label(FormatWorldSummary(), labelStyle, GUILayout.Height(FieldHeight));
            GUILayout.Space(8f);

            GUILayout.Label("Floor", titleStyle, GUILayout.Height(FieldHeight));
            DrawTextField("width m", ref floorWidthText);
            DrawTextField("depth m", ref floorDepthText);
            if (GUILayout.Button("Apply Floor Size", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                ApplyFloorSize();
            }

            if (GUILayout.Button($"View: {sceneBuilder.NextCameraViewLabel}", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                sceneBuilder.ToggleCameraView();
            }

            GUILayout.Space(10f);
            GUILayout.Label("Map", titleStyle, GUILayout.Height(FieldHeight));
            DrawTextField("map name", ref mapNameText);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Map", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                SaveMap();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(mapStatus, labelStyle, GUILayout.Height(FieldHeight));
            DrawSavedMapList();

            GUILayout.Space(10f);
            GUILayout.Label("Selected Wall", titleStyle, GUILayout.Height(FieldHeight));
            DrawSelectedWallAngleControls();

            GUILayout.Space(10f);
            GUILayout.Label("Start / Goal", titleStyle, GUILayout.Height(FieldHeight));
            DrawTextField($"start x 0..{sceneBuilder.FloorSize.x:0.#}", ref startXText);
            DrawTextField($"start z 0..{sceneBuilder.FloorSize.y:0.#}", ref startZText);
            DrawTextField($"goal x 0..{sceneBuilder.FloorSize.x:0.#}", ref goalXText);
            DrawTextField($"goal z 0..{sceneBuilder.FloorSize.y:0.#}", ref goalZText);
            if (GUILayout.Button("Apply Start / Goal", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                ApplyStartGoal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTextField(string label, ref string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(LabelWidth), GUILayout.Height(FieldHeight));
            GUI.SetNextControlName(TextFieldFocusPrefix + label);
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Height(FieldHeight));
            NavigationInputBlocker.RegisterTextInputFocus(TextFieldFocusPrefix);
            GUILayout.EndHorizontal();
        }

        private void DrawSavedMapList()
        {
            List<MapRecordDto> maps = LoadMapHistory().maps;
            if (maps.Count == 0)
            {
                GUILayout.Label("Saved maps: none", labelStyle, GUILayout.Height(FieldHeight));
                return;
            }

            GUILayout.Label($"Saved maps: {maps.Count}", labelStyle, GUILayout.Height(FieldHeight));
            for (int i = 0; i < maps.Count; i++)
            {
                MapRecordDto map = maps[i];
                if (map == null || string.IsNullOrWhiteSpace(map.path))
                {
                    continue;
                }

                GUILayout.Label(FormatMapRecord(map), labelStyle, GUILayout.Height(FieldHeight));
                GUILayout.BeginHorizontal();
                bool exists = TryResolveSafeMapPath(map.path, out string safePath) && File.Exists(safePath);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && exists;
                if (GUILayout.Button("Load", buttonStyle, GUILayout.Height(ButtonHeight)))
                {
                    LoadMapFromPath(map.path, string.IsNullOrWhiteSpace(map.display_name) ? Path.GetFileName(map.path) : map.display_name);
                }

                GUI.enabled = previousEnabled && exists;
                string deleteLabel = string.Equals(pendingDeleteMapPath, map.path, StringComparison.Ordinal)
                    ? "Confirm"
                    : "Delete";
                if (GUILayout.Button(deleteLabel, buttonStyle, GUILayout.Height(ButtonHeight)))
                {
                    DeleteSavedMap(map.path);
                }

                GUI.enabled = previousEnabled;
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }
        }

        private void DrawSelectedWallAngleControls()
        {
            bool hasWall = sceneBuilder.TryGetUserWall(selectedWallIndex, out NavigationScenarioWallSpec wallSpec);
            if (!hasWall)
            {
                GUILayout.Label("No wall selected", labelStyle, GUILayout.Height(FieldHeight));
                return;
            }

            GUILayout.Label($"Angle: {NormalizeAngle(wallSpec.RotationYDegrees):0.#} deg", labelStyle, GUILayout.Height(FieldHeight));
            float nextAngle = GUILayout.HorizontalSlider(NormalizeAngle(wallSpec.RotationYDegrees), -180f, 180f, GUILayout.Height(FieldHeight));
            if (Mathf.Abs(Mathf.DeltaAngle(wallSpec.RotationYDegrees, nextAngle)) >= 0.25f)
            {
                SetSelectedWallAngle(nextAngle);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-15", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                RotateSelectedWallBy(-RotationStepDegrees);
            }

            if (GUILayout.Button("+15", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                RotateSelectedWallBy(RotationStepDegrees);
            }

            if (GUILayout.Button("Apply", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                ApplySelectedWallAngleText();
            }

            GUILayout.EndHorizontal();
            DrawTextField("angle deg", ref wallAngleText);
        }

        private string FormatWorldSummary()
        {
            Vector2 size = sceneBuilder.FloorSize;
            return $"World {size.x:0.#} x {size.y:0.#}m · boundary on · walls {sceneBuilder.UserWallCount}";
        }

        private void SyncTextFromScene()
        {
            if (sceneBuilder == null)
            {
                return;
            }

            Vector2 size = sceneBuilder.FloorSize;
            floorWidthText = size.x.ToString("0.###", CultureInfo.InvariantCulture);
            floorDepthText = size.y.ToString("0.###", CultureInfo.InvariantCulture);
            SyncStartGoalText();
            if (!sceneBuilder.TryGetUserWall(selectedWallIndex, out _))
            {
                wallXText = (size.x * 0.5f).ToString("0.###", CultureInfo.InvariantCulture);
                wallZText = (size.y * 0.5f).ToString("0.###", CultureInfo.InvariantCulture);
                wallAngleText = "0";
            }
            else
            {
                SyncSelectedWallAngleText();
            }
        }

        private void ApplyFloorSize()
        {
            if (TryParse(floorWidthText, out float width) &&
                TryParse(floorDepthText, out float depth))
            {
                sceneBuilder.SetFloorSize(new Vector2(width, depth));
                SyncTextFromScene();
                SyncSelectedWallCenterText();
                ApplyStartGoal();
                mapStatus = "Map: unsaved changes";
            }
        }

        private void ApplyStartGoal()
        {
            if (TryParse(startXText, out float startX) &&
                TryParse(startZText, out float startZ))
            {
                sceneBuilder.SetAgentStartPosition(UiToWorldPoint(new Vector2(startX, startZ)));
            }

            if (TryParse(goalXText, out float goalX) &&
                TryParse(goalZText, out float goalZ))
            {
                sceneBuilder.SetGoalStartPosition(UiToWorldPoint(new Vector2(goalX, goalZ)));
            }

            SyncStartGoalText();
            mapStatus = "Map: unsaved changes";
        }

        private void AddWallFromFields()
        {
            if (!TryParse(wallXText, out float x) ||
                !TryParse(wallZText, out float z))
            {
                return;
            }

            selectedWallIndex = sceneBuilder.AddDefaultUserWall(UiToWorldWallCenter(new Vector2(x, z)));
            SyncSelectedWallCenterText();
            SyncSelectedWallAngleText();
            dragMode = WallDragMode.None;
            mapStatus = "Map: unsaved changes";
        }

        private void RemoveLastWall()
        {
            sceneBuilder.RemoveLastUserWall();
            selectedWallIndex = Mathf.Min(selectedWallIndex, sceneBuilder.UserWallCount - 1);
            SyncSelectedWallAngleText();
            dragMode = WallDragMode.None;
            mapStatus = "Map: unsaved changes";
        }

        private void DeleteSelectedWall()
        {
            if (!sceneBuilder.RemoveUserWall(selectedWallIndex))
            {
                return;
            }

            selectedWallIndex = -1;
            SyncSelectedWallAngleText();
            dragMode = WallDragMode.None;
            mapStatus = "Map: unsaved changes";
        }

        private void RotateSelectedWallBy(float deltaDegrees)
        {
            if (!sceneBuilder.TryGetUserWall(selectedWallIndex, out NavigationScenarioWallSpec wallSpec))
            {
                return;
            }

            Vector2 center = new(wallSpec.Center.x, wallSpec.Center.z);
            ApplyWallEdit(sceneBuilder.UpdateUserWall(
                selectedWallIndex,
                center,
                wallSpec.Size.x,
                NormalizeAngle(wallSpec.RotationYDegrees + deltaDegrees)));
            SyncSelectedWallAngleText();
        }

        private void HandleWorldMouseEditing()
        {
            IsPointerEditingWorld = dragMode != WallDragMode.None;
            if (sceneBuilder == null || IsReplayPanelExpanded())
            {
                IsPointerEditingWorld = false;
                return;
            }

            Mouse mouse = Mouse.current;
            Camera camera = Camera.main;
            if (mouse == null || camera == null)
            {
                IsPointerEditingWorld = false;
                return;
            }

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector2 guiPosition = new(screenPosition.x, Screen.height - screenPosition.y);
            if (IsPointerOverAnyUiPanel(guiPosition))
            {
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    dragMode = WallDragMode.None;
                    IsPointerEditingWorld = false;
                }

                return;
            }

            Ray pointerRay = camera.ScreenPointToRay(screenPosition);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (TryFindWallHandleHit(screenPosition, camera, out int wallIndex, out WallDragMode nextDragMode, out Vector2 fixedEndpoint))
                {
                    selectedWallIndex = wallIndex;
                    SyncSelectedWallAngleText();
                    dragMode = nextDragMode;
                    dragOffset = Vector2.zero;
                    fixedResizeEndpoint = fixedEndpoint;
                }
                else if ((TryFindWallBodyHit(screenPosition, camera, out wallIndex) || sceneBuilder.TryRaycastUserWall(pointerRay, out wallIndex)) &&
                    sceneBuilder.TryGetUserWall(wallIndex, out NavigationScenarioWallSpec moveWallSpec) &&
                    TryGetGroundPoint(camera, screenPosition, out Vector3 moveStartPoint))
                {
                    selectedWallIndex = wallIndex;
                    SyncSelectedWallAngleText();
                    dragMode = WallDragMode.Move;
                    Vector2 center = new(moveWallSpec.Center.x, moveWallSpec.Center.z);
                    Vector2 groundPoint = new(moveStartPoint.x, moveStartPoint.z);
                    dragOffset = center - groundPoint;
                }
                else
                {
                    if (TryGetGroundPoint(camera, screenPosition, out _))
                    {
                        selectedWallIndex = -1;
                        SyncSelectedWallAngleText();
                    }

                    dragMode = WallDragMode.None;
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                dragMode = WallDragMode.None;
            }

            if (dragMode == WallDragMode.None ||
                !sceneBuilder.TryGetUserWall(selectedWallIndex, out NavigationScenarioWallSpec wallSpec) ||
                !TryGetGroundPoint(camera, screenPosition, out Vector3 dragPoint3))
            {
                IsPointerEditingWorld = false;
                return;
            }

            IsPointerEditingWorld = true;
            Vector2 dragPoint = new(dragPoint3.x, dragPoint3.z);
            if (dragMode == WallDragMode.Move && mouse.leftButton.isPressed)
            {
                Vector2 nextCenter = dragPoint + dragOffset;
                ApplyWallEdit(sceneBuilder.UpdateUserWall(selectedWallIndex, nextCenter, wallSpec.Size.x, wallSpec.RotationYDegrees));
                return;
            }

            if ((dragMode != WallDragMode.ResizeNegative && dragMode != WallDragMode.ResizePositive) ||
                !mouse.leftButton.isPressed)
            {
                return;
            }

            Vector2 movingEndpoint = dragPoint;
            Vector2 axis = GetWallAxis(wallSpec.RotationYDegrees);
            float signedLength = Vector2.Dot(movingEndpoint - fixedResizeEndpoint, axis);
            if (Mathf.Abs(signedLength) < 0.25f)
            {
                signedLength = signedLength < 0f ? -0.25f : 0.25f;
            }

            Vector2 centerAfterResize = fixedResizeEndpoint + axis * (signedLength * 0.5f);
            ApplyWallEdit(sceneBuilder.UpdateUserWall(selectedWallIndex, centerAfterResize, Mathf.Abs(signedLength), wallSpec.RotationYDegrees));
        }

        private void ApplyWallEdit(bool updated)
        {
            if (!updated)
            {
                ShowNotification("Wall blocked by robot, goal, or another wall");
                return;
            }

            SyncSelectedWallCenterText();
            SyncSelectedWallAngleText();
            mapStatus = "Map: unsaved changes";
        }

        private void ShowNotification(string message)
        {
            mapStatus = message;
            notificationMessage = message;
            notificationUntil = Time.unscaledTime + NotificationDurationSeconds;
        }

        private void DrawNotificationOverlay()
        {
            if (string.IsNullOrWhiteSpace(notificationMessage) || Time.unscaledTime > notificationUntil)
            {
                return;
            }

            float width = Mathf.Min(620f, Screen.width - Padding * 2f);
            Rect rect = new((Screen.width - width) * 0.5f, Padding, width, 48f);
            GUI.Box(rect, GUIContent.none, boxStyle);
            GUI.Label(new Rect(rect.x + Padding, rect.y, rect.width - Padding * 2f, rect.height), notificationMessage, notificationStyle);
        }

        private bool IsPointerOverAnyUiPanel(Vector2 guiPosition)
        {
            return NavigationInputBlocker.IsPointerOverPanel(guiPosition) ||
                NavigationInputBlocker.ShouldBlockWorldKeyboardInput;
        }

        private bool TryFindWallHandleHit(Vector2 screenPosition, Camera camera, out int wallIndex, out WallDragMode nextDragMode, out Vector2 fixedEndpoint)
        {
            wallIndex = -1;
            nextDragMode = WallDragMode.None;
            fixedEndpoint = Vector2.zero;

            for (int i = sceneBuilder.UserWallCount - 1; i >= 0; i--)
            {
                if (!sceneBuilder.TryGetUserWall(i, out NavigationScenarioWallSpec wallSpec))
                {
                    continue;
                }

                Vector2 center = new(wallSpec.Center.x, wallSpec.Center.z);
                Vector2 axis = GetWallAxis(wallSpec.RotationYDegrees);
                Vector2 negativeEndpoint = center - axis * (wallSpec.Size.x * 0.5f);
                Vector2 positiveEndpoint = center + axis * (wallSpec.Size.x * 0.5f);
                Vector2 negativeHandle = GetResizeHandlePoint(center, axis, wallSpec.Size.x, true);
                Vector2 positiveHandle = GetResizeHandlePoint(center, axis, wallSpec.Size.x, false);
                if (IsNearScreenPoint(screenPosition, camera, positiveHandle))
                {
                    wallIndex = i;
                    nextDragMode = WallDragMode.ResizePositive;
                    fixedEndpoint = negativeEndpoint;
                    return true;
                }

                if (IsNearScreenPoint(screenPosition, camera, negativeHandle))
                {
                    wallIndex = i;
                    nextDragMode = WallDragMode.ResizeNegative;
                    fixedEndpoint = positiveEndpoint;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindWallBodyHit(Vector2 screenPosition, Camera camera, out int wallIndex)
        {
            wallIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = sceneBuilder.UserWallCount - 1; i >= 0; i--)
            {
                if (!sceneBuilder.TryGetUserWall(i, out NavigationScenarioWallSpec wallSpec))
                {
                    continue;
                }

                Vector2 center = new(wallSpec.Center.x, wallSpec.Center.z);
                Vector2 axis = GetWallAxis(wallSpec.RotationYDegrees);
                Vector2 negativeEndpoint = center - axis * (wallSpec.Size.x * 0.5f);
                Vector2 positiveEndpoint = center + axis * (wallSpec.Size.x * 0.5f);
                if (!TryGetScreenPoint(camera, negativeEndpoint, out Vector2 screenA) ||
                    !TryGetScreenPoint(camera, positiveEndpoint, out Vector2 screenB))
                {
                    continue;
                }

                float distance = DistanceToScreenSegment(screenPosition, screenA, screenB);
                if (distance > BodyPickRadius || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                wallIndex = i;
            }

            return wallIndex >= 0;
        }

        private static bool IsNearScreenPoint(Vector2 screenPosition, Camera camera, Vector2 worldPoint)
        {
            if (!TryGetScreenPoint(camera, worldPoint, out Vector2 handleScreenPosition))
            {
                return false;
            }

            return (screenPosition - handleScreenPosition).sqrMagnitude <= HandlePickRadius * HandlePickRadius;
        }

        private static bool TryGetScreenPoint(Camera camera, Vector2 worldPoint, out Vector2 screenPoint)
        {
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(worldPoint.x, 0.25f, worldPoint.y));
            if (screen.z <= 0f)
            {
                screenPoint = Vector2.zero;
                return false;
            }

            screenPoint = new Vector2(screen.x, screen.y);
            return true;
        }

        private static float DistanceToScreenSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float segmentLength = segment.sqrMagnitude;
            if (segmentLength <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segmentLength);
            return Vector2.Distance(point, a + segment * t);
        }

        private void SyncSelectedWallCenterText()
        {
            if (!sceneBuilder.TryGetUserWall(selectedWallIndex, out NavigationScenarioWallSpec wallSpec))
            {
                return;
            }

            Vector2 uiCenter = WorldToUiWallCenter(new Vector2(wallSpec.Center.x, wallSpec.Center.z));
            wallXText = uiCenter.x.ToString("0.###", CultureInfo.InvariantCulture);
            wallZText = uiCenter.y.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void SyncSelectedWallAngleText()
        {
            if (!sceneBuilder.TryGetUserWall(selectedWallIndex, out NavigationScenarioWallSpec wallSpec))
            {
                wallAngleText = "0";
                return;
            }

            wallAngleText = NormalizeAngle(wallSpec.RotationYDegrees).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void ApplySelectedWallAngleText()
        {
            if (TryParse(wallAngleText, out float angle))
            {
                SetSelectedWallAngle(angle);
            }
        }

        private void SetSelectedWallAngle(float angle)
        {
            if (!sceneBuilder.TryGetUserWall(selectedWallIndex, out NavigationScenarioWallSpec wallSpec))
            {
                return;
            }

            Vector2 center = new(wallSpec.Center.x, wallSpec.Center.z);
            ApplyWallEdit(sceneBuilder.UpdateUserWall(selectedWallIndex, center, wallSpec.Size.x, NormalizeAngle(angle)));
        }

        private Vector2 UiToWorldWallCenter(Vector2 uiCenter)
        {
            return UiToWorldPoint(uiCenter);
        }

        private Vector2 UiToWorldPoint(Vector2 uiPoint)
        {
            Vector2 size = sceneBuilder.FloorSize;
            return new Vector2(uiPoint.x - size.x * 0.5f, uiPoint.y - size.y * 0.5f);
        }

        private Vector2 WorldToUiWallCenter(Vector2 worldCenter)
        {
            Vector2 size = sceneBuilder.FloorSize;
            return new Vector2(worldCenter.x + size.x * 0.5f, worldCenter.y + size.y * 0.5f);
        }

        private void SyncStartGoalText()
        {
            Vector2 start = WorldToUiWallCenter(new Vector2(sceneBuilder.AgentStartPosition.x, sceneBuilder.AgentStartPosition.z));
            Vector2 goal = WorldToUiWallCenter(new Vector2(sceneBuilder.GoalStartPosition.x, sceneBuilder.GoalStartPosition.z));
            startXText = start.x.ToString("0.###", CultureInfo.InvariantCulture);
            startZText = start.y.ToString("0.###", CultureInfo.InvariantCulture);
            goalXText = goal.x.ToString("0.###", CultureInfo.InvariantCulture);
            goalZText = goal.y.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool TryGetGroundPoint(Camera camera, Vector2 screenPosition, out Vector3 point)
        {
            Ray ray = camera.ScreenPointToRay(screenPosition);
            Plane ground = new(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance))
            {
                point = Vector3.zero;
                return false;
            }

            point = ray.GetPoint(distance);
            return true;
        }

        private void DrawSelectedWallHandles()
        {
            if (!sceneBuilder.TryGetUserWall(selectedWallIndex, out NavigationScenarioWallSpec wallSpec) || Camera.main == null)
            {
                return;
            }

            Vector2 center = new(wallSpec.Center.x, wallSpec.Center.z);
            Vector2 axis = GetWallAxis(wallSpec.RotationYDegrees);
            DrawWallOutline(center, axis, wallSpec.Size.x, wallSpec.Size.z);
            DrawHandle(GetResizeHandlePoint(center, axis, wallSpec.Size.x, true), selectedButtonStyle);
            DrawHandle(GetResizeHandlePoint(center, axis, wallSpec.Size.x, false), selectedButtonStyle);
        }

        private static Vector2 GetWallAxis(float rotationYDegrees)
        {
            float radians = -rotationYDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float NormalizeAngle(float rotationYDegrees)
        {
            return Mathf.DeltaAngle(0f, rotationYDegrees);
        }

        private static Vector2 GetResizeHandlePoint(Vector2 center, Vector2 axis, float wallLength, bool negativeSide)
        {
            float halfLength = Mathf.Max(0.125f, wallLength * 0.5f);
            float inset = Mathf.Min(HandleInsetMeters, Mathf.Max(0f, halfLength - 0.125f));
            float direction = negativeSide ? -1f : 1f;
            return center + axis * (direction * Mathf.Max(0f, halfLength - inset));
        }

        private static void DrawHandle(Vector2 worldPoint, GUIStyle style)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(new Vector3(worldPoint.x, 0.25f, worldPoint.y));
            if (screen.z <= 0f)
            {
                return;
            }

            Rect rect = new(screen.x - HandleSize * 0.5f, Screen.height - screen.y - HandleSize * 0.5f, HandleSize, HandleSize);
            GUI.Box(rect, GUIContent.none, style);
        }

        private void DrawWallOutline(Vector2 center, Vector2 axis, float wallLength, float wallThickness)
        {
            Vector2 normal = new(-axis.y, axis.x);
            float halfLength = wallLength * 0.5f;
            float halfThickness = wallThickness * 0.5f;
            Vector2 a = center - axis * halfLength - normal * halfThickness;
            Vector2 b = center + axis * halfLength - normal * halfThickness;
            Vector2 c = center + axis * halfLength + normal * halfThickness;
            Vector2 d = center - axis * halfLength + normal * halfThickness;
            DrawGuideLine(a, b);
            DrawGuideLine(b, c);
            DrawGuideLine(c, d);
            DrawGuideLine(d, a);
        }

        private void DrawGuideLine(Vector2 start, Vector2 end)
        {
            if (!TryGetScreenPoint(Camera.main, start, out Vector2 screenStart) ||
                !TryGetScreenPoint(Camera.main, end, out Vector2 screenEnd))
            {
                return;
            }

            Vector2 guiStart = new(screenStart.x, Screen.height - screenStart.y);
            Vector2 guiEnd = new(screenEnd.x, Screen.height - screenEnd.y);
            Vector2 delta = guiEnd - guiStart;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, guiStart);
            GUI.DrawTexture(new Rect(guiStart.x, guiStart.y - OutlineThicknessPixels * 0.5f, length, OutlineThicknessPixels), wallGuideTexture);
            GUI.matrix = previousMatrix;
        }

        private void SaveMap()
        {
            try
            {
                string directory = GetMapDirectory();
                Directory.CreateDirectory(directory);
                string json = sceneBuilder.BuildScenarioBundleJson(SavedMapScenarioId);
                File.WriteAllText(GetMapPath(), json);

                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string historyPath = Path.Combine(directory, $"map-{timestamp}.json");
                File.WriteAllText(historyPath, json);
                UpsertMapHistory(historyPath, GetCurrentMapDisplayName(timestamp));
                pendingDeleteMapPath = null;
                mapStatus = $"Map: saved {GetCurrentMapDisplayName(timestamp)}";
            }
            catch (IOException exception)
            {
                mapStatus = "Map: save failed";
                Debug.LogError($"Map save failed: {exception.Message}");
            }
            catch (System.UnauthorizedAccessException exception)
            {
                mapStatus = "Map: save failed";
                Debug.LogError($"Map save failed: {exception.Message}");
            }
        }

        private void LoadMapFromPath(string path, string displayName)
        {
            if (!TryResolveSafeMapPath(path, out string safePath))
            {
                RemoveMapHistory(path);
                mapStatus = "Map: removed unsafe history entry";
                return;
            }

            if (!File.Exists(safePath))
            {
                mapStatus = "Map: no saved map";
                return;
            }

            try
            {
                ScenarioBundle scenario = ScenarioBundleJson.Deserialize(File.ReadAllText(safePath));
                sceneBuilder.ApplyScenarioBundle(scenario);
                sceneBuilder.RecordScenarioSource($"Map: loaded {displayName}");
                selectedWallIndex = sceneBuilder.UserWallCount > 0 ? sceneBuilder.UserWallCount - 1 : -1;
                dragMode = WallDragMode.None;
                SyncTextFromScene();
                SyncSelectedWallCenterText();
                mapNameText = Path.GetFileNameWithoutExtension(displayName);
                mapStatus = $"Map: loaded {displayName}";
            }
            catch (IOException exception)
            {
                mapStatus = "Map: load failed";
                Debug.LogError($"Map load failed: {exception.Message}");
            }
            catch (System.ArgumentException exception)
            {
                mapStatus = "Map: load failed";
                Debug.LogError($"Map load failed: {exception.Message}");
            }
        }

        private void DeleteSavedMap(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!TryResolveSafeMapPath(path, out string safePath))
            {
                RemoveMapHistory(path);
                pendingDeleteMapPath = null;
                mapStatus = "Map: removed unsafe history entry";
                return;
            }

            if (!string.Equals(pendingDeleteMapPath, path, StringComparison.Ordinal))
            {
                pendingDeleteMapPath = path;
                mapStatus = $"Map: confirm delete {Path.GetFileName(safePath)}";
                return;
            }

            try
            {
                if (File.Exists(safePath))
                {
                    File.Delete(safePath);
                }

                RemoveMapHistory(path);
                pendingDeleteMapPath = null;
                mapStatus = $"Map: deleted {Path.GetFileName(safePath)}";
            }
            catch (IOException exception)
            {
                mapStatus = "Map: delete failed";
                Debug.LogError($"Map delete failed: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                mapStatus = "Map: delete failed";
                Debug.LogError($"Map delete failed: {exception.Message}");
            }
        }

        private static string FormatMapRecord(MapRecordDto map)
        {
            string name = string.IsNullOrWhiteSpace(map.display_name)
                ? Path.GetFileName(map.path)
                : map.display_name;
            string saved = string.IsNullOrWhiteSpace(map.saved_at_local) ? "unknown time" : map.saved_at_local;
            string missing = TryResolveSafeMapPath(map.path, out string safePath) && File.Exists(safePath) ? string.Empty : " · missing";
            return $"{name} · {saved}{missing}";
        }

        private string GetCurrentMapDisplayName(string fallback)
        {
            return string.IsNullOrWhiteSpace(mapNameText) ? $"Map {fallback}" : mapNameText.Trim();
        }

        private static void UpsertMapHistory(string path, string displayName)
        {
            if (!TryResolveSafeMapPath(path, out string safePath))
            {
                return;
            }

            MapHistoryDocument document = LoadMapHistory();
            document.maps.RemoveAll(map => string.Equals(map?.path, safePath, StringComparison.Ordinal));
            document.maps.Insert(0, new MapRecordDto
            {
                path = safePath,
                display_name = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(safePath) : displayName.Trim(),
                saved_at_local = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            });
            SaveMapHistory(document);
        }

        private static void RemoveMapHistory(string path)
        {
            MapHistoryDocument document = LoadMapHistory();
            document.maps.RemoveAll(map => string.Equals(map?.path, path, StringComparison.Ordinal));
            SaveMapHistory(document);
        }

        private static MapHistoryDocument LoadMapHistory()
        {
            string path = GetMapHistoryPath();
            if (!File.Exists(path))
            {
                return new MapHistoryDocument();
            }

            try
            {
                MapHistoryDocument document = JsonUtility.FromJson<MapHistoryDocument>(File.ReadAllText(path)) ?? new MapHistoryDocument();
                if (document.maps == null)
                {
                    document.maps = new List<MapRecordDto>();
                }

                document.maps = document.maps
                    .Where(map => map != null && TryResolveSafeMapPath(map.path, out _))
                    .ToList();
                return document;
            }
            catch (ArgumentException)
            {
                return new MapHistoryDocument();
            }
            catch (IOException)
            {
                return new MapHistoryDocument();
            }
            catch (UnauthorizedAccessException)
            {
                return new MapHistoryDocument();
            }
        }

        private static void SaveMapHistory(MapHistoryDocument document)
        {
            Directory.CreateDirectory(GetMapDirectory());
            File.WriteAllText(GetMapHistoryPath(), JsonUtility.ToJson(document ?? new MapHistoryDocument(), prettyPrint: true));
        }

        private static string GetMapDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "EnvForge", MapDirectoryName);
        }

        private static string GetMapPath()
        {
            return Path.Combine(GetMapDirectory(), MapFileName);
        }

        private static string GetMapHistoryPath()
        {
            return Path.Combine(GetMapDirectory(), MapHistoryFileName);
        }

        private static bool TryResolveSafeMapPath(string path, out string safePath)
        {
            safePath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string root = Path.GetFullPath(GetMapDirectory())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(path);
                bool isUnderRoot = fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                if (!isUnderRoot || !string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                safePath = fullPath;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class MapHistoryDocument
        {
            public List<MapRecordDto> maps = new();
        }

        [Serializable]
        private sealed class MapRecordDto
        {
            public string path;
            public string display_name;
            public string saved_at_local;
        }

        private static bool TryParse(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void EnsureStyles()
        {
            if (buttonStyle != null)
            {
                return;
            }

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = FontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            Texture2D buttonBackground = CreateTexture(new Color(0.08f, 0.1f, 0.12f, 0.92f));
            Texture2D buttonHoverBackground = CreateTexture(new Color(0.14f, 0.17f, 0.2f, 0.96f));
            buttonStyle.normal.background = buttonBackground;
            buttonStyle.hover.background = buttonHoverBackground;
            buttonStyle.active.background = buttonHoverBackground;
            buttonStyle.focused.background = buttonHoverBackground;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.focused.textColor = Color.white;
            buttonStyle.clipping = TextClipping.Clip;

            compactButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 18,
                clipping = TextClipping.Clip,
            };

            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = CreateTexture(new Color(1f, 0.72f, 0.12f, 1f));
            selectedButtonStyle.normal.textColor = Color.black;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            FreezeReadOnlyStyle(labelStyle);

            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = TitleFontSize,
                fontStyle = FontStyle.Bold,
            };

            notificationStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = FontSize,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white },
                focused = { textColor = Color.white },
            };

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = CreateTexture(new Color(0f, 0f, 0f, 0.9f));
            wallGuideTexture = CreateTexture(new Color(1f, 0.72f, 0.12f, 0.85f));
        }

        private static void FreezeReadOnlyStyle(GUIStyle style)
        {
            style.hover.textColor = style.normal.textColor;
            style.active.textColor = style.normal.textColor;
            style.focused.textColor = style.normal.textColor;
            style.hover.background = style.normal.background;
            style.active.background = style.normal.background;
            style.focused.background = style.normal.background;
        }

        private void UpdatePointerOverPanel()
        {
            NavigationInputBlocker.RegisterPanel(nameof(NavigationWorldEditorPanel), lastPanelRect);
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
