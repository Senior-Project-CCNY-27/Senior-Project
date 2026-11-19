using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [System.Serializable]
    internal class SnapOntoPathOperation : SceneOperation {
        private const string MenuPath = "Tools/tsitski/Core/Placement/Snap Onto Path";
        private const int StairsRoundingSegments = 8;

        [SerializeField] private float _offset;
        [SerializeField] private bool _floorPlanCenter;
        [SerializeField] private bool _snapHeight;

        private readonly List<(Transform transform, Vector3 position, Quaternion rotation)> _targets = new();
        private int _undoGroup;

        protected override string title => "Snap Onto Path";
        protected override int fieldCount => 3;

        private static readonly Vector3[] Axes = {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        private struct PathSegment {
            public Vector3 start;
            public Vector3 end;
            public Vector3 up;
        }

        [MenuItem(MenuPath, false, 104)]
        private static void SnapOntoPath() => Run(new SnapOntoPathOperation());

        [MenuItem(MenuPath, true)]
        private static bool SnapOntoPathValidate() => Selection.transforms.Length > 0;

        protected override void DrawFields() {
            _offset = EditorGUILayout.FloatField("Offset", _offset);
            _floorPlanCenter = EditorGUILayout.Toggle(new GUIContent("Floor Plan Center",
                "Only snap onto floor plans, centred inside the thickness of their walls."), _floorPlanCenter);
            _snapHeight = EditorGUILayout.Toggle(new GUIContent("Snap Height",
                "Move the objects to the path's height, otherwise they keep their own height."), _snapHeight);
        }

        protected override bool Begin() {
            foreach (Transform transform in Selection.transforms)
                _targets.Add((transform, transform.position, transform.rotation));

            if (_targets.Count == 0)
                return false;

            Undo.SetCurrentGroupName(title);
            _undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(_targets.Select(t => (Object)t.transform).ToArray(), title);
            return true;
        }

        protected override void Apply() {
            Transform[] transforms = _targets.Select(t => t.transform).ToArray();
            List<PathSegment> segments = _floorPlanCenter ? FloorPlanSegments(transforms) : Segments(transforms);

            foreach ((Transform transform, Vector3 position, Quaternion rotation) in _targets) {
                if (segments.Count == 0) {
                    transform.SetPositionAndRotation(position, rotation);
                    continue;
                }

                PathSegment nearest = default;
                Vector3 nearestPoint = position;
                float nearestDistance = float.MaxValue;

                foreach (PathSegment segment in segments) {
                    Vector3 point = ClosestPoint(segment, position, _snapHeight);
                    float distance = (point - position).sqrMagnitude;
                    if (distance >= nearestDistance)
                        continue;

                    nearestDistance = distance;
                    nearestPoint = point;
                    nearest = segment;
                }

                Vector3 direction = (nearest.end - nearest.start).normalized;
                Vector3 up = Vector3.ProjectOnPlane(nearest.up, direction).normalized;
                Vector3 right = Vector3.Cross(up, direction);

                Quaternion snapped = Quaternion.FromToRotation(NearestAxis(rotation, direction, out _), direction) * rotation;
                Vector3 upAxis = NearestAxis(snapped, up, out int upIndex, direction);
                if (upIndex >= 0)
                    snapped = Quaternion.AngleAxis(Vector3.SignedAngle(upAxis, up, direction), direction) * snapped;

                Vector3 snappedPosition = nearestPoint + right * _offset;
                if (!_snapHeight)
                    snappedPosition.y = position.y;

                transform.SetPositionAndRotation(snappedPosition, snapped);
            }
        }

        protected override void Cancel() {
            Undo.RevertAllDownToGroup(_undoGroup);
            foreach ((Transform transform, Vector3 position, Quaternion rotation) in _targets)
                if (transform)
                    transform.SetPositionAndRotation(position, rotation);
            _targets.Clear();
        }

        protected override void Complete() {
            Undo.CollapseUndoOperations(_undoGroup);
            _targets.Clear();
        }

        protected override void Abandon() => _targets.Clear();

        private static Vector3 NearestAxis(Quaternion rotation, Vector3 target, out int index, Vector3? exclude = null) {
            index = -1;
            float best = float.MinValue;
            Vector3 nearest = target;

            for (int i = 0; i < Axes.Length; i++) {
                Vector3 axis = rotation * Axes[i];
                if (exclude.HasValue && Mathf.Abs(Vector3.Dot(axis, exclude.Value)) > 0.5f)
                    continue;

                float dot = Vector3.Dot(axis, target);
                if (dot <= best)
                    continue;

                best = dot;
                nearest = axis;
                index = i;
            }

            return nearest;
        }

        private static Vector3 ClosestPoint(PathSegment segment, Vector3 position, bool useHeight) {
            Vector3 delta = segment.end - segment.start;
            Vector3 flat = useHeight ? delta : new Vector3(delta.x, 0f, delta.z);
            if (flat.sqrMagnitude < 1e-12f) {
                Vector3 nearest = (segment.start - position).sqrMagnitude < (segment.end - position).sqrMagnitude ? segment.start : segment.end;
                return useHeight ? nearest : new Vector3(nearest.x, position.y, nearest.z);
            }

            Vector3 offset = position - segment.start;
            if (!useHeight)
                offset.y = 0f;

            Vector3 point = segment.start + delta * Mathf.Clamp01(Vector3.Dot(offset, flat) / flat.sqrMagnitude);
            return useHeight ? point : new Vector3(point.x, position.y, point.z);
        }

        private static bool Excluded(Component owner, Transform[] selected) =>
            !owner || !owner.gameObject.activeInHierarchy || selected.Any(s => owner.transform.IsChildOf(s));

        private static List<PathSegment> FloorPlanSegments(Transform[] selected) {
            List<PathSegment> segments = new();

            foreach (TsitskiProBuilderFloorPlan plan in TsitskiProBuilderFloorPlan.All) {
                if (Excluded(plan, selected))
                    continue;

                if (plan.wallCenters.Count == 0)
                    plan.Rebuild();

                foreach ((List<Vector3> points, bool loop) in plan.wallCenters)
                    AddSegments(segments, plan.transform, points, loop);
            }

            return segments;
        }

        private static List<PathSegment> Segments(Transform[] selected) {
            List<PathSegment> segments = new();

            foreach (MonoBehaviour owner in UnityCompat.FindAll<MonoBehaviour>()) {
                if (Excluded(owner, selected) || !owner.enabled)
                    continue;

                List<Vector3> points;
                bool loop = false;

                switch (owner) {
                    case TsitskiArray array:
                        points = PathUtility.Rounded(array.activePath, array.activePathRadii, array.activeClosed, array.activeRoundingSegments, out loop);
                        break;
                    case TsitskiProBuilderTrim trim:
                        points = PathUtility.Rounded(trim.activePath, trim.activePathRadii, trim.activeClosed, trim.activeRoundingSegments, out loop);
                        break;
                    case TsitskiProBuilderFloorPlan plan:
                        points = PathUtility.Rounded(plan.path, plan.pathRadii, true, plan.activeRoundingSegments, out loop);
                        break;
                    case TsitskiProBuilderStairs stairs:
                        points = PathUtility.Rounded(stairs.path, stairs.pathRadii, false, StairsRoundingSegments, out loop);
                        break;
                    case TsitskiProBuilderCutter { shape: ProBuilderCutterShape.Path } cutter:
                        points = PathUtility.Clean(CutterVolume.PathPoints(cutter));
                        break;
                    default:
                        continue;
                }

                AddSegments(segments, owner.transform, points, loop);
            }

            return segments;
        }

        private static void AddSegments(List<PathSegment> segments, Transform owner, List<Vector3> points, bool loop) {
            List<Vector3> world = PathUtility.Clean(points.Select(owner.TransformPoint).ToList());
            loop &= world.Count >= 3;
            if (world.Count < 2)
                return;

            PathUtility.Segments(world, loop, out Vector3[] directions, out float[] lengths, out _, out Vector3[] ups);

            for (int i = 0; i < directions.Length; i++)
                if (lengths[i] > 1e-6f)
                    segments.Add(new PathSegment { start = world[i], end = world[(i + 1) % world.Count], up = ups[i] });
        }
    }
}
