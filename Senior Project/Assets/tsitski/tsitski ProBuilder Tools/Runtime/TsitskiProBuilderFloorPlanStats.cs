using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Floor Plan Stats", fileName = "Floor Plan Stats")]
    public class TsitskiProBuilderFloorPlanStats : ScriptableObject {
        [Header("Walls")]
        [Tooltip(Tooltips.StoreHeight)] public bool storeHeight = true;
        [TsitskiShowIf(nameof(storeHeight))] [Tooltip(Tooltips.WallHeight)] [Min(0.01f)] public float height = 3f;
        [Tooltip(Tooltips.StoreThickness)] public bool storeThickness = true;
        [TsitskiShowIf(nameof(storeThickness))] [Tooltip(Tooltips.WallThickness)] [Min(0.01f)] public float thickness = 0.2f;
        [Tooltip(Tooltips.Alignment)] public WallAlignment alignment;
        [Tooltip(Tooltips.WallsAroundHoles)] public bool wallsAroundHoles;
        [Tooltip(Tooltips.SquareCorners)] public bool squareCorners;

        [Header("Mesh")]
        [Tooltip(Tooltips.LoopCutStep)] [Min(0f)] public float loopCutStep;
        [Tooltip(Tooltips.VerticalLoopCutStep)] [Min(0f)] public float verticalLoopCutStep;
        [Tooltip(Tooltips.ApplyCutsToProBuilderMesh)] public bool applyCutsToProBuilderMesh;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;

        [Header("Roof")]
        [Tooltip(Tooltips.FloorPlanRoofStats)] public TsitskiProBuilderRoofStats roofStats;

        [Header("Layers")]
        [Tooltip(Tooltips.FloorPlanLayers)] [FormerlySerializedAs("floorPlanOutlineSettings")] [FormerlySerializedAs("arrays")]
        [TsitskiPathLayers(true, typeof(TsitskiArrayStats), typeof(TsitskiProBuilderTrimStats))]
        public List<TsitskiPathLayer> layers = new();

        [Header("Materials")]
        [Tooltip(Tooltips.InsideMaterial)] public Material insideMaterial;
        [Tooltip(Tooltips.OutsideMaterial)] public Material outsideMaterial;

        public static event Action<TsitskiProBuilderFloorPlanStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnEnable() => TsitskiPathLayer.Upgrade(this, layers);

        private void OnValidate() {
            TsitskiPathLayer.Upgrade(this, layers);
            Changed?.Invoke(this);
        }
    }
}
