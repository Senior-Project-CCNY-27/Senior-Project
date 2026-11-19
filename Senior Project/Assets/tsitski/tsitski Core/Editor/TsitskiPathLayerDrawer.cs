using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    [CustomPropertyDrawer(typeof(TsitskiPathLayer))]
    public class TsitskiPathLayerDrawer : PropertyDrawer {
        private const float WarningHeight = 38f;
        private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private TsitskiPathLayersAttribute Options => fieldInfo?.GetCustomAttribute<TsitskiPathLayersAttribute>();

        private bool Rejected(SerializedProperty property) {
            UnityEngine.Object stats = property.FindPropertyRelative(nameof(TsitskiPathLayer.stats)).objectReferenceValue;
            Type[] accepts = Options?.accepts;
            return stats && accepts is { Length: > 0 } && !accepts.Any(type => type.IsInstanceOfType(stats));
        }

        private IEnumerable<string> Rows(SerializedProperty property) {
            bool walls = Options?.wallSides ?? false;
            bool array = property.FindPropertyRelative(nameof(TsitskiPathLayer.stats)).objectReferenceValue is TsitskiArrayStats;

            yield return nameof(TsitskiPathLayer.name);
            yield return nameof(TsitskiPathLayer.stats);
            if (walls)
                yield return nameof(TsitskiPathLayer.wallSide);
            else if (!array)
                yield return nameof(TsitskiPathLayer.polesFrom);
            yield return nameof(TsitskiPathLayer.height);
            yield return nameof(TsitskiPathLayer.heightFromTop);
            if (array)
                yield return nameof(TsitskiPathLayer.matchHeight);
            if (!walls)
                yield return nameof(TsitskiPathLayer.sideOffset);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            return Line * (1 + Rows(property).Count()) + (Rejected(property) ? WarningHeight : 0f);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            string name = property.FindPropertyRelative(nameof(TsitskiPathLayer.name)).stringValue;
            Rect row = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, string.IsNullOrEmpty(name) ? label.text : name, true);
            if (!property.isExpanded)
                return;

            using (new EditorGUI.IndentLevelScope()) {
                foreach (string rowName in Rows(property)) {
                    row.y += Line;
                    SerializedProperty field = property.FindPropertyRelative(rowName);
                    if (rowName == nameof(TsitskiPathLayer.polesFrom))
                        DrawPolesFrom(row, property, field);
                    else
                        EditorGUI.PropertyField(row, field);
                }

                if (Rejected(property)) {
                    row.y += Line;
                    row.height = WarningHeight - EditorGUIUtility.standardVerticalSpacing;
                    EditorGUI.HelpBox(EditorGUI.IndentedRect(row), "This generator can't build these stats, so the layer is skipped.", MessageType.Warning);
                }
            }
        }

        // Pole alignment comes from another layer's array, so each layer keeps a single stats slot.
        private static void DrawPolesFrom(Rect row, SerializedProperty property, SerializedProperty field) {
            List<string> labels = new() { "None" };
            List<TsitskiArrayStats> choices = new() { null };

            string path = property.propertyPath;
            SerializedProperty list = property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf(".Array.data[", StringComparison.Ordinal)));
            for (int i = 0; list != null && i < list.arraySize; i++) {
                SerializedProperty layer = list.GetArrayElementAtIndex(i);
                if (layer.FindPropertyRelative(nameof(TsitskiPathLayer.stats)).objectReferenceValue is not TsitskiArrayStats array || choices.Contains(array))
                    continue;

                labels.Add(layer.FindPropertyRelative(nameof(TsitskiPathLayer.name)).stringValue);
                choices.Add(array);
            }

            TsitskiArrayStats current = (TsitskiArrayStats)field.objectReferenceValue;
            if (current && !choices.Contains(current)) {
                labels.Add($"{current.name} (no layer)");
                choices.Add(current);
            }

            GUIContent label = new(field.displayName, field.tooltip);
            int picked = EditorGUI.Popup(row, label, choices.IndexOf(current), labels.Select(text => new GUIContent(text)).ToArray());
            if (picked >= 0)
                field.objectReferenceValue = choices[picked];
        }
    }
}
