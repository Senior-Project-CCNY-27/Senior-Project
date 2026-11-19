using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    [CustomPropertyDrawer(typeof(OrientedBounds))]
    public class OrientedBoundsPropertyDrawer : PropertyDrawer {
        private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            property.isExpanded ? Line * 4f : EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            Rect row = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (property.isExpanded) {
                using (new EditorGUI.IndentLevelScope()) {
                    SerializedProperty forward = property.FindPropertyRelative(nameof(OrientedBounds.forward));
                    SerializedProperty right = property.FindPropertyRelative(nameof(OrientedBounds.right));
                    SerializedProperty initialized = property.FindPropertyRelative(nameof(OrientedBounds.initialized));

                    row.y += Line;
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(nameof(OrientedBounds.center)));
                    row.y += Line;
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(nameof(OrientedBounds.size)));
                    if (EditorGUI.EndChangeCheck())
                        initialized.boolValue = true;

                    row.y += Line;
                    Vector3 f = forward.vector3Value;
                    Vector3 u = Vector3.Cross(f, right.vector3Value);
                    Vector3 euler = f.sqrMagnitude > 1e-8f && u.sqrMagnitude > 1e-8f
                        ? Quaternion.LookRotation(f, u).eulerAngles
                        : Vector3.zero;

                    bool wasMixed = EditorGUI.showMixedValue;
                    EditorGUI.showMixedValue = forward.hasMultipleDifferentValues || right.hasMultipleDifferentValues;
                    EditorGUI.BeginChangeCheck();
                    euler = EditorGUI.Vector3Field(row, "Rotation", euler);
                    if (EditorGUI.EndChangeCheck()) {
                        Quaternion rotation = Quaternion.Euler(euler);
                        forward.vector3Value = rotation * Vector3.forward;
                        right.vector3Value = rotation * Vector3.right;
                        initialized.boolValue = true;
                    }
                    EditorGUI.showMixedValue = wasMixed;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
