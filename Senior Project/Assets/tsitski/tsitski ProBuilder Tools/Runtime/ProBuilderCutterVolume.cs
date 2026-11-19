using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public struct CutterFace {
        public List<Vector3> corners;
        public int face;
    }

    public static class CutterVolume {
        private const float MinSize = 0.01f;
        private const float MinMitre = 0.25f;


        public static bool IsPath(TsitskiProBuilderCutter cutter) => cutter.shape == ProBuilderCutterShape.Path;

        public static bool IsMesh(TsitskiProBuilderCutter cutter) => cutter.shape == ProBuilderCutterShape.Mesh;

        public static int MeshHash(Mesh mesh) {
            if (!mesh)
                return 0;
            HashCode hash = new();
            hash.Add(mesh.subMeshCount);
            foreach (Vector3 vertex in mesh.vertices)
                hash.Add(vertex);
            for (int s = 0; s < mesh.subMeshCount; s++)
                foreach (int index in mesh.GetTriangles(s))
                    hash.Add(index);
            return hash.ToHashCode();
        }

        public static Bounds LocalBounds(TsitskiProBuilderCutter cutter) {
            if (IsMesh(cutter))
                return cutter.shapeMesh ? cutter.shapeMesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
            if (!IsPath(cutter))
                return new Bounds(Vector3.zero, Vector3.one);

            List<Vector3> points = PathPoints(cutter);
            if (points.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Bounds bounds = new(points[0], Vector3.zero);
            foreach (Vector3 point in points)
                bounds.Encapsulate(point);

            float reach = Mathf.Sqrt(HalfWidth(cutter) * HalfWidth(cutter) + HalfHeight(cutter) * HalfHeight(cutter)) * 2f;
            bounds.Expand(Vector3.one * reach);
            return bounds;
        }

        public static List<Vector3> PathPoints(TsitskiProBuilderCutter cutter) => PathPoints(cutter, out _);

        public static List<Vector3> PathPoints(TsitskiProBuilderCutter cutter, out List<Quaternion> rotations) {
            List<Vector3> points = new();
            rotations = new List<Quaternion>();
            if (cutter.pathPoints == null)
                return points;

            List<int> sources = new();
            List<Vector3> rounded = PointRounding.Round(cutter.pathPoints, cutter.pathRadii, false, Mathf.Max(1, cutter.cornerSegments), sources);
            for (int i = 0; i < rounded.Count; i++) {
                if (points.Count > 0 && (points[^1] - rounded[i]).sqrMagnitude <= 1e-10f)
                    continue;

                points.Add(rounded[i]);
                int source = sources[i];
                rotations.Add(cutter.pathRotations != null && source < cutter.pathRotations.Count ? cutter.pathRotations[source] : Quaternion.identity);
            }

            return points;
        }

        public static Vector3 SectionUp(Quaternion rotation) {
            Vector3 up = rotation * Vector3.up;
            return up.sqrMagnitude > 1e-10f ? up.normalized : Vector3.up;
        }

        private static float HalfWidth(TsitskiProBuilderCutter cutter) => Mathf.Max(MinSize, cutter.pathWidth) * 0.5f;

        private static float HalfHeight(TsitskiProBuilderCutter cutter) => Mathf.Max(MinSize, cutter.pathHeight) * 0.5f;

        public static List<Vector4[]> Cells(TsitskiProBuilderCutter cutter) {
            if (IsPath(cutter) || IsMesh(cutter))
                return PathTube(cutter).cells;

            List<Vector4[]> cells = new();
            foreach (List<Vector2> piece in CutterOutline.ConvexPieces(CutterOutline.Build(cutter))) {
                Vector4[] planes = new Vector4[piece.Count + 2];
                planes[0] = new Vector4(0f, 0f, 1f, 0.5f);
                planes[1] = new Vector4(0f, 0f, -1f, 0.5f);

                for (int i = 0; i < piece.Count; i++) {
                    Vector2 a = piece[i];
                    Vector3 normal = CutterOutline.Outward(a, piece[(i + 1) % piece.Count]);
                    planes[i + 2] = new Vector4(normal.x, normal.y, normal.z, Vector3.Dot(normal, new Vector3(a.x, a.y, 0f)));
                }

                cells.Add(planes);
            }

            return cells;
        }

        public static List<CutterFace> Faces(TsitskiProBuilderCutter cutter) {
            if (IsPath(cutter) || IsMesh(cutter))
                return PathFaces(cutter);

            List<CutterFace> faces = new();
            List<Vector2> outline = CutterOutline.Build(cutter);

            foreach (List<Vector2> piece in CutterOutline.ConvexPieces(outline)) {
                faces.Add(new CutterFace { corners = piece.Select(p => new Vector3(p.x, p.y, 0.5f)).ToList(), face = 4 });
                faces.Add(new CutterFace { corners = piece.Select(p => new Vector3(p.x, p.y, -0.5f)).Reverse().ToList(), face = 5 });
            }

            for (int i = 0; i < outline.Count; i++) {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Count];
                Vector3 outward = CutterOutline.Outward(a, b);
                int face = Mathf.Abs(outward.x) >= Mathf.Abs(outward.y)
                    ? outward.x > 0f ? 0 : 1
                    : outward.y > 0f ? 2 : 3;

                faces.Add(new CutterFace {
                    corners = new List<Vector3> { new(a.x, a.y, -0.5f), new(b.x, b.y, -0.5f), new(b.x, b.y, 0.5f), new(a.x, a.y, 0.5f) },
                    face = face
                });
            }

            return faces;
        }

        private static List<CutterFace> PathFaces(TsitskiProBuilderCutter cutter) => PathTube(cutter).faces;

        private static Tube PathTube(TsitskiProBuilderCutter cutter) {
            int hash = PathHash(cutter);
            if (cutter.pathTubeCache is Tube cached && cutter.pathFaceHash == hash)
                return cached;

            Tube tube = IsMesh(cutter) ? BuildMeshVolume(cutter.shapeMesh) : BuildTube(cutter);
            cutter.pathTubeCache = tube;
            cutter.pathFaceHash = hash;
            return tube;
        }

        private static int PathHash(TsitskiProBuilderCutter cutter) {
            HashCode hash = new();
            hash.Add((int)cutter.shape);
            hash.Add(MeshHash(cutter.shapeMesh));
            hash.Add(cutter.pathWidth);
            hash.Add(cutter.pathHeight);
            hash.Add(cutter.cornerSegments);
            if (cutter.pathPoints != null)
                foreach (Vector3 point in cutter.pathPoints)
                    hash.Add(point);
            if (cutter.pathRadii != null)
                foreach (float radius in cutter.pathRadii)
                    hash.Add(radius);
            if (cutter.pathRotations != null)
                foreach (Quaternion rotation in cutter.pathRotations)
                    hash.Add(rotation);
            return hash.ToHashCode();
        }

        internal class Tube {
            public readonly List<CutterFace> faces = new();
            public readonly List<Vector4[]> cells = new();
            public readonly List<Vector3[]> sections = new();
        }

        public static IReadOnlyList<Vector3[]> PathSections(TsitskiProBuilderCutter cutter) => PathTube(cutter).sections;

        private static Tube BuildTube(TsitskiProBuilderCutter cutter) {
            Tube tube = new();
            List<Vector3> points = PathPoints(cutter, out List<Quaternion> rotations);
            if (points.Count < 2)
                return tube;

            float halfWidth = HalfWidth(cutter), halfHeight = HalfHeight(cutter);
            int count = points.Count;
            Vector3[][] sections = new Vector3[count][];
            Vector3[] tangents = new Vector3[count];
            Vector3 previousUp = Vector3.up;

            for (int i = 0; i < count; i++) {
                Vector3 into = i > 0 ? (points[i] - points[i - 1]).normalized : Vector3.zero;
                Vector3 outOf = i < count - 1 ? (points[i + 1] - points[i]).normalized : Vector3.zero;
                Vector3 tangent = (into + outOf).sqrMagnitude > 1e-8f ? (into + outOf).normalized : (outOf != Vector3.zero ? outOf : into);
                tangents[i] = tangent;

                Vector3 up = Vector3.ProjectOnPlane(SectionUp(rotations[i]), tangent);
                if (up.sqrMagnitude < 1e-8f)
                    up = Vector3.ProjectOnPlane(previousUp, tangent);
                up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.Cross(tangent, Vector3.right).normalized;
                previousUp = up;
                Vector3 right = Vector3.Cross(up, tangent).normalized;

                Vector3 bend = into != Vector3.zero && outOf != Vector3.zero ? outOf - into : Vector3.zero;
                float stretch = 0f;
                if (bend.sqrMagnitude > 1e-8f) {
                    bend.Normalize();
                    float cosine = Mathf.Max(Vector3.Dot(into, tangent), MinMitre);
                    stretch = 1f / cosine - 1f;
                }

                Vector3 Offset(float x, float y) {
                    Vector3 offset = right * x + up * y;
                    return offset + bend * (Vector3.Dot(offset, bend) * stretch);
                }

                sections[i] = new[] {
                    points[i] + Offset(halfWidth, halfHeight),
                    points[i] + Offset(-halfWidth, halfHeight),
                    points[i] + Offset(-halfWidth, -halfHeight),
                    points[i] + Offset(halfWidth, -halfHeight)
                };
            }

            tube.sections.AddRange(sections);
            int[] sideFaces = { 2, 1, 3, 0 };
            for (int i = 0; i < count - 1; i++) {
                Vector3[] a = sections[i], b = sections[i + 1];
                Vector3 middle = (points[i] + points[i + 1]) * 0.5f;

                for (int k = 0; k < 4; k++) {
                    int next = (k + 1) % 4;
                    Vector3 outward = (a[k] + a[next] + b[k] + b[next]) * 0.25f - middle;
                    AddTriangle(tube, outward, sideFaces[k], a[k], a[next], b[next]);
                    AddTriangle(tube, outward, sideFaces[k], a[k], b[next], b[k]);
                }

                AddTetrahedra(tube, a, b);
            }

            AddCap(tube, -tangents[0], 5, sections[0]);
            AddCap(tube, tangents[count - 1], 4, sections[count - 1]);
            return tube;
        }

        private static Tube BuildMeshVolume(Mesh mesh) {
            Tube tube = new();
            if (!mesh)
                return tube;

            Vector3[] vertices = mesh.vertices;
            List<(Vector3 a, Vector3 b, Vector3 c, int part)> triangles = new();
            for (int part = 0; part < mesh.subMeshCount; part++) {
                int[] indices = mesh.GetTriangles(part);
                if (indices.Length < 12)
                    continue;

                Vector3 centroid = Vector3.zero;
                foreach (int index in indices)
                    centroid += vertices[index];
                centroid /= indices.Length;

                List<Vector4> planes = new();
                for (int t = 0; t + 2 < indices.Length; t += 3) {
                    Vector3 a = vertices[indices[t]], b = vertices[indices[t + 1]], c = vertices[indices[t + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-12f)
                        continue;
                    normal.Normalize();
                    if (Vector3.Dot(normal, a - centroid) < 0f) {
                        normal = -normal;
                        (b, c) = (c, b);
                    }

                    triangles.Add((a, b, c, part));
                    Vector4 plane = Plane(normal, Vector3.Dot(normal, a));
                    if (!planes.Any(p => Vector3.Dot(new Vector3(p.x, p.y, p.z), normal) > 0.99999f && Mathf.Abs(p.w - plane.w) < 1e-4f))
                        planes.Add(plane);
                }

                if (planes.Count >= 4 && Bounded(planes))
                    tube.cells.Add(planes.ToArray());
            }

            Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));
            string Shape((Vector3 a, Vector3 b, Vector3 c, int part) t) =>
                string.Join("|", new[] { Key(t.a), Key(t.b), Key(t.c) }.Select(k => $"{k.x},{k.y},{k.z}").OrderBy(k => k, StringComparer.Ordinal));

            Dictionary<string, HashSet<int>> owners = new();
            foreach (var t in triangles) {
                string shape = Shape(t);
                if (!owners.TryGetValue(shape, out HashSet<int> parts))
                    owners[shape] = parts = new HashSet<int>();
                parts.Add(t.part);
            }

            foreach (var t in triangles) {
                if (owners[Shape(t)].Count > 1)
                    continue;

                Vector3 normal = Vector3.Cross(t.b - t.a, t.c - t.a).normalized;
                Vector3 size = new(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                int face = size.x >= size.y && size.x >= size.z ? normal.x > 0f ? 0 : 1
                    : size.y >= size.z ? normal.y > 0f ? 2 : 3
                    : normal.z > 0f ? 4 : 5;
                tube.faces.Add(new CutterFace { corners = new List<Vector3> { t.a, t.b, t.c }, face = face });
            }

            return tube;
        }

        private static bool Bounded(List<Vector4> planes) {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++) {
                Vector3 direction = new(x, y, z);
                if (direction == Vector3.zero)
                    continue;
                direction.Normalize();
                if (!planes.Any(p => Vector3.Dot(new Vector3(p.x, p.y, p.z), direction) > 0.05f))
                    return false;
            }
            return true;
        }

        private static void AddTriangle(Tube tube, Vector3 outward, int face, Vector3 a, Vector3 b, Vector3 c) {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            tube.faces.Add(new CutterFace {
                corners = Vector3.Dot(normal, outward) < 0f ? new List<Vector3> { a, c, b } : new List<Vector3> { a, b, c },
                face = face
            });
        }

        private static void AddCap(Tube tube, Vector3 outward, int face, Vector3[] section) {
            AddTriangle(tube, outward, face, section[0], section[1], section[2]);
            AddTriangle(tube, outward, face, section[0], section[2], section[3]);
        }

        private static void AddTetrahedra(Tube tube, Vector3[] a, Vector3[] b) {
            AddTetrahedron(tube, a[0], a[1], a[2], b[2]);
            AddTetrahedron(tube, a[0], a[1], b[1], b[2]);
            AddTetrahedron(tube, a[0], b[0], b[1], b[2]);
            AddTetrahedron(tube, a[0], a[2], a[3], b[3]);
            AddTetrahedron(tube, a[0], a[2], b[2], b[3]);
            AddTetrahedron(tube, a[0], b[0], b[2], b[3]);
        }

        private static void AddTetrahedron(Tube tube, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) {
            Vector3 centroid = (p0 + p1 + p2 + p3) * 0.25f;
            Vector3[] corners = { p0, p1, p2, p3 };
            int[][] sides = { new[] { 0, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 3 } };
            Vector4[] planes = new Vector4[4];

            for (int f = 0; f < 4; f++) {
                Vector3 a = corners[sides[f][0]], b = corners[sides[f][1]], c = corners[sides[f][2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                    return;

                normal.Normalize();
                if (Vector3.Dot(normal, centroid - a) > 0f)
                    normal = -normal;
                planes[f] = Plane(normal, Vector3.Dot(normal, a));
            }

            tube.cells.Add(planes);
        }

        private static Vector4 Plane(Vector3 normal, float distance) => new(normal.x, normal.y, normal.z, distance);


        public static Vector3 Normal(IReadOnlyList<Vector3> corners) {
            Vector3 normal = Vector3.zero;
            for (int i = 2; i < corners.Count; i++)
                normal += Vector3.Cross(corners[i - 1] - corners[0], corners[i] - corners[0]);
            return normal;
        }

        public static List<Vector3> StartAtCorner(IReadOnlyList<Vector3> corners) {
            int best = 0;
            float bestArea = -1f;
            for (int i = 0; i < corners.Count; i++) {
                Vector3 a = corners[i], b = corners[(i + 1) % corners.Count], c = corners[(i + 2) % corners.Count];
                float area = Vector3.Cross(b - a, c - a).sqrMagnitude;
                if (area > bestArea) {
                    bestArea = area;
                    best = i;
                }
            }

            List<Vector3> rotated = new(corners.Count);
            for (int i = 0; i < corners.Count; i++)
                rotated.Add(corners[(best + i) % corners.Count]);
            return rotated;
        }

    }
}
