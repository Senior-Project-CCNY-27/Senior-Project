using System.Collections.Generic;
using System.Linq;
using Tsitski.Core;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    public static class ProBuilderArrayPieces {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            ArrayPieceTypes.Register(new ProBuilderArrayPiece());
            TsitskiProBuilderPillarStats.Changed -= TsitskiArray.PieceChanged;
            TsitskiProBuilderPillarStats.Changed += TsitskiArray.PieceChanged;
        }

        public static ProBuilderMesh Source(Object piece) =>
            piece as ProBuilderMesh ?? (piece is GameObject gameObject ? gameObject.GetComponent<ProBuilderMesh>() : null);

        public static ProBuilderMesh LocalSource(TsitskiArray array) => Source(array.piece);
        public static ProBuilderMesh ActiveSource(TsitskiArray array) => Source(array.activePiece);
        public static TsitskiProBuilderPillarStats ActivePillarStats(TsitskiArray array) => array.activePiece as TsitskiProBuilderPillarStats;

        public static ArraySourceData StoredSource(TsitskiArrayStats stats) =>
            stats && stats.storeSource && stats.storedPiece is ArraySourceData { hasData: true } data ? data : null;

        public static bool SourceInStats(TsitskiArray array) => StoredSource(array.stats) != null;

        public static bool IsSource(ProBuilderMesh mesh) => mesh && TsitskiArray.All.Any(a => a && ActiveSource(a) == mesh);

        public static void CreateSource(TsitskiArray array) {
            ProBuilderMesh cube = ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one * 0.25f);
            if (cube.TryGetComponent(out MeshRenderer cubeRenderer))
                cubeRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;
            AdoptSource(array, cube);
        }

        public static void CreateSourceFrom(TsitskiArray array, ProBuilderMesh original) {
            GameObject child = new("Array Source");
            ProBuilderMesh copy = child.AddComponent<ProBuilderMesh>();
            copy.Clear();
            copy.ToMesh();
            copy.CopyFrom(original);
            copy.ToMesh();
            copy.Refresh();

            if (original.TryGetComponent(out MeshRenderer originalRenderer) && child.TryGetComponent(out MeshRenderer copyRenderer))
                copyRenderer.sharedMaterials = originalRenderer.sharedMaterials;

            AdoptSource(array, copy);
        }

        public static void CreateSourceFrom(TsitskiArray array, ArraySourceData data) {
            ProBuilderMesh mesh = new GameObject("Array Source").AddComponent<ProBuilderMesh>();
            mesh.Clear();
            mesh.ToMesh();
            data.ApplyTo(mesh);
            AdoptSource(array, mesh);
        }

        private static void AdoptSource(TsitskiArray array, ProBuilderMesh mesh) {
            mesh.gameObject.name = "Array Source";

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create Array Source");
            Undo.SetTransformParent(mesh.transform, array.transform, false, "Create Array Source");
#else
            mesh.transform.SetParent(array.transform, false);
#endif

            mesh.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            mesh.transform.localScale = Vector3.one;
            array.piece = mesh;
        }
    }

    internal class ProBuilderArrayPiece : ArrayPieceType {
        public override int priority => 1;

        public override bool Accepts(Object piece) => piece is TsitskiProBuilderPillarStats || ProBuilderArrayPieces.Source(piece);

        public override bool ClaimsEmpty(TsitskiArray array) => ProBuilderArrayPieces.SourceInStats(array);

        public override void OnReset(TsitskiArray array) {
            if (array.TryGetComponent(out ProBuilderMesh mesh) && mesh.faceCount > 0)
                ProBuilderArrayPieces.CreateSourceFrom(array, mesh);
            else
                ProBuilderArrayPieces.CreateSource(array);
        }

        public override void Clear(TsitskiArray array) {
            GeneratedChildren.Resize<TsitskiProBuilderPillar>(array, array.generatedPieces, 0, "Array Pillar");
            if (!array.TryGetComponent(out ProBuilderMesh mesh) || mesh.faceCount == 0)
                return;

            mesh.Clear();
            mesh.ToMesh();
            mesh.Refresh();
        }

        public override void Build(TsitskiArray array, List<ArrayPlacement> placements) {
            bool pillars = ProBuilderArrayPieces.ActivePillarStats(array);
            if (pillars && !array.activeMergeIntoMesh) {
                BuildPillars(array, placements);
                return;
            }

            if (!array.TryGetComponent(out ProBuilderMesh mesh))
                mesh = array.gameObject.AddComponent<ProBuilderMesh>();

            if (pillars) {
                BuildMergedPillars(array, mesh, placements);
                return;
            }

            GeneratedChildren.Resize<TsitskiProBuilderPillar>(array, array.generatedPieces, 0, "Array Pillar");

            ArraySourceData data = SourceData(array, mesh);
            if (data is not { hasData: true })
                return;

            List<Vector3> positions = new();
            List<Vector2> uvs = new();
            List<Face> faces = new();
            foreach (ArrayPlacement placement in placements)
                data.AppendTo(placement.matrix, positions, uvs, faces);

            if (faces.Count == 0)
                mesh.Clear();
            else
                ManualUvs.Rebuild(mesh, positions, faces, uvs);

            if (array.TryGetComponent(out MeshRenderer meshRenderer)) {
                Material[] materials = data.materials.Where(m => m).ToArray();
                meshRenderer.sharedMaterials = materials is { Length: > 0 } ? materials : new[] { TsitskiProBuilderSettings.DefaultMaterial };
            }
            if (faces.Count > 0)
                ForcedMaterials.Apply(mesh, null, array.activeMaterial);
            mesh.ToMesh();
            mesh.Refresh();

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
        }

        private static ArraySourceData SourceData(TsitskiArray array, ProBuilderMesh self) {
            ArraySourceData stored = ProBuilderArrayPieces.StoredSource(array.stats);
            if (stored != null)
                return stored;

            ProBuilderMesh template = ProBuilderArrayPieces.ActiveSource(array);
            return template && template != self && template.faceCount > 0 ? ArraySourceData.Capture(template) : null;
        }

        private static TsitskiProBuilderPillar PreparePillar(TsitskiArray array, TsitskiProBuilderPillar pillar, ArrayPlacement placement,
            Vector3 defaultStart, Vector3 defaultEnd) {
            pillar.stats = ProBuilderArrayPieces.ActivePillarStats(array);
            pillar.height = array.activePillarHeight;
            pillar.cornerSide = array.cornerSide;
            pillar.cornerStart = defaultStart;
            pillar.cornerEnd = defaultEnd;
            if (pillar.activeAutoSnapToPath)
                pillar.SnapToPath(pillar.snapDistance, array.transform, array.activePath, array.activeClosed, placement.kind == PlacementKind.Corner);
            return pillar;
        }

        private static void BuildMergedPillars(TsitskiArray array, ProBuilderMesh mesh, List<ArrayPlacement> placements) {
            GeneratedChildren.Resize<TsitskiProBuilderPillar>(array, array.generatedPieces, 0, "Array Pillar");

            List<Vector3> positions = new();
            List<Vector2> uvs = new();
            List<Face> faces = new();

            GameObject temporary = new("Array Pillar") { hideFlags = HideFlags.HideAndDontSave };
            temporary.SetActive(false);
            try {
                TsitskiProBuilderPillar pillar = temporary.AddComponent<TsitskiProBuilderPillar>();
                Vector3 defaultStart = pillar.cornerStart, defaultEnd = pillar.cornerEnd;

                List<Vector3> piecePositions = new();
                List<Vector2> pieceUvs = new();
                List<Face> pieceFaces = new();
                foreach (ArrayPlacement placement in placements) {
                    temporary.transform.SetPositionAndRotation(array.transform.TransformPoint(placement.matrix.GetColumn(3)),
                        array.transform.rotation * TsitskiArray.PlacementRotation(placement.matrix));
                    PreparePillar(array, pillar, placement, defaultStart, defaultEnd);

                    piecePositions.Clear();
                    pieceUvs.Clear();
                    pieceFaces.Clear();
                    pillar.BuildGeometry(piecePositions, pieceUvs, pieceFaces);

                    Matrix4x4 toArray = Matrix4x4.TRS(array.transform.InverseTransformPoint(temporary.transform.position),
                        Quaternion.Inverse(array.transform.rotation) * temporary.transform.rotation, Vector3.one);
                    int start = positions.Count;
                    foreach (Vector3 p in piecePositions)
                        positions.Add(toArray.MultiplyPoint3x4(p));
                    uvs.AddRange(pieceUvs);
                    foreach (Face face in pieceFaces)
                        faces.Add(new Face(face.indexes.Select(i => i + start).ToArray()) {
                            smoothingGroup = face.smoothingGroup,
                            submeshIndex = face.submeshIndex,
                            manualUV = face.manualUV,
                            uv = face.uv
                        });
                }
            }
            finally {
                Object.DestroyImmediate(temporary);
            }

            if (faces.Count == 0)
                mesh.Clear();
            else {
                ManualUvs.Rebuild(mesh, positions, faces, uvs);
                ForcedMaterials.Apply(mesh, null, array.activeMaterial);
            }
            mesh.ToMesh();
            mesh.Refresh();

            if (array.TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
        }

        private static void BuildPillars(TsitskiArray array, List<ArrayPlacement> placements) {
            if (array.TryGetComponent(out ProBuilderMesh mesh) && mesh.faceCount > 0) {
                mesh.Clear();
                mesh.ToMesh();
                mesh.Refresh();
#if UNITY_EDITOR
                EditorUtility.SetDirty(mesh);
#endif
            }

            List<TsitskiProBuilderPillar> pillars =
                GeneratedChildren.Resize<TsitskiProBuilderPillar>(array, array.generatedPieces, placements.Count, "Array Pillar");

            for (int i = 0; i < placements.Count; i++) {
                ArrayPlacement placement = placements[i];
                TsitskiProBuilderPillar pillar = pillars[i];
                GeneratedChildren.Place(array, pillar, placement.matrix.GetColumn(3), TsitskiArray.PlacementRotation(placement.matrix));
                PreparePillar(array, pillar, placement, pillar.cornerStart, pillar.cornerEnd);
                pillar.Rebuild();
                GeneratedChildren.MarkDirty(pillar);
            }
        }
    }
}
