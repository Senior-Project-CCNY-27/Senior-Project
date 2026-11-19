using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    [InitializeOnLoad]
    internal static class DragRebuild {
        static DragRebuild() => SceneView.beforeSceneGui += Track;

        private static void Track(SceneView view) {
            Event e = Event.current;
            switch (e.rawType) {
                case EventType.MouseDown when e.button == 0:
                    RebuildQueue.SetHandleHeld(true);
                    break;
                case EventType.MouseUp when e.button == 0:
                case EventType.MouseMove:
                    RebuildQueue.SetHandleHeld(false);
                    break;
            }
        }
    }
}
