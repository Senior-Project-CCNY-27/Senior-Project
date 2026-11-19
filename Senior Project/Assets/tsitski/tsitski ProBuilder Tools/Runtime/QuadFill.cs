using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal static class QuadFill {
        private const double Tolerance = 1e-4;
        private const double AxisTolerance = 1e-3;
        private const double AngleEpsilon = 1e-6;

        public static List<Vector2[]> Pieces(List<D2> outline, List<List<D2>> holes) {
            List<D2> clean = Clean(outline);
            if (clean.Count < 3)
                return null;

            if (holes.Count > 0)
                return Rectangles(clean, holes.Select(Clean).Where(h => h.Count >= 3).ToList());

            if (SignedArea(clean) < 0)
                clean.Reverse();

            List<List<(D2 origin, D2 direction)>[]> options = ReflexOptions(clean);
            int[] choice = new int[options.Count];
            List<Vector2[]> best = Build(clean, options, choice);

            for (int v = 0; v < options.Count; v++)
            for (int o = 1; o < options[v].Length; o++) {
                int previous = choice[v];
                choice[v] = o;
                List<Vector2[]> candidate = Build(clean, options, choice);
                if (Cost(candidate) < Cost(best))
                    best = candidate;
                else
                    choice[v] = previous;
            }

            return best;
        }

        private static List<Vector2[]> Build(List<D2> loop, List<List<(D2 origin, D2 direction)>[]> options, int[] choice) {
            List<List<D2>> pieces = new() { new List<D2>(loop) };
            for (int v = 0; v < options.Count; v++)
                foreach ((D2 origin, D2 direction) in options[v][choice[v]])
                    Cut(pieces, origin, direction);

            List<Vector2[]> result = new();
            foreach (List<D2> piece in pieces) {
                List<D2> corners = Clean(piece);
                if (corners.Count < 3)
                    continue;

                for (int i = 1; i + 2 < corners.Count; i += 2)
                    result.Add(new[] { corners[0], corners[i], corners[i + 1], corners[i + 2] }.Select(ToVector).ToArray());
                if ((corners.Count - 2) % 2 == 1)
                    result.Add(new[] { corners[0], corners[^2], corners[^1] }.Select(ToVector).ToArray());
            }

            return result;
        }

        private static int Cost(List<Vector2[]> pieces) => pieces.Count(p => p.Length != 4);

        private static List<List<(D2 origin, D2 direction)>[]> ReflexOptions(List<D2> loop) {
            double frame = MainAxis(loop);
            List<List<(D2, D2)>[]> options = new();

            for (int i = 0; i < loop.Count; i++) {
                D2 previous = loop[(i + loop.Count - 1) % loop.Count], current = loop[i], next = loop[(i + 1) % loop.Count];
                if (Cross(current - previous, next - current) >= -AngleEpsilon)
                    continue;

                D2 incoming = Normalize(current - previous), outgoing = Normalize(current - next);
                double start = Angle(incoming), span = Wrap(Angle(outgoing) - start);

                List<(D2, D2)> square = new();
                for (int k = 0; k < 4; k++) {
                    double axis = frame + k * Math.PI * 0.5;
                    if (Wrap(axis - start + AxisTolerance) <= span + AxisTolerance * 2)
                        square.Add((current, new D2(Math.Cos(axis), Math.Sin(axis))));
                }

                List<(D2, D2)> edges = new() { (current, incoming), (current, outgoing) };
                if (square.Count == 0) {
                    options.Add(new[] { edges });
                    continue;
                }

                if (square.Count > 1) {
                    options.Add(square.Select(ray => new List<(D2, D2)> { ray }).Prepend(square).ToArray());
                    continue;
                }

                D2 axisDirection = square[0].Item2;
                D2 farther = Dot(axisDirection, incoming) < Dot(axisDirection, outgoing) ? incoming : outgoing;
                options.Add(new[] { square, new List<(D2, D2)> { square[0], (current, farther) }, edges });
            }

            return options;
        }

        private static double MainAxis(List<D2> loop) {
            double best = -1, angle = 0;
            for (int i = 0; i < loop.Count; i++) {
                D2 edge = loop[(i + 1) % loop.Count] - loop[i];
                double length = Length(edge);
                if (length <= best)
                    continue;

                best = length;
                angle = Angle(edge);
            }

            return angle;
        }

        private static void Cut(List<List<D2>> pieces, D2 origin, D2 direction) {
            for (int p = 0; p < pieces.Count; p++) {
                List<D2> piece = pieces[p];
                int k = piece.FindIndex(v => Distance(v, origin) < Tolerance);
                if (k < 0 || !PointsInside(piece, k, direction))
                    continue;

                double nearest = double.MaxValue;
                int hitEdge = -1;
                for (int j = 0; j < piece.Count; j++) {
                    int next = (j + 1) % piece.Count;
                    if (j == k || next == k)
                        continue;

                    if (RayHit(origin, direction, piece[j], piece[next], out double t) && t > Tolerance && t < nearest) {
                        nearest = t;
                        hitEdge = j;
                    }
                }

                if (hitEdge < 0)
                    return;

                D2 hit = origin + direction * nearest;
                int end = hitEdge;
                if (Distance(hit, piece[hitEdge]) >= Tolerance) {
                    int after = (hitEdge + 1) % piece.Count;
                    if (Distance(hit, piece[after]) < Tolerance) {
                        end = after;
                    }
                    else {
                        piece.Insert(hitEdge + 1, hit);
                        end = hitEdge + 1;
                        if (k > hitEdge)
                            k++;
                    }
                }

                pieces[p] = Walk(piece, k, end);
                pieces.Add(Walk(piece, end, k));
                return;
            }
        }

        private static List<D2> Walk(List<D2> loop, int from, int to) {
            List<D2> result = new();
            for (int i = from;; i = (i + 1) % loop.Count) {
                result.Add(loop[i]);
                if (i == to)
                    return result;
            }
        }

        private static bool PointsInside(List<D2> loop, int index, D2 direction) {
            D2 previous = loop[(index + loop.Count - 1) % loop.Count], current = loop[index], next = loop[(index + 1) % loop.Count];
            double outgoing = Angle(next - current);
            double interior = Wrap(Angle(previous - current) - outgoing);
            double ray = Wrap(Angle(direction) - outgoing);
            return ray > AngleEpsilon && ray < interior - AngleEpsilon;
        }

        private static bool RayHit(D2 origin, D2 direction, D2 a, D2 b, out double t) {
            D2 edge = b - a;
            double denominator = Cross(direction, edge);
            t = 0;
            if (Math.Abs(denominator) < 1e-12)
                return false;

            D2 offset = a - origin;
            t = Cross(offset, edge) / denominator;
            double u = Cross(offset, direction) / denominator;
            return u >= -1e-9 && u <= 1 + 1e-9;
        }

        private static List<D2> Clean(List<D2> loop) {
            List<D2> points = new(loop);
            bool removed = true;
            while (removed && points.Count >= 3) {
                removed = false;
                for (int i = 0; i < points.Count && points.Count >= 3; i++) {
                    D2 previous = points[(i + points.Count - 1) % points.Count], current = points[i], next = points[(i + 1) % points.Count];
                    D2 a = current - previous, b = next - current;
                    double scale = Math.Max(Length(a) * Length(b), 1e-12);
                    if (Distance(current, previous) >= Tolerance && Math.Abs(Cross(a, b)) / scale > AngleEpsilon)
                        continue;

                    points.RemoveAt(i);
                    removed = true;
                    i--;
                }
            }

            return points;
        }

        private static double SignedArea(List<D2> loop) {
            double area = 0;
            for (int i = 0; i < loop.Count; i++)
                area += Cross(loop[i], loop[(i + 1) % loop.Count]);
            return area * 0.5;
        }

        private static double Cross(D2 a, D2 b) => a.x * b.y - a.y * b.x;
        private static double Dot(D2 a, D2 b) => a.x * b.x + a.y * b.y;
        private static double Length(D2 a) => Math.Sqrt(a.x * a.x + a.y * a.y);
        private static double Distance(D2 a, D2 b) => Length(a - b);
        private static D2 Normalize(D2 a) => a * (1.0 / Math.Max(Length(a), 1e-12));
        private static double Angle(D2 a) => Math.Atan2(a.y, a.x);
        private static double Wrap(double angle) => (angle % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2);

        private static List<Vector2[]> Rectangles(List<D2> outline, List<List<D2>> holes) {
            List<List<D2>> loops = new() { outline };
            loops.AddRange(holes);

            double angle = FrameAngle(outline);
            if (double.IsNaN(angle))
                return null;

            double cos = Math.Cos(-angle), sin = Math.Sin(-angle);
            List<List<D2>> rotated = loops.Select(loop => loop.Select(p => Rotate(p, cos, sin)).ToList()).ToList();

            foreach (List<D2> loop in rotated)
                for (int i = 0; i < loop.Count; i++) {
                    D2 d = loop[(i + 1) % loop.Count] - loop[i];
                    if (Math.Abs(d.x) > AxisTolerance && Math.Abs(d.y) > AxisTolerance)
                        return null;
                }

            List<double> xs = Coordinates(rotated.SelectMany(l => l).Select(p => p.x));
            List<double> ys = Coordinates(rotated.SelectMany(l => l).Select(p => p.y));
            int columns = xs.Count - 1, rows = ys.Count - 1;
            if (columns < 1 || rows < 1)
                return null;

            HashSet<(int, int)> corners = new();
            foreach (D2 p in rotated.SelectMany(l => l))
                corners.Add((Index(xs, p.x), Index(ys, p.y)));

            bool[,] inside = new bool[columns, rows];
            for (int i = 0; i < columns; i++)
            for (int j = 0; j < rows; j++) {
                D2 center = new((xs[i] + xs[i + 1]) * 0.5, (ys[j] + ys[j + 1]) * 0.5);
                inside[i, j] = Contains(rotated[0], center) && !rotated.Skip(1).Any(hole => Contains(hole, center));
            }

            List<(int x0, int x1, int y0, int y1)> strips = new();
            for (int j = 0; j < rows; j++) {
                int i = 0;
                while (i < columns) {
                    if (!inside[i, j]) {
                        i++;
                        continue;
                    }

                    int start = i++;
                    while (i < columns && inside[i, j] && !corners.Contains((i, j)) && !corners.Contains((i, j + 1)))
                        i++;
                    strips.Add((start, i, j, j + 1));
                }
            }

            List<(int x0, int x1, int y0, int y1)> rectangles = new();
            foreach ((int x0, int x1, int y0, int y1) strip in strips) {
                int index = rectangles.FindIndex(r => r.x0 == strip.x0 && r.x1 == strip.x1 && r.y1 == strip.y0 &&
                                                      !corners.Contains((strip.x0, strip.y0)) && !corners.Contains((strip.x1, strip.y0)));
                if (index >= 0)
                    rectangles[index] = (strip.x0, strip.x1, rectangles[index].y0, strip.y1);
                else
                    rectangles.Add(strip);
            }

            double backCos = Math.Cos(angle), backSin = Math.Sin(angle);
            return rectangles.Select(r => new[] {
                ToVector(Rotate(new D2(xs[r.x0], ys[r.y0]), backCos, backSin)),
                ToVector(Rotate(new D2(xs[r.x1], ys[r.y0]), backCos, backSin)),
                ToVector(Rotate(new D2(xs[r.x1], ys[r.y1]), backCos, backSin)),
                ToVector(Rotate(new D2(xs[r.x0], ys[r.y1]), backCos, backSin))
            }).ToList();
        }

        private static double FrameAngle(List<D2> loop) {
            for (int i = 0; i < loop.Count; i++) {
                D2 d = loop[(i + 1) % loop.Count] - loop[i];
                if (Math.Abs(d.x) + Math.Abs(d.y) > Tolerance)
                    return Math.Atan2(d.y, d.x) % (Math.PI * 0.5);
            }

            return double.NaN;
        }

        private static List<double> Coordinates(IEnumerable<double> values) {
            List<double> result = new();
            foreach (double value in values.OrderBy(v => v))
                if (result.Count == 0 || value - result[^1] > AxisTolerance)
                    result.Add(value);
            return result;
        }

        private static int Index(List<double> coordinates, double value) {
            int best = 0;
            for (int i = 1; i < coordinates.Count; i++)
                if (Math.Abs(coordinates[i] - value) < Math.Abs(coordinates[best] - value))
                    best = i;
            return best;
        }

        private static bool Contains(List<D2> loop, D2 point) {
            bool inside = false;
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++) {
                D2 a = loop[i], b = loop[j];
                if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }

        private static D2 Rotate(D2 p, double cos, double sin) => new(p.x * cos - p.y * sin, p.x * sin + p.y * cos);

        private static Vector2 ToVector(D2 p) => new((float)p.x, (float)p.y);
    }
}
