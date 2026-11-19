using System.Collections.Generic;
using Tsitski.Core;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal class LightmapUvs : ILightmapUvSource {
        static LightmapUvs() => LightmapUvUtilities.RegisterSource(new LightmapUvs());

        public bool Owns(MeshFilter meshFilter) => meshFilter.TryGetComponent(out ProBuilderMesh _);

        public bool TryGenerate(MeshFilter meshFilter) {
            if (!meshFilter.TryGetComponent(out ProBuilderMesh mesh))
                return false;

            if (TsitskiProBuilderCutTarget.IsResult(meshFilter))
                meshFilter.transform.parent.GetComponent<TsitskiProBuilderCutTarget>().GenerateLightmapUvs();
            else if (mesh.TryGetComponent(out TsitskiProBuilderStairs stairs))
                stairs.GenerateLightmapUvs();
            else if (mesh.TryGetComponent(out TsitskiProBuilderPath path))
                path.GenerateLightmapUvs();
            else
                mesh.Optimize(true);
            return true;
        }

        public static bool Wanted(ProBuilderMesh mesh) =>
            mesh && GameObjectUtility.AreStaticEditorFlagsSet(mesh.gameObject, StaticEditorFlags.ContributeGI);

        public static bool Missing(ProBuilderMesh mesh) {
            if (!Wanted(mesh) || !mesh.TryGetComponent(out MeshFilter filter))
                return false;

            Mesh compiled = filter.sharedMesh;
            return compiled && compiled.vertexCount > 0 && !compiled.HasVertexAttribute(VertexAttribute.TexCoord1);
        }

        public static void ToMeshKeeping(ProBuilderMesh mesh) {
            Mesh before = mesh.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            List<Vector2> uv2 = new();
            if (before && before.HasVertexAttribute(VertexAttribute.TexCoord1))
                before.GetUVs(1, uv2);

            mesh.ToMesh();

            Mesh after = filter ? filter.sharedMesh : null;
            if (after && uv2.Count > 0 && uv2.Count == after.vertexCount)
                after.SetUVs(1, uv2);
        }

        public static int RegenerateMissing() {
            int regenerated = 0;
            foreach (ProBuilderMesh mesh in UnityCompat.FindAll<ProBuilderMesh>(true)) {
                if (!mesh.gameObject.scene.IsValid() || !Missing(mesh))
                    continue;

                mesh.Optimize(true);
                EditorSceneManager.MarkSceneDirty(mesh.gameObject.scene);
                regenerated++;
            }

            return regenerated;
        }
    }
}
