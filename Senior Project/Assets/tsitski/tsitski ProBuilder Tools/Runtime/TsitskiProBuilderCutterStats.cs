using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Cutter Stats", fileName = "Cutter Stats")]
    public class TsitskiProBuilderCutterStats : ScriptableObject {
        [Header("Shape")]
        [Tooltip(Tooltips.Shape)] public ProBuilderCutterShape shape = ProBuilderCutterShape.Box;
        [Tooltip(Tooltips.CornerRadius)] public BoxCorners cornerRadius;
        [Tooltip(Tooltips.CornerSegments)] [Min(1)] public int cornerSegments = 8;
        [Tooltip(Tooltips.CylinderSegments)] [Min(3)] public int cylinderSegments = 16;
        [Tooltip(Tooltips.ExplosionSeed)] public int explosionSeed = 1;
        [Tooltip(Tooltips.ExplosionSpikes)] [Min(3)] public int explosionSpikes = 12;
        [Tooltip(Tooltips.ExplosionMinorSpikes)] [Min(0)] public int explosionMinorSpikes = 3;
        [Tooltip(Tooltips.ExplosionJaggedness)] [Range(0f, 1f)] public float explosionJaggedness = 0.6f;
        [Tooltip(Tooltips.PolygonPoints)] public List<Vector2> polygonPoints = new() {
            new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f)
        };
        [Tooltip(Tooltips.Radii)] public List<float> polygonRadii = new();
        [Tooltip(Tooltips.CutterPath)] public List<Vector3> pathPoints = new() { Vector3.zero, new(0f, 0f, 2f) };
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.PathRotations)] public List<Quaternion> pathRotations = new();
        [Tooltip(Tooltips.PathWidth)] [Min(0.01f)] public float pathWidth = 1f;
        [Tooltip(Tooltips.PathHeight)] [Min(0.01f)] public float pathHeight = 2f;

        [Header("Bounds")]
        [Tooltip(Tooltips.CutterCenter)] public Vector3 center;
        [Tooltip(Tooltips.CutterSize)] public Vector3 size = Vector3.one;

        [Header("Cutting")]
        [Tooltip(Tooltips.CutMode)] public ProBuilderCutMode cutMode = ProBuilderCutMode.CutProBuilder;
        [Tooltip(Tooltips.CutThroughParent)] public bool cutThroughParent = true;
        [Tooltip(Tooltips.IgnorePrefabMeshes)] public bool ignorePrefabMeshes;

        [Header("Faces")]
        [Tooltip(Tooltips.CreateMesh)] public bool createMesh = true;
        [Tooltip(Tooltips.FacesOn)] public ProBuilderCutFaces facesOn = ProBuilderCutFaces.First;
        [Tooltip(Tooltips.GenerateFaces)] public AxisToggles generateFaces = new(true, true, true);
        [Tooltip(Tooltips.ClipToTunnel)] public AxisToggles clipToTunnel = new(false, false, false);
        [Tooltip(Tooltips.RevealMaterial)] public Material revealMaterial;
        [Tooltip(Tooltips.CutterSmoothingAngle)] [Range(0f, 89f)] public float smoothingAngle = 30f;

        public static event Action<TsitskiProBuilderCutterStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
