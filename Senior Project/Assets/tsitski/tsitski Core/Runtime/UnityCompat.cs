using UnityEngine;

namespace Tsitski.Core {
    public static class UnityCompat {
        public static T[] FindAll<T>(bool includeInactive = false) where T : Object {
#if UNITY_6000_4_OR_NEWER
            return Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>(includeInactive);
#endif
        }

        public static void DrawGizmoLineList(Vector3[] points) {
#if UNITY_2022_1_OR_NEWER
            Gizmos.DrawLineList(points);
#else
            for (int i = 0; i + 1 < points.Length; i += 2)
                Gizmos.DrawLine(points[i], points[i + 1]);
#endif
        }
    }
}
