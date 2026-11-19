using System;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    [InitializeOnLoad]
    public static class TsitskiCreateMenu {
        private static SceneView _cursorView;
        private static Vector2 _cursorScreenPosition;
        private static SceneView _heldView;
        private static Vector2 _heldScreenPosition;

        static TsitskiCreateMenu() => SceneView.duringSceneGui += TrackCursor;

        [MenuItem("Tools/tsitski/Core/Create/Array", false, 400)]
        [MenuItem("GameObject/tsitski Core/Array", false, 10)]
        private static void CreateArray(MenuCommand command) => Create<TsitskiArray>("tsitski Array", command);

        public static T Create<T>(string name, MenuCommand command = null) where T : Component {
            GameObject parent = command?.context as GameObject;
            GameObject gameObject = new(name);
            GameObjectUtility.SetParentAndAlign(gameObject, parent);
            if (!parent && SceneView.lastActiveSceneView)
                gameObject.transform.position = TsitskiPlacementTools.SnapPosition(SpawnPosition());

            T component = gameObject.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
            Selection.activeGameObject = gameObject;
            return component;
        }

        private static void TrackCursor(SceneView view) {
            Event current = Event.current;
            if (!current.isMouse)
                return;

            _cursorView = view;
            _cursorScreenPosition = GUIUtility.GUIToScreenPoint(current.mousePosition);
        }

        internal static void RunAtCursor(SceneView view, Vector2 screenPosition, Action run) {
            _heldView = view;
            _heldScreenPosition = screenPosition;
            try {
                run();
            }
            finally {
                _heldView = null;
            }
        }

        private static Vector3 SpawnPosition() {
            if (_heldView)
                return CursorPosition(_heldView, _heldScreenPosition);
            if (_cursorView && EditorWindow.mouseOverWindow is SceneView)
                return CursorPosition(_cursorView, _cursorScreenPosition);
            return SceneView.lastActiveSceneView.pivot;
        }

        private static Vector3 CursorPosition(SceneView view, Vector2 screenPosition) {
            Camera camera = view.camera;
            Rect window = view.position;
            Vector2 point = screenPosition - window.position;
            point.y -= window.height - camera.pixelHeight / EditorGUIUtility.pixelsPerPoint;

            Camera previous = Camera.current;
            Camera.SetupCurrent(camera);
            bool hit = HandleUtility.PlaceObject(point, out Vector3 position, out _);
            Camera.SetupCurrent(previous);

            return hit ? position : view.pivot;
        }
    }
}
