using System.Collections.Generic;
using System.Linq;
using Tsitski.Core;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using EditorUtility = UnityEditor.EditorUtility;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal static class TsitskiArrayWatcher {
        static TsitskiArrayWatcher() {
            ProBuilderEditor.afterMeshModification += SourcesChanged;
            EditorMeshUtility.meshOptimized += (mesh, _) => SourcesChanged(new[] { mesh });
            Selection.selectionChanged += UpdateVisibility;
            EditorApplication.hierarchyChanged += UpdateVisibility;
            EditorApplication.update += Update;

            TsitskiArrayEditorHooks.Suspend += SetSuspended;
            TsitskiArrayEditorHooks.InspectorGUI += DrawSourceGUI;
            TsitskiArrayEditorHooks.StatsAssigned += array => {
                if (ProBuilderArrayPieces.SourceInStats(array))
                    LoadSource(array);
            };
            TsitskiArrayEditorHooks.SavingStats += (array, stats) => {
                ProBuilderMesh source = ProBuilderArrayPieces.LocalSource(array);
                if (!source || EditorUtility.IsPersistent(source))
                    return;

                stats.piece = null;
                stats.storeSource = true;
                stats.storedPiece = ArraySourceData.Capture(source);
            };
            TsitskiArrayEditorHooks.CopiedStats += array => {
                if (ProBuilderArrayPieces.SourceInStats(array))
                    LoadSource(array);
            };
        }

        private static void SetSuspended(TsitskiArray array, bool suspended) {
            if (array && array.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        private static void DrawSourceGUI(TsitskiArray array) {
            if (array.activePiece && !ProBuilderArrayPieces.ActiveSource(array))
                return;

            bool sourceInStats = ProBuilderArrayPieces.SourceInStats(array);
            ProBuilderMesh localSource = ProBuilderArrayPieces.LocalSource(array);

            using (new EditorGUILayout.HorizontalScope()) {
                ProBuilderMesh source = ProBuilderArrayPieces.ActiveSource(array);
                if (source && GUILayout.Button("Select Source"))
                    Selection.activeObject = source.gameObject;
                if (!array.piece && !sourceInStats && GUILayout.Button("Create Source")) {
                    Undo.RecordObject(array, "Create Array Source");
                    ProBuilderArrayPieces.CreateSource(array);
                    array.Rebuild();
                }
            }

            bool canRotate = localSource && localSource.gameObject.scene.IsValid() || sourceInStats;
            using (new EditorGUI.DisabledScope(!canRotate))
                if (GUILayout.Button("Rotate Source 90° Y"))
                    RotateSource(array, Quaternion.Euler(0f, 90f, 0f));

            if (!array.stats)
                return;

            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUI.DisabledScope(!localSource))
                    if (GUILayout.Button(new GUIContent("Save Source To Stats", "Copy this array's source mesh into the stats.")))
                        StoreSource(array);

                using (new EditorGUI.DisabledScope(!sourceInStats))
                    if (GUILayout.Button(new GUIContent("Load Source From Stats", "Replace this array's source mesh with the one in the stats.")))
                        LoadSource(array);
            }
        }

        private static void RotateSource(TsitskiArray array, Quaternion rotation) {
            ProBuilderMesh source = ProBuilderArrayPieces.LocalSource(array);

            if (source && source.gameObject.scene.IsValid()) {
                Undo.RecordObject(source, "Rotate Array Source");
                source.positions = source.positions.Select(p => rotation * p).ToArray();
                source.ToMesh();
                source.Refresh();
                EditorUtility.SetDirty(source);

                if (array.stats && array.stats.storeSource)
                    StoreSource(array);
                else
                    array.Rebuild();
                return;
            }

            ArraySourceData data = ProBuilderArrayPieces.StoredSource(array.stats);
            if (data == null)
                return;

            Undo.RecordObject(array.stats, "Rotate Array Source");
            data.positions = data.positions.Select(p => rotation * p).ToList();
            EditorUtility.SetDirty(array.stats);
            array.stats.NotifyChanged();
        }

        private static void LoadSource(TsitskiArray array) {
            ArraySourceData data = ProBuilderArrayPieces.StoredSource(array.stats);
            if (data == null)
                return;

            ProBuilderMesh source = ProBuilderArrayPieces.LocalSource(array);
            if (source && source.gameObject.scene.IsValid()) {
                Undo.RecordObjects(new Object[] { source, source.transform }, "Load Array Source");
                if (source.TryGetComponent(out MeshRenderer renderer))
                    Undo.RecordObject(renderer, "Load Array Source");
                data.ApplyTo(source);
                EditorUtility.SetDirty(source);
            }
            else {
                Undo.RecordObject(array, "Load Array Source");
                ProBuilderArrayPieces.CreateSourceFrom(array, data);
            }

            array.Rebuild();
        }

        private static void SourcesChanged(IEnumerable<ProBuilderMesh> meshes) {
            HashSet<ProBuilderMesh> changed = new(meshes.Where(m => m));
            foreach (TsitskiArray array in TsitskiArray.All.ToArray()) {
                if (!array)
                    continue;

                if (StoresLocalSource(array) && changed.Contains(ProBuilderArrayPieces.LocalSource(array)))
                    StoreSource(array);
                else if (changed.Contains(ProBuilderArrayPieces.ActiveSource(array)))
                    array.QueueRebuild();
            }
        }

        private static bool StoresLocalSource(TsitskiArray array) {
            ProBuilderMesh source = ProBuilderArrayPieces.LocalSource(array);
            return array.stats && array.stats.storeSource && source && source.gameObject.scene.IsValid();
        }

        internal static void StoreSource(TsitskiArray array) {
            Undo.RecordObject(array.stats, "Store Array Source");
            array.stats.storedPiece = ArraySourceData.Capture(ProBuilderArrayPieces.LocalSource(array));
            EditorUtility.SetDirty(array.stats);
            array.stats.NotifyChanged();
        }

        private static void Update() {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (TsitskiArray array in TsitskiArray.All.ToArray()) {
                if (!array)
                    continue;

                bool storing = StoresLocalSource(array);
                ProBuilderMesh source = storing ? ProBuilderArrayPieces.LocalSource(array) : ProBuilderArrayPieces.ActiveSource(array);
                if (!source || !source.gameObject.scene.IsValid() || !source.transform.hasChanged)
                    continue;

                source.transform.hasChanged = false;
                if (storing)
                    StoreSource(array);
                else
                    array.QueueRebuild();
            }
        }

        private static void UpdateVisibility() {
            foreach (TsitskiArray array in TsitskiArray.All.ToArray()) {
                ProBuilderMesh source = array ? ProBuilderArrayPieces.LocalSource(array) : null;
                if (!source || !source.gameObject.scene.IsValid() || !source.TryGetComponent(out MeshRenderer renderer))
                    continue;

                bool visible = Selection.gameObjects.Contains(source.gameObject);
                if (renderer.enabled != visible)
                    renderer.enabled = visible;
            }
        }
    }
}
