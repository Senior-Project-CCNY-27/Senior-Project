using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class TsitskiInspector {
        public static bool DrawProperties(SerializedObject serializedObject, Func<string, bool> hidden = null) {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (hidden != null && hidden(property.name) || HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            return changed;
        }

        private static readonly Dictionary<(Type, string), FieldInfo> Fields = new();

        public static bool HiddenByCondition(SerializedProperty property) {
            string condition = FieldOf(property)?.GetCustomAttribute<TsitskiShowIfAttribute>()?.condition;
            if (condition == null)
                return false;

            SerializedProperty toggle = property.serializedObject.FindProperty(condition);
            return toggle is { propertyType: SerializedPropertyType.Boolean, hasMultipleDifferentValues: false } && !toggle.boolValue;
        }

        private static FieldInfo FieldOf(SerializedProperty property) {
            if (property.depth != 0 || !property.serializedObject.targetObject)
                return null;

            Type type = property.serializedObject.targetObject.GetType();
            if (Fields.TryGetValue((type, property.name), out FieldInfo field))
                return field;

            for (Type current = type; current != null && field == null; current = current.BaseType)
                field = current.GetField(property.name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            Fields[(type, property.name)] = field;
            return field;
        }

        public sealed class Headers {

            private string _pending;
            private string _pendingOwner;

            public void Next(SerializedProperty property) {
                string header = HeaderOf(property);
                if (header == null)
                    return;

                _pending = header;
                _pendingOwner = property.propertyPath;
            }

            public void BeforePropertyField(SerializedProperty property) {
                if (_pending != null && _pendingOwner != property.propertyPath)
                    Draw(_pending);
                _pending = null;
            }

            public void BeforeCustomField() {
                if (_pending != null)
                    Draw(_pending);
                _pending = null;
            }

            public static void Draw(string header) {
                GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
                EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            }

            private static string HeaderOf(SerializedProperty property) =>
                FieldOf(property)?.GetCustomAttribute<HeaderAttribute>()?.header;
        }
    }
}
