using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    public static class ProBuilderCutSystem {
        private const double RebuildInterval = 0.05;
        private const double LightmapUvDelay = 1.0;
        private const double ColliderRepairInterval = 0.5;
        private const double MissingUvCheckInterval = 2.0;

        private static readonly HashSet<TsitskiProBuilderCutter> Cutters = new();
        private static readonly HashSet<TsitskiProBuilderCutter> DirtyCutters = new();
        private static readonly HashSet<MeshFilter> DirtyMeshes = new();
        private static readonly List<TsitskiProBuilderCutTarget> Targets = new();
        private static readonly HashSet<ProBuilderMesh> SuspendedMeshes = new();
        private static readonly HashSet<TsitskiProBuilderCutTarget> PendingLightmapUvs = new();
        private static readonly Dictionary<TsitskiProBuilderCutter, Bounds> CutterBounds = new();
        private static readonly Dictionary<TsitskiProBuilderCutTarget, Mesh> SourceMeshes = new();
        private static readonly Dictionary<MeshFilter, Bounds> MeshBounds = new();

        private static double _nextRebuildTime;
        private static double _lastRebuildTime;
        private static double _nextColliderRepairTime;
        private static double _nextMissingUvCheck;
        private static bool _needsRefresh = true;
        private static bool _resetChanges = true;
        private static bool _rebuildAll;
        private static bool _silentRebuild;
        private static bool _forceRebuild;

        static ProBuilderCutSystem() {
            TsitskiProBuilderCutter.Changed += OnCutterChanged;
            EditorMeshUtility.meshOptimized += (mesh, _) => MarkDirty(mesh);
            ProBuilderEditor.afterMeshModification += meshes => {
                foreach (ProBuilderMesh mesh in meshes)
                    MarkDirty(mesh);
            };
            TsitskiProBuilderTrim.Rebuilt += MarkDirty;
            TsitskiArray.Rebuilt += array => {
                if (array.TryGetComponent(out ProBuilderMesh mesh))
                    MarkDirty(mesh);
            };
            TsitskiProBuilderFloorPlan.Rebuilt += MarkDirty;
            TsitskiProBuilderPolygon.Rebuilt += MarkDirty;
            TsitskiProBuilderStairs.Rebuilt += MarkDirty;
            TsitskiProBuilderPillar.Rebuilt += MarkDirty;
            TsitskiProBuilderRoof.Rebuilt += MarkDirty;
            TsitskiProBuilderPath.Rebuilt += MarkDirty;
            Selection.selectionChanged += UpdateEditing;
            ToolManager.activeContextChanged += UpdateEditing;
            EditorSceneManager.sceneOpened += (_, _) => _needsRefresh = _resetChanges = _rebuildAll = true;
            EditorSceneManager.sceneClosed += _ => _needsRefresh = true;
            PrefabStage.prefabStageOpened += _ => _needsRefresh = _resetChanges = _rebuildAll = true;
            PrefabStage.prefabStageClosing += _ => _needsRefresh = true;
            EditorApplication.hierarchyChanged += () => _needsRefresh = true;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingEditMode) {
                    GeneratePendingLightmapUvs();
                    LightmapUvs.RegenerateMissing();
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                    _needsRefresh = _resetChanges = true;
            };
            EditorApplication.update += Update;
            EditorSceneManager.sceneSaving += (_, _) => {
                GeneratePendingLightmapUvs();
                LightmapUvs.RegenerateMissing();
            };
            RebuildQueue.Released += () => _nextRebuildTime = 0;

            if (!SessionState.GetBool("ProBuilderCutSystem.Started", false)) {
                SessionState.SetBool("ProBuilderCutSystem.Started", true);
                _rebuildAll = true;
            }
        }

        internal static void MarkDirty(ProBuilderMesh mesh) {
            if (mesh && mesh.TryGetComponent(out MeshFilter filter))
                DirtyMeshes.Add(filter);
        }

        internal static void RebuildNow(IEnumerable<ProBuilderMesh> meshes) {
            foreach (ProBuilderMesh mesh in meshes)
                MarkDirty(mesh);

            if (DirtyMeshes.Count == 0)
                return;

            Refresh();
            Rebuild();
        }

        private static bool IsSuspended(MeshFilter mesh) =>
            mesh.TryGetComponent(out ProBuilderMesh proBuilderMesh) && SuspendedMeshes.Contains(proBuilderMesh);

        private static List<MeshFilter> CandidateMeshes() {
            List<MeshFilter> meshes = new();
            foreach (ProBuilderMesh mesh in FindAll<ProBuilderMesh>())
                if (mesh.TryGetComponent(out MeshFilter filter))
                    meshes.Add(filter);

            if (!Cutters.Any(cutter => cutter && cutter.cutsMeshes))
                return meshes;

            foreach (MeshFilter filter in FindAll<MeshFilter>())
                if (IsRegularMesh(filter))
                    meshes.Add(filter);

            return meshes;
        }

        private static IEnumerable<T> FindAll<T>() where T : Component {
            HashSet<T> found = new(UnityCompat.FindAll<T>());
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                found.UnionWith(stage.FindComponentsOfType<T>());
            return found;
        }

        private static bool IsRegularMesh(MeshFilter filter) =>
            filter && !filter.TryGetComponent(out ProBuilderMesh _) && filter.TryGetComponent(out MeshRenderer _) &&
            filter.sharedMesh && (filter.sharedMesh.hideFlags & HideFlags.DontSaveInEditor) == 0 &&
            !TsitskiProBuilderCutTarget.IsResult(filter);

        [MenuItem("Tools/tsitski/ProBuilder/Generate/Rebuild All Cutters")]
        private static void ForceRebuildAll() {
            RebuildAll();
            _forceRebuild = true;
        }

        private static void RebuildAll() {
            Refresh();
            DirtyMeshes.UnionWith(CandidateMeshes());
            _nextRebuildTime = 0;
        }

        private static void OnCutterChanged(TsitskiProBuilderCutter cutter) {
            DirtyCutters.Add(cutter);
            _needsRefresh = true;
        }

        private static void Refresh() {
            _needsRefresh = false;

            Cutters.Clear();
            foreach (TsitskiProBuilderCutter cutter in FindAll<TsitskiProBuilderCutter>())
                Cutters.Add(cutter);

            Targets.Clear();
            Targets.AddRange(FindAll<TsitskiProBuilderCutTarget>());
            foreach (TsitskiProBuilderCutTarget target in SourceMeshes.Keys.Where(t => !t).ToList())
                SourceMeshes.Remove(target);
            SuspendedMeshes.RemoveWhere(m => !m);
        }

        private static void Update() {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_needsRefresh)
                Refresh();

            if (_resetChanges) {
                _resetChanges = false;
                DirtyCutters.Clear();
                DirtyMeshes.Clear();
                foreach (TsitskiProBuilderCutter cutter in Cutters)
                    cutter.transform.hasChanged = false;
                foreach (TsitskiProBuilderCutTarget target in Targets)
                    target.transform.hasChanged = false;
                UpdateEditing();
            }

            if (_rebuildAll) {
                _rebuildAll = false;
                _silentRebuild = true;
                RebuildAll();
            }

            if (DraggingPrefab())
                return;

            foreach (TsitskiProBuilderCutter cutter in Cutters) {
                if (!cutter || !cutter.transform.hasChanged)
                    continue;

                cutter.transform.hasChanged = false;
                DirtyCutters.Add(cutter);
            }

            bool repairColliders = EditorApplication.timeSinceStartup >= _nextColliderRepairTime;
            if (repairColliders)
                _nextColliderRepairTime = EditorApplication.timeSinceStartup + ColliderRepairInterval;

            bool dragging = DragAndDrop.objectReferences.Length > 0;
            foreach (TsitskiProBuilderCutTarget target in Targets) {
                if (repairColliders && target && target.RepairCollider() && target.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(target.gameObject.scene);

                if (target && target.TryGetComponent(out MeshFilter source)) {
                    if (SourceMeshes.TryGetValue(target, out Mesh previous) && previous != source.sharedMesh)
                        DirtyMeshes.Add(source);
                    SourceMeshes[target] = source.sharedMesh;

                    if (!dragging && !IsSuspended(source) && target.SyncMaterials())
                        DirtyMeshes.Add(source);
                }

                if (!target || !target.transform.hasChanged)
                    continue;

                target.transform.hasChanged = false;
                if (target.TryGetComponent(out MeshFilter mesh))
                    DirtyMeshes.Add(mesh);
            }

            if (DirtyCutters.Count == 0 && DirtyMeshes.Count == 0) {
                _silentRebuild = false;
                if (!RebuildQueue.HandleHeld && EditorApplication.timeSinceStartup - _lastRebuildTime > LightmapUvDelay) {
                    GeneratePendingLightmapUvs();
                    if (EditorApplication.timeSinceStartup >= _nextMissingUvCheck) {
                        _nextMissingUvCheck = EditorApplication.timeSinceStartup + MissingUvCheckInterval;
                        LightmapUvs.RegenerateMissing();
                    }
                }
                return;
            }

            if (DirtyCutters.Count > 0 && RebuildQueue.Deferring)
                return;

            if (EditorApplication.timeSinceStartup < _nextRebuildTime)
                return;

            _nextRebuildTime = EditorApplication.timeSinceStartup + RebuildInterval;
            Rebuild();
            _lastRebuildTime = EditorApplication.timeSinceStartup;
            _silentRebuild = false;
        }

        private static bool DraggingPrefab() =>
            DragAndDrop.objectReferences.Any(dragged => dragged is GameObject && UnityEditor.EditorUtility.IsPersistent(dragged));

        private static void GeneratePendingLightmapUvs() {
            if (PendingLightmapUvs.Count == 0)
                return;

            foreach (TsitskiProBuilderCutTarget target in PendingLightmapUvs)
                if (target)
                    target.GenerateLightmapUvs();
            PendingLightmapUvs.Clear();
        }

        private static void Rebuild() {
            List<MeshFilter> allMeshes = CandidateMeshes();
            CutterBounds.Clear();
            MeshBounds.Clear();

            Dictionary<TsitskiProBuilderCutter, List<MeshFilter>> cuts = new();
            foreach (TsitskiProBuilderCutter cutter in Cutters) {
                if (!IsActive(cutter))
                    continue;

                List<MeshFilter> meshes = new();
                foreach (MeshFilter mesh in allMeshes)
                    if (Cuts(cutter, mesh))
                        meshes.Add(mesh);
                if (meshes.Count > 0)
                    cuts[cutter] = meshes;
            }

            HashSet<MeshFilter> affected = new(DirtyMeshes.Where(m => m));

            foreach (TsitskiProBuilderCutTarget target in Targets) {
                if (!target)
                    continue;

                if (target.appliedCutters.Any(c => !c || DirtyCutters.Contains(c)) && target.TryGetComponent(out MeshFilter mesh))
                    affected.Add(mesh);
            }

            foreach (TsitskiProBuilderCutter cutter in DirtyCutters)
                if (cutter && cuts.TryGetValue(cutter, out List<MeshFilter> meshes))
                    affected.UnionWith(meshes);

            DirtyCutters.Clear();
            DirtyMeshes.Clear();
            bool force = _forceRebuild;
            _forceRebuild = false;

            List<TsitskiProBuilderCutter> involved = cuts.Where(pair => pair.Value.Any(affected.Contains)).Select(pair => pair.Key).ToList();
            foreach (TsitskiProBuilderCutter cutter in involved)
                affected.UnionWith(cuts[cutter]);

            Dictionary<MeshFilter, List<TsitskiProBuilderCutter>> cuttersByMesh = new();
            foreach ((TsitskiProBuilderCutter cutter, List<MeshFilter> meshes) in cuts)
                foreach (MeshFilter mesh in meshes) {
                    if (!cuttersByMesh.TryGetValue(mesh, out List<TsitskiProBuilderCutter> list))
                        cuttersByMesh[mesh] = list = new List<TsitskiProBuilderCutter>();
                    list.Add(cutter);
                }

            Dictionary<TsitskiProBuilderCutter, HashSet<MeshFilter>> owners = new();
            Dictionary<TsitskiProBuilderCutter, TunnelCap[]> tunnels = new();
            Dictionary<(TsitskiProBuilderCutter, MeshFilter), TunnelCap[]> slabs = new();
            foreach ((TsitskiProBuilderCutter cutter, List<MeshFilter> candidates) in cuts) {
                if (!candidates.Any(affected.Contains))
                    continue;

                if (cutter.clipsToTunnel)
                    tunnels[cutter] = TunnelCaps(cutter, candidates);

                List<MeshFilter> ranked = Ranked(cutter, candidates);
                switch (cutter.facesOn) {
                    case ProBuilderCutFaces.Shared:
                        owners[cutter] = new HashSet<MeshFilter>(ranked);
                        if (ranked.Count > 1 && tunnels.TryGetValue(cutter, out TunnelCap[] caps))
                            AddSlabs(cutter, ranked, caps, slabs);
                        break;
                    case ProBuilderCutFaces.Self:
                        owners[cutter] = new HashSet<MeshFilter>();
                        TsitskiProBuilderCutTarget.BuildCutterFaces(cutter, ranked, tunnels.GetValueOrDefault(cutter));
                        break;
                    default:
                        int index = cutter.facesOn == ProBuilderCutFaces.Second ? Mathf.Min(1, ranked.Count - 1) : 0;
                        owners[cutter] = new HashSet<MeshFilter> { ranked[index] };
                        break;
                }
            }

            foreach (TsitskiProBuilderCutter cutter in Cutters)
                if (cutter && cutter.generatedFaces && (cutter.facesOn != ProBuilderCutFaces.Self || !cuts.ContainsKey(cutter)))
                    TsitskiProBuilderCutTarget.ClearCutterFaces(cutter);

            foreach (MeshFilter mesh in affected.Where(m => !IsSuspended(m))) {
                List<TsitskiProBuilderCutter> cutters = cuttersByMesh.TryGetValue(mesh, out List<TsitskiProBuilderCutter> list) ? list : new List<TsitskiProBuilderCutter>();
                Dictionary<TsitskiProBuilderCutter, TunnelCap[]> meshTunnels = tunnels;
                foreach (TsitskiProBuilderCutter cutter in cutters)
                    if (slabs.TryGetValue((cutter, mesh), out TunnelCap[] slab)) {
                        meshTunnels = meshTunnels == tunnels ? new Dictionary<TsitskiProBuilderCutter, TunnelCap[]>(tunnels) : meshTunnels;
                        meshTunnels[cutter] = slab;
                    }

                RebuildMesh(mesh, cutters, owners, meshTunnels, force);
            }
        }

        private static void AddSlabs(TsitskiProBuilderCutter cutter, List<MeshFilter> meshes, TunnelCap[] caps,
            Dictionary<(TsitskiProBuilderCutter, MeshFilter), TunnelCap[]> slabs) {
            int axis = cutter.clipToTunnel.x ? 0 : cutter.clipToTunnel.y ? 1 : 2;
            List<(MeshFilter mesh, Vector2 range)> ordered = new();
            foreach (MeshFilter mesh in meshes)
                if (TsitskiProBuilderCutTarget.AxisRange(mesh.sharedMesh, mesh.transform.localToWorldMatrix, cutter, axis, out Vector2 range))
                    ordered.Add((mesh, range));
            ordered.Sort((a, b) => (a.range.x + a.range.y).CompareTo(b.range.x + b.range.y));

            for (int i = 0; i < ordered.Count; i++) {
                List<TunnelCap> slab = new(caps);
                if (i > 0)
                    slab.Add(SplitCap(axis, -1f, (ordered[i - 1].range.y + ordered[i].range.x) * 0.5f));
                if (i < ordered.Count - 1)
                    slab.Add(SplitCap(axis, 1f, (ordered[i].range.y + ordered[i + 1].range.x) * 0.5f));
                slabs[(cutter, ordered[i].mesh)] = slab.ToArray();
            }
        }

        private static TunnelCap SplitCap(int axis, float side, float position) {
            Vector3 normal = Vector3.zero;
            Vector3 point = Vector3.zero;
            normal[axis] = side;
            point[axis] = position;
            return new TunnelCap { valid = true, normal = normal, point = point };
        }

        private static List<MeshFilter> Ranked(TsitskiProBuilderCutter cutter, List<MeshFilter> candidates) {
            if (candidates.Count <= 1)
                return candidates.ToList();

            return candidates
                .Select(m => (mesh: m, area: Mathf.Round(TsitskiProBuilderCutTarget.PenetrationArea(
                    m.sharedMesh, m.transform.localToWorldMatrix, cutter) * 1000f)))
                .OrderByDescending(c => c.area)
                .ThenBy(c => GlobalObjectId.GetGlobalObjectIdSlow(c.mesh).ToString())
                .Select(c => c.mesh)
                .ToList();
        }

        private static TunnelCap[] TunnelCaps(TsitskiProBuilderCutter cutter, List<MeshFilter> candidates) {
            TunnelCap[] caps = new TunnelCap[6];

            foreach (MeshFilter mesh in candidates)
                TsitskiProBuilderCutTarget.TunnelCaps(mesh.sharedMesh,
                    mesh.transform.localToWorldMatrix, cutter, caps);

            return caps;
        }

        private static string HierarchyKey(Component component) {
            List<string> parts = new();
            for (Transform t = component.transform; t; t = t.parent)
                parts.Add(t.GetSiblingIndex().ToString("D6"));

            parts.Reverse();
            int componentIndex = Array.IndexOf(component.GetComponents<TsitskiProBuilderCutter>(), component);
            return $"{component.gameObject.scene.path}/{string.Join("/", parts)}#{componentIndex}";
        }

        private static void RebuildMesh(MeshFilter mesh, List<TsitskiProBuilderCutter> cutters,
            Dictionary<TsitskiProBuilderCutter, HashSet<MeshFilter>> owners, Dictionary<TsitskiProBuilderCutter, TunnelCap[]> tunnels, bool force) {
            cutters = cutters.OrderBy(HierarchyKey, StringComparer.Ordinal).ToList();
            bool changed = true;
            HashSet<TsitskiProBuilderCutter> insideCutters = new(cutters.Where(c => !owners.TryGetValue(c, out HashSet<MeshFilter> owner) || owner.Contains(mesh)));
            bool hasTarget = mesh.TryGetComponent(out TsitskiProBuilderCutTarget target);

            if (cutters.Count == 0 && !hasTarget)
                return;

            try {
                if (cutters.Count == 0) {
                    target.Remove();
                    _needsRefresh = true;
                }
                else {
                    if (!hasTarget) {
                        target = mesh.gameObject.AddComponent<TsitskiProBuilderCutTarget>();
                        target.transform.hasChanged = false;
                        _needsRefresh = true;
                    }

                    if (target.Rebuild(cutters, insideCutters, tunnels, out bool needsLightmapUvs, _silentRebuild, force)) {
                        target.SetEditing(IsEditing(target));
                        if (target.lastRebuildSkipped)
                            return;

                        if (needsLightmapUvs)
                            PendingLightmapUvs.Add(target);
                        else
                            PendingLightmapUvs.Remove(target);
                    }
                    else {
                        changed = hasTarget;
                        target.Remove();
                        _needsRefresh = true;
                    }
                }
            }
            catch (Exception e) {
                Debug.LogException(e, mesh);
            }

            if (changed && mesh.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(mesh.gameObject.scene);
        }

        private static bool IsActive(TsitskiProBuilderCutter cutter) =>
            cutter && cutter.isActiveAndEnabled && cutter.size.x > 0f && cutter.size.y > 0f && cutter.size.z > 0f;

        private static bool Cuts(TsitskiProBuilderCutter cutter, MeshFilter mesh) {
            if (!mesh || mesh.gameObject.scene != cutter.gameObject.scene || !mesh.gameObject.activeInHierarchy)
                return false;

            bool isProBuilder = mesh.TryGetComponent(out ProBuilderMesh proBuilderMesh);
            if (isProBuilder ? !cutter.cutsProBuilder || !proBuilderMesh.enabled : !cutter.cutsMeshes)
                return false;

            if (!MeshBounds.TryGetValue(mesh, out Bounds meshBounds)) {
                meshBounds = mesh.sharedMesh
                    ? WorldBounds(mesh.sharedMesh.bounds, mesh.transform.localToWorldMatrix)
                    : new Bounds(Vector3.positiveInfinity, Vector3.zero);
                MeshBounds[mesh] = meshBounds;
            }

            if (!CutterBounds.TryGetValue(cutter, out Bounds cutterBounds))
                CutterBounds[cutter] = cutterBounds = cutter.worldBounds;

            if (float.IsInfinity(meshBounds.center.x) || !cutterBounds.Intersects(meshBounds))
                return false;

            if ((isProBuilder && ProBuilderArrayPieces.IsSource(proBuilderMesh)) || TsitskiProBuilderCutTarget.IsResult(mesh))
                return false;

            if (mesh.TryGetComponent(out TsitskiProBuilderBakedCuts baked) && baked.cutters.Contains(cutter))
                return false;

            if (cutter.ignorePrefabMeshes && PrefabUtility.GetOutermostPrefabInstanceRoot(mesh.gameObject))
                return false;

            GameObject ignoreRoot = PrefabUtility.GetNearestPrefabInstanceRoot(cutter.gameObject);
            if (mesh.transform.IsChildOf(ignoreRoot ? ignoreRoot.transform : cutter.transform))
                return false;

            if (!cutter.cutThroughParent && cutter.transform.IsChildOf(mesh.transform))
                return false;

            if (!cutter.cutThroughParent && cutter.transform.parent) {
                MeshFilter parentMesh = cutter.transform.parent.GetComponentInParent<MeshFilter>(true);
                if (parentMesh && mesh.transform.IsChildOf(parentMesh.transform))
                    return false;
            }

            return true;
        }

        private static Bounds WorldBounds(Bounds local, Matrix4x4 matrix) {
            Bounds bounds = new(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f))));
            return bounds;
        }

        public static void SetSuspended(ProBuilderMesh mesh, bool suspended) {
            if (!mesh || (suspended ? !SuspendedMeshes.Add(mesh) : !SuspendedMeshes.Remove(mesh)))
                return;

            if (!suspended) {
                MarkDirty(mesh);
                _nextRebuildTime = 0;
            }

            if (mesh.TryGetComponent(out TsitskiProBuilderCutTarget target))
                target.SetEditing(IsEditing(target));
        }

        private static bool IsEditing(TsitskiProBuilderCutTarget target) =>
            (target.TryGetComponent(out MeshFilter mesh) && IsSuspended(mesh)) ||
            ToolManager.activeContextType != typeof(GameObjectToolContext) &&
            Selection.gameObjects.Contains(target.gameObject);

        private static void UpdateEditing() {
            foreach (TsitskiProBuilderCutTarget target in Targets)
                if (target)
                    target.SetEditing(IsEditing(target));
        }
    }
}
