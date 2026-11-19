namespace Tsitski.Core {
    public static class CoreTooltips {
        public const string PathLayerName = "Name of this layer. Follows the stats' name until you type your own.";
        public const string PathLayerStats = "What this layer builds along the path: Array, Trim or Fence Stats, whichever this generator supports.";
        public const string PathLayerWallSide = "Which wall faces get this layer: Inside faces the floor plan's interior, Outside faces away from it, or Both.";
        public const string PathLayerPolesFrom = "Line the fence's poles up with the slots of an array layer on the same path, for example posts from that layer.";
        public const string PathLayerHeight = "Height of this layer, measured from the path, or from the top when Height From Top is set.";
        public const string PathLayerHeightFromTop = "Measure Height from the top instead of the path. A floor plan measures down from the top of its walls, a path assembly up from its height.";
        public const string PathLayerMatchHeight = "Pillars placed by this layer's array reach the top of the assembly or walls, however high the layer starts.";
        public const string PathLayerSideOffset = "How far this layer is moved sideways from the path, positive is to the right of its direction.";

        public const string PathEditing =
            "Edited in the Scene view. Click a point to select it, Shift+click or Shift+drag to select several. Selected points follow the Move / Rotate / Scale tool. " +
            "Click an edge midpoint to insert a point, click the dot past an open end to extend the path.";
        public const string Radii = "Corner radius per point. Select points and drag the cyan handle on a corner to round it.";

        public const string ArrayStats = "Shared settings. Also holds the piece, the stored source when Store Source is on, and the path when Store Path is on.";
        public const string ArrayPiece =
            "What is copied along the path: a prefab or model, Mesh Pillar Stats, a ProBuilder mesh, or ProBuilder Pillar Stats. " +
            "Its +Z points along the path and +Y up. Left empty, the array places 0.25m cubes.";
        public const string ArrayStatsPiece = "Piece shared by every array using these stats, overrides the array's own piece.";
        public const string ArrayPillarHeight = "Height of each pillar when the piece is pillar stats.";
        public const string MergeIntoMesh =
            "Combine the pieces into this array's own mesh instead of placing separate objects for pillars. Snapping still applies to each pillar before merging.";
        public const string StoreSource =
            "Store the source mesh in the stats. Every array using them copies it, and editing an array's own source saves into the stats.";
        public const string ArrayPlacement =
            "Spacing places one copy per slot along the path. Span Ends places, at every slot, one copy facing into each span next to it, for example brackets on both sides of a post.";
        public const string ArrayPieceRotation = "Extra rotation applied to every copy, in degrees, for pieces modelled facing another way.";
        public const string ArrayCornerSide =
            "Corner Side given to the pillars this array places. Floor plans set it per wall face: Outside for exterior faces, Inside for interior ones.";
        public const string Spacing = "Distance between copies along the path.";
        public const string Offset = "Moves the first copy along each division (or run) when Fit Spacing is off.";
        public const string FitSpacing =
            "Adjust the spacing inside each division (or run) so copies land evenly from end to end. Off keeps the exact Spacing, starting at Offset.";
        public const string SplitAtCorners =
            "Treat each straight run between corners (turns over 15 degrees) on its own, so spacing restarts at every corner and corners always get a slot.";
        public const string DivisionLength =
            "Split every run into equal divisions as close to this length as fits (0 = no divisions), and space copies inside each division. " +
            "Match it with another array's spacing to fill the gaps between its copies.";
        public const string SkipCorners = "Leave out the copies that land on corners (and the ends of an open path).";
        public const string SkipDivisions = "Leave out the copies that land on division points, for example where another array's pillars stand.";
        public const string AlignToPath = "Rotate each copy to follow the path direction.";
        public const string RoundingSegments = "How smooth rounded corners are.";
        public const string ArrayPath = "Path the copies are placed along. " + PathEditing;
        public const string Closed = "Connect the last point back to the first.";
        public const string StorePath = "Store the path in the stats, editing it updates everything using the asset.";
        public const string GeneratorMaterial =
            "Material locked onto every face of the generated mesh, reapplied on each rebuild and skipped by Setup Materials. Empty leaves materials free.";
        public const string AutoNameGenerators = "Name generator objects (cutters, trims, arrays, stairs, pillars, floor plans) after their stats asset, or after the component when they have no stats.";
        public const string ContributeGIByDefault = "Turn on the Contribute GI static flag on objects when a generator component is added to them (polygons, floor plans, trims, arrays, stairs, pillars) and on the child objects generators create.";
        public const string PlacementLayers = "Layers Snap To Wall snaps onto.";
        public const string ShowReviewButton = "Show the Leave A Review button at the bottom of the tools popup.";
        public const string GizmosOnlyOnSelected = "Only draw cutter gizmos for selected cutters.";
        public const string RebuildAfterDragging = "Wait until a dragged handle is released before rebuilding: recutting meshes around a cutter (box, polygon points, path) and rebuilding trims, arrays, stairs, pillars and floor plans from their paths and profiles. Turn off to rebuild live while dragging.";
        public const string DefaultMeshMaterial = "Material given to generated meshes (polygons, roofs, trims, arrays, stairs, pillars, floor plan walls) when they don't have one.";
    }
}
