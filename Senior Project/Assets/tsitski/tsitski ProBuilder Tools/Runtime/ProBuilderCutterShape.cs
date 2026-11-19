using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public enum ProBuilderCutterShape {
        Box = 0,
        Cylinder = 1,
        Polygon = 2,
        Explosion = 3,
        Path = 4,
        Mesh = 5
    }

    public enum ProBuilderCutFaces {
        First = 0,
        Second = 1,
        Shared = 2,
        Self = 3
    }

    public enum ProBuilderCutMode {
        CutProBuilder = 0,
        CutMesh = 1,
        CutBoth = 2
    }

    public struct CutterShapeSettings {
        public ProBuilderCutterShape shape;
        public BoxCorners cornerRadius;
        public int cornerSegments;
        public int cylinderSegments;
        public int explosionSeed;
        public int explosionSpikes;
        public int explosionMinorSpikes;
        public float explosionJaggedness;
        public List<Vector2> polygonPoints;
        public List<float> polygonRadii;
        public Vector3 size;

        public static CutterShapeSettings From(TsitskiProBuilderCutter c) => new() {
            shape = c.shape, cornerRadius = c.cornerRadius, cornerSegments = c.cornerSegments, cylinderSegments = c.cylinderSegments,
            explosionSeed = c.explosionSeed, explosionSpikes = c.explosionSpikes, explosionMinorSpikes = c.explosionMinorSpikes,
            explosionJaggedness = c.explosionJaggedness, polygonPoints = c.polygonPoints, polygonRadii = c.polygonRadii, size = c.size
        };

        public static CutterShapeSettings From(TsitskiProBuilderCutterStats c) => new() {
            shape = c.shape, cornerRadius = c.cornerRadius, cornerSegments = c.cornerSegments, cylinderSegments = c.cylinderSegments,
            explosionSeed = c.explosionSeed, explosionSpikes = c.explosionSpikes, explosionMinorSpikes = c.explosionMinorSpikes,
            explosionJaggedness = c.explosionJaggedness, polygonPoints = c.polygonPoints, polygonRadii = c.polygonRadii, size = c.size
        };
    }

    public static class CutterOutline {
        private static readonly Vector2[] Square = {
            new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f)
        };

        public static List<Vector2> Build(TsitskiProBuilderCutter cutter) => Build(CutterShapeSettings.From(cutter));

        public static List<Vector2> Build(TsitskiProBuilderCutterStats stats) => Build(CutterShapeSettings.From(stats));

        public static List<Vector2> Build(CutterShapeSettings cutter) {
            List<Vector2> outline = new();

            switch (cutter.shape) {
                case ProBuilderCutterShape.Cylinder:
                    int segments = Mathf.Max(3, cutter.cylinderSegments);
                    for (int i = 0; i < segments; i++) {
                        float angle = i * Mathf.PI * 2f / segments;
                        outline.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f);
                    }
                    break;
                case ProBuilderCutterShape.Explosion:
                    Explosion(cutter, outline);
                    break;
                case ProBuilderCutterShape.Path:
                case ProBuilderCutterShape.Mesh:
                    return outline;
                case ProBuilderCutterShape.Polygon:
                    if (cutter.polygonPoints != null)
                        foreach (Vector2 point in RoundedPolygon(cutter))
                            if (outline.Count == 0 || (outline[^1] - point).sqrMagnitude > 1e-10f)
                                outline.Add(new Vector2(Mathf.Clamp(point.x, -0.5f, 0.5f), Mathf.Clamp(point.y, -0.5f, 0.5f)));
                    if (outline.Count > 1 && (outline[0] - outline[^1]).sqrMagnitude <= 1e-10f)
                        outline.RemoveAt(outline.Count - 1);
                    break;
                default:
                    if (cutter.cornerRadius.any)
                        RoundedBox(cutter, outline);
                    else
                        outline.AddRange(Square);
                    break;
            }

            for (int i = 0; outline.Count >= 3 && i < outline.Count;) {
                Vector2 a = outline[(i + outline.Count - 1) % outline.Count];
                Vector2 c = outline[(i + 1) % outline.Count];
                if (Mathf.Abs(Turn(a, outline[i], c)) < 1e-8f)
                    outline.RemoveAt(i);
                else
                    i++;
            }

            if (outline.Count < 3 || Mathf.Abs(SignedArea(outline)) < 1e-8f)
                return new List<Vector2>();

            if (SignedArea(outline) < 0f)
                outline.Reverse();

            return outline;
        }

        private static void RoundedBox(CutterShapeSettings cutter, List<Vector2> outline) {
            float width = Mathf.Abs(cutter.size.x);
            float height = Mathf.Abs(cutter.size.y);
            float limit = Mathf.Min(width, height) * 0.5f;
            int segments = Mathf.Max(1, cutter.cornerSegments);
            BoxCorners corners = cutter.cornerRadius;

            Corner(new Vector2(0.5f, -0.5f), corners.bottomRight, -90f);
            Corner(new Vector2(0.5f, 0.5f), corners.topRight, 0f);
            Corner(new Vector2(-0.5f, 0.5f), corners.topLeft, 90f);
            Corner(new Vector2(-0.5f, -0.5f), corners.bottomLeft, 180f);

            void Corner(Vector2 corner, float radius, float startAngle) {
                radius = Mathf.Clamp(radius, 0f, limit);
                if (radius <= 0f || width <= 0f || height <= 0f) {
                    outline.Add(corner);
                    return;
                }

                Vector2 radii = new(radius / width, radius / height);
                Vector2 arcCenter = corner - new Vector2(Mathf.Sign(corner.x) * radii.x, Mathf.Sign(corner.y) * radii.y);

                for (int i = 0; i <= segments; i++) {
                    float angle = (startAngle + 90f * i / segments) * Mathf.Deg2Rad;
                    outline.Add(arcCenter + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radii));
                }
            }
        }

        private static void Explosion(CutterShapeSettings cutter, List<Vector2> outline) {
            System.Random random = new(cutter.explosionSeed);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            int spikes = Mathf.Max(3, cutter.explosionSpikes);
            float jaggedness = Mathf.Clamp01(cutter.explosionJaggedness);
            float step = Mathf.PI * 2f / spikes;
            float startAngle = Range(0f, step);
            List<(float angle, float radius)> points = new();

            for (int i = 0; i < spikes; i++) {
                float tipAngle = startAngle + i * step + Range(-0.3f, 0.3f) * step * jaggedness;
                bool crack = random.NextDouble() < 0.25 * jaggedness;
                float tip = crack ? Range(0.92f, 1f) : Range(Mathf.Lerp(0.85f, 0.6f, jaggedness), 0.95f);
                float valleyAngle = tipAngle + step * Range(0.4f, 0.6f);
                float valley = Range(Mathf.Lerp(0.75f, 0.3f, jaggedness), Mathf.Lerp(0.85f, 0.55f, jaggedness));

                points.Add((tipAngle, tip));

                int jitters = Mathf.RoundToInt(jaggedness * 2f);
                for (int j = 1; j <= jitters; j++) {
                    float t = (float)j / (jitters + 1);
                    float noise = Range(-0.12f, 0.12f) * jaggedness;
                    points.Add((Mathf.Lerp(tipAngle, valleyAngle, t), Mathf.Lerp(tip, valley, t) + noise));
                }

                points.Add((valleyAngle, valley));
            }

            points = points.Select(p => (Mathf.Repeat(p.angle, Mathf.PI * 2f), p.radius)).ToList();
            points.Sort((a, b) => a.angle.CompareTo(b.angle));

            List<(float angle, float radius)> main = new();
            foreach ((float angle, float radius) point in points)
                if (main.Count == 0 || point.angle - main[^1].angle >= 1e-3f)
                    main.Add(point);

            System.Random minorRandom = new(unchecked(cutter.explosionSeed * 31 + 17));
            float MinorRange(float min, float max) => min + (float)minorRandom.NextDouble() * (max - min);
            int minorPerSpike = Mathf.Max(0, cutter.explosionMinorSpikes);
            float minorHeight = Mathf.Lerp(0.04f, 0.12f, jaggedness);

            for (int i = 0; i < main.Count; i++) {
                (float angle, float radius) from = main[i];
                (float angle, float radius) to = main[(i + 1) % main.Count];
                float span = Mathf.Repeat(to.angle - from.angle, Mathf.PI * 2f);

                AddPolar(from.angle, from.radius);

                float share = minorPerSpike * span / step;
                int count = Mathf.FloorToInt(share + (float)minorRandom.NextDouble());
                for (int m = 0; m < count; m++) {
                    float t = (m + MinorRange(0.25f, 0.45f)) / count;
                    float dip = (m + MinorRange(0.6f, 0.85f)) / count;
                    float height = minorHeight * MinorRange(0.5f, 1f);
                    AddPolar(from.angle + span * t, Mathf.Lerp(from.radius, to.radius, t) + height);
                    AddPolar(from.angle + span * dip, Mathf.Lerp(from.radius, to.radius, dip) - height * 0.5f);
                }
            }

            void AddPolar(float angle, float radius) {
                float r = Mathf.Clamp(radius, 0.1f, 1f) * 0.5f;
                outline.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }

        private static List<Vector2> RoundedPolygon(CutterShapeSettings cutter) {
            Vector2 scale = new(Mathf.Max(Mathf.Abs(cutter.size.x), 1e-6f), Mathf.Max(Mathf.Abs(cutter.size.y), 1e-6f));
            List<Vector2> local = cutter.polygonPoints.Select(p => Vector2.Scale(p, scale)).ToList();
            List<Vector2> rounded = PointRounding.Round(local, cutter.polygonRadii, true, cutter.cornerSegments);
            return rounded.Select(p => new Vector2(p.x / scale.x, p.y / scale.y)).ToList();
        }

        public static float SignedArea(IReadOnlyList<Vector2> outline) {
            float area = 0f;
            for (int i = 0; i < outline.Count; i++) {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }

        public static List<List<Vector2>> ConvexPieces(List<Vector2> outline) {
            List<List<Vector2>> pieces = new();
            if (outline.Count < 3)
                return pieces;

            if (IsConvex(outline)) {
                pieces.Add(outline);
                return pieces;
            }

            List<Vector2> remaining = new(outline);
            while (remaining.Count > 3) {
                bool clipped = false;

                for (int i = 0; i < remaining.Count; i++) {
                    Vector2 a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    Vector2 b = remaining[i];
                    Vector2 c = remaining[(i + 1) % remaining.Count];

                    if (Turn(a, b, c) <= 1e-9f || ContainsAny(remaining, a, b, c))
                        continue;

                    pieces.Add(new List<Vector2> { a, b, c });
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                    break;
            }

            if (remaining.Count == 3 || IsConvex(remaining))
                pieces.Add(remaining);
            else
                for (int i = 1; i < remaining.Count - 1; i++)
                    pieces.Add(new List<Vector2> { remaining[0], remaining[i], remaining[i + 1] });

            return pieces;
        }

        private static float Turn(Vector2 a, Vector2 b, Vector2 c) {
            Vector2 ab = b - a;
            Vector2 bc = c - b;
            return ab.x * bc.y - ab.y * bc.x;
        }

        private static bool IsConvex(List<Vector2> outline) {
            for (int i = 0; i < outline.Count; i++)
                if (Turn(outline[i], outline[(i + 1) % outline.Count], outline[(i + 2) % outline.Count]) < -1e-9f)
                    return false;
            return true;
        }

        private static bool ContainsAny(List<Vector2> points, Vector2 a, Vector2 b, Vector2 c) {
            foreach (Vector2 p in points) {
                if (p == a || p == b || p == c)
                    continue;

                if (Turn(a, b, p) >= 0f && Turn(b, c, p) >= 0f && Turn(c, a, p) >= 0f)
                    return true;
            }

            return false;
        }

        public static Vector3 Outward(Vector2 a, Vector2 b) => new(b.y - a.y, a.x - b.x, 0f);
    }
}
