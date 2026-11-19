using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    public static class RealBlendSetup {
        private const string Define = "TSITSKI_REALBLEND";
        private const string StorageTypeName = "RealBlend.VertexPaintStorage";
        private const float KeptCellSize = 0.5f;

        private static Type _storageType;

        static RealBlendSetup() {
            _storageType = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(t => t.FullName == StorageTypeName);
            EditorApplication.delayCall += UpdateDefine;
        }

        private static void UpdateDefine() {
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            List<string> defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

            bool installed = _storageType != null;
            if (defines.Contains(Define) == installed)
                return;

            if (installed)
                defines.Add(Define);
            else
                defines.Remove(Define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

#if TSITSKI_REALBLEND
        [InitializeOnLoadMethod]
        private static void Subscribe() {
            TsitskiProBuilderCutTarget.ResultRebuilt += OnResultRebuilt;
            TsitskiArray.Rebuilt += array => {
                if (array.TryGetComponent(out ProBuilderMesh mesh))
                    OnResultRebuilt(mesh);
            };
            TsitskiProBuilderFloorPlan.Rebuilt += OnResultRebuilt;
            TsitskiProBuilderPillar.Rebuilt += OnResultRebuilt;
            TsitskiProBuilderPolygon.Rebuilt += OnResultRebuilt;
            TsitskiProBuilderRoof.Rebuilt += OnResultRebuilt;
            TsitskiProBuilderStairs.Rebuilt += OnResultRebuilt;
            TsitskiProBuilderTrim.Rebuilt += OnResultRebuilt;
            TsitskiProBuilderPath.Rebuilt += OnResultRebuilt;
        }

        private static MethodInfo setBaseTopology => _storageType?.GetMethod("SetBaseTopology", new[] { typeof(Mesh) });
        private static MethodInfo savePaintedColors => _storageType?.GetMethod("SavePaintedColors", new[] { typeof(Mesh), typeof(Color[]) });

        private static bool MissingColors(ProBuilderMesh mesh) => mesh.colors == null || mesh.colors.Count != mesh.vertexCount;

        internal static bool installed => setBaseTopology != null;

        internal static int SetupVertexPaint(IEnumerable<ProBuilderMesh> meshes, IReadOnlyDictionary<ProBuilderMesh, Dictionary<Face, Color>> layers) {
            if (!installed)
                return 0;

            int added = 0;

            foreach (ProBuilderMesh mesh in meshes) {
                if (!mesh.TryGetComponent(out MeshFilter _))
                    continue;

                layers.TryGetValue(mesh, out Dictionary<Face, Color> colors);

                if (mesh.GetComponent(_storageType) is Component existing) {
                    if (MissingColors(mesh) || colors != null) {
                        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { mesh, existing }, "Setup Materials");
                        Fill(mesh, existing, true, colors);
                    }
                    continue;
                }

                Undo.RegisterCompleteObjectUndo(mesh, "Setup Materials");
                Component storage = Undo.AddComponent(mesh.gameObject, _storageType);
                Fill(mesh, storage, true, colors);
                PrefabUtility.RecordPrefabInstancePropertyModifications(storage);
                added++;
            }

            return added;
        }

        private static void OnResultRebuilt(ProBuilderMesh result) {
            if (result && _storageType != null && result.GetComponent(_storageType) is Component storage)
                Fill(result, storage, MissingColors(result));
        }

        private static Color[] KeptColors(ProBuilderMesh mesh, Component storage) {
            Color[] kept = new Color[mesh.vertexCount];
            Mesh compiled = mesh.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] oldPositions = _storageType.GetField("currentVertices")?.GetValue(storage) as Vector3[];
            Vector3[] oldNormals = _storageType.GetField("currentNormals")?.GetValue(storage) as Vector3[];
            Color[] oldColors = _storageType.GetField("paintedColors")?.GetValue(storage) as Color[];

            if (!compiled || compiled.vertexCount != kept.Length || oldPositions == null || oldColors == null ||
                oldPositions.Length != oldColors.Length || oldColors.Length == 0)
                return kept;

            Vector3[] positions = compiled.vertices;
            Vector3[] normals = compiled.normals;
            bool hasNormals = oldNormals != null && oldNormals.Length == oldPositions.Length && normals.Length == positions.Length;

            if (oldPositions.SequenceEqual(positions)) {
                Array.Copy(oldColors, kept, kept.Length);
                return kept;
            }

            Color common = oldColors.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
            Dictionary<Vector3Int, List<int>> cells = new();
            for (int i = 0; i < oldPositions.Length; i++) {
                Vector3Int key = Vector3Int.FloorToInt(oldPositions[i] / KeptCellSize);
                if (!cells.TryGetValue(key, out List<int> list))
                    cells[key] = list = new List<int>();
                list.Add(i);
            }

            for (int i = 0; i < positions.Length; i++) {
                Vector3Int center = Vector3Int.FloorToInt(positions[i] / KeptCellSize);
                float best = float.MaxValue;
                kept[i] = common;

                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++) {
                    if (!cells.TryGetValue(center + new Vector3Int(x, y, z), out List<int> list))
                        continue;

                    foreach (int old in list) {
                        float score = (oldPositions[old] - positions[i]).sqrMagnitude;
                        if (hasNormals)
                            score += 1f - Vector3.Dot(oldNormals[old], normals[i]);
                        if (score >= best)
                            continue;

                        best = score;
                        kept[i] = oldColors[old];
                    }
                }
            }

            return kept;
        }

        private static void Fill(ProBuilderMesh mesh, Component storage, bool writeColors, IReadOnlyDictionary<Face, Color> layers = null) {
            if (writeColors) {
                Color[] colors = MissingColors(mesh) ? KeptColors(mesh, storage) : mesh.colors.ToArray();
                if (layers != null)
                    foreach (KeyValuePair<Face, Color> pair in layers)
                        foreach (int index in pair.Key.distinctIndexes)
                            colors[index] = pair.Value;

                mesh.colors = colors;
                LightmapUvs.ToMeshKeeping(mesh);
                mesh.Refresh();
            }

            Mesh compiled = mesh.GetComponent<MeshFilter>().sharedMesh;
            setBaseTopology?.Invoke(storage, new object[] { compiled });

            if (writeColors)
                savePaintedColors?.Invoke(storage, new object[] { compiled, compiled.colors });

            EditorUtility.SetDirty(storage);
        }

#endif
    }

}
