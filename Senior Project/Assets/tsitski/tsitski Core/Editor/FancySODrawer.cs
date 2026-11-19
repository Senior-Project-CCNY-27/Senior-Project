using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Tsitski.Core {

    public abstract class FancySODrawerBase : PropertyDrawer {

        private const float ButtonWidth = 20f;
        private const float Gap        = 2f;

        // The ScriptableObject base type offered by the creator window's type popup.
        protected abstract Type BaseType { get; }

        // When true, "+" nests the created ScriptableObject as a sub-asset of the asset that
        // owns the field instead of saving a standalone asset into a folder.
        protected virtual bool CreateAsSubAsset => false;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (BaseType == null || !typeof(ScriptableObject).IsAssignableFrom(BaseType)) {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // A "✕" appears next to "+" when the assigned reference is a sub-asset nested in
            // the same asset as the field's owner (ie. what the sub-asset "+" created).
            var current = property.objectReferenceValue;
            string ownerPath = AssetDatabase.GetAssetPath(property.serializedObject.targetObject);
            bool canDelete = current && !string.IsNullOrEmpty(ownerPath) &&
                             AssetDatabase.IsSubAsset(current) && AssetDatabase.GetAssetPath(current) == ownerPath;

            float buttonsWidth = canDelete ? ButtonWidth * 2 + Gap : ButtonWidth;

            var fieldRect = new Rect(position.x, position.y, position.width - buttonsWidth - Gap, position.height);
            var plusRect  = new Rect(position.xMax - ButtonWidth, position.y + 1f, ButtonWidth, position.height - 2f);

            EditorGUI.PropertyField(fieldRect, property, label);

            if (canDelete) {
                var deleteRect = new Rect(plusRect.x - ButtonWidth - Gap, plusRect.y, ButtonWidth, plusRect.height);

                GUI.color = new Color(1f, 0.55f, 0.55f);
                if (GUI.Button(deleteRect, "✕", EditorStyles.miniButton))
                    DeleteSubAsset(property, current);
            }

            GUI.color = new Color(0.55f, 0.85f, 1f);
            if (GUI.Button(plusRect, "+", EditorStyles.miniButton)) {
                string folder      = DefaultFolder(property);
                string defaultName = ObjectNames.NicifyVariableName(property.name);
                FancySOCreatorWindow.Show(BaseType, property, folder, defaultName, GUIUtility.GUIToScreenRect(plusRect), CreateAsSubAsset);
            }
            GUI.color = Color.white;
        }

        private static void DeleteSubAsset(SerializedProperty property, UnityEngine.Object subAsset) {
            string path = AssetDatabase.GetAssetPath(subAsset);

            if (!EditorUtility.DisplayDialog("Delete Sub Asset",
                    $"Delete '{subAsset.name}' nested in '{Path.GetFileName(path)}'?\n" +
                    "Anything else referencing it will lose the reference.", "Delete", "Cancel"))
                return;

            property.objectReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();

            AssetDatabase.RemoveObjectFromAsset(subAsset);
            UnityEngine.Object.DestroyImmediate(subAsset, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
        }

        private string DefaultFolder(SerializedProperty prop) {
            string activeFolder = GetActiveProjectWindowFolder();
            if (!string.IsNullOrEmpty(activeFolder) && AssetDatabase.IsValidFolder(activeFolder))
                return activeFolder;

            string saved = EditorPrefs.GetString(FancySOCreatorWindow.FolderPrefKey(BaseType), "");
            if (!string.IsNullOrEmpty(saved) && AssetDatabase.IsValidFolder(saved))
                return saved;

            var path = AssetDatabase.GetAssetPath(prop.serializedObject.targetObject);
            return string.IsNullOrEmpty(path) ? "Assets" : Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        }

        private static string GetActiveProjectWindowFolder() {
            var method = typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            return method?.Invoke(null, null) as string;
        }
    }

    public abstract class FancySODrawer<T> : FancySODrawerBase where T : ScriptableObject {
        protected override Type BaseType => typeof(T);
    }

    // ─────────────────────────────────────────────────────────────────────────

    internal class FancySOCreatorWindow : EditorWindow {

        // ── Prefs keys ────────────────────────────────────────────────────────

        private static string Prefix(Type t)           => "FancySODrawer." + t.Name + ".";
        internal static string FolderPrefKey(Type t)   => Prefix(t) + "LastFolder";
        private  static string TypePrefKey(Type t)     => Prefix(t) + "LastType";

        // ── State ─────────────────────────────────────────────────────────────

        private Type                _baseType;
        private Type[]              _types;
        private string[]            _typeNames;
        private SerializedProperty  _prop;
        private string              _folder;
        private string              _assetName;
        private int                 _typeIndex;
        private string              _feedback;
        private bool                _asSubAsset;
        private UnityEngine.Object  _parentAsset;

        // ── Open ──────────────────────────────────────────────────────────────

        internal static void Show(Type baseType, SerializedProperty prop, string folder, string assetName, Rect screenButtonRect, bool asSubAsset = false) {
            var win = CreateInstance<FancySOCreatorWindow>();
            win.titleContent = new GUIContent("Create " + ObjectNames.NicifyVariableName(baseType.Name.Replace("Stats", "")));
            win._baseType  = baseType;
            win._prop      = prop;

            // Sub-asset mode nests under the main asset of whatever owns the field; if the
            // owner isn't a saved asset (eg. a scene object), fall back to folder creation.
            if (asSubAsset) {
                string parentPath = AssetDatabase.GetAssetPath(prop.serializedObject.targetObject);
                win._parentAsset = string.IsNullOrEmpty(parentPath) ? null : AssetDatabase.LoadMainAssetAtPath(parentPath);
                win._asSubAsset = win._parentAsset;
            }

            // The base type itself is a valid choice too (and the only one for types without
            // subclasses); TypeCache only returns types derived from it.
            win._types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract)
                .Concat(baseType.IsAbstract ? Type.EmptyTypes : new[] { baseType })
                .OrderBy(t => t.Name)
                .ToArray();
            win._typeNames = win._types.Select(NiceName).ToArray();

            // Restore last type; only fall back to declared field type when nothing is saved yet
            string savedType = EditorPrefs.GetString(TypePrefKey(baseType), "");
            if (!string.IsNullOrEmpty(savedType)) {
                int idx = Array.FindIndex(win._types, t => t.Name == savedType);
                if (idx >= 0) win._typeIndex = idx;
            } else {
                var declared = prop.serializedObject.targetObject.GetType()
                    .GetField(prop.name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.FieldType;
                if (declared != null && !declared.IsAbstract) {
                    int idx = Array.IndexOf(win._types, declared);
                    if (idx >= 0) win._typeIndex = idx;
                }
            }

            win._assetName = assetName;

            win._folder = folder;

            win.minSize = new Vector2(420, 152);
            win.maxSize = new Vector2(1000, 152);

            // Position just below the button that opened us
            const float width = 600;
            win.position = new Rect(screenButtonRect.xMax - width, screenButtonRect.yMax + 2, width, 152);
            win.ShowUtility();
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI() {
            GUILayout.Space(6);

            // Type
            int next = EditorGUILayout.Popup("Type", _typeIndex, _typeNames);
            if (next != _typeIndex) {
                _typeIndex = next;
                _feedback  = null;
                if (_types.Length > 0)
                    EditorPrefs.SetString(TypePrefKey(_baseType), _types[_typeIndex].Name);
            }

            GUILayout.Space(3);

            // Name
            _assetName = EditorGUILayout.TextField("Name", _assetName);

            GUILayout.Space(3);

            // Folder (standalone) / parent asset (sub-asset)
            if (_asSubAsset) {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Nested Under", _parentAsset, typeof(UnityEngine.Object), false);
            }
            else {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _folder = EditorGUILayout.TextField("Folder", _folder);
                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetString(FolderPrefKey(_baseType), _folder);

                if (GUILayout.Button("…", EditorStyles.miniButton, GUILayout.Width(22))) {
                    string picked = EditorUtility.OpenFolderPanel("Save location", _folder, "");
                    if (!string.IsNullOrEmpty(picked)) {
                        _folder = ToProjectPath(picked);
                        EditorPrefs.SetString(FolderPrefKey(_baseType), _folder);
                        GUI.FocusControl(null);
                        Repaint();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            // Create button
            bool valid = _types.Length > 0 && !string.IsNullOrWhiteSpace(_assetName) &&
                         (_asSubAsset || !string.IsNullOrWhiteSpace(_folder));
            EditorGUI.BeginDisabledGroup(!valid);
            GUI.color = new Color(0.50f, 0.85f, 1f);
            if (GUILayout.Button("Create & Assign", GUILayout.Height(24)))
                CreateAndAssign();
            GUI.color = Color.white;
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_feedback)) {
                GUILayout.Space(2);
                EditorGUILayout.HelpBox(_feedback, MessageType.Info);
            }
        }

        private void CreateAndAssign() {
            if (_types.Length == 0) return;

            var    type     = _types[_typeIndex];
            var    instance = ScriptableObject.CreateInstance(type);
            string name     = string.IsNullOrWhiteSpace(_assetName) ? type.Name : _assetName;

            if (_asSubAsset) {
                instance.name = UniqueSubAssetName(name);
                AssetDatabase.AddObjectToAsset(instance, _parentAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(_parentAsset));
            }
            else {
                if (!AssetDatabase.IsValidFolder(_folder)) {
                    _feedback = $"Folder not found: {_folder}";
                    return;
                }

                string path = AssetDatabase.GenerateUniqueAssetPath($"{_folder}/{name}.asset");
                AssetDatabase.CreateAsset(instance, path);
                AssetDatabase.SaveAssets();
            }

            _prop.objectReferenceValue = instance;
            _prop.serializedObject.ApplyModifiedProperties();

            EditorGUIUtility.PingObject(instance);
            Close();
        }

        private string UniqueSubAssetName(string name) {
            var existing = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_parentAsset))
                .Where(asset => asset)
                .Select(asset => asset.name)
                .ToHashSet();

            string unique = name;

            for (int i = 1; existing.Contains(unique); i++)
                unique = $"{name} {i}";

            return unique;
        }

        private static string NiceName(Type t) => ObjectNames.NicifyVariableName(t.Name.Replace("Stats", ""));

        private static string ToProjectPath(string absolute) {
            string data = Application.dataPath;
            return absolute.StartsWith(data)
                ? "Assets" + absolute.Substring(data.Length).Replace('\\', '/')
                : absolute.Replace('\\', '/');
        }
    }
}
