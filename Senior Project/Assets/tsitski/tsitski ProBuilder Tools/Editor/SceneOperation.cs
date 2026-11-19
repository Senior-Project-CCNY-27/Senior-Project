using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal static class SceneMouse {
        public static Vector2 screenPosition { get; private set; }

        static SceneMouse() => SceneView.beforeSceneGui += Track;

        private static void Track(SceneView sceneView) {
            if (Event.current.type is EventType.MouseMove or EventType.MouseDrag or EventType.MouseDown)
                screenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        }
    }

    internal class SceneOperationWindow : EditorWindow {
        private const float Width = 280f;
        private const float Padding = 4f;

        private SceneOperation _operation;
        private bool _destroyed;

        public static SceneOperationWindow Open(SceneOperation operation, int fieldCount) {
            SceneOperationWindow window = CreateInstance<SceneOperationWindow>();
            window._operation = operation;
            float height = Padding * 2f + EditorGUIUtility.singleLineHeight + 4f + fieldCount * (EditorGUIUtility.singleLineHeight + 2f) + 30f;
            window.ShowAsDropDown(new Rect(SceneMouse.screenPosition, Vector2.zero), new Vector2(Width, height));
            return window;
        }

        public void CloseQuietly() {
            _operation = null;
            if (!_destroyed)
                Close();
        }

        private void OnGUI() {
            if (_operation == null)
                return;

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) {
                _operation.EndFromWindow(true);
                e.Use();
                return;
            }

            if (e.type == EventType.KeyDown && e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) {
                _operation.EndFromWindow(false);
                e.Use();
                return;
            }

            GUILayout.BeginArea(new Rect(Padding, Padding, position.width - Padding * 2f, position.height - Padding * 2f));
            GUILayout.Label(_operation.displayTitle, EditorStyles.boldLabel);
            _operation.DrawContents();
            TsitskiToolsPopup.FitHeight(this, GUILayoutUtility.GetLastRect().yMax + Padding * 2f);
            GUILayout.EndArea();
        }

        private void OnDestroy() {
            _destroyed = true;
            SceneOperation operation = _operation;
            _operation = null;
            operation?.EndFromWindow(false);
        }
    }

    [System.Serializable]
    internal abstract class SceneOperation {
        private const double SettleTime = 0.25;

        public static SceneOperation active { get; private set; }

        private bool _applying;
        private double _settleUntil;
        private SceneOperationWindow _window;

        protected abstract string title { get; }
        protected virtual int fieldCount => 1;
        protected virtual string help => "Click outside to finish, Esc to cancel.";

        protected abstract bool Begin();
        protected abstract void Apply();
        protected abstract void Cancel();
        protected abstract void Complete();
        protected abstract void Abandon();
        protected abstract void DrawFields();

        internal string displayTitle => title;

        private enum Ending {
            Complete,
            Cancel,
            Abandon
        }

        public static void Run(SceneOperation operation) {
            active?.Finish(Ending.Complete);
            JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(operation.prefsKey, "{}"), operation);
            if (!operation.Begin())
                return;

            active = operation;
            operation.Reapply();
            operation._window = SceneOperationWindow.Open(operation, operation.fieldCount);

            SceneView.beforeSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            ProBuilderEditor.selectionUpdated += OnElementSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneView.RepaintAll();
        }

        private string prefsKey => $"Tsitski.ProBuilderTools.SceneOperation.{GetType().Name}";

        private void Reapply() {
            _applying = true;
            try {
                Apply();
            }
            finally {
                _applying = false;
                _settleUntil = EditorApplication.timeSinceStartup + SettleTime;
            }

            EditorPrefs.SetString(prefsKey, JsonUtility.ToJson(this));
        }

        internal void EndFromWindow(bool cancel) {
            if (active == this)
                Finish(cancel ? Ending.Cancel : Ending.Complete);
        }

        private static void OnSelectionChanged() {
            if (active is { _applying: false } operation && EditorApplication.timeSinceStartup > operation._settleUntil)
                operation.Finish(Ending.Complete);
        }

        private static void OnElementSelectionChanged(IEnumerable<ProBuilderMesh> meshes) => OnSelectionChanged();

        private static void OnUndoRedo() => active?.Finish(Ending.Abandon);

        private void Finish(Ending ending) {
            SceneView.beforeSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            ProBuilderEditor.selectionUpdated -= OnElementSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (active == this)
                active = null;

            if (_window) {
                SceneOperationWindow window = _window;
                _window = null;
                window.CloseQuietly();
            }

            _applying = true;
            try {
                switch (ending) {
                    case Ending.Cancel:
                        Cancel();
                        break;
                    case Ending.Abandon:
                        Abandon();
                        break;
                    default:
                        Complete();
                        break;
                }
            }
            finally {
                _applying = false;
            }

            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView view) {
            SceneOperation operation = active;
            Event e = Event.current;
            if (operation == null || e.type != EventType.KeyDown)
                return;

            if (e.keyCode == KeyCode.Escape) {
                operation.Finish(Ending.Cancel);
                e.Use();
            }
            else if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) {
                operation.Finish(Ending.Complete);
                e.Use();
            }
        }

        internal void DrawContents() {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 90f;
            EditorGUI.BeginChangeCheck();
            DrawFields();
            if (EditorGUI.EndChangeCheck())
                Reapply();
            EditorGUIUtility.labelWidth = labelWidth;

            GUILayout.Label(help, EditorStyles.wordWrappedMiniLabel);
        }
    }
}
