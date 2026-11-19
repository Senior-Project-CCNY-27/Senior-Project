using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    [CustomPropertyDrawer(typeof(PathSourceAttribute))]
    public class PathSourceDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType != SerializedPropertyType.ObjectReference) {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            Object picked = EditorGUI.ObjectField(position, label, property.objectReferenceValue, typeof(Object), true);

            if (EditorGUI.EndChangeCheck())
                property.objectReferenceValue = Resolve(picked);

            EditorGUI.EndProperty();
        }

        public static MonoBehaviour Resolve(Object picked) {
            if (picked is ITsitskiPath and MonoBehaviour behaviour)
                return behaviour;

            GameObject gameObject = picked switch {
                GameObject go => go,
                Component component => component.gameObject,
                _ => null
            };

            return gameObject ? gameObject.GetComponent<ITsitskiPath>() as MonoBehaviour : null;
        }
    }
}
