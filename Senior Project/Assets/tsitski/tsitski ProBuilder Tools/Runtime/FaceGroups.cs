using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal static class FaceGroups {
        private const float WeldPrecision = 1e4f;

        public static List<int> Merge(List<Vector3> positions, List<Face> faces, List<int> groups, Func<int, int, bool> canWeld = null,
            List<int> faceData = null) {
            List<Vector3> mergedPositions = new();
            List<int> kept = new();
            List<Face> mergedFaces = new();
            Dictionary<int, (Face template, List<int> indexes, Dictionary<Vector3Int, List<int>> welds)> open = new();
            List<int> order = new();
            List<int> sources = new();

            int Keep(int old) {
                kept.Add(old);
                mergedPositions.Add(positions[old]);
                return kept.Count - 1;
            }

            for (int f = 0; f < faces.Count; f++) {
                Face face = faces[f];
                int group = f < groups.Count ? groups[f] : -1;

                if (group < 0) {
                    Dictionary<int, int> own = new();
                    int[] indexes = new int[face.indexes.Count];
                    for (int i = 0; i < indexes.Length; i++) {
                        int old = face.indexes[i];
                        if (!own.TryGetValue(old, out int index))
                            own[old] = index = Keep(old);
                        indexes[i] = index;
                    }
                    mergedFaces.Add(Copy(face, indexes));
                    order.Add(-1);
                    sources.Add(f);
                    continue;
                }

                if (!open.TryGetValue(group, out var merged)) {
                    open[group] = merged = (face, new List<int>(), new Dictionary<Vector3Int, List<int>>());
                    mergedFaces.Add(null);
                    order.Add(group);
                    sources.Add(f);
                }

                foreach (int old in face.indexes) {
                    Vector3Int key = Vector3Int.RoundToInt(positions[old] * WeldPrecision);
                    if (!merged.welds.TryGetValue(key, out List<int> candidates))
                        merged.welds[key] = candidates = new List<int>();

                    int index = candidates.FindIndex(c => canWeld == null || canWeld(kept[c], old));
                    if (index < 0) {
                        candidates.Add(Keep(old));
                        index = candidates.Count - 1;
                    }
                    merged.indexes.Add(candidates[index]);
                }
            }

            for (int f = 0; f < mergedFaces.Count; f++)
                if (mergedFaces[f] == null) {
                    var merged = open[order[f]];
                    mergedFaces[f] = Copy(merged.template, merged.indexes.ToArray());
                }

            positions.Clear();
            positions.AddRange(mergedPositions);
            faces.Clear();
            faces.AddRange(mergedFaces);
            if (faceData != null) {
                List<int> mergedData = sources.Select(s => s < faceData.Count ? faceData[s] : -1).ToList();
                faceData.Clear();
                faceData.AddRange(mergedData);
            }
            return kept;
        }

        private static Face Copy(Face face, int[] indexes) => new(indexes) {
            smoothingGroup = face.smoothingGroup,
            submeshIndex = face.submeshIndex,
            manualUV = face.manualUV,
            uv = face.uv
        };
    }
}
