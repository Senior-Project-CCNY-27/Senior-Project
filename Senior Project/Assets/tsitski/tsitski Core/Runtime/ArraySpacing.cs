using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.Core {
    public enum PlacementKind {
        Interior,
        Division,
        Corner
    }

    public struct ArraySpacing {
        public const float CornerAngle = 15f;
        private const float Epsilon = 1e-4f;

        public float spacing;
        public float offset;
        public bool fitSpacing;
        public bool splitAtCorners;
        public float divisionLength;
        public bool skipCorners;
        public bool skipDivisions;

        public struct Placement {
            public float distance;
            public PlacementKind kind;
            public bool skipped;
        }

        public List<Placement> Layout(List<Vector3> points, bool loop, int maxInstances) {
            List<Placement> result = new();
            if (points == null || points.Count == 0)
                return result;
            if (points.Count == 1) {
                result.Add(new Placement { distance = 0f, kind = PlacementKind.Interior });
                return result;
            }

            int segmentCount = loop ? points.Count : points.Count - 1;
            Vector3[] directions = new Vector3[segmentCount];
            float[] cumulative = new float[segmentCount + 1];
            for (int s = 0; s < segmentCount; s++) {
                Vector3 delta = points[(s + 1) % points.Count] - points[s];
                float length = delta.magnitude;
                directions[s] = length > 1e-6f ? delta / length : Vector3.zero;
                cumulative[s + 1] = cumulative[s] + length;
            }

            float total = cumulative[^1];
            if (total <= Epsilon)
                return result;

            List<float> corners = new();
            for (int v = 0; v < points.Count; v++) {
                if (!loop && (v == 0 || v == points.Count - 1))
                    continue;
                Vector3 before = directions[(v - 1 + segmentCount) % segmentCount], after = directions[v % segmentCount];
                if (before != Vector3.zero && after != Vector3.zero && Vector3.Angle(before, after) > CornerAngle)
                    corners.Add(cumulative[v]);
            }

            float step = Mathf.Max(TsitskiArray.MinSpacing, spacing);
            ArraySpacing settings = this;

            void Add(float distance, PlacementKind kind) {
                if (result.Count >= maxInstances)
                    return;
                if (kind == PlacementKind.Interior && IsCorner(distance))
                    kind = PlacementKind.Corner;
                result.Add(new Placement {
                    distance = loop ? Mathf.Repeat(distance, total) : Mathf.Clamp(distance, 0f, total),
                    kind = kind,
                    skipped = kind == PlacementKind.Corner && settings.skipCorners || kind == PlacementKind.Division && settings.skipDivisions
                });
            }

            bool IsCorner(float distance) {
                float d = loop ? Mathf.Repeat(distance, total) : distance;
                foreach (float corner in corners)
                    if (Mathf.Abs(corner - d) < Epsilon || loop && Mathf.Abs(corner - d) > total - Epsilon)
                        return true;
                return !loop && (d < Epsilon || d > total - Epsilon);
            }

            List<(float from, float to, PlacementKind startKind)> runs = new();
            if (splitAtCorners && corners.Count > 0) {
                if (loop) {
                    for (int i = 0; i < corners.Count; i++)
                        runs.Add((corners[i], i + 1 < corners.Count ? corners[i + 1] : corners[0] + total, PlacementKind.Corner));
                }
                else {
                    float previous = 0f;
                    foreach (float corner in corners) {
                        runs.Add((previous, corner, PlacementKind.Corner));
                        previous = corner;
                    }
                    runs.Add((previous, total, PlacementKind.Corner));
                }
            }
            else {
                PlacementKind seam = divisionLength > Epsilon ? PlacementKind.Division : PlacementKind.Interior;
                runs.Add((0f, total, loop ? seam : PlacementKind.Corner));
            }

            for (int r = 0; r < runs.Count; r++) {
                (float from, float to, PlacementKind startKind) = runs[r];
                float runLength = to - from;
                if (runLength <= Epsilon)
                    continue;

                int divisions = divisionLength > Epsilon ? Mathf.Max(1, Mathf.RoundToInt(runLength / divisionLength)) : 1;
                float division = runLength / divisions;
                bool lastRun = r == runs.Count - 1;

                for (int k = 0; k < divisions; k++) {
                    float a = from + k * division;
                    PlacementKind boundary = k == 0 ? startKind : PlacementKind.Division;
                    bool includeEnd = !loop && lastRun && k == divisions - 1;

                    if (fitSpacing) {
                        int gaps = Mathf.Max(1, Mathf.RoundToInt(division / step));
                        float fitted = division / gaps;
                        for (int j = 0; j < gaps; j++)
                            Add(a + j * fitted, j == 0 ? boundary : PlacementKind.Interior);
                        if (includeEnd)
                            Add(a + division, PlacementKind.Corner);
                        continue;
                    }

                    float start = Mathf.Clamp(offset, 0f, division);
                    for (int j = 0; ; j++) {
                        float t = start + j * step;
                        bool atEnd = t > division - Epsilon;
                        if (atEnd && (!includeEnd || t > division + Epsilon))
                            break;
                        PlacementKind kind = t < Epsilon ? boundary : atEnd ? PlacementKind.Corner : PlacementKind.Interior;
                        Add(a + t, kind);
                        if (result.Count >= maxInstances)
                            break;
                    }
                }
            }

            return result;
        }
    }
}
