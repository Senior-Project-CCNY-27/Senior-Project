using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public class PathBranch {
        [Tooltip(Tooltips.PathBranchPoints)] public List<Vector3> path = new();
        [Tooltip(Tooltips.PathBranchWidths)] public List<float> widths = new();

        public PathBranch Copy() => new() { path = new List<Vector3>(path), widths = new List<float>(widths) };
    }

    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Path Stats", fileName = "Path Stats")]
    public class TsitskiProBuilderPathStats : ScriptableObject {
        [Header("Mesh")]
        [Tooltip(Tooltips.StoreWidth)] public bool storeWidth;
        [TsitskiShowIf(nameof(storeWidth))] [Tooltip(Tooltips.PathStatsWidth)] [Min(TsitskiProBuilderPath.MinWidth)] public float width = 2f;
        [Tooltip(Tooltips.StoreHeight)] public bool storeHeight = true;
        [TsitskiShowIf(nameof(storeHeight))] [Tooltip(Tooltips.PathGeneratorHeight)] public float height = 0.15f;
        [Tooltip(Tooltips.PathSamplesPerSegment)] [Min(1)] public int samplesPerSegment = 8;
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 30f;

        [Header("Paths")]
        [Tooltip(Tooltips.SavePathBranches)] public bool savePaths;
        [TsitskiShowIf(nameof(savePaths))] [Tooltip(Tooltips.PathBranches)] public List<PathBranch> branches = TsitskiProBuilderPath.DefaultBranches();

        [Header("Materials")]
        [Tooltip(Tooltips.PathTopMaterial)] public Material topMaterial;
        [Tooltip(Tooltips.PathSideMaterial)] public Material sideMaterial;
        [Tooltip(Tooltips.PathBottomMaterial)] public Material bottomMaterial;

        public static event Action<TsitskiProBuilderPathStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
