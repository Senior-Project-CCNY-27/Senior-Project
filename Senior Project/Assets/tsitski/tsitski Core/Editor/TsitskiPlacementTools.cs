using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class TsitskiPlacementTools {
        private const string Root = "Tools/tsitski/Core/Placement/";
        private const int StepPhysicsSteps = 50;
        private const float SnapToWallDistance = 10f;
        private const float SnapToWallMaxAngle = 60f;

        [MenuItem(Root + "Snap To Grid", false, 100)]
        public static void SnapToGrid() {
            foreach (Transform transform in Selection.transforms) {
                Undo.RecordObject(transform, "Snap To Grid");
                transform.position = Round(transform.position, EditorSnapSettings.move);
            }
        }

        [MenuItem(Root + "Snap To Oriented Grid", false, 101)]
        public static void SnapToOrientedGrid() => SnapOriented("Snap To Oriented Grid", false);

        [MenuItem(Root + "Snap To Oriented Grid And Angle", false, 102)]
        public static void SnapToOrientedGridAndAngle() => SnapOriented("Snap To Oriented Grid And Angle", true);

        [MenuItem(Root + "Snap To Wall", false, 103)]
        public static void SnapToWall() {
            Undo.SetCurrentGroupName("Snap To Wall");
            int group = Undo.GetCurrentGroup();
            int mask = TsitskiSettings.PlacementLayers;

            foreach (Transform transform in Selection.transforms) {
                Vector3[] directions = { transform.right, -transform.right, transform.up, -transform.up, transform.forward, -transform.forward };
                RaycastHit best = default;
                Vector3 bestDirection = default;
                bool found = false;

                foreach (Vector3 direction in directions) {
                    if (!RaycastIgnoring(transform, new Ray(transform.position, direction), mask, out RaycastHit hit))
                        continue;
                    if (Vector3.Angle(direction, -hit.normal) > SnapToWallMaxAngle || found && hit.distance >= best.distance)
                        continue;

                    best = hit;
                    bestDirection = direction;
                    found = true;
                }

                if (!found || (transform.position - best.point).sqrMagnitude < 0.05f * 0.05f)
                    continue;

                Undo.RecordObject(transform, "Snap To Wall");
                transform.rotation = Quaternion.FromToRotation(bestDirection, -best.normal) * transform.rotation;
                transform.position = best.point;
            }

            Undo.CollapseUndoOperations(group);
        }

        [MenuItem(Root + "Step Physics", false, 140)]
        public static void StepPhysics() {
            List<GameObject> targets = Selection.gameObjects.Where(g => g.scene.IsValid() && g.GetComponentInChildren<Collider>()).ToList();
            if (targets.Count == 0)
                return;

            Undo.SetCurrentGroupName("Step Physics");
            int group = Undo.GetCurrentGroup();
            Undo.RecordObjects(targets.Select(g => (Object)g.transform).ToArray(), "Step Physics");

            HashSet<Rigidbody> simulated = new();
            List<Rigidbody> added = new();
            Dictionary<Rigidbody, bool> kinematic = new();
            Dictionary<MeshCollider, bool> convex = new();

            foreach (GameObject target in targets) {
                if (!target.TryGetComponent(out Rigidbody body)) {
                    body = target.AddComponent<Rigidbody>();
                    body.hideFlags = HideFlags.DontSave;
                    added.Add(body);
                }

                simulated.Add(body);
                foreach (MeshCollider meshCollider in target.GetComponentsInChildren<MeshCollider>()) {
                    convex[meshCollider] = meshCollider.convex;
                    meshCollider.convex = true;
                }
            }

            foreach (Rigidbody body in UnityCompat.FindAll<Rigidbody>()) {
                kinematic[body] = body.isKinematic;
                body.isKinematic = !simulated.Contains(body);
            }

#if UNITY_2022_2_OR_NEWER
            SimulationMode mode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
#else
            bool mode = Physics.autoSimulation;
            Physics.autoSimulation = false;
#endif
            Physics.SyncTransforms();
            for (int i = 0; i < StepPhysicsSteps; i++)
                Physics.Simulate(Time.fixedDeltaTime);
#if UNITY_2022_2_OR_NEWER
            Physics.simulationMode = mode;
#else
            Physics.autoSimulation = mode;
#endif

            foreach (KeyValuePair<Rigidbody, bool> pair in kinematic) {
                if (!pair.Key)
                    continue;
                if (!pair.Key.isKinematic) {
                    SetVelocity(pair.Key, Vector3.zero);
                    pair.Key.angularVelocity = Vector3.zero;
                }
                pair.Key.isKinematic = pair.Value;
            }

            foreach (KeyValuePair<MeshCollider, bool> pair in convex)
                pair.Key.convex = pair.Value;

            foreach (Rigidbody body in added)
                Object.DestroyImmediate(body);

            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }

        [MenuItem(Root + "Snap To Grid", true)]
        [MenuItem(Root + "Snap To Oriented Grid", true)]
        [MenuItem(Root + "Snap To Oriented Grid And Angle", true)]
        [MenuItem(Root + "Snap To Wall", true)]
        [MenuItem(Root + "Step Physics", true)]
        private static bool HasSelection() => Selection.transforms.Length > 0;

        private static void SnapOriented(string undoName, bool snapAngle) {
            Transform[] selected = Selection.transforms;
            if (selected.Length == 0)
                return;

            Quaternion reference = MostCommonRotation(selected);
            Quaternion inverse = Quaternion.Inverse(reference);
            float angle = Mathf.Max(0.01f, EditorSnapSettings.rotate);

            foreach (Transform transform in selected) {
                Undo.RecordObject(transform, undoName);
                transform.position = reference * Round(inverse * transform.position, EditorSnapSettings.move);
                transform.rotation = snapAngle
                    ? Quaternion.Euler(Round(transform.rotation.eulerAngles, Vector3.one * angle))
                    : reference * Quaternion.Euler(Round((inverse * transform.rotation).eulerAngles, Vector3.one));
            }
        }

        internal static Vector3 SnapPosition(Vector3 position) => Round(position, EditorSnapSettings.move);

        private static Vector3 Round(Vector3 value, Vector3 step) => new(
            step.x > 0f ? Mathf.Round(value.x / step.x) * step.x : value.x,
            step.y > 0f ? Mathf.Round(value.y / step.y) * step.y : value.y,
            step.z > 0f ? Mathf.Round(value.z / step.z) * step.z : value.z);

        private static Quaternion MostCommonRotation(Transform[] transforms) {
            int bestCount = -1;
            Quaternion best = transforms[0].rotation;

            foreach (Transform candidate in transforms) {
                int count = transforms.Count(t => Quaternion.Angle(candidate.rotation, t.rotation) <= 1f);
                if (count <= bestCount)
                    continue;

                bestCount = count;
                best = candidate.rotation;
            }

            return best;
        }

        private static bool RaycastIgnoring(Transform ignore, Ray ray, int mask, out RaycastHit nearest) {
            nearest = default;
            bool found = false;

            foreach (RaycastHit hit in Physics.RaycastAll(ray, SnapToWallDistance, mask, QueryTriggerInteraction.Ignore)) {
                if (hit.transform.IsChildOf(ignore) || found && hit.distance >= nearest.distance)
                    continue;

                nearest = hit;
                found = true;
            }

            return found;
        }

        private static void SetVelocity(Rigidbody body, Vector3 velocity) {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }
    }
}
