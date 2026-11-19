using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using Math = UnityEngine.ProBuilder.Math;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [System.Serializable]
    internal class InsetFacesOperation : MeshSceneOperation {
        private const string MenuPath = "Tools/tsitski/ProBuilder/Modelling/Inset Faces &i";
        private const float MinMitre = 0.2f;

        [SerializeField] private float _insetWidth = 0.1f;
        [SerializeField] private bool _individual;
        private float _maxWidth = 1f;

        protected override string title => "Inset Faces";
        protected override int fieldCount => 2;

        [MenuItem(MenuPath)]
        private static void Inset() => Run(new InsetFacesOperation());

        [MenuItem(MenuPath, true)]
        private static bool InsetValidate() => ProBuilderEditor.selectMode == SelectMode.Face && MeshSelection.selectedFaceCount > 0;

        protected override bool Accepts(ProBuilderMesh mesh) => mesh.selectedFaceCount > 0;

        protected override void Prepare() {
            float largest = 0f;
            foreach (Target target in targets)
            foreach (int face in target.faces) {
                IList<Vector3> positions = target.mesh.positions;
                IList<int> indexes = target.mesh.faces[face].distinctIndexes;
                Bounds bounds = new(positions[indexes[0]], Vector3.zero);
                foreach (int index in indexes)
                    bounds.Encapsulate(positions[index]);
                largest = Mathf.Max(largest, bounds.size.magnitude);
            }

            _maxWidth = largest > 0f ? largest * 0.5f : 1f;
            _insetWidth = Mathf.Clamp(_insetWidth, 0f, _maxWidth);
        }

        protected override void DrawFields() {
            _insetWidth = EditorGUILayout.Slider("Inset Width", _insetWidth, 0f, _maxWidth);
            _individual = EditorGUILayout.Toggle("Individual", _individual);
        }

        protected override void ApplyTo(Target target) {
            ProBuilderMesh mesh = target.mesh;
            Face[] faces = target.faces.Select(i => mesh.faces[i]).ToArray();
            mesh.Extrude(faces, _individual ? ExtrudeMethod.IndividualFaces : ExtrudeMethod.FaceNormal, 0f);
            InsetBoundary(mesh, faces, _insetWidth, _individual);
            mesh.SetSelectedFaces(faces);
        }

        private static void InsetBoundary(ProBuilderMesh mesh, Face[] faces, float width, bool individual) {
            if (width <= 0f)
                return;

            Dictionary<int, int> shared = SharedLookup(mesh);
            IList<SharedVertex> groups = mesh.sharedVertices;
            List<Vector3> positions = mesh.positions.ToList();
            IEnumerable<Face[]> regions = individual ? faces.Select(f => new[] { f }) : new[] { faces };
            Dictionary<int, Vector3> offsets = new();

            foreach (Face[] region in regions) {
                Dictionary<(int, int), int> edgeCount = new();
                foreach (Face face in region)
                foreach (Edge edge in face.edges) {
                    (int, int) key = Key(shared[edge.a], shared[edge.b]);
                    edgeCount[key] = edgeCount.TryGetValue(key, out int count) ? count + 1 : 1;
                }

                Dictionary<int, List<Vector3>> inward = new();
                foreach (Face face in region) {
                    Vector3 normal = Math.Normal(mesh, face);
                    Vector3 centroid = Vector3.zero;
                    foreach (int index in face.distinctIndexes)
                        centroid += positions[index];
                    centroid /= face.distinctIndexes.Count;

                    foreach (Edge edge in face.edges) {
                        int a = shared[edge.a], b = shared[edge.b];
                        if (edgeCount[Key(a, b)] != 1)
                            continue;

                        Vector3 pa = positions[edge.a], pb = positions[edge.b];
                        Vector3 direction = Vector3.Cross(normal, pb - pa).normalized;
                        if (Vector3.Dot(direction, centroid - pa) < 0f)
                            direction = -direction;

                        foreach (int vertex in new[] { a, b }) {
                            if (!inward.TryGetValue(vertex, out List<Vector3> list))
                                inward[vertex] = list = new List<Vector3>();
                            list.Add(direction);
                        }
                    }
                }

                foreach ((int vertex, List<Vector3> directions) in inward) {
                    Vector3 sum = directions.Aggregate(Vector3.zero, (total, d) => total + d);
                    if (sum.sqrMagnitude < 1e-10f)
                        continue;

                    Vector3 direction = sum.normalized;
                    float mitre = Mathf.Max(directions.Min(d => Vector3.Dot(direction, d)), MinMitre);
                    offsets[vertex] = direction * (width / mitre);
                }
            }

            foreach ((int vertex, Vector3 offset) in offsets)
            foreach (int index in groups[vertex])
                positions[index] += offset;

            mesh.positions = positions;
        }
    }
}
