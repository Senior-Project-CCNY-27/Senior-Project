using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Tsitski.Core {
    public interface ILightmapUvSource {
        bool Owns(MeshFilter meshFilter);
        bool TryGenerate(MeshFilter meshFilter);
    }

    // Baked lighting reads a second UV set whose charts sit inside 0-1 and do not overlap. A mesh
    // built by combining prefab pieces inherits each piece's own uv2, so every piece lands on the
    // same lightmap texels and the bake comes out as mush. Where there is no uv2 at all the
    // lightmapper falls back to uv, which for a tiled material is worse still. Everything that
    // generates a mesh meant to take baked light goes through here.
    public static class LightmapUvUtilities {
        public static void PrepareForBakedLighting(GameObject gameObject, MeshRenderer meshRenderer, Mesh mesh,
            bool hasLightmapUvs = false) {
            SetStaticForBakedLighting(gameObject);

#if UNITY_EDITOR
            if (meshRenderer && meshRenderer.receiveGI != ReceiveGI.Lightmaps)
                meshRenderer.receiveGI = ReceiveGI.Lightmaps;
#endif

            if (!hasLightmapUvs)
                GenerateLightmapUvs(mesh, gameObject);
        }

        private const float AtlasMargin = 4f / 1024f;

        public static bool TryPackLightmapUvs(IEnumerable<List<CombineInstance>> lists) {
            Dictionary<(Mesh, Matrix4x4), int> tileIndices = new();
            List<float> tileSides = new();
            List<(List<CombineInstance> list, int index, int tile)> users = new();
            Dictionary<Mesh, float> meshAreas = new();

            foreach (List<CombineInstance> list in lists) {
                for (int i = 0; i < list.Count; i++) {
                    Mesh mesh = list[i].mesh;

                    if (!mesh || !mesh.isReadable || !mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                        return false;

                    if (!tileIndices.TryGetValue((mesh, list[i].transform), out int tile)) {
                        if (!meshAreas.TryGetValue(mesh, out float area))
                            meshAreas[mesh] = area = SurfaceArea(mesh);

                        float scale = Mathf.Pow(Mathf.Abs(list[i].transform.determinant), 2f / 3f);

                        tile = tileSides.Count;
                        tileSides.Add(Mathf.Sqrt(Mathf.Max(area * scale, 0.000001f)));
                        tileIndices[(mesh, list[i].transform)] = tile;
                    }

                    users.Add((list, i, tile));
                }
            }

            if (tileSides.Count == 0)
                return false;

            List<int> order = new();
            for (int i = 0; i < tileSides.Count; i++)
                order.Add(i);

            order.Sort((a, b) => tileSides[b].CompareTo(tileSides[a]));

            List<float> sides = order.ConvertAll(i => tileSides[i]);
            float low = 0;
            float high = 1f / sides[0];

            for (int iteration = 0; iteration < 24; iteration++) {
                float mid = (low + high) * 0.5f;

                if (ShelfPack(sides, mid, null))
                    low = mid;
                else
                    high = mid;
            }

            Vector4[] packed = new Vector4[sides.Count];

            if (low <= 0 || !ShelfPack(sides, low, packed))
                return false;

            Vector4[] rects = new Vector4[sides.Count];
            for (int i = 0; i < order.Count; i++)
                rects[order[i]] = packed[i];

            foreach ((List<CombineInstance> list, int index, int tile) in users) {
                CombineInstance instance = list[index];
                instance.lightmapScaleOffset = rects[tile];
                list[index] = instance;
            }

            return true;
        }

        private static bool ShelfPack(List<float> sides, float scale, Vector4[] rects) {
            float x = 0;
            float y = 0;
            float rowHeight = 0;

            for (int i = 0; i < sides.Count; i++) {
                float size = sides[i] * scale;
                float cell = size + AtlasMargin;

                if (x + cell > 1f && x > 0) {
                    y += rowHeight;
                    x = 0;
                    rowHeight = 0;
                }

                if (x + cell > 1f || y + cell > 1f)
                    return false;

                if (rects != null)
                    rects[i] = new Vector4(size, size, x + AtlasMargin * 0.5f, y + AtlasMargin * 0.5f);

                x += cell;
                rowHeight = Mathf.Max(rowHeight, cell);
            }

            return true;
        }

        private static float SurfaceArea(Mesh mesh) {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float area = 0;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
                area += Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * 0.5f;

            return area;
        }

        // Every static flag but batching. Static batching swaps the object's mesh for a shared
        // "Combined Mesh (root: scene)" that is not readable, which stops anything cloning it at
        // runtime. Setting GameObject.isStatic instead would turn that on too.
        public static void SetStaticForBakedLighting(GameObject gameObject) {
#if UNITY_EDITOR
            if (!gameObject || Application.isPlaying)
                return;

            StaticEditorFlags flags = StaticEditorFlags.ContributeGI |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
#if !UNITY_2022_2_OR_NEWER
            flags |= StaticEditorFlags.NavigationStatic | StaticEditorFlags.OffMeshLinkGeneration;
#endif

            if (GameObjectUtility.GetStaticEditorFlags(gameObject) != flags)
                GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
#endif
        }

        private static readonly List<ILightmapUvSource> Sources = new();

        public static void RegisterSource(ILightmapUvSource source) {
            if (source != null && !Sources.Contains(source))
                Sources.Add(source);
        }

        public static void GenerateLightmapUvs(Mesh mesh, Object context = null) {
#if UNITY_EDITOR
            if (!CanUnwrap(mesh, context))
                return;

            UnwrapParam.SetDefaults(out UnwrapParam unwrapParam);

            // Wider than the default 4/1024. These meshes are a lot of separate pieces, so they
            // pack into many small charts, and tight margins let them bleed into each other.
            unwrapParam.packMargin = PackMargin;

            Unwrapping.GenerateSecondaryUVSet(mesh, unwrapParam);
#endif
        }

#if UNITY_EDITOR
        private const float PackMargin = 8f / 1024f;

        // Unwrapping is not linear in vertex count. Past this it locks the editor up for long
        // enough to look like a hang, so it says so rather than doing it.
        private const int MaxUnwrapVertices = 250000;

        [MenuItem("Tools/tsitski/Core/Generate Lightmap UVs", false, 300)]
        private static void GenerateAllLightmapUvsMenu() {
            int count = GenerateAllLightmapUvs();

            Debug.Log($"Lightmap UVs: generated for {count} mesh{(count == 1 ? "" : "es")}.");
        }

        public static int GenerateAllLightmapUvs() {
            List<MeshFilter> meshFilters = FindMeshFilters(IsGenerated);
            HashSet<Mesh> done = new();
            int count = 0;

            try {
                for (int i = 0; i < meshFilters.Count; i++) {
                    MeshFilter meshFilter = meshFilters[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Generate Lightmap UVs", meshFilter.sharedMesh.name, (float)i / meshFilters.Count))
                        break;

                    if (done.Add(meshFilter.sharedMesh) && Generate(meshFilter))
                        count++;
                }
            }
            finally {
                EditorUtility.ClearProgressBar();
            }

            return count;
        }

        public static bool IsGenerated(MeshFilter meshFilter) {
            Mesh mesh = meshFilter ? meshFilter.sharedMesh : null;
            if (!mesh || EditorUtility.IsPersistent(mesh) ||
                !GameObjectUtility.AreStaticEditorFlagsSet(meshFilter.gameObject, StaticEditorFlags.ContributeGI))
                return false;

            if (Sources.Any(source => source.Owns(meshFilter)))
                return true;

            Transform parent = meshFilter.transform.parent;
            return HasTsitskiComponent(meshFilter.gameObject) || parent && HasTsitskiComponent(parent.gameObject);
        }

        private static bool HasTsitskiComponent(GameObject gameObject) {
            foreach (MonoBehaviour behaviour in gameObject.GetComponents<MonoBehaviour>()) {
                string space = behaviour ? behaviour.GetType().Namespace : null;
                if (space != null && space.StartsWith("Tsitski"))
                    return true;
            }

            return false;
        }

        public static bool Generate(MeshFilter meshFilter) {
            if (!Sources.Any(source => source.TryGenerate(meshFilter)))
                GenerateLightmapUvs(meshFilter.sharedMesh, meshFilter);

            if (meshFilter.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(meshFilter.gameObject.scene);

            return true;
        }

        public static List<MeshFilter> FindMeshFilters(System.Func<MeshFilter, bool> include) {
            List<MeshFilter> found = new();

            for (int i = 0; i < SceneManager.sceneCount; i++) {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
                        if (include(meshFilter))
                            found.Add(meshFilter);
            }

            return found;
        }

        [MenuItem("CONTEXT/MeshFilter/Generate Lightmap UVs")]
        private static void GenerateContextMenu(MenuCommand command) => Generate((MeshFilter)command.context);

        [MenuItem("CONTEXT/MeshFilter/Generate Lightmap UVs", true)]
        private static bool GenerateContextMenuValidate(MenuCommand command) =>
            command.context is MeshFilter meshFilter && meshFilter.sharedMesh && !EditorUtility.IsPersistent(meshFilter.sharedMesh);

        private static bool CanUnwrap(Mesh mesh, Object context) {
            if (!mesh || Application.isPlaying)
                return false;

            if (mesh.vertexCount == 0 || mesh.subMeshCount == 0)
                return false;

            if (!mesh.isReadable) {
                Debug.LogWarning($"Lightmap UVs: \"{mesh.name}\" is not readable, so it cannot be unwrapped.",
                    context);

                return false;
            }

            // Unwrapping only understands triangles, and throws on anything else. Checked before
            // mesh.triangles is touched, which errors out on a mesh that is not triangles either.
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                    return false;

            if (mesh.triangles.Length < 3)
                return false;

            if (mesh.vertexCount > MaxUnwrapVertices) {
                Debug.LogWarning($"Lightmap UVs: \"{mesh.name}\" has {mesh.vertexCount} vertices, past the " +
                                 $"{MaxUnwrapVertices} this will unwrap. Baked lighting on it will be wrong " +
                                 "until it is split up or given lightmap UVs by hand.", context);

                return false;
            }

            return true;
        }
#endif
    }
}
