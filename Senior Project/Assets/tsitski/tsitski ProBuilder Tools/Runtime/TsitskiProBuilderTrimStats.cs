using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Trim Stats", fileName = "Trim Stats")]
    public class TsitskiProBuilderTrimStats : TsitskiPathStats {
        [Header("Profile")]
        [Tooltip(Tooltips.Profile)] public List<Vector2> profile = TsitskiProBuilderTrim.DefaultProfile();
        [Tooltip(Tooltips.Radii)] public List<float> profileRadii = new();
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.ProfileMirror)] public ProfileMirror profileMirror;

        [Header("Path")]
        [Tooltip(Tooltips.StorePath)] public bool storePath;
        [TsitskiShowIf(nameof(storePath))] [Tooltip(Tooltips.TrimPath)] public List<Vector3> path = new() { Vector3.zero };
        [TsitskiShowIf(nameof(storePath))] [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [TsitskiShowIf(nameof(storePath))] [Tooltip(Tooltips.Closed)] public bool closed;

        [Header("Mesh")]
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 30f;
        [Tooltip(Tooltips.LoopCutStep)] [Min(0f)] public float loopCutStep;
        [Tooltip(Tooltips.ApplyCutsToProBuilderMesh)] public bool applyCutsToProBuilderMesh;
        [Tooltip(Tooltips.RoundBevel)] [Min(0)] public int roundBevel;
        [Tooltip(Tooltips.FlattenAngledEnds)] public bool flattenAngledEnds;

        [Header("Material")]
        [Tooltip(Tooltips.GeneratorMaterial)] public Material material;

        public static event Action<TsitskiProBuilderTrimStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
