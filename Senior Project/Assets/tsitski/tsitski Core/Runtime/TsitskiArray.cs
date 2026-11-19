using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.Core {
    public enum CornerSide {
        Outside,
        Inside
    }

    public enum ArrayPlacementMode {
        Spacing,
        SpanEnds
    }

    public struct ArrayPlacement {
        public Matrix4x4 matrix;
        public PlacementKind kind;
    }

    public abstract class ArrayPieceType {
        public virtual int priority => 0;
        public abstract bool Accepts(Object piece);
        public virtual bool ClaimsEmpty(TsitskiArray array) => false;
        public abstract void Build(TsitskiArray array, List<ArrayPlacement> placements);
        public abstract void Clear(TsitskiArray array);
        public virtual void OnReset(TsitskiArray array) { }
    }

    public static class ArrayPieceTypes {
        private static readonly List<ArrayPieceType> Types = new();

        public static IReadOnlyList<ArrayPieceType> all => Types;

        public static void Register(ArrayPieceType type) {
            Types.RemoveAll(existing => existing.GetType() == type.GetType());
            Types.Add(type);
        }

        public static ArrayPieceType For(Object piece) =>
            piece ? Types.Where(type => type.Accepts(piece)).OrderByDescending(type => type.priority).FirstOrDefault() : null;
    }

    [AddComponentMenu("tsitski/tsitski Array")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TsitskiArray : MonoBehaviour, IPathOwner, IGenerated, IInputHashed, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "its path is stored in its stats asset and shared with other objects.";
            return !pathInStats;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) => path = PivotShift.Shifted(path, offset);

        void IPivotShiftable.ApplyPivotShift() => Rebuild();

        public const float MinSpacing = 0.05f;
        private const int MaxInstances = 10000;

        [Tooltip(CoreTooltips.ArrayStats)] public TsitskiArrayStats stats;

        [Header("Material")]
        [Tooltip(CoreTooltips.GeneratorMaterial)] public Material material;

        [Header("Piece")]
        [Tooltip(CoreTooltips.ArrayPiece)] [FormerlySerializedAs("source")] public Object piece;
        [Tooltip(CoreTooltips.ArrayPillarHeight)] [Min(0.01f)] public float pillarHeight = 3f;
        [Tooltip(CoreTooltips.MergeIntoMesh)] public bool mergeIntoMesh;
        [Tooltip(CoreTooltips.ArrayCornerSide)] public CornerSide cornerSide;
        [Tooltip(CoreTooltips.ArrayPlacement)] public ArrayPlacementMode placement;
        [Tooltip(CoreTooltips.ArrayPieceRotation)] public Vector3 pieceRotation;

        [Header("Spacing")]
        [Tooltip(CoreTooltips.Spacing)] [Min(MinSpacing)] public float spacing = 1f;
        [Tooltip(CoreTooltips.Offset)] [Min(0f)] public float offset;
        [Tooltip(CoreTooltips.FitSpacing)] [FormerlySerializedAs("spacePerSegment")] [FormerlySerializedAs("fitToPath")] public bool fitSpacing;
        [Tooltip(CoreTooltips.SplitAtCorners)] public bool splitAtCorners = true;
        [Tooltip(CoreTooltips.DivisionLength)] [Min(0f)] public float divisionLength;
        [Tooltip(CoreTooltips.SkipCorners)] [FormerlySerializedAs("avoidCorners")] public bool skipCorners;
        [Tooltip(CoreTooltips.SkipDivisions)] public bool skipDivisions = true;
        [Tooltip(CoreTooltips.AlignToPath)] public bool alignToPath = true;

        [Header("Path")]
        [Tooltip(CoreTooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(CoreTooltips.ArrayPath)] public List<Vector3> path = new() { Vector3.zero };
        [Tooltip(CoreTooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(CoreTooltips.Closed)] public bool closed;

        [SerializeField] [HideInInspector] [FormerlySerializedAs("pillarStats")] private Object legacyPillar;
        [HideInInspector] [FormerlySerializedAs("generatedPillars")] public List<MonoBehaviour> generatedPieces = new();
        [HideInInspector] public Mesh generatedMesh;
        [HideInInspector] public float pillarHeightOverride;
        [SerializeField] [HideInInspector] internal MonoBehaviour generator;
        MonoBehaviour IGenerated.generator { get => generator; set => generator = value; }

        public bool? generatedInterior => generator ? cornerSide == CornerSide.Inside : (bool?)null;

        public static bool suppressReset;

        public static readonly HashSet<TsitskiArray> All = new();
        public static event Action<TsitskiArray> Rebuilt;

        public Material activeMaterial => stats ? stats.material : material;
        public Object activePiece => stats && stats.piece ? stats.piece : piece;
        public float activePillarHeight => Mathf.Max(0.01f, pillarHeightOverride > 0f ? pillarHeightOverride : stats ? stats.pillarHeight : pillarHeight);
        public bool activeMergeIntoMesh => stats ? stats.mergeIntoMesh : mergeIntoMesh;
        public ArrayPlacementMode activePlacement => stats ? stats.placement : placement;
        public Vector3 activePieceRotation => stats ? stats.pieceRotation : pieceRotation;
        public float activeSpacing => Mathf.Max(MinSpacing, stats ? stats.spacing : spacing);
        public float activeOffset => Mathf.Max(0f, stats ? stats.offset : offset);
        public ArraySpacing activeSpacingSettings => stats
            ? new ArraySpacing {
                spacing = activeSpacing, offset = activeOffset, fitSpacing = stats.fitSpacing, splitAtCorners = stats.splitAtCorners,
                divisionLength = stats.divisionLength, skipCorners = stats.skipCorners, skipDivisions = stats.skipDivisions
            }
            : new ArraySpacing {
                spacing = activeSpacing, offset = activeOffset, fitSpacing = fitSpacing, splitAtCorners = splitAtCorners,
                divisionLength = divisionLength, skipCorners = skipCorners, skipDivisions = skipDivisions
            };
        public bool activeAlignToPath => stats ? stats.alignToPath : alignToPath;
        public int activeRoundingSegments => Mathf.Max(1, stats ? stats.roundingSegments : roundingSegments);
        public bool pathInStats => stats && stats.storePath;
        public bool activeClosed => pathInStats ? stats.closed : closed;

        List<Vector3> IPathOwner.localPath { get => path; set => path = value; }

        public List<Vector3> activePath {
            get => pathInStats ? stats.path : path;
            set {
                if (pathInStats)
                    stats.path = value;
                else
                    path = value;
            }
        }

        public List<float> activePathRadii {
            get => pathInStats ? stats.pathRadii : pathRadii;
            set {
                if (pathInStats)
                    stats.pathRadii = value;
                else
                    pathRadii = value;
            }
        }

        public static void PieceChanged(Object changed) {
            if (!changed)
                return;

            foreach (TsitskiArray array in All.ToArray())
                if (array && array.activePiece == changed)
                    array.QueueRebuild();
        }

        private void Reset() {
            TsitskiSettings.ApplyDefaultStaticFlags(gameObject);
            if (piece || suppressReset)
                return;

            foreach (ArrayPieceType type in ArrayPieceTypes.all)
                type.OnReset(this);
        }

        private void OnEnable() {
            MigrateLegacy();
            All.Add(this);
            TsitskiArrayStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            TsitskiArrayStats.Changed -= OnStatsChanged;
        }

        private void OnValidate() {
            MigrateLegacy();
            QueueRebuild();
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

        private void OnStatsChanged(TsitskiArrayStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        public Matrix4x4 PathStartFrame() =>
            PathUtility.StartFrame(PathUtility.Rounded(activePath, activePathRadii, activeClosed, activeRoundingSegments, out bool loop), loop);

        [SerializeField] [HideInInspector] private int inputHash;
        int IInputHashed.inputHash { get => inputHash; set => inputHash = value; }

        public void Rebuild() {
            BuildGenerated();
            GeneratorInputs.Store(this);
        }

        private void BuildGenerated() {
            MigrateLegacy();
            TsitskiSettings.AutoName(this, stats, "Array");

            Object active = activePiece;
            ArrayPieceType type = active ? ArrayPieceTypes.For(active) : ArrayPieceTypes.all.FirstOrDefault(candidate => candidate.ClaimsEmpty(this));
            foreach (ArrayPieceType other in ArrayPieceTypes.all)
                if (other != type)
                    other.Clear(this);

            if (!active && type == null) {
                ArrayDefaultCube.Build(this, PlacementSlots());
            }
            else {
                ArrayDefaultCube.Clear(this);
                type?.Build(this, PlacementSlots());
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Rebuilt?.Invoke(this);
        }

        public static Quaternion PlacementRotation(Matrix4x4 placement) {
            Vector3 forward = placement.GetColumn(2), up = placement.GetColumn(1);
            return forward.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(forward, up) : Quaternion.identity;
        }

        public List<Matrix4x4> Placements() => PlacementSlots().Select(slot => slot.matrix).ToList();

        public List<ArrayPlacement> PlacementSlots() {
            List<ArrayPlacement> placements = SpacedSlots(activePlacement == ArrayPlacementMode.SpanEnds);

            Vector3 rotation = activePieceRotation;
            if (rotation != Vector3.zero) {
                Matrix4x4 extra = Matrix4x4.Rotate(Quaternion.Euler(rotation));
                for (int i = 0; i < placements.Count; i++)
                    placements[i] = new ArrayPlacement { matrix = placements[i].matrix * extra, kind = placements[i].kind };
            }

            return placements;
        }

        private static Matrix4x4 Frame(Vector3 position, Vector3 right, Vector3 up, Vector3 forward, bool align) {
            Matrix4x4 placement = Matrix4x4.identity;
            if (align) {
                placement.SetColumn(0, right);
                placement.SetColumn(1, up);
                placement.SetColumn(2, forward);
            }
            placement.SetColumn(3, new Vector4(position.x, position.y, position.z, 1f));
            return placement;
        }

        private List<ArrayPlacement> SpacedSlots(bool spanEnds) {
            List<ArrayPlacement> placements = new();
            List<Vector3> points = PathUtility.Rounded(activePath, activePathRadii, activeClosed, activeRoundingSegments, out bool loop);

            if (points.Count == 1) {
                placements.Add(new ArrayPlacement { matrix = Matrix4x4.Translate(points[0]), kind = PlacementKind.Interior });
                return placements;
            }

            if (points.Count < 2)
                return placements;

            PathUtility.Segments(points, loop, out Vector3[] directions, out float[] lengths, out Vector3[] rights, out Vector3[] ups);
            float[] cumulative = new float[lengths.Length + 1];
            for (int s = 0; s < lengths.Length; s++)
                cumulative[s + 1] = cumulative[s] + lengths[s];

            float total = cumulative[^1];
            if (total <= 0f)
                return placements;

            bool align = activeAlignToPath;
            foreach (ArraySpacing.Placement slot in activeSpacingSettings.Layout(points, loop, MaxInstances)) {
                if (slot.skipped)
                    continue;

                float distance = slot.distance;
                float d = loop ? Mathf.Repeat(distance, total) : Mathf.Clamp(distance, 0f, total);
                int index = Array.BinarySearch(cumulative, d);
                int segment = Mathf.Clamp(index >= 0 ? index : ~index - 1, 0, lengths.Length - 1);

                Vector3 position = points[segment] + directions[segment] * Mathf.Clamp(d - cumulative[segment], 0f, lengths[segment]);
                if (!spanEnds) {
                    placements.Add(new ArrayPlacement {
                        matrix = Frame(position, rights[segment], ups[segment], directions[segment], align), kind = slot.kind
                    });
                    continue;
                }

                bool atStart = !loop && d <= 1e-4f, atEnd = !loop && d >= total - 1e-4f;
                int forward = atEnd ? -1 : segment;
                int backward = atStart ? -1 : Mathf.Abs(d - cumulative[segment]) <= 1e-4f ? (segment - 1 + lengths.Length) % lengths.Length : segment;
                if (atEnd)
                    backward = lengths.Length - 1;

                if (forward >= 0)
                    placements.Add(new ArrayPlacement {
                        matrix = Frame(position, rights[forward], ups[forward], directions[forward], true), kind = slot.kind
                    });
                if (backward >= 0)
                    placements.Add(new ArrayPlacement {
                        matrix = Frame(position, -rights[backward], ups[backward], -directions[backward], true), kind = slot.kind
                    });
            }

            return placements;
        }
    }
}
