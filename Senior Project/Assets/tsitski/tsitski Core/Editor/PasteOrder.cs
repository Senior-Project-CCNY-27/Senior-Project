using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.Core {
    public struct PastedPoints {
        public int anchor;
        public int start;
        public int count;

        public bool Matches(HashSet<int> selected, int pointCount) {
            if (selected.Count != count || start < 0 || start + count > pointCount || anchor < 0 || anchor >= pointCount)
                return false;

            for (int i = start; i < start + count; i++)
                if (!selected.Contains(i))
                    return false;
            return true;
        }
    }

    public static class PasteOrder {
        private const float Hysteresis = 1e-4f;

        public static void Reorder<T>(List<T> points, List<float> radii, ref PastedPoints pasted, bool closed, Func<T, T, float> distance,
            HashSet<int> selected, List<Quaternion> rotations = null) {
            List<T> block = points.GetRange(pasted.start, pasted.count);
            List<float> blockRadii = radii.GetRange(pasted.start, pasted.count);
            List<T> rest = new(points);
            List<float> restRadii = new(radii);
            rest.RemoveRange(pasted.start, pasted.count);
            restRadii.RemoveRange(pasted.start, pasted.count);
            List<Quaternion> blockRotations = rotations?.GetRange(pasted.start, pasted.count);
            List<Quaternion> restRotations = rotations != null ? new List<Quaternion>(rotations) : null;
            restRotations?.RemoveRange(pasted.start, pasted.count);

            int count = rest.Count;
            if (count == 0)
                return;

            int anchor = pasted.start > pasted.anchor ? pasted.anchor : pasted.anchor - pasted.count;
            bool after = pasted.start > pasted.anchor;
            bool wraps = closed && count >= 2;

            bool HasPrevious() => anchor > 0 || wraps;
            bool HasNext() => anchor < count - 1 || wraps;
            T Previous() => rest[(anchor - 1 + count) % count];
            T Next() => rest[(anchor + 1) % count];

            T a = rest[anchor], first = block[0], last = block[^1];
            float costAfter = distance(a, first) + (HasNext() ? distance(last, Next()) : 0f) + (HasPrevious() ? distance(Previous(), a) : 0f);
            float costBefore = (HasPrevious() ? distance(Previous(), first) : 0f) + distance(last, a) + (HasNext() ? distance(a, Next()) : 0f);

            bool before = after ? costBefore < costAfter - Hysteresis : costBefore <= costAfter + Hysteresis;
            int insertAt = before ? anchor : anchor + 1;

            rest.InsertRange(insertAt, block);
            restRadii.InsertRange(insertAt, blockRadii);
            if (rotations != null) {
                restRotations.InsertRange(insertAt, blockRotations);
                rotations.Clear();
                rotations.AddRange(restRotations);
            }

            points.Clear();
            points.AddRange(rest);
            radii.Clear();
            radii.AddRange(restRadii);

            pasted.start = insertAt;
            pasted.anchor = before ? anchor + pasted.count : anchor;
            selected.Clear();
            for (int i = insertAt; i < insertAt + pasted.count; i++)
                selected.Add(i);
        }
    }
}
