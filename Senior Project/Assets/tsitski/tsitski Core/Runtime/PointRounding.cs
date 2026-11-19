using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tsitski.Core {
    public static class PointRounding {
        public static float Radius(IReadOnlyList<float> radii, int index) =>
            radii != null && index >= 0 && index < radii.Count ? Mathf.Max(0f, radii[index]) : 0f;

        public static List<float> Synced(IReadOnlyList<float> radii, int count) {
            List<float> synced = new(count);
            for (int i = 0; i < count; i++)
                synced.Add(Radius(radii, i));
            return synced;
        }

        public static List<Vector2> Round(IReadOnlyList<Vector2> points, IReadOnlyList<float> radii, bool closed, int segments) =>
            Round(points.Select(p => (Vector3)p).ToList(), radii, closed, segments).Select(p => (Vector2)p).ToList();

        public static List<Vector3> Round(IReadOnlyList<Vector3> points, IReadOnlyList<float> radii, bool closed, int segments,
            List<int> sources = null, System.Func<float, int> segmentsForArc = null) {
            List<Vector3> result = new();
            int count = points.Count;
            segments = Mathf.Max(1, segments);

            float[] angles = new float[count];
            float[] wanted = new float[count];
            for (int i = 0; i < count; i++)
                wanted[i] = WantedTangent(points, radii, closed, i, out angles[i]);

            float EdgeScale(int from) {
                int to = (from + 1) % count;
                float demand = wanted[from] + wanted[to];
                float length = Vector3.Distance(points[from], points[to]);
                return demand > length && demand > 0f ? length / demand : 1f;
            }

            for (int i = 0; i < count; i++) {
                Vector3 point = points[i];

                if (wanted[i] <= 0f) {
                    result.Add(point);
                    sources?.Add(i);
                    continue;
                }

                Vector3 u = (points[(i - 1 + count) % count] - point).normalized;
                Vector3 v = (points[(i + 1) % count] - point).normalized;
                float angle = angles[i];
                float tanHalf = Mathf.Tan(angle * 0.5f);
                float tangent = wanted[i] * Mathf.Min(EdgeScale((i - 1 + count) % count), EdgeScale(i));
                float radius = tangent * tanHalf;

                Vector3 center = point + (u + v).normalized * (radius / Mathf.Sin(angle * 0.5f));
                Vector3 from = point + u * tangent - center;
                Vector3 to = point + v * tangent - center;
                float sweep = Mathf.PI - angle;
                float sinSweep = Mathf.Sin(sweep);
                int arcSegments = segmentsForArc != null ? Mathf.Max(1, segmentsForArc(radius * sweep)) : segments;

                for (int k = 0; k <= arcSegments; k++) {
                    float t = (float)k / arcSegments;
                    result.Add(center + (Mathf.Sin((1f - t) * sweep) * from + Mathf.Sin(t * sweep) * to) / sinSweep);
                    sources?.Add(i);
                }
            }

            return result;
        }

        private static float WantedTangent(IReadOnlyList<Vector3> points, IReadOnlyList<float> radii, bool closed, int i, out float angle) {
            angle = 0f;
            int count = points.Count;
            float radius = Radius(radii, i);
            if (radius <= 0f || count < 3 || (!closed && (i == 0 || i == count - 1)))
                return 0f;

            Vector3 toPrevious = points[(i - 1 + count) % count] - points[i];
            Vector3 toNext = points[(i + 1) % count] - points[i];
            if (toPrevious.sqrMagnitude < 1e-12f || toNext.sqrMagnitude < 1e-12f)
                return 0f;

            angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(toPrevious.normalized, toNext.normalized), -1f, 1f));
            if (angle < 1e-3f || angle > Mathf.PI - 1e-3f)
                return 0f;

            return radius / Mathf.Tan(angle * 0.5f);
        }
    }
}
