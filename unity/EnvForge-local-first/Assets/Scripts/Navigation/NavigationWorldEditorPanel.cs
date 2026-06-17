using System.Globalization;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Replay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Navigation
{
    public sealed class NavigationWorldEditorPanel : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float CompactWidth = 520f;
        private const float CompactHeight = 112f;
        private const float DetailsWidth = 620f;
        private const float DetailsHeight = 560f;
        private const float ButtonHeight = 42f;
        private const float FieldHeight = 34f;
        private const float LabelWidth = 180f;
        private const int FontSize = 22;
        private const int TitleFontSize = 26;

        private NavigationSceneBuilder sceneBuilder;
        private bool showPanel = true;
        private bool showDetails;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle titleStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle boxStyle;
        private NavigationReplayPlayer replayPlayer;
        private Vector2 detailsScroll;

        private string floorWidthText;
        private string floorDepthText;
        private string wallXText = "0";
        private string wallZText = "0";
        private string wallLengthText = "3";
        private string wallThicknessText = "0.35";
        private string wallRotationText = "0";

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
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                showPanel = !showPanel;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && showDetails)
            {
                showDetails = false;
            }
        }

        private void OnGUI()
        {
            if (!showPanel || sceneBuilder == null || IsReplayPanelExpanded())
            {
                return;
            }

            EnsureStyles();
            if (showDetails)
            {
                DrawDetails();
                return;
            }

            DrawCompact();
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
            Rect rect = new(Padding, Padding + 214f, width, CompactHeight);
            GUI.Box(rect, GUIContent.none, boxStyle);

            Rect content = new(rect.x + Padding, rect.y + Padding, rect.width - Padding * 2f, rect.height - Padding * 2f);
            GUI.Label(new Rect(content.x, content.y, content.width, 32f), FormatWorldSummary(), labelStyle);

            float buttonTop = content.y + 42f;
            float buttonWidth = (content.width - Padding * 2f) / 3f;
            if (GUI.Button(new Rect(content.x, buttonTop, buttonWidth, ButtonHeight), "Edit", buttonStyle))
            {
                showDetails = true;
                SyncTextFromScene();
            }

            if (GUI.Button(new Rect(content.x + buttonWidth + Padding, buttonTop, buttonWidth, ButtonHeight), "Add Wall", buttonStyle))
            {
                AddWallFromFields();
            }

            if (GUI.Button(new Rect(content.x + (buttonWidth + Padding) * 2f, buttonTop, buttonWidth, ButtonHeight), "Undo", buttonStyle))
            {
                sceneBuilder.RemoveLastUserWall();
            }
        }

        private void DrawDetails()
        {
            float width = Mathf.Min(DetailsWidth, Screen.width - Padding * 2f);
            Rect rect = new(Padding, Padding + 214f, width, DetailsHeight);
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

            GUILayout.Space(10f);
            GUILayout.Label("Wall", titleStyle, GUILayout.Height(FieldHeight));
            DrawTextField("center x", ref wallXText);
            DrawTextField("center z", ref wallZText);
            DrawTextField("length m", ref wallLengthText);
            DrawTextField("thickness m", ref wallThicknessText);
            DrawTextField("rotation deg", ref wallRotationText);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Wall", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                AddWallFromFields();
            }

            if (GUILayout.Button("Undo", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                sceneBuilder.RemoveLastUserWall();
            }

            if (GUILayout.Button("Clear", buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                sceneBuilder.ClearUserWalls();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTextField(string label, ref string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(LabelWidth), GUILayout.Height(FieldHeight));
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Height(FieldHeight));
            GUILayout.EndHorizontal();
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
            wallThicknessText = sceneBuilder.WallThickness.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void ApplyFloorSize()
        {
            if (TryParse(floorWidthText, out float width) &&
                TryParse(floorDepthText, out float depth))
            {
                sceneBuilder.SetFloorSize(new Vector2(width, depth));
                SyncTextFromScene();
            }
        }

        private void AddWallFromFields()
        {
            if (!TryParse(wallXText, out float x) ||
                !TryParse(wallZText, out float z) ||
                !TryParse(wallLengthText, out float length) ||
                !TryParse(wallThicknessText, out float thickness) ||
                !TryParse(wallRotationText, out float rotation))
            {
                return;
            }

            sceneBuilder.AddUserWall(new Vector2(x, z), length, thickness, rotation);
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

            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = TitleFontSize,
                fontStyle = FontStyle.Bold,
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
