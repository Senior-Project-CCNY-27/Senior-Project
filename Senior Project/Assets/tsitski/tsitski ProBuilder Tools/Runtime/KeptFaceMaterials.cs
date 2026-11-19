using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using Math = UnityEngine.ProBuilder.Math;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public class KeptFaceMaterials {
        private const float CellSize = 0.5f;
        private const float MinNormalDot = 0.7f;

        private readonly Material[] _materials;
        private readonly List<(Vector3 center, Vector3 normal, int submesh)> _faces = new();
        private readonly Dictionary<Vector3Int, List<int>> _cells = new();
        private readonly int _common;

        private KeptFaceMaterials(ProBuilderMesh mesh, Material[] materials) {
            _materials = materials;
            IList<Vector3> positions = mesh.positions;

            foreach (Face face in mesh.faces) {
                Vector3 center = Center(positions, face);
                _faces.Add((center, Math.Normal(mesh, face), face.submeshIndex));

                Vector3Int key = Cell(center);
                if (!_cells.TryGetValue(key, out List<int> list))
                    _cells[key] = list = new List<int>();
                list.Add(_faces.Count - 1);
            }

            _common = _faces.GroupBy(f => f.submesh).OrderByDescending(g => g.Count()).First().Key;
        }

        public static KeptFaceMaterials Capture(ProBuilderMesh mesh) {
            if (!mesh || mesh.faceCount == 0 || !mesh.TryGetComponent(out MeshRenderer renderer))
                return null;

            Material[] materials = renderer.sharedMaterials;
            return materials.Length > 1 || mesh.faces.Any(f => f.submeshIndex != 0) ? new KeptFaceMaterials(mesh, materials) : null;
        }

        public void Apply(ProBuilderMesh mesh) {
            IList<Vector3> positions = mesh.positions;

            foreach (Face face in mesh.faces)
                face.submeshIndex = Mathf.Min(Match(Center(positions, face), Math.Normal(mesh, face)), _materials.Length - 1);

            if (mesh.TryGetComponent(out MeshRenderer renderer))
                renderer.sharedMaterials = _materials;
        }

        private int Match(Vector3 center, Vector3 normal) {
            Vector3Int cell = Cell(center);
            float best = float.MaxValue;
            int submesh = -1;

            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++) {
                if (!_cells.TryGetValue(cell + new Vector3Int(x, y, z), out List<int> list))
                    continue;

                foreach (int index in list) {
                    (Vector3 oldCenter, Vector3 oldNormal, int oldSubmesh) = _faces[index];
                    float dot = Vector3.Dot(oldNormal, normal);
                    if (dot < MinNormalDot)
                        continue;

                    float score = (oldCenter - center).sqrMagnitude + 1f - dot;
                    if (score >= best)
                        continue;

                    best = score;
                    submesh = oldSubmesh;
                }
            }

            return submesh >= 0 ? submesh : Nearest(center, normal);
        }

        private int Nearest(Vector3 center, Vector3 normal) {
            float best = float.MaxValue;
            int submesh = _common;

            foreach ((Vector3 oldCenter, Vector3 oldNormal, int oldSubmesh) in _faces) {
                if (Vector3.Dot(oldNormal, normal) < MinNormalDot)
                    continue;

                float distance = (oldCenter - center).sqrMagnitude;
                if (distance >= best)
                    continue;

                best = distance;
                submesh = oldSubmesh;
            }

            return submesh;
        }

        private static Vector3 Center(IList<Vector3> positions, Face face) {
            Vector3 center = Vector3.zero;
            foreach (int index in face.distinctIndexes)
                center += positions[index];
            return center / face.distinctIndexes.Count;
        }

        private static Vector3Int Cell(Vector3 point) => Vector3Int.FloorToInt(point / CellSize);
    }
}
