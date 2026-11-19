using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class SceneRectSelection {
        public static bool Handle(int hash, ref Vector2 start, ref Vector2 end, out Rect rect) {
            rect = default;
            Event e = Event.current;
            int id = GUIUtility.GetControlID(hash, FocusType.Passive);

            switch (e.GetTypeForControl(id)) {
                case EventType.Layout:
                    if (e.shift && !e.alt)
                        HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseDown:
                    if (e.button == 0 && e.shift && HandleUtility.nearestControl == id) {
                        if (ToggleOtherObject(e.mousePosition)) {
                            e.Use();
                            break;
                        }

                        GUIUtility.hotControl = id;
                        start = end = e.mousePosition;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) {
                        end = e.mousePosition;
                        HandleUtility.Repaint();
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) {
                        GUIUtility.hotControl = 0;
                        rect = MinMax(start, end);
                        e.Use();
                        return true;
                    }
                    break;
                case EventType.Repaint:
                    if (GUIUtility.hotControl == id) {
                        Handles.BeginGUI();
                        EditorStyles.selectionRect.Draw(MinMax(start, end), GUIContent.none, false, false, false, false);
                        Handles.EndGUI();
                    }
                    break;
            }

            return false;
        }

        private static bool ToggleOtherObject(Vector2 mousePosition) {
            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            Transform owner = Selection.activeTransform;
            if (!picked || owner && picked.transform.IsChildOf(owner))
                return false;

            List<Object> selection = new(Selection.objects);
            if (!selection.Remove(picked))
                selection.Add(picked);
            Selection.objects = selection.ToArray();
            return true;
        }

        private static Rect MinMax(Vector2 a, Vector2 b) => Rect.MinMaxRect(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
