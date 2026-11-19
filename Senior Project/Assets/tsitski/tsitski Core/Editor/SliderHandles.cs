using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class SliderHandles {
        private const float HandleScale = 0.08f;

        public static bool Width(Vector3 center, Vector3 across, ref float width, Color color, float min = 0.01f) {
            across = across.normalized;
            float half = width * 0.5f;
            bool changed = false;

            using (new Handles.DrawingScope(color)) {
                Handles.DrawDottedLine(center - across * half, center + across * half, 3f);
                foreach (float side in new[] { -1f, 1f }) {
                    Vector3 direction = across * side;
                    Vector3 position = center + direction * half;
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.Slider(position, direction, HandleUtility.GetHandleSize(position) * HandleScale, Handles.DotHandleCap, 0f);
                    if (!EditorGUI.EndChangeCheck())
                        continue;

                    width = Mathf.Max(min, Vector3.Dot(moved - center, direction) * 2f);
                    changed = true;
                }
            }

            return changed;
        }

        public static bool Length(Vector3 origin, Vector3 direction, ref float length, Color color, float min = 0.01f,
            Handles.CapFunction cap = null) {
            direction = direction.normalized;
            Vector3 tip = origin + direction * length;

            using (new Handles.DrawingScope(color)) {
                Handles.DrawDottedLine(origin, tip, 3f);
                float size = HandleUtility.GetHandleSize(tip) * HandleScale * (cap == null ? 1.5f : 1f);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider(tip, direction, size, cap ?? Handles.ConeHandleCap, 0f);
                if (!EditorGUI.EndChangeCheck())
                    return false;

                length = Mathf.Max(min, Vector3.Dot(moved - origin, direction));
                return true;
            }
        }

        public static bool SignedLength(Vector3 origin, Vector3 direction, ref float length, Color color, float min = 0.01f) {
            direction = direction.normalized;
            Vector3 pointing = length < 0f ? -direction : direction;
            Vector3 tip = origin + direction * length;

            using (new Handles.DrawingScope(color)) {
                Handles.DrawDottedLine(origin, tip, 3f);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider(tip, pointing, HandleUtility.GetHandleSize(tip) * HandleScale * 1.5f, Handles.ConeHandleCap, 0f);
                if (!EditorGUI.EndChangeCheck())
                    return false;

                float value = Vector3.Dot(moved - origin, direction);
                length = Mathf.Abs(value) >= min ? value : value < 0f ? -min : min;
                return true;
            }
        }

        public static bool Toggle(Vector3 position, Vector3 facing, bool value, Color on, Color off) {
            float size = HandleUtility.GetHandleSize(position) * HandleScale;
            Quaternion rotation = facing.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(facing) : Quaternion.identity;

            using (new Handles.DrawingScope(value ? on : off)) {
                if (Handles.Button(position, rotation, size, size * 1.5f, value ? Handles.CubeHandleCap : Handles.SphereHandleCap))
                    return !value;
            }

            return value;
        }
    }
}
