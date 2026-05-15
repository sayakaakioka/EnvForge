using UnityEngine;

namespace EnvForge.Mvp0
{
    public sealed class NavigationDebugOverlay : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float Width = 720f;
        private const float Height = 190f;
        private const int FontSize = 26;

        private NavigationMetrics navigationMetrics;
        private NavigationObservationProvider observationProvider;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        public void Configure(NavigationMetrics metrics, NavigationObservationProvider observations)
        {
            navigationMetrics = metrics;
            observationProvider = observations;
        }

        private void OnGUI()
        {
            EnsureStyles();

            Rect boxRect = new(Padding, Padding, Width, Height);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            GUILayout.BeginArea(new Rect(Padding * 2f, Padding * 1.5f, Width - Padding * 2f, Height - Padding));
            GUILayout.Label("MVP 0.1 Navigation Observation", labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(FormatMetrics(), labelStyle);
            GUILayout.Label(FormatObservation(), labelStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (labelStyle != null && boxStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = FontSize;
            labelStyle.normal.textColor = Color.white;

            Texture2D background = new(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            background.Apply();

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = background;
        }

        private string FormatMetrics()
        {
            return navigationMetrics != null ? navigationMetrics.FormatSummary() : "metrics unavailable";
        }

        private string FormatObservation()
        {
            return observationProvider != null ? observationProvider.FormatSummary() : "observation unavailable";
        }
    }
}
