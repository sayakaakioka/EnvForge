using System.Collections.Generic;
using UnityEngine;

namespace EnvForge.Navigation
{
    public static class NavigationInputBlocker
    {
        private static readonly Dictionary<string, Rect> PanelRects = new();
        private static int textInputFocusFrame = -1000;
        private static string focusedTextInputControl = string.Empty;

        public static bool ShouldBlockWorldKeyboardInput =>
            Time.frameCount - textInputFocusFrame <= 1;

        public static string FocusedTextInputControl =>
            ShouldBlockWorldKeyboardInput ? focusedTextInputControl : string.Empty;

        public static void RegisterPanel(string key, Rect rect)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (rect.width <= 0f || rect.height <= 0f)
            {
                PanelRects.Remove(key);
                return;
            }

            PanelRects[key] = rect;
        }

        public static void UnregisterPanel(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                PanelRects.Remove(key);
            }
        }

        public static bool IsPointerOverPanel(Vector2 guiPosition)
        {
            foreach (Rect rect in PanelRects.Values)
            {
                if (rect.Contains(guiPosition))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool RegisterTextInputFocus(string controlPrefix)
        {
            string focusedControl = GUI.GetNameOfFocusedControl();
            bool focused = !string.IsNullOrEmpty(focusedControl) &&
                !string.IsNullOrEmpty(controlPrefix) &&
                focusedControl.StartsWith(controlPrefix, System.StringComparison.Ordinal);
            if (focused)
            {
                textInputFocusFrame = Time.frameCount;
                focusedTextInputControl = focusedControl;
            }

            return focused;
        }
    }
}
