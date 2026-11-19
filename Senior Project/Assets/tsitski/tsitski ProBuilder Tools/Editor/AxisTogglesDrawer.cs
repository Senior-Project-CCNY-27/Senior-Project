using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomPropertyDrawer(typeof(AxisToggles))]
    public class AxisTogglesDrawer : PropertyDrawer {
        private const float AxisLabelWidth = 14f;
        private static readonly string[] Axes = { "x", "y", "z" };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);

            int indent = EditorGUI.indentLevel;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = AxisLabelWidth;

            float width = position.width / 3f;

            for (int i = 0; i < Axes.Length; i++) {
                SerializedProperty axis = property.FindPropertyRelative(Axes[i]);
                Rect rect = new(position.x + width * i, position.y, width, position.height);
                GUIContent axisLabel = new(Axes[i].ToUpperInvariant());

                EditorGUI.BeginProperty(rect, axisLabel, axis);
                EditorGUI.showMixedValue = axis.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                bool value = EditorGUI.Toggle(rect, axisLabel, axis.boolValue);
                if (EditorGUI.EndChangeCheck())
                    axis.boolValue = value;
                EditorGUI.showMixedValue = false;
                EditorGUI.EndProperty();
            }

            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
