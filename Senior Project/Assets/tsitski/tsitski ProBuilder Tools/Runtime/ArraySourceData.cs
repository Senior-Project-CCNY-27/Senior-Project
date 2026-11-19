using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public struct ArrayFaceData {
        public int[] indexes;
        public int smoothingGroup;
        public int submeshIndex;
        public bool manualUV;
        public AutoUnwrapSettings uv;
    }

    [Serializable]
    public class ArraySourceData {
        public List<Vector3> positions = new();
        public List<Vector2> uvs = new();
        public List<ArrayFaceData> faces = new();
        public List<Material> materials = new();

        public bool hasData => positions.Count > 0 && faces.Count > 0;

        public static ArraySourceData Capture(ProBuilderMesh mesh) {
            ArraySourceData data = new();
            if (!mesh)
                return data;

            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, mesh.transform.localRotation, mesh.transform.localScale);
            bool flip = matrix.determinant < 0f;
            data.positions = mesh.positions.Select(p => matrix.MultiplyPoint3x4(p)).ToList();

            IList<Vector2> textures = mesh.textures;
            data.uvs = textures != null && textures.Count == data.positions.Count
                ? textures.ToList()
                : Enumerable.Repeat(Vector2.zero, data.positions.Count).ToList();

            foreach (Face face in mesh.faces) {
                int[] indexes = face.indexes.ToArray();
                if (flip)
                    for (int i = 0; i < indexes.Length; i += 3)
                        (indexes[i + 1], indexes[i + 2]) = (indexes[i + 2], indexes[i + 1]);

                data.faces.Add(new ArrayFaceData {
                    indexes = indexes,
                    smoothingGroup = face.smoothingGroup,
                    submeshIndex = face.submeshIndex,
                    manualUV = face.manualUV,
                    uv = face.uv
                });
            }

            if (mesh.TryGetComponent(out MeshRenderer renderer))
                data.materials = renderer.sharedMaterials.ToList();

            return data;
        }

        public void AppendTo(Matrix4x4 placement, List<Vector3> outPositions, List<Vector2> outUvs, List<Face> outFaces) {
            bool flip = placement.determinant < 0f;
            int start = outPositions.Count;

            foreach (Vector3 p in positions)
                outPositions.Add(placement.MultiplyPoint3x4(p));
            for (int i = 0; i < positions.Count; i++)
                outUvs.Add(i < uvs.Count ? uvs[i] : Vector2.zero);

            foreach (ArrayFaceData face in faces) {
                if (face.indexes == null)
                    continue;

                int[] indexes = face.indexes.Select(i => i + start).ToArray();
                if (flip)
                    for (int i = 0; i < indexes.Length; i += 3)
                        (indexes[i + 1], indexes[i + 2]) = (indexes[i + 2], indexes[i + 1]);

                outFaces.Add(new Face(indexes) {
                    smoothingGroup = face.smoothingGroup,
                    submeshIndex = face.submeshIndex,
                    manualUV = face.manualUV,
                    uv = face.uv
                });
            }
        }

        public void ApplyTo(ProBuilderMesh mesh) {
            List<Vector3> outPositions = new();
            List<Vector2> outUvs = new();
            List<Face> outFaces = new();
            AppendTo(Matrix4x4.identity, outPositions, outUvs, outFaces);

            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = Vector3.one;
            ManualUvs.Rebuild(mesh, outPositions, outFaces, outUvs);
            mesh.ToMesh();
            mesh.Refresh();

            if (mesh.TryGetComponent(out MeshRenderer renderer) && materials.Count > 0)
                renderer.sharedMaterials = materials.ToArray();
        }
    }
}
