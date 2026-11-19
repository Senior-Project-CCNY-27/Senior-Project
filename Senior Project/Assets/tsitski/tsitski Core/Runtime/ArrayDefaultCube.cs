using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsitski.Core {
    internal static class ArrayDefaultCube {
        private const float Size = 0.25f;
        private const int CubeVertices = 24;

        private static readonly Vector3[] Normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        public static void Build(TsitskiArray array, List<ArrayPlacement> placements) {
            Clear(array);

            MeshFilter filter = GetOrAdd<MeshFilter>(array);
            MeshRenderer meshRenderer = GetOrAdd<MeshRenderer>(array);

            Mesh cube = CreateCube();
            CombineInstance[] instances = new CombineInstance[placements.Count];
            for (int i = 0; i < placements.Count; i++)
                instances[i] = new CombineInstance { mesh = cube, transform = placements[i].matrix };

            Mesh mesh = new() { name = "Array" };
            if (placements.Count * CubeVertices > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(instances, true, true);
            mesh.RecalculateTangents();
            Destroy(cube);

            array.generatedMesh = mesh;
            filter.sharedMesh = mesh;
            if (array.TryGetComponent(out MeshCollider meshCollider))
                meshCollider.sharedMesh = mesh;

            Material material = array.activeMaterial ? array.activeMaterial : TsitskiSettings.DefaultMaterial;
            if (material)
                meshRenderer.sharedMaterial = material;

            LightmapUvUtilities.PrepareForBakedLighting(array.gameObject, meshRenderer, mesh);
        }

        public static void Clear(TsitskiArray array) {
            if (!array.generatedMesh)
                return;

            if (array.TryGetComponent(out MeshFilter filter) && filter.sharedMesh == array.generatedMesh)
                filter.sharedMesh = null;
            if (array.TryGetComponent(out MeshCollider meshCollider) && meshCollider.sharedMesh == array.generatedMesh)
                meshCollider.sharedMesh = null;

            Destroy(array.generatedMesh);
            array.generatedMesh = null;
        }

        private static Mesh CreateCube() {
            Vector3[] vertices = new Vector3[CubeVertices], normals = new Vector3[CubeVertices];
            Vector2[] uvs = new Vector2[CubeVertices];
            int[] triangles = new int[36];

            for (int face = 0; face < Normals.Length; face++) {
                Vector3 normal = Normals[face];
                Vector3 u = normal.y != 0f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u);
                int start = face * 4;

                vertices[start] = (normal - u - v) * (Size * 0.5f);
                vertices[start + 1] = (normal + u - v) * (Size * 0.5f);
                vertices[start + 2] = (normal + u + v) * (Size * 0.5f);
                vertices[start + 3] = (normal - u + v) * (Size * 0.5f);
                uvs[start] = new Vector2(0f, 0f);
                uvs[start + 1] = new Vector2(1f, 0f);
                uvs[start + 2] = new Vector2(1f, 1f);
                uvs[start + 3] = new Vector2(0f, 1f);
                for (int i = 0; i < 4; i++)
                    normals[start + i] = normal;

                int index = face * 6;
                triangles[index] = start;
                triangles[index + 1] = start + 1;
                triangles[index + 2] = start + 2;
                triangles[index + 3] = start;
                triangles[index + 4] = start + 2;
                triangles[index + 5] = start + 3;
            }

            return new Mesh { vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
        }

        private static T GetOrAdd<T>(TsitskiArray array) where T : Component =>
            array.TryGetComponent(out T component) ? component : array.gameObject.AddComponent<T>();

        private static void Destroy(Object target) {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
