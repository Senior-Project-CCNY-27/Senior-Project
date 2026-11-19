using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class ArraySpacingPreview {
        private const float Height = 130f;
        private const int MaxPreviewSlots = 2000;
        private static readonly Color Background = new(0.16f, 0.16f, 0.16f, 1f);
        private static readonly Color PathColor = new(1f, 1f, 1f, 0.25f);
        private static readonly Color InteriorColor = new(0.9f, 0.9f, 0.9f, 1f);
        private static readonly Color DivisionColor = new(0.3f, 0.85f, 1f, 1f);
        private static readonly Color CornerColor = new(1f, 0.6f, 0.2f, 1f);
        private static readonly Color SkippedColor = new(1f, 1f, 1f, 0.15f);

        public static void Draw(ArraySpacing spacing) {
            float unit = spacing.divisionLength > 1e-4f ? spacing.divisionLength : Mathf.Max(TsitskiArray.MinSpacing, spacing.spacing) * 4f;
            float width = unit * 3f, depth = unit * 2f;
            List<Vector3> square = new() { Vector3.zero, new(width, 0f, 0f), new(width, 0f, depth), new(0f, 0f, depth) };
            List<ArraySpacing.Placement> slots = spacing.Layout(square, true, MaxPreviewSlots);
            int placed = slots.Count(s => !s.skipped);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField("Placement Preview", $"{placed} placed, {slots.Count - placed} skipped (sample path)", EditorStyles.miniLabel);
                Rect rect = GUILayoutUtility.GetRect(0f, Height, GUILayout.ExpandWidth(true));
                if (Event.current.type != EventType.Repaint)
                    return;

                EditorGUI.DrawRect(rect, Background);
                float scale = Mathf.Min((rect.width - 24f) / width, (rect.height - 24f) / depth);
                Vector2 center = rect.center;
                Vector2 Map(Vector3 p) => new(center.x + (p.x - width * 0.5f) * scale, center.y - (p.z - depth * 0.5f) * scale);

                Handles.color = PathColor;
                Handles.DrawAAPolyLine(2f, square.Append(square[0]).Select(p => (Vector3)Map(p)).ToArray());

                float perimeter = 2f * (width + depth);
                foreach (ArraySpacing.Placement slot in slots.OrderBy(s => !s.skipped)) {
                    Vector2 point = Map(Along(square, Mathf.Repeat(slot.distance, perimeter)));
                    float size = slot.skipped ? 4f : 6f;
                    Color color = slot.skipped ? SkippedColor : slot.kind switch {
                        PlacementKind.Corner => CornerColor,
                        PlacementKind.Division => DivisionColor,
                        _ => InteriorColor
                    };
                    EditorGUI.DrawRect(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), color);
                }
            }
        }

        private static Vector3 Along(List<Vector3> loop, float distance) {
            for (int i = 0; i < loop.Count; i++) {
                Vector3 a = loop[i], b = loop[(i + 1) % loop.Count];
                float length = Vector3.Distance(a, b);
                if (distance <= length)
                    return Vector3.Lerp(a, b, length > 0f ? distance / length : 0f);
                distance -= length;
            }
            return loop[0];
        }
    }

    [CustomEditor(typeof(TsitskiArrayStats))]
    public class TsitskiArrayStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            TsitskiInspector.DrawProperties(serializedObject);
            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiArrayStats stats = (TsitskiArrayStats)target;
            ArraySpacingPreview.Draw(new ArraySpacing {
                spacing = stats.spacing, offset = stats.offset, fitSpacing = stats.fitSpacing, splitAtCorners = stats.splitAtCorners,
                divisionLength = stats.divisionLength, skipCorners = stats.skipCorners, skipDivisions = stats.skipDivisions
            });
        }
    }
}
