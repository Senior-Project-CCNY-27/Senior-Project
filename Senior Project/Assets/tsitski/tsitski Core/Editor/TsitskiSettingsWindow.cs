using System;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public class TsitskiSettingsWindow : EditorWindow {
        public static event Action NamingAllGenerators;

        private UnityEditor.Editor _editor;

        [MenuItem("Tools/tsitski/Settings", false, 10000)]
        private static void Open() {
            TsitskiSettingsWindow window = GetWindow<TsitskiSettingsWindow>(true, "tsitski Settings");
            window.minSize = new Vector2(320f, 120f);
            window.Show();
        }

        private void OnDisable() {
            if (_editor)
                DestroyImmediate(_editor);
        }

        private void OnGUI() {
            TsitskiSettings settings = TsitskiSettings.Instance;
            if (!settings)
                return;

            UnityEditor.Editor.CreateCachedEditor(settings, null, ref _editor);
            _editor.serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiSettings.defaultMeshMaterial)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiSettings.rebuildAfterDragging)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiSettings.autoNameGenerators)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiSettings.gizmosOnlyOnSelected)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiSettings.contributeGIByDefault)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiSettings.placementLayers)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiSettings.showReviewButton)));

            bool wasAutoNaming = settings.autoNameGenerators;
            if (!_editor.serializedObject.ApplyModifiedProperties())
                return;

            AssetDatabase.SaveAssetIfDirty(settings);
            if (!wasAutoNaming && settings.autoNameGenerators)
                NameAllGenerators();
        }

        public static void NameAllGenerators() {
            foreach (TsitskiArray array in UnityCompat.FindAll<TsitskiArray>())
                if (((IGenerated)array).generator == null)
                    TsitskiSettings.AutoName(array, array.stats, "Array");
            NamingAllGenerators?.Invoke();
        }
    }
}
