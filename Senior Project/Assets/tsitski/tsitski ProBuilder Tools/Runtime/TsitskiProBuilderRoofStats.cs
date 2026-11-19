using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public class RoofPath {
        [Tooltip(Tooltips.RoofPathWidth)] [Min(0.01f)] public float width = 4f;
        [Tooltip(Tooltips.RoofPathHeight)] [Min(0.01f)] public float height = 1.5f;
        [Tooltip(Tooltips.Closed)] public bool closed;
        [Tooltip(Tooltips.RoofGableStart)] public bool gableStart;
        [Tooltip(Tooltips.RoofGableEnd)] public bool gableEnd;
        [Tooltip(Tooltips.RoofPathPoints)] public List<Vector3> path = new();
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();

        public RoofPath Copy() => new() {
            width = width, height = height, closed = closed, gableStart = gableStart, gableEnd = gableEnd,
            path = new List<Vector3>(path), pathRadii = new List<float>(pathRadii)
        };
    }

    [Serializable]
    public class RoofOutline {
        public List<Vector2> points = new();
    }

    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Roof Stats", fileName = "Roof Stats")]
    public class TsitskiProBuilderRoofStats : ScriptableObject {
        [Header("Profile")]
        [Tooltip(Tooltips.RoofProfile)] public List<Vector2> profile = TsitskiProBuilderRoof.DefaultProfile();
        [Tooltip(Tooltips.Radii)] public List<float> profileRadii = new();
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.RoofThickness)] [Min(0.01f)] public float thickness = 0.2f;
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 30f;

        [Header("Fill & Gables")]
        [Tooltip(Tooltips.FillRoof)] public bool fillRoof;
        [Tooltip(Tooltips.GenerateGableWalls)] public bool generateGableWalls;
        [Tooltip(Tooltips.GableWallIndent)] [Min(0f)] public float gableWallIndent = 0.25f;
        [Tooltip(Tooltips.GableWallThickness)] [Min(0.01f)] public float gableWallThickness = 0.25f;

        [Header("Paths")]
        [Tooltip(Tooltips.SaveRoofPaths)] public bool savePaths;
        [TsitskiShowIf(nameof(savePaths))] [Tooltip(Tooltips.RoofPaths)] public List<RoofPath> paths = TsitskiProBuilderRoof.DefaultPaths();

        [Header("Material")]
        [Tooltip(Tooltips.GeneratorMaterial)] public Material material;

        public static event Action<TsitskiProBuilderRoofStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
