# tsitski Generator Conventions

Shared features every tsitski generator (ProBuilder Tools, and Mesh Tools where it applies) should support.
When adding a generator, go through this list. When adding a feature to one generator, add it here and
bring the others in line, or record the gap in the table at the bottom.

## Component

- Attributes: `[AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder X")]`, `[ExecuteAlways]`,
  `[DisallowMultipleComponent]`, `[RequireComponent(typeof(ProBuilderMesh))]`.
- `Reset()`: `TsitskiSettings.ApplyDefaultStaticFlags(gameObject)` and add a `MeshCollider` (via `Undo.AddComponent` in the editor).
- `OnEnable()`: `mesh.Clear()` when the ProBuilderMesh has no faces, subscribe to `XStats.Changed`, `QueueRebuild()`.
  `OnDisable()` unsubscribes. `OnValidate()` calls `QueueRebuild()`.
- Keep a `public static readonly HashSet<X> All` (added in OnEnable, removed in OnDisable) when an editor watcher needs to find instances.
- `QueueRebuild() => RebuildQueue.Queue(this, Rebuild)`. RebuildQueue never runs in play mode, so anything the
  mesh needs at runtime must already be serialized (see Smooth Collider below).
- `Rebuild()` builds the mesh then calls `GeneratorInputs.Store(this)`. Implement `IInputHashed` with a
  `[SerializeField, HideInInspector] int inputHash` so unchanged generators are skipped when scenes load.
- `public static event Action<ProBuilderMesh> Rebuilt`, invoked at the end of every rebuild. Register it in
  `ProBuilderCutSystem` (`MarkDirty`) and `RealBlendSetup` (`OnResultRebuilt`) so cutters and Real Blend follow the mesh.

## Rebuild

- `TsitskiSettings.AutoName(this, stats, "ProBuilder X")` first. Also add the generator to `TsitskiProBuilderAutoNames`.
- `KeptFaceMaterials.Capture(mesh)` before rebuilding and `kept?.Apply(mesh)` after, so hand-painted face materials survive.
- `mesh.RebuildWithPositionsAndFaces`, then `ForcedMaterials.Apply(mesh, regions, materials...)`, `mesh.ToMesh()`, `mesh.Refresh()`.
- Lightmap UVs: `CutLightmapUnwrap.ApplyToProBuilderMesh(mesh)` after Refresh, and a public `GenerateLightmapUvs()`
  hooked into `LightmapUvs.cs` so Tools > Core > Generate Lightmap UVs covers it.
- Default material: if the renderer has no material, assign `TsitskiProBuilderSettings.DefaultMaterial`.
- `EditorUtility.SetDirty(mesh)` in the editor, then invoke `Rebuilt`.

## Stats

- Every generator has a paired `TsitskiProBuilderXStats` ScriptableObject (`[CreateAssetMenu(menuName = "tsitski ProBuilder Tools/X Stats")]`)
  with `public static event Action<XStats> Changed`, `NotifyChanged()`, and `OnValidate() => Changed?.Invoke(this)`.
- Each setting exists on both the component and the stats, read through an `activeX` property:
  `public float activeX => stats ? stats.x : x;`. Clamp in the `active` getter, not only with `[Min]`.
- Paths can optionally live in stats (`savePolygon` / `savePaths` / `storePath`), exposed as `pathInStats` with `activePath` get/set
  that writes to whichever object owns them. Undo-record that owner (`pathsOwner`), not always the component.
- The inspector hides local fields that the stats override, and offers "Save As X Stats…" (`StatsSavePanel`) and
  "Copy Stats Into X". Both must copy every field, including newly added ones.
- Values that are often per object (height, thickness, width) get a `storeX` toggle on the stats (default on for existing assets),
  with `[TsitskiShowIf(nameof(storeX))]` on the value. Off means each object keeps its own value; read it through an
  `xInStats` property, hide the component field only when it is true, and point gizmos at the stats or the component accordingly.
- Tooltips are constants in `Tooltips.cs`, never inline strings.
- When renaming or restructuring serialized fields, keep old data: `[FormerlySerializedAs]` for renames, or hidden
  `legacy*` fields upgraded in `ISerializationCallbackReceiver.OnAfterDeserialize` (see `PolygonPath.Upgrade`), then
  resave the scenes and stats assets that use it.

## Materials

- Implement `IForcedMaterials` returning the locked material slots. Setup Materials skips locked faces.
- Tag faces with region indices while building and pass them to `ForcedMaterials.Apply`. Untagged faces (-1) stay
  free to paint, so tag every face that belongs to a slot.
- Name slots after the surface they cover (for example `topStepMaterial`, `forwardStepMaterial`, `sideMaterial`).

## Shading and collision

- `smoothingAngle` (`[Range(0, 180)]`, tooltip `Tooltips.SmoothingAngle`) on component and stats, applied with the shared
  `SmoothingAngle.Apply(positions, faces, angle)` after building. Faces sharing an edge at less than the angle share a smoothing
  group; adjacent smooth regions get different groups (ProBuilder has 24 smooth groups).
- Optional smooth collider (Stairs): a separate collider mesh serialized on the component, re-assigned in `Start()` and
  `OnEnable()` because `ProBuilderMesh.Awake` overwrites the MeshCollider with the render mesh.

## Path editing (editor)

- Use `PathPointHandles` for scene editing: rounding radii, insert, delete, transform handles, point copy/paste.
  The radii slot is for corner rounding only. Other per-point data (heights, widths) goes through the `sourceValues` argument of
  `PathPointHandles.Do` and comes back as `PathEdit.values`, so it follows inserts, deletes and pastes (new points get 0).
  Draw inside `Handles.DrawingScope(Color.white, transform.localToWorldMatrix)` so paths are in local space.
- Implement `IPathOwner` (`localPath`, `pathInStats`, `Rebuild`) so Move Pivot and other path tools keep world positions.
  For several paths, return the first and shift the others by the same delta.
- Implement `IPivotShiftable` so Tools > Core > Center Transform To Mesh keeps the generator in place: `ShiftLocalPoints(offset)`
  adds the offset to every local-space point it stores (all paths, holes, branches), `ApplyPivotShift()` rebuilds, and
  `CanShiftPivot` refuses with a reason when the points live in a stats asset or the shape is built around the pivot.
  Generators without it are skipped by the tool rather than moved.
- Implement `ITsitskiPath` (`GetWorldPath`) for open/closed paths so Mesh Supports and other path consumers can follow it.
- Split Path: the editor implements `ITsitskiPathSplitter` with `TsitskiPathSplit.CanSplitPieces` / `TsitskiPathSplit.SplitPieces`
  and `TsitskiPathSplit.Duplicate`, so Tools > Core > Split Path works on it. SplitPieces works on point indices, so per-point data
  (widths, rotations, radii) is sliced with `TsitskiPathSplit.Pick`. Pass the number of paths the generator holds: with more than
  one, a fully selected path moves into a new object. Closed paths split into open pieces (Polygon keeps each piece as a closed
  outline). Disable splitting while the path lives in a stats asset or is generated by another object.
  Any object copied with `Instantiate` must go through `TsitskiPathSplit.Duplicate` or call `TsitskiPathSplit.MakeMeshesUnique`,
  otherwise the copy shares the original's scene mesh and rebuilding one overwrites both.
- Whole-path copy/paste: copying with every point of a path selected stores it with `PointClipboard.CopyPath<T>`;
  pasting creates a new path (or hole) of that type, selected so it can be dragged straight away.
- Deleting: generators with several paths set `deletesPath = true` (and `openMinimum` for open paths) so deleting a fully
  selected path, or leaving it below the minimum point count, removes the path. The inspector graph does the same via
  `GraphPoints.deletePath`.
- Rect selection via `SceneRectSelection.Handle`, `Tools.hidden` while points are selected, and
  `ProBuilderCutSystem.SetSuspended(mesh, true)` while editing so cuts don't rebuild on every drag.
- After scene edits: `Undo.RecordObject(owner)`, edit, `SetDirty` the stats if they own it, `RebuildQueue.Run(stats, stats.NotifyChanged)`,
  then `RebuildQueue.Run(generator, generator.Rebuild)`.
- An `[InitializeOnLoad]` watcher that requeues every instance on `Undo.undoRedoPerformed` (with `onlyIfChanged: true`).

## Gizmos

- Height / thickness: `SliderHandles.Length(origin, up, ref height, HandleTheme.Secondary)` (dotted line + cone).
  Use `SliderHandles.SignedLength` when the value may go negative. Width across a path: `SliderHandles.Width`.
- Colours come from `HandleTheme` (Path for outlines, Secondary for holes and dimensions, Selected, Guide).

## Registration

- Create menu entries in `TsitskiProBuilderCreateMenu` (Tools/tsitski/ProBuilder/Create and GameObject/tsitski ProBuilder Tools),
  created through `TsitskiCreateMenu.Create<T>`.
- Add a demo object to the Demo scene, and a Changelog entry under the current version.

## Current gaps

| Generator | Split Path | Path copy/paste | Delete path | Smoothing angle | Lightmap UVs on rebuild | Undo watcher |
|---|---|---|---|---|---|---|
| Trim | yes | no | n/a (one path) | yes | yes | no |
| Stairs | yes | no | n/a (one path) | no | yes | no |
| Polygon | yes | yes | yes | no | no | yes |
| Floor Plan | yes | no | n/a (one path) | no | no | yes |
| Roof | yes | yes | yes | yes | no | yes |
| Pillar | n/a (profile) | no | n/a | yes | no | no |
| Path | yes | yes | yes | yes | yes | yes |
| Cutter | yes (no stats) | no | n/a (one path) | n/a | n/a | no |

Mesh Tools: Array, Fence, Mesh Decal, Path Assembly and Support split; Rope does not (its points come from what it hangs between).
