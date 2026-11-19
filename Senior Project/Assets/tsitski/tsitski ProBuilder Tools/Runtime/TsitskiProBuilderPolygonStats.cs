using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public class PolygonPath {
        [Tooltip(Tooltips.PolygonPath)] public List<Vector3> path = new();
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();

        public PolygonPath Copy() => new() { path = new List<Vector3>(path), pathRadii = new List<float>(pathRadii) };

        internal static List<PolygonPath> Upgrade(List<PolygonPath> paths, ref List<Vector3> legacyPath, ref List<float> legacyPathRadii) {
            if (legacyPath is { Count: > 0 })
                paths = new List<PolygonPath> { new() { path = legacyPath, pathRadii = legacyPathRadii ?? new List<float>() } };
            legacyPath = new List<Vector3>();
            legacyPathRadii = new List<float>();
            return paths ?? new List<PolygonPath>();
        }
    }

    [Serializable]
    public class PolygonHole {
        [Tooltip(Tooltips.PolygonHolePath)] public List<Vector3> path = new();
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();

        public PolygonHole Copy() => new() { path = new List<Vector3>(path), pathRadii = new List<float>(pathRadii) };
    }

    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Polygon Stats", fileName = "Polygon Stats")]
    public class TsitskiProBuilderPolygonStats : ScriptableObject, ISerializationCallbackReceiver {
        [Header("Mesh")]
        [Tooltip(Tooltips.PolygonThickness)] public float thickness = 0.25f;
        [Tooltip(Tooltips.PolygonSubdivisionStep)] [Min(0f)] public float subdivisionStep;
        [Tooltip(Tooltips.ApplyCutsToProBuilderMesh)] public bool applyCutsToProBuilderMesh;

        [Header("Path")]
        [Tooltip(Tooltips.SavePolygon)] public bool savePolygon;
        [TsitskiShowIf(nameof(savePolygon))] [Tooltip(Tooltips.PolygonPaths)] public List<PolygonPath> paths = TsitskiProBuilderPolygon.DefaultPaths();
        [TsitskiShowIf(nameof(savePolygon))] [Tooltip(Tooltips.PolygonHoles)] public List<PolygonHole> holes = new();

        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("path")] private List<Vector3> legacyPath = new();
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("pathRadii")] private List<float> legacyPathRadii = new();

        [Header("Materials")]
        [Tooltip(Tooltips.PolygonTopMaterial)] public Material topMaterial;
        [Tooltip(Tooltips.PolygonBottomMaterial)] public Material bottomMaterial;

        public static event Action<TsitskiProBuilderPolygonStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => paths = PolygonPath.Upgrade(paths, ref legacyPath, ref legacyPathRadii);
    }
}
