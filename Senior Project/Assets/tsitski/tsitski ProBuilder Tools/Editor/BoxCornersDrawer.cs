using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomPropertyDrawer(typeof(BoxCorners))]
    public class BoxCornersDrawer : PropertyDrawer {
        private const float CornerLabelWidth = 22f;
        private static readonly string[,] Corners = {
            { nameof(BoxCorners.topLeft), nameof(BoxCorners.topRight) },
            { nameof(BoxCorners.bottomLeft), nameof(BoxCorners.bottomRight) }
        };
        private static readonly string[,] Labels = { { "TL", "TR" }, { "BL", "BR" } };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            Rect labelRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect fields = EditorGUI.PrefixLabel(labelRect, label);
            fields.height = position.height;

            int indent = EditorGUI.indentLevel;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = CornerLabelWidth;

            float width = fields.width / 2f;
            float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            for (int row = 0; row < 2; row++)
            for (int column = 0; column < 2; column++) {
                SerializedProperty corner = property.FindPropertyRelative(Corners[row, column]);
                Rect rect = new(fields.x + width * column + (column == 1 ? 2f : 0f), fields.y + line * row,
                    width - 2f, EditorGUIUtility.singleLineHeight);
                GUIContent cornerLabel = new(Labels[row, column]);

                EditorGUI.BeginProperty(rect, cornerLabel, corner);
                EditorGUI.showMixedValue = corner.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                float value = EditorGUI.FloatField(rect, cornerLabel, corner.floatValue);
                if (EditorGUI.EndChangeCheck())
                    corner.floatValue = Mathf.Max(0f, value);
                EditorGUI.showMixedValue = false;
                EditorGUI.EndProperty();
            }

            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
