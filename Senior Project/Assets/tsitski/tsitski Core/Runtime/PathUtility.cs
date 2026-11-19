using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tsitski.Core {
    public static class PathUtility {
        private const float Epsilon = 1e-5f;

        public static List<Vector3> Clean(List<Vector3> source) {
            List<Vector3> points = new();
            if (source == null)
                return points;

            foreach (Vector3 p in source)
                if (points.Count == 0 || (points[^1] - p).sqrMagnitude > Epsilon * Epsilon)
                    points.Add(p);
            if (points.Count > 2 && (points[0] - points[^1]).sqrMagnitude <= Epsilon * Epsilon)
                points.RemoveAt(points.Count - 1);

            return points;
        }

        public static List<Vector3> Rounded(List<Vector3> path, List<float> radii, bool closed, int segments, out bool loop) {
            loop = closed && Clean(path).Count >= 3;
            if (path == null)
                return new List<Vector3>();

            List<Vector3> points = Clean(PointRounding.Round(path, radii, loop, segments));
            loop &= points.Count >= 3;
            return points;
        }

        public static void Segments(List<Vector3> points, bool loop, out Vector3[] directions, out float[] lengths,
            out Vector3[] rights, out Vector3[] ups) {
            int count = points.Count < 2 ? 0 : loop ? points.Count : points.Count - 1;
            directions = new Vector3[count];
            lengths = new float[count];
            rights = new Vector3[count];
            ups = new Vector3[count];

            for (int s = 0; s < count; s++) {
                Vector3 delta = points[(s + 1) % points.Count] - points[s];
                lengths[s] = delta.magnitude;
                directions[s] = delta / lengths[s];
            }

            Frames(points, directions, rights, ups);
        }

        public static void Frames(List<Vector3> points, Vector3[] directions, Vector3[] rights, Vector3[] ups) {
            Vector3 reference = Vector3.zero;
            foreach (Vector3 candidate in new[] { Vector3.up, PlaneNormal(points) }) {
                if (candidate == Vector3.zero || directions.Any(d => Mathf.Abs(Vector3.Dot(d, candidate)) > 0.999f))
                    continue;

                reference = candidate;
                break;
            }

            for (int s = 0; s < directions.Length; s++) {
                Vector3 direction = directions[s];
                Vector3 right;

                if (reference != Vector3.zero) {
                    right = Vector3.Cross(reference, direction);
                }
                else if (s == 0) {
                    right = Vector3.Cross(Mathf.Abs(direction.y) < 0.999f ? Vector3.up : Vector3.forward, direction);
                }
                else {
                    Vector3 previous = directions[s - 1];
                    Quaternion turn = Vector3.Dot(previous, direction) < -0.9999f
                        ? Quaternion.AngleAxis(180f, ups[s - 1])
                        : Quaternion.FromToRotation(previous, direction);
                    right = Vector3.ProjectOnPlane(turn * rights[s - 1], direction);
                }

                rights[s] = right.normalized;
                ups[s] = Vector3.Cross(direction, rights[s]);
            }
        }

        public static Vector3 PlaneNormal(List<Vector3> points) {
            Vector3 normal = Vector3.zero;
            float perimeter = 0f;
            for (int i = 0; i < points.Count; i++) {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                perimeter += Vector3.Distance(a, b);
                normal.x += (a.y - b.y) * (a.z + b.z);
                normal.y += (a.z - b.z) * (a.x + b.x);
                normal.z += (a.x - b.x) * (a.y + b.y);
            }

            if (normal.magnitude < perimeter * perimeter * 1e-4f)
                return Vector3.zero;

            normal.Normalize();
            float sign = Mathf.Abs(normal.y) > 0.01f ? normal.y : Mathf.Abs(normal.z) > 0.01f ? normal.z : normal.x;
            return sign < 0f ? -normal : normal;
        }

        public static Matrix4x4 StartFrame(List<Vector3> points, bool loop) {
            Vector3 origin = points.Count > 0 ? points[0] : Vector3.zero;
            Vector3 forward = Vector3.forward, right = Vector3.right, up = Vector3.up;

            if (points.Count > 1) {
                Segments(points, loop, out Vector3[] directions, out _, out Vector3[] rights, out Vector3[] ups);
                forward = directions[0];
                right = rights[0];
                up = ups[0];
            }

            Matrix4x4 frame = Matrix4x4.identity;
            frame.SetColumn(0, right);
            frame.SetColumn(1, up);
            frame.SetColumn(2, forward);
            frame.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            return frame;
        }
    }
}
