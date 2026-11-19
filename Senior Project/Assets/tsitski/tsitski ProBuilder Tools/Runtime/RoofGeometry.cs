using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal readonly struct D2 {
        public readonly double x, y;

        public D2(double x, double y) {
            this.x = x;
            this.y = y;
        }

        public static D2 operator +(D2 a, D2 b) => new(a.x + b.x, a.y + b.y);
        public static D2 operator -(D2 a, D2 b) => new(a.x - b.x, a.y - b.y);
        public static D2 operator *(D2 a, double s) => new(a.x * s, a.y * s);

        public double Dot(D2 b) => x * b.x + y * b.y;
        public double Cross(D2 b) => x * b.y - y * b.x;
        public double Length => Math.Sqrt(x * x + y * y);
        public D2 Left => new(-y, x);

        public D2 Normalized {
            get {
                double length = Length;
                return length > 1e-12 ? new D2(x / length, y / length) : new D2(0, 0);
            }
        }

        public static D2 From(Vector2 v) => new(v.x, v.y);
        public Vector2 ToVector2() => new((float)x, (float)y);
    }

    internal sealed class RoofWing {
        public List<D2> centerline;
        public bool closed, gableStart, gableEnd;
        public double halfWidth, height;
    }

    internal sealed class RoofCurve {
        private const double MinStep = 1e-3;
        private const double MinSlope = 0.01;

        public readonly List<D2> points = new();

        public RoofCurve(IReadOnlyList<Vector2> source) {
            if (source is { Count: >= 2 }) {
                Vector2 first = source[0], last = source[^1];
                double width = last.x - first.x, height = last.y - first.y;
                if (width > 1e-5 && height > 1e-5)
                    foreach (Vector2 p in source) {
                        double x = (p.x - first.x) / width, y = (p.y - first.y) / height;
                        if (points.Count > 0) {
                            D2 previous = points[^1];
                            x = Math.Max(x, previous.x + MinStep);
                            y = Math.Max(y, previous.y + MinSlope * (x - previous.x));
                        }
                        points.Add(new D2(x, y));
                    }
            }

            if (points.Count < 2) {
                points.Clear();
                points.Add(new D2(0, 0));
                points.Add(new D2(1, 1));
            }
        }

        public IEnumerable<D2> inner => points.Skip(1).Take(points.Count - 2);

        private D2 last => points[^1];
        private double beyondRunPerRise => last.x / last.y;

        public double Height(double x) {
            if (x > last.x)
                return last.y + (x - last.x) / beyondRunPerRise;

            int i = 0;
            while (i < points.Count - 2 && x > points[i + 1].x)
                i++;
            D2 a = points[i], b = points[i + 1];
            return a.y + (b.y - a.y) * (x - a.x) / (b.x - a.x);
        }

        public double Inset(double y) {
            if (y > last.y)
                return last.x + (y - last.y) * beyondRunPerRise;

            int i = 0;
            while (i < points.Count - 2 && y > points[i + 1].y)
                i++;
            D2 a = points[i], b = points[i + 1];
            return a.x + (b.x - a.x) * (y - a.y) / (b.y - a.y);
        }

        public double RunPerRise(double y) {
            if (y >= last.y)
                return beyondRunPerRise;

            int i = 0;
            while (i < points.Count - 2 && y >= points[i + 1].y)
                i++;
            D2 a = points[i], b = points[i + 1];
            return (b.x - a.x) / (b.y - a.y);
        }
    }

    internal sealed class FootprintLoop {
        public List<D2> points = new();
        public List<int> wings = new();
        public List<bool> gables = new();
    }

    internal sealed class RoofFace {
        public D2 edgeStart, edgeEnd, normal;
        public int wing;
        public bool gable;
        public List<D2> polygon;
        public List<double> heights;

        public double Inset(D2 p) => (p - edgeStart).Dot(normal);
    }

    internal static class RoofGeometry {
        public const double Epsilon = 1e-4;
        private const long MaxMilliseconds = 250;
        private const double MiterLimit = 2.0;

        private sealed class Piece {
            public D2[] corners;
            public int wing;
            public bool[] gables;
        }

        public static List<FootprintLoop> Footprint(List<RoofWing> wings) {
            List<Piece> pieces = new();

            for (int w = 0; w < wings.Count; w++) {
                RoofWing wing = wings[w];
                List<D2> points = wing.centerline;
                double half = wing.halfWidth;
                if (half <= Epsilon || points == null || points.Count < 2)
                    continue;

                int segments = wing.closed ? points.Count : points.Count - 1;
                for (int i = 0; i < segments; i++) {
                    D2 a = points[i], b = points[(i + 1) % points.Count];
                    D2 normal = (b - a).Normalized.Left * half;
                    bool endGable = !wing.closed && wing.gableEnd && i == segments - 1;
                    bool startGable = !wing.closed && wing.gableStart && i == 0;
                    pieces.Add(new Piece {
                        corners = new[] { a - normal, b - normal, b + normal, a + normal }, wing = w,
                        gables = new[] { false, endGable, false, startGable }
                    });
                }

                int first = wing.closed ? 0 : 1, last = wing.closed ? points.Count : points.Count - 1;
                for (int i = first; i < last; i++) {
                    D2 vertex = points[i];
                    D2 incoming = (vertex - points[(i - 1 + points.Count) % points.Count]).Normalized;
                    D2 outgoing = (points[(i + 1) % points.Count] - vertex).Normalized;
                    double turn = incoming.Cross(outgoing);
                    if (Math.Abs(turn) < 1e-6)
                        continue;

                    double side = turn > 0 ? -half : half;
                    D2 c1 = vertex + incoming.Left * side, c2 = vertex + outgoing.Left * side;
                    D2 miter = c1 + incoming * ((c2 - c1).Cross(outgoing) / turn);
                    D2[] corners = (miter - vertex).Length <= half * MiterLimit ? new[] { vertex, c1, miter, c2 } : new[] { vertex, c1, c2 };
                    if (SignedArea(corners) < 0)
                        Array.Reverse(corners);
                    pieces.Add(new Piece { corners = corners, wing = w, gables = new bool[corners.Length] });
                }
            }

            pieces.RemoveAll(p => Math.Abs(SignedArea(p.corners)) < Epsilon * Epsilon);
            return Union(pieces);
        }

        public static double SignedArea(IReadOnlyList<D2> loop) {
            double area = 0;
            for (int i = 0; i < loop.Count; i++)
                area += loop[i].Cross(loop[(i + 1) % loop.Count]);
            return area * 0.5;
        }

        public static bool Contains(IReadOnlyList<D2> loop, D2 point) {
            bool inside = false;
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++) {
                D2 a = loop[i], b = loop[j];
                if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }

        public static List<List<FootprintLoop>> Regions(List<FootprintLoop> loops) {
            List<FootprintLoop> outers = loops.Where(l => SignedArea(l.points) > 0).OrderBy(l => SignedArea(l.points)).ToList();
            List<List<FootprintLoop>> regions = outers.Select(o => new List<FootprintLoop> { o }).ToList();

            foreach (FootprintLoop hole in loops.Where(l => SignedArea(l.points) < 0)) {
                D2 a = hole.points[0], b = hole.points[1];
                D2 probe = (a + b) * 0.5 + (b - a).Normalized.Left * (Epsilon * 10);
                int owner = outers.FindIndex(o => Contains(o.points, probe));
                if (owner >= 0)
                    regions[owner].Add(hole);
            }

            return regions;
        }

        private static List<FootprintLoop> Union(List<Piece> pieces) {
            List<(D2 a, D2 b, int wing, bool gable)> kept = new();
            List<double> cuts = new();

            for (int i = 0; i < pieces.Count; i++) {
                D2[] corners = pieces[i].corners;
                for (int e = 0; e < corners.Length; e++) {
                    D2 a = corners[e], b = corners[(e + 1) % corners.Length];
                    double length = (b - a).Length;
                    if (length < Epsilon)
                        continue;

                    D2 direction = (b - a) * (1 / length);
                    cuts.Clear();
                    cuts.Add(0);
                    cuts.Add(length);
                    for (int j = 0; j < pieces.Count; j++)
                        if (j != i)
                            for (int f = 0; f < pieces[j].corners.Length; f++)
                                AddCuts(a, direction, length, pieces[j].corners[f], pieces[j].corners[(f + 1) % pieces[j].corners.Length], cuts);

                    cuts.Sort();
                    for (int k = 0; k + 1 < cuts.Count; k++) {
                        if (cuts[k + 1] - cuts[k] < Epsilon)
                            continue;

                        D2 mid = a + direction * ((cuts[k] + cuts[k + 1]) * 0.5);
                        if (KeepBoundary(pieces, i, mid, direction))
                            kept.Add((a + direction * cuts[k], a + direction * cuts[k + 1], pieces[i].wing, pieces[i].gables[e]));
                    }
                }
            }

            return Chain(kept);
        }

        private static void AddCuts(D2 a, D2 direction, double length, D2 c, D2 d, List<double> cuts) {
            foreach (D2 q in new[] { c, d }) {
                if (Math.Abs(direction.Cross(q - a)) >= Epsilon)
                    continue;
                double t = direction.Dot(q - a);
                if (t > Epsilon && t < length - Epsilon)
                    cuts.Add(t);
            }

            D2 cd = d - c;
            double denominator = direction.Cross(cd);
            if (Math.Abs(denominator) < 1e-12)
                return;

            double along = (c - a).Cross(cd) / denominator;
            double other = (c - a).Cross(direction) / denominator;
            double slack = Epsilon / Math.Max(cd.Length, Epsilon);
            if (along > Epsilon && along < length - Epsilon && other >= -slack && other <= 1 + slack)
                cuts.Add(along);
        }

        private static bool KeepBoundary(List<Piece> pieces, int index, D2 mid, D2 direction) {
            for (int j = 0; j < pieces.Count; j++) {
                if (j == index)
                    continue;

                D2[] corners = pieces[j].corners;
                bool inside = true, onBoundary = false, sameDirection = false;
                for (int f = 0; f < corners.Length; f++) {
                    D2 c = corners[f], cd = corners[(f + 1) % corners.Length] - c;
                    double length = cd.Length;
                    if (length < 1e-12)
                        continue;

                    D2 unit = cd * (1 / length);
                    double side = unit.Cross(mid - c);
                    if (Math.Abs(side) < Epsilon) {
                        double t = unit.Dot(mid - c);
                        if (t > -Epsilon && t < length + Epsilon) {
                            onBoundary = true;
                            sameDirection = unit.Dot(direction) > 0;
                        }
                    }

                    if (side < Epsilon)
                        inside = false;
                }

                if (onBoundary) {
                    if (!sameDirection || j < index)
                        return false;
                    continue;
                }

                if (inside)
                    return false;
            }

            return true;
        }

        private static int Node(List<D2> nodes, D2 point) {
            for (int i = 0; i < nodes.Count; i++)
                if ((nodes[i] - point).Length < Epsilon)
                    return i;
            nodes.Add(point);
            return nodes.Count - 1;
        }

        private static List<FootprintLoop> Chain(List<(D2 a, D2 b, int wing, bool gable)> segments) {
            List<D2> nodes = new();
            List<(int from, int to, int wing, bool gable)> edges = new();
            HashSet<(int, int)> seen = new();
            foreach ((D2 a, D2 b, int wing, bool gable) in segments) {
                int from = Node(nodes, a), to = Node(nodes, b);
                if (from != to && seen.Add((from, to)))
                    edges.Add((from, to, wing, gable));
            }

            Dictionary<int, List<int>> outgoing = new();
            for (int i = 0; i < edges.Count; i++) {
                if (!outgoing.TryGetValue(edges[i].from, out List<int> list))
                    outgoing[edges[i].from] = list = new List<int>();
                list.Add(i);
            }

            bool[] used = new bool[edges.Count];
            List<FootprintLoop> loops = new();
            for (int start = 0; start < edges.Count; start++) {
                if (used[start])
                    continue;

                FootprintLoop loop = new();
                int edge = start;
                bool closed = false;
                while (true) {
                    used[edge] = true;
                    loop.points.Add(nodes[edges[edge].from]);
                    loop.wings.Add(edges[edge].wing);
                    loop.gables.Add(edges[edge].gable);
                    int at = edges[edge].to;
                    if (at == edges[start].from) {
                        closed = true;
                        break;
                    }

                    D2 incoming = (nodes[at] - nodes[edges[edge].from]).Normalized;
                    int next = -1;
                    double best = double.MaxValue;
                    if (outgoing.TryGetValue(at, out List<int> candidates))
                        foreach (int candidate in candidates) {
                            if (used[candidate])
                                continue;
                            D2 direction = (nodes[edges[candidate].to] - nodes[at]).Normalized;
                            double turn = Math.Atan2(incoming.Cross(direction), incoming.Dot(direction));
                            if (turn < best) {
                                best = turn;
                                next = candidate;
                            }
                        }

                    if (next < 0)
                        break;
                    edge = next;
                }

                if (!closed)
                    continue;

                SimplifyLoop(loop);
                if (loop.points.Count >= 3 && Math.Abs(SignedArea(loop.points)) > Epsilon * Epsilon)
                    loops.Add(loop);
            }

            return loops;
        }

        private static void SimplifyLoop(FootprintLoop loop) {
            List<D2> points = loop.points;
            bool changed = true;
            while (changed && points.Count >= 3) {
                changed = false;
                for (int i = 0; i < points.Count && points.Count >= 3; i++) {
                    int before = (i - 1 + points.Count) % points.Count;
                    D2 a = points[i] - points[before], b = points[(i + 1) % points.Count] - points[i];
                    bool duplicate = a.Length < Epsilon;
                    double turn = a.Normalized.Cross(b.Normalized);
                    bool straight = Math.Abs(turn) < 1e-7 && a.Dot(b) > 0 && loop.wings[before] == loop.wings[i] && loop.gables[before] == loop.gables[i];
                    bool spike = Math.Abs(turn) < 1e-7 && a.Dot(b) < 0;
                    if (!duplicate && !straight && !spike)
                        continue;

                    if (duplicate) {
                        loop.wings[before] = loop.wings[i];
                        loop.gables[before] = loop.gables[i];
                    }

                    points.RemoveAt(i);
                    loop.wings.RemoveAt(i);
                    loop.gables.RemoveAt(i);
                    changed = true;
                    i--;
                }
            }
        }

        public static List<D2> Simplify(List<D2> loop) {
            List<D2> points = new(loop);
            bool changed = true;
            while (changed && points.Count >= 3) {
                changed = false;
                for (int i = 0; i < points.Count && points.Count >= 3; i++) {
                    D2 a = points[i] - points[(i - 1 + points.Count) % points.Count], b = points[(i + 1) % points.Count] - points[i];
                    double turn = a.Normalized.Cross(b.Normalized);
                    if (a.Length >= Epsilon && Math.Abs(turn) >= 1e-7)
                        continue;

                    points.RemoveAt(i);
                    changed = true;
                    i--;
                }
            }

            return points;
        }

        public static List<RoofFace> Skeleton(List<FootprintLoop> region, List<RoofWing> wings, RoofCurve curve, out double maxHeight, out bool complete) =>
            new SkeletonBuilder(region, wings, curve).Build(out maxHeight, out complete);

        public static List<int> Triangulate(List<D2> polygon) {
            List<int> remaining = Enumerable.Range(0, polygon.Count).ToList();
            List<int> triangles = new();
            if (polygon.Count < 3)
                return triangles;
            if (SignedArea(polygon) < 0)
                remaining.Reverse();

            int guard = polygon.Count * polygon.Count + 10;
            while (remaining.Count > 3 && guard-- > 0) {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++) {
                    int a = remaining[(i - 1 + remaining.Count) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if ((polygon[b] - polygon[a]).Cross(polygon[c] - polygon[b]) <= 1e-12)
                        continue;

                    bool blocked = false;
                    foreach (int other in remaining) {
                        if (other == a || other == b || other == c || !InTriangle(polygon[other], polygon[a], polygon[b], polygon[c]))
                            continue;
                        blocked = true;
                        break;
                    }

                    if (blocked)
                        continue;

                    triangles.AddRange(new[] { a, b, c });
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (clipped)
                    continue;

                int collinear = -1;
                for (int i = 0; i < remaining.Count && collinear < 0; i++) {
                    D2 p = polygon[remaining[(i - 1 + remaining.Count) % remaining.Count]], n = polygon[remaining[(i + 1) % remaining.Count]];
                    if (Math.Abs((polygon[remaining[i]] - p).Cross(n - polygon[remaining[i]])) <= 1e-12)
                        collinear = i;
                }

                if (collinear >= 0) {
                    remaining.RemoveAt(collinear);
                    continue;
                }

                for (int i = 1; i + 1 < remaining.Count; i++)
                    triangles.AddRange(new[] { remaining[0], remaining[i], remaining[i + 1] });
                return triangles;
            }

            if (remaining.Count == 3)
                triangles.AddRange(remaining);
            return triangles;
        }

        private static bool InTriangle(D2 p, D2 a, D2 b, D2 c) =>
            (b - a).Cross(p - a) > 1e-12 && (c - b).Cross(p - b) > 1e-12 && (a - c).Cross(p - c) > 1e-12;

        private sealed class SkeletonBuilder {
            private sealed class Edge {
                public D2 start, end, direction, normal;
                public int wing;
                public bool gable;
                public double Distance(D2 p) => (p - start).Dot(normal);
            }

            private sealed class Vertex {
                public D2 point;
                public double time, rate;
                public Edge left, right;
                public D2 bisector;
                public bool reflex, valid = true;
                public Vertex previous, next;

                public D2 At(double time) => rate > 1e-12 ? point + bisector * ((time - this.time) / rate) : point;
            }

            private enum Kind {
                Edge,
                Split,
                Phase
            }

            private sealed class Event {
                public double time, jump;
                public long order;
                public Kind kind;
                public Vertex a, b;
                public Edge edge;
                public D2 point;
            }

            private readonly List<RoofWing> _wings;
            private readonly RoofCurve _curve;
            private readonly List<Edge> _edges = new();
            private readonly List<Vertex> _vertices = new();
            private readonly List<Vertex> _live = new();
            private readonly List<(D2 a, double ta, D2 b, double tb)> _arcs = new();
            private readonly List<Event> _heap = new();
            private long _order;
            private double _now;

            public SkeletonBuilder(List<FootprintLoop> region, List<RoofWing> wings, RoofCurve curve) {
                _wings = wings;
                _curve = curve;

                foreach (FootprintLoop loop in region) {
                    List<D2> points = loop.points;
                    List<Vertex> loopVertices = new();
                    List<Edge> loopEdges = new();
                    for (int i = 0; i < points.Count; i++) {
                        D2 a = points[i], b = points[(i + 1) % points.Count];
                        D2 direction = (b - a).Normalized;
                        loopEdges.Add(new Edge { start = a, end = b, direction = direction, normal = direction.Left, wing = loop.wings[i], gable = loop.gables[i] });
                    }

                    for (int i = 0; i < points.Count; i++)
                        loopVertices.Add(new Vertex { point = points[i], left = loopEdges[(i - 1 + points.Count) % points.Count], right = loopEdges[i] });

                    for (int i = 0; i < loopVertices.Count; i++) {
                        loopVertices[i].previous = loopVertices[(i - 1 + loopVertices.Count) % loopVertices.Count];
                        loopVertices[i].next = loopVertices[(i + 1) % loopVertices.Count];
                    }

                    _edges.AddRange(loopEdges);
                    _vertices.AddRange(loopVertices);
                }
            }

            public List<RoofFace> Build(out double maxHeight, out bool complete) {
                _live.AddRange(_vertices);
                foreach (Vertex vertex in _vertices)
                    SetBisector(vertex);
                foreach (Vertex vertex in _vertices.ToArray())
                    QueueEvents(vertex);

                HashSet<int> used = new(_edges.Select(e => e.wing));
                foreach (double phase in used.SelectMany(w => _curve.points.Skip(1).Select(p => p.y * _wings[w].height)).Distinct())
                    Push(new Event { time = phase, kind = Kind.Phase });

                System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                int guard = 20000 + _edges.Count * 500;
                while (_heap.Count > 0 && guard-- > 0 && clock.ElapsedMilliseconds < MaxMilliseconds) {
                    Event next = Pop();
                    switch (next.kind) {
                        case Kind.Split:
                            ProcessSplit(next);
                            break;
                        case Kind.Phase:
                            ProcessPhase(next.time);
                            break;
                        default:
                            ProcessEdge(next);
                            break;
                    }
                }

                complete = _heap.Count == 0;
                maxHeight = _now;
                return complete ? Faces() : new List<RoofFace>();
            }

            private double Speed(Edge edge, double time) {
                if (edge.gable)
                    return 0;

                RoofWing wing = _wings[edge.wing];
                return _curve.RunPerRise((time + 1e-7) / wing.height) * wing.halfWidth / wing.height;
            }

            private void SetBisector(Vertex vertex) {
                Edge left = vertex.left, right = vertex.right;
                double leftSpeed = Speed(left, vertex.time), rightSpeed = Speed(right, vertex.time);
                double determinant = left.normal.Cross(right.normal);
                D2 velocity;
                if (Math.Abs(determinant) > 1e-9)
                    velocity = new D2((leftSpeed * right.normal.y - rightSpeed * left.normal.y) / determinant,
                        (left.normal.x * rightSpeed - right.normal.x * leftSpeed) / determinant);
                else if (left.normal.Dot(right.normal) > 0)
                    velocity = left.normal * Math.Max(leftSpeed, rightSpeed);
                else
                    velocity = new D2(0, 0);

                double speed = velocity.Length;
                if (speed > 1e-9) {
                    vertex.bisector = velocity * (1 / speed);
                    vertex.rate = 1 / speed;
                }
                else {
                    vertex.bisector = right.direction;
                    vertex.rate = 0;
                }

                vertex.reflex = left.direction.Cross(right.direction) < -1e-9;
            }

            private void QueueEvents(Vertex vertex) {
                QueueEdgeEvent(vertex.previous, vertex);
                QueueEdgeEvent(vertex, vertex.next);
                if (!vertex.reflex)
                    return;

                foreach (Edge edge in _edges) {
                    if (edge == vertex.left || edge == vertex.right)
                        continue;

                    double speed = Speed(edge, vertex.time);
                    double closing = speed * vertex.rate - vertex.bisector.Dot(edge.normal);
                    if (closing <= 1e-9)
                        continue;

                    double offset = OffsetAt(edge, vertex.time);
                    double along = (edge.Distance(vertex.point) - offset) / closing;
                    if (along < -Epsilon)
                        continue;

                    along = Math.Max(0, along);
                    double time = vertex.time + along * vertex.rate;
                    if (time >= _now - Epsilon)
                        Push(new Event { time = time, kind = Kind.Split, a = vertex, edge = edge, point = vertex.point + vertex.bisector * along });
                }
            }

            private double OffsetAt(Edge edge, double time) {
                if (edge.gable)
                    return 0;

                RoofWing wing = _wings[edge.wing];
                return _curve.Inset(time / wing.height) * wing.halfWidth;
            }

            private void QueueEdgeEvent(Vertex a, Vertex b) {
                if (a == b || !a.valid || !b.valid)
                    return;

                double start = Math.Max(a.time, b.time);
                D2 startA = a.At(start), startB = b.At(start);
                if ((startA - startB).Length < Epsilon) {
                    Push(new Event { time = start, kind = Kind.Edge, a = a, b = b, point = (startA + startB) * 0.5 });
                    return;
                }

                D2 difference = b.point - a.point;
                double denominator = a.bisector.Cross(b.bisector);
                D2 point;
                double time;
                if (Math.Abs(denominator) < 1e-9) {
                    if (Math.Abs(a.bisector.Cross(difference)) > Epsilon || a.bisector.Dot(b.bisector) >= 0 || a.bisector.Dot(difference) < -Epsilon)
                        return;

                    double gap = a.bisector.Dot(difference);
                    if (a.rate > 1e-12 && b.rate > 1e-12) {
                        time = (gap + a.time / a.rate + b.time / b.rate) / (1 / a.rate + 1 / b.rate);
                        point = a.At(time);
                    }
                    else {
                        time = Math.Max(a.time, b.time);
                        point = a.rate > 1e-12 ? b.point : b.rate > 1e-12 ? a.point : (a.point + b.point) * 0.5;
                    }
                }
                else {
                    double alongA = Math.Max(0, difference.Cross(b.bisector) / denominator);
                    double alongB = Math.Max(0, difference.Cross(a.bisector) / denominator);
                    if (difference.Cross(b.bisector) / denominator < -Epsilon || difference.Cross(a.bisector) / denominator < -Epsilon)
                        return;
                    point = a.point + a.bisector * alongA;
                    time = a.rate >= b.rate ? a.time + alongA * a.rate : b.time + alongB * b.rate;
                }

                if (time >= _now - Epsilon)
                    Push(new Event { time = time, kind = Kind.Edge, a = a, b = b, point = point });
            }

            private void Arc(Vertex from, D2 point, double time) => _arcs.Add((from.point, from.time, point, time));

            private void ProcessPhase(double time) {
                _now = Math.Max(_now, time);
                List<Vertex> current = _live.Where(v => v.valid).ToList();
                Dictionary<Vertex, Vertex> replaced = new();
                foreach (Vertex vertex in current) {
                    D2 point = vertex.At(time);
                    Arc(vertex, point, time);
                    replaced[vertex] = new Vertex { point = point, time = time, left = vertex.left, right = vertex.right };
                    vertex.valid = false;
                }

                foreach (Vertex vertex in current) {
                    Vertex fresh = replaced[vertex];
                    fresh.previous = replaced.TryGetValue(vertex.previous, out Vertex p) ? p : vertex.previous;
                    fresh.next = replaced.TryGetValue(vertex.next, out Vertex n) ? n : vertex.next;
                }

                foreach (Vertex fresh in replaced.Values) {
                    _live.Add(fresh);
                    SetBisector(fresh);
                }

                foreach (Vertex fresh in replaced.Values)
                    QueueEvents(fresh);
            }

            private void ProcessEdge(Event e) {
                Vertex a = e.a, b = e.b;
                if (!a.valid || !b.valid || a.next != b)
                    return;

                _now = Math.Max(_now, e.time);
                if (a.previous == b) {
                    Arc(a, b.point, b.time);
                    a.valid = b.valid = false;
                    return;
                }

                if (a.previous == b.next) {
                    Vertex c = b.next;
                    Arc(a, e.point, e.time);
                    Arc(b, e.point, e.time);
                    Arc(c, e.point, e.time);
                    a.valid = b.valid = c.valid = false;
                    return;
                }

                Vertex merged = new() { point = e.point, time = e.time, left = a.left, right = b.right, previous = a.previous, next = b.next };
                a.previous.next = merged;
                b.next.previous = merged;
                Arc(a, e.point, e.time);
                Arc(b, e.point, e.time);
                a.valid = b.valid = false;
                Activate(merged);
            }

            private void ProcessSplit(Event e) {
                Vertex vertex = e.a;
                if (!vertex.valid)
                    return;

                Vertex opposite = _live.FirstOrDefault(x =>
                    x.valid && x != vertex && x.right == e.edge && x != vertex.previous && InEdgeRegion(x, e.point, e.time));

                if (opposite == null)
                    return;

                _now = Math.Max(_now, e.time);
                Vertex after = opposite.next;
                Vertex first = new() { point = e.point, time = e.time, left = vertex.left, right = e.edge, previous = vertex.previous, next = after };
                Vertex second = new() { point = e.point, time = e.time, left = e.edge, right = vertex.right, previous = opposite, next = vertex.next };
                vertex.previous.next = first;
                after.previous = first;
                opposite.next = second;
                vertex.next.previous = second;
                Arc(vertex, e.point, e.time);
                vertex.valid = false;

                Activate(first);
                Activate(second);
            }

            private static bool InEdgeRegion(Vertex start, D2 point, double time) {
                Vertex end = start.next;
                D2 from = start.At(time), to = end.At(time);
                D2 along = to - from;
                double length = along.Length;
                if (length < Epsilon)
                    return (point - from).Length < Epsilon * 10;

                double t = along.Dot(point - from) / (length * length);
                return t > -Epsilon / length && t < 1 + Epsilon / length;
            }

            private void Activate(Vertex vertex) {
                if (!vertex.valid)
                    return;

                _live.Add(vertex);

                if (vertex.next == vertex.previous) {
                    Vertex other = vertex.next;
                    Arc(other, vertex.point, vertex.time);
                    vertex.valid = other.valid = false;
                    return;
                }

                if (vertex.next == vertex) {
                    vertex.valid = false;
                    return;
                }

                SetBisector(vertex);
                QueueEvents(vertex);
            }

            private void Push(Event e) {
                e.order = _order++;
                if (e.a != null)
                    e.jump = (e.point - e.a.At(e.time)).Length + (e.b == null ? 0 : (e.point - e.b.At(e.time)).Length);
                _heap.Add(e);
                int i = _heap.Count - 1;
                while (i > 0) {
                    int parent = (i - 1) / 2;
                    if (!Less(_heap[i], _heap[parent]))
                        break;
                    (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
                    i = parent;
                }
            }

            private Event Pop() {
                Event top = _heap[0];
                _heap[0] = _heap[^1];
                _heap.RemoveAt(_heap.Count - 1);
                int i = 0;
                while (true) {
                    int left = i * 2 + 1, right = left + 1, smallest = i;
                    if (left < _heap.Count && Less(_heap[left], _heap[smallest]))
                        smallest = left;
                    if (right < _heap.Count && Less(_heap[right], _heap[smallest]))
                        smallest = right;
                    if (smallest == i)
                        break;
                    (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                    i = smallest;
                }

                return top;
            }

            private static bool Less(Event a, Event b) {
                if (Math.Abs(a.time - b.time) > 1e-7)
                    return a.time < b.time;
                if (Math.Abs(a.jump - b.jump) > 1e-6)
                    return a.jump < b.jump;
                if (a.kind != b.kind)
                    return a.kind < b.kind;
                return a.order < b.order;
            }

            private List<RoofFace> Faces() {
                List<D2> nodes = new();
                Dictionary<int, double> heights = new();
                HashSet<(int, int)> links = new();

                int TimedNode(D2 point, double time) {
                    int node = Node(nodes, point);
                    if (!heights.ContainsKey(node))
                        heights[node] = time;
                    return node;
                }

                void Link(int a, int b) {
                    if (a != b)
                        links.Add((Math.Min(a, b), Math.Max(a, b)));
                }

                List<(int from, int to, Edge edge)> boundary = _edges.Select(edge => (TimedNode(edge.start, 0), TimedNode(edge.end, 0), edge)).ToList();
                List<(int, int)> arcs = _arcs.Select(arc => (TimedNode(arc.a, arc.ta), TimedNode(arc.b, arc.tb))).ToList();

                foreach ((int from, int to, Edge edge) in boundary)
                    if (!edge.gable)
                        Link(from, to);
                foreach ((int a, int b) in arcs)
                    SplitLink(nodes, a, b, Link);

                List<int>[] neighbours = Enumerable.Range(0, nodes.Count).Select(_ => new List<int>()).ToArray();
                foreach ((int a, int b) in links) {
                    neighbours[a].Add(b);
                    neighbours[b].Add(a);
                }

                List<RoofFace> faces = new();
                foreach ((int from, int to, Edge edge) in boundary) {
                    if (edge.gable) {
                        faces.Add(GableFace(nodes, heights, edge));
                        continue;
                    }

                    List<D2> polygon = new() { nodes[from] };
                    int previous = from, current = to, steps = 0;
                    bool closed = false;
                    while (steps++ < nodes.Count * 2 + 4) {
                        if (current == from) {
                            closed = true;
                            break;
                        }

                        polygon.Add(nodes[current]);
                        int next = NextClockwise(nodes, neighbours[current], current, previous);
                        if (next < 0)
                            break;
                        previous = current;
                        current = next;
                    }

                    if (!closed)
                        continue;

                    polygon = Simplify(polygon);
                    if (polygon.Count >= 3 && SignedArea(polygon) > Epsilon * Epsilon)
                        faces.Add(new RoofFace { edgeStart = edge.start, edgeEnd = edge.end, normal = edge.normal, wing = edge.wing, polygon = polygon });
                }

                return faces;
            }

            private static RoofFace GableFace(List<D2> nodes, Dictionary<int, double> heights, Edge edge) {
                double length = (edge.end - edge.start).Length;
                List<(double along, int node)> onEdge = new();
                for (int i = 0; i < nodes.Count; i++) {
                    D2 offset = nodes[i] - edge.start;
                    double along = edge.direction.Dot(offset);
                    if (Math.Abs(edge.direction.Cross(offset)) < Epsilon && along > -Epsilon && along < length + Epsilon)
                        onEdge.Add((along, i));
                }

                onEdge.Sort((x, y) => x.along.CompareTo(y.along));
                return new RoofFace {
                    edgeStart = edge.start, edgeEnd = edge.end, normal = edge.normal, wing = edge.wing, gable = true,
                    polygon = onEdge.Select(n => nodes[n.node]).ToList(),
                    heights = onEdge.Select(n => heights.TryGetValue(n.node, out double h) ? h : 0).ToList()
                };
            }

            private static void SplitLink(List<D2> nodes, int a, int b, Action<int, int> link) {
                if (a == b)
                    return;

                D2 start = nodes[a], segment = nodes[b] - start;
                double length = segment.Length;
                D2 unit = segment * (1 / length);
                List<(double t, int node)> inner = new();
                for (int i = 0; i < nodes.Count; i++) {
                    if (i == a || i == b)
                        continue;
                    D2 offset = nodes[i] - start;
                    double t = unit.Dot(offset);
                    if (t > Epsilon && t < length - Epsilon && Math.Abs(unit.Cross(offset)) < Epsilon)
                        inner.Add((t, i));
                }

                int previous = a;
                foreach ((double _, int node) in inner.OrderBy(n => n.t)) {
                    link(previous, node);
                    previous = node;
                }

                link(previous, b);
            }

            private static int NextClockwise(List<D2> nodes, List<int> candidates, int current, int previous) {
                D2 back = nodes[previous] - nodes[current];
                double backAngle = Math.Atan2(back.y, back.x);
                int best = -1;
                double bestTurn = double.MaxValue;
                foreach (int candidate in candidates) {
                    D2 direction = nodes[candidate] - nodes[current];
                    double turn = backAngle - Math.Atan2(direction.y, direction.x);
                    while (turn <= 1e-9)
                        turn += Math.PI * 2;
                    if (candidate == previous)
                        turn = Math.PI * 4;
                    if (turn < bestTurn) {
                        bestTurn = turn;
                        best = candidate;
                    }
                }

                return best;
            }
        }
    }
}
