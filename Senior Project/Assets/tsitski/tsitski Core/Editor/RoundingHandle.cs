using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class RoundingHandle {
        private static Color HandleColor => HandleTheme.Secondary;
        private static Color LineColor => HandleTheme.Faded(HandleTheme.Secondary, 0.5f);

        public static bool Do(Vector3 point, Vector3 previous, Vector3 next, float radius, out float result) {
            result = radius;
            Vector3 toPrevious = previous - point;
            Vector3 toNext = next - point;
            Vector3 bisector = toPrevious.normalized + toNext.normalized;

            if (toPrevious.sqrMagnitude < 1e-10f || toNext.sqrMagnitude < 1e-10f || bisector.sqrMagnitude < 1e-6f) {
                Handles.Slider(point, Vector3.up, 0f, NoCap, 0f);
                return false;
            }

            bisector.Normalize();
            float offset = HandleUtility.GetHandleSize(point) * 0.35f;
            Vector3 position = point + bisector * (offset + radius);
            float size = HandleUtility.GetHandleSize(position) * 0.05f;

            Handles.color = LineColor;
            Handles.DrawDottedLine(point, position, 2f);
            Handles.color = HandleColor;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(position, bisector, size, Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
                return false;

            result = Mathf.Max(0f, Vector3.Dot(moved - point, bisector) - offset);
            return true;
        }

        public static void NoCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) {
        }
    }
}
