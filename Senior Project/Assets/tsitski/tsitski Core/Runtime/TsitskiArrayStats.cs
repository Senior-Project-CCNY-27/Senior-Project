using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.Core {
    [CreateAssetMenu(menuName = "tsitski/Array Stats", fileName = "Array Stats")]
    public class TsitskiArrayStats : TsitskiPathStats {
        [Header("Piece")]
        [Tooltip(CoreTooltips.ArrayStatsPiece)] [FormerlySerializedAs("source")] public Object piece;
        [Tooltip(CoreTooltips.StoreSource)] public bool storeSource;
        [SerializeReference] [HideInInspector] public object storedPiece;
        [Tooltip(CoreTooltips.ArrayPillarHeight)] [Min(0.01f)] public float pillarHeight = 3f;
        [Tooltip(CoreTooltips.MergeIntoMesh)] public bool mergeIntoMesh;
        [Tooltip(CoreTooltips.ArrayPlacement)] public ArrayPlacementMode placement;
        [Tooltip(CoreTooltips.ArrayPieceRotation)] public Vector3 pieceRotation;

        [Header("Spacing")]
        [Tooltip(CoreTooltips.Spacing)] [Min(TsitskiArray.MinSpacing)] public float spacing = 1f;
        [Tooltip(CoreTooltips.Offset)] [Min(0f)] public float offset;
        [Tooltip(CoreTooltips.FitSpacing)] [FormerlySerializedAs("spacePerSegment")] [FormerlySerializedAs("fitToPath")] public bool fitSpacing;
        [Tooltip(CoreTooltips.SplitAtCorners)] public bool splitAtCorners = true;
        [Tooltip(CoreTooltips.DivisionLength)] [Min(0f)] public float divisionLength;
        [Tooltip(CoreTooltips.SkipCorners)] [FormerlySerializedAs("avoidCorners")] public bool skipCorners;
        [Tooltip(CoreTooltips.SkipDivisions)] public bool skipDivisions = true;
        [Tooltip(CoreTooltips.AlignToPath)] public bool alignToPath = true;

        [Header("Path")]
        [Tooltip(CoreTooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(CoreTooltips.StorePath)] public bool storePath;
        [TsitskiShowIf(nameof(storePath))] [Tooltip(CoreTooltips.ArrayPath)] public List<Vector3> path = new() { Vector3.zero };
        [TsitskiShowIf(nameof(storePath))] [Tooltip(CoreTooltips.Radii)] public List<float> pathRadii = new();
        [TsitskiShowIf(nameof(storePath))] [Tooltip(CoreTooltips.Closed)] public bool closed;

        [Header("Material")]
        [Tooltip(CoreTooltips.GeneratorMaterial)] public Material material;

        [SerializeField] [HideInInspector] [FormerlySerializedAs("pillarStats")] private Object legacyPillar;

        public static event Action<TsitskiArrayStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnEnable() => MigrateLegacy();

        private void OnValidate() {
            MigrateLegacy();
            Changed?.Invoke(this);
        }

        private void MigrateLegacy() {
            if (!legacyPillar)
                return;

            piece = legacyPillar;
            legacyPillar = null;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
