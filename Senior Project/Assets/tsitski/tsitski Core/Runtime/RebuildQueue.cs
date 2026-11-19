using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.Core {
    public static class RebuildQueue {
        private static readonly Dictionary<Object, bool> Queued = new();
        private static readonly Dictionary<Object, Action> Deferred = new();

        public static bool HandleHeld { get; private set; }
        public static bool Deferring => HandleHeld && TsitskiSettings.RebuildAfterDragging;
        public static event Action Released;

#if UNITY_EDITOR
        private static bool _restoringScene;
        private static bool _loading = true;

        [InitializeOnLoadMethod]
        private static void TrackLoading() {
            EndLoadingLater();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpening += (_, _) => _loading = true;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (_, _) => EndLoadingLater();
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    _restoringScene = true;
                else if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += () => _restoringScene = false;
            };
        }

        private static void EndLoadingLater() => EditorApplication.delayCall += () => _loading = false;
#endif

        public static void Queue(Object owner, Action rebuild, bool onlyIfChanged = false) {
#if UNITY_EDITOR
            if (EditorApplication.isPlayingOrWillChangePlaymode || _restoringScene)
                return;

            bool checkInputs = onlyIfChanged || _loading;
            if (Queued.TryGetValue(owner, out bool queuedChecking)) {
                if (queuedChecking && !checkInputs)
                    Queued[owner] = false;
                return;
            }

            Queued[owner] = checkInputs;
            EditorApplication.delayCall += () => {
                bool check = Queued.TryGetValue(owner, out bool value) && value;
                Queued.Remove(owner);
                if (!owner || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (check && GeneratorInputs.UpToDate(owner))
                    return;
                Run(owner, rebuild);
            };
#endif
        }

        public static void Run(Object owner, Action rebuild) {
            if (Deferring) {
                Deferred[owner] = rebuild;
                return;
            }

            Deferred.Remove(owner);
            rebuild();
        }

        public static void SetHandleHeld(bool held) {
            if (held) {
                HandleHeld = true;
                return;
            }

            if (!HandleHeld)
                return;

            HandleHeld = false;
            List<KeyValuePair<Object, Action>> deferred = new(Deferred);
            Deferred.Clear();
            foreach ((Object owner, Action rebuild) in deferred)
                if (owner)
                    rebuild();
            Released?.Invoke();
        }
    }
}
