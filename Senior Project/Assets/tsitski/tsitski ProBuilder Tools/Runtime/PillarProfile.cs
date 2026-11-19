using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public class PillarProfile {
        [Tooltip(Tooltips.PillarSectionPoints)] public List<Vector2> points = new();
        [Tooltip(Tooltips.Radii)] public List<float> radii = new();

        public bool valid => points is { Count: >= 2 };

        public PillarProfile() { }

        public PillarProfile(params Vector2[] points) => this.points = new List<Vector2>(points);

        public PillarProfile Copy() => new() { points = new List<Vector2>(points ?? new List<Vector2>()), radii = new List<float>(radii ?? new List<float>()) };

        public static float Width(params PillarProfile[] profiles) {
            float width = 0f;
            foreach (PillarProfile profile in profiles)
                if (profile is { valid: true })
                    foreach (Vector2 p in profile.points)
                        width = Mathf.Max(width, Mathf.Abs(p.x));
            return width;
        }

        public static void SyncWidth(ref float width, ref float lastWidth, params PillarProfile[] profiles) {
            float measured = Width(profiles);
            if (lastWidth >= 0f && Mathf.Abs(width - lastWidth) > 1e-5f && measured > 1e-5f) {
                width = Mathf.Max(0.001f, width);
                float scale = width / measured;
                foreach (PillarProfile profile in profiles)
                    if (profile?.points != null)
                        for (int i = 0; i < profile.points.Count; i++)
                            profile.points[i] = new Vector2(profile.points[i].x * scale, profile.points[i].y);
            }
            else {
                width = measured;
            }

            lastWidth = width;
        }

        public static PillarProfile DefaultBottom() => new(new Vector2(0.3f, 0f), new Vector2(0.3f, 2f / 3f), new Vector2(0.25f, 1f));
        public static PillarProfile DefaultCenter() => new(new Vector2(0.25f, 0f), new Vector2(0.25f, 1f));
        public static PillarProfile DefaultTop() => new(new Vector2(0.25f, 0f), new Vector2(0.3f, 1f / 3f), new Vector2(0.3f, 1f));

        public static bool Split(List<Vector2> legacy, List<float> legacyRadii, float bottomHeight, float topHeight,
            out PillarProfile bottom, out PillarProfile center, out PillarProfile top,
            out float actualBottom, out float actualCenter, out float actualTop) {
            bottom = new PillarProfile();
            center = new PillarProfile();
            top = new PillarProfile();
            actualBottom = actualCenter = actualTop = 0f;
            if (legacy is not { Count: >= 2 })
                return false;

            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector2 p in legacy) {
                min = Mathf.Min(min, p.y);
                max = Mathf.Max(max, p.y);
            }

            float bottomY = Mathf.Clamp(min + Mathf.Max(0f, bottomHeight), min, max);
            float topY = Mathf.Clamp(max - Mathf.Max(0f, topHeight), bottomY, max);
            actualBottom = bottomY - min;
            actualCenter = topY - bottomY;
            actualTop = max - topY;

            Fill(bottom, legacy, legacyRadii, float.NegativeInfinity, bottomY, min, actualBottom);
            Fill(center, legacy, legacyRadii, bottomY, topY, bottomY, actualCenter);
            Fill(top, legacy, legacyRadii, topY, float.PositiveInfinity, topY, actualTop);
            return true;
        }

        private static void Fill(PillarProfile section, List<Vector2> legacy, List<float> legacyRadii, float low, float high, float start, float span) {
            void Add(Vector2 p, float radius) {
                Vector2 local = new(p.x, span > 1e-5f ? Mathf.Clamp01((p.y - start) / span) : 0f);
                if (section.points.Count > 0 && (section.points[^1] - local).sqrMagnitude < 1e-10f)
                    return;
                section.points.Add(local);
                section.radii.Add(radius);
            }

            for (int i = 0; i < legacy.Count; i++) {
                Vector2 p = legacy[i];
                float radius = legacyRadii != null && i < legacyRadii.Count ? legacyRadii[i] : 0f;

                if (i > 0) {
                    Vector2 previous = legacy[i - 1];
                    foreach (float boundary in new[] { low, high }) {
                        if (float.IsInfinity(boundary) || (previous.y - boundary) * (p.y - boundary) >= 0f)
                            continue;
                        Add(Vector2.Lerp(previous, p, (boundary - previous.y) / (p.y - previous.y)), 0f);
                    }
                }

                if (p.y >= low - 1e-4f && p.y <= high + 1e-4f)
                    Add(p, radius);
            }

            if (section.points.Count < 2) {
                section.points.Clear();
                section.radii.Clear();
            }
        }
    }
}
