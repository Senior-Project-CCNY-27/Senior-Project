using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [System.Serializable]
    internal class OutlineTrimOperation : SceneOperation {
        private const string MenuPath = "Tools/tsitski/ProBuilder/Generate/Outline Trim";
        private const float MinMitre = 0.25f;

        private enum Side {
            Outside,
            Inside,
            Center
        }

        private enum Height {
            Bottom,
            Top
        }

        private enum Output {
            Trim,
            Array
        }

        [SerializeField] private Output _output;
        [SerializeField] private Side _side;
        [SerializeField] private Height _height;
        [SerializeField] private float _offset;
        [SerializeField] private string _statsGuid = "";
        [SerializeField] private string _arrayStatsGuid = "";

        private readonly List<Source> _sources = new();
        private readonly List<Component> _created = new();
        private Output _builtOutput;
        private string _builtStats;

        protected override string title => "Outline Trim";
        protected override int fieldCount => 5;

        private class Source {
            public ProBuilderMesh mesh;
            public List<List<Vector2>> outer;
            public List<List<Vector2>> inner;
            public float bottom, top, halfThickness;
        }

        [MenuItem(MenuPath)]
        private static void Outline() => Run(new OutlineTrimOperation());

        [MenuItem(MenuPath, true)]
        private static bool OutlineValidate() => Selection.gameObjects.Any(g => g.GetComponent<ProBuilderMesh>());

        protected override bool Begin() {
            HashSet<ProBuilderMesh> meshes = new();
            foreach (GameObject selected in Selection.gameObjects)
                if (selected.TryGetComponent(out ProBuilderMesh mesh))
                    meshes.Add(WallsFor(mesh));

            foreach (ProBuilderMesh mesh in meshes) {
                Source source = Read(mesh);
                if (source != null)
                    _sources.Add(source);
            }

            if (_sources.Count > 0)
                return true;

            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("No outline found on the selected mesh"));
            return false;
        }

        private static ProBuilderMesh WallsFor(ProBuilderMesh mesh) {
            if (mesh.GetComponent<TsitskiProBuilderFloorPlan>())
                return mesh;

            foreach (TsitskiProBuilderFloorPlan plan in UnityCompat.FindAll<TsitskiProBuilderFloorPlan>())
                if (plan.floor == mesh && plan.TryGetComponent(out ProBuilderMesh walls))
                    return walls;
            return mesh;
        }

        private static Source Read(ProBuilderMesh mesh) {
            List<List<Vector2>> loops = TsitskiProBuilderFloorPlan.FloorOutlines(mesh, out float top);
            if (loops.Count == 0)
                return null;

            float outerSign = Mathf.Sign(CutterOutline.SignedArea(loops.OrderByDescending(l => Mathf.Abs(CutterOutline.SignedArea(l))).First()));
            Source source = new() {
                mesh = mesh,
                outer = new List<List<Vector2>>(),
                inner = new List<List<Vector2>>(),
                top = top,
                bottom = mesh.positions.Min(p => p.y)
            };

            foreach (List<Vector2> loop in loops) {
                bool isOuter = Mathf.Sign(CutterOutline.SignedArea(loop)) == outerSign;
                if (CutterOutline.SignedArea(loop) < 0f == isOuter)
                    loop.Reverse();
                (isOuter ? source.outer : source.inner).Add(loop);
            }

            source.halfThickness = HalfThickness(source.outer, source.inner);
            return source;
        }

        private static float HalfThickness(List<List<Vector2>> outer, List<List<Vector2>> inner) {
            if (inner.Count == 0)
                return 0f;

            float total = 0f;
            int count = 0;
            foreach (Vector2 point in outer.SelectMany(l => l)) {
                float nearest = float.MaxValue;
                foreach (List<Vector2> loop in inner)
                    for (int i = 0; i < loop.Count; i++)
                        nearest = Mathf.Min(nearest, SegmentDistance(point, loop[i], loop[(i + 1) % loop.Count]));
                total += nearest;
                count++;
            }

            return count > 0 ? total / count * 0.5f : 0f;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        protected override void DrawFields() {
            _output = (Output)EditorGUILayout.EnumPopup(new GUIContent("Create", "Create a Trim, or an Array of meshes placed along the outline."), _output);
            _side = (Side)EditorGUILayout.EnumPopup(new GUIContent("Side", "Outside runs around the outer face of the walls, Inside around the room faces (or inward along the edge of a mesh with no inner outline), Center along the middle of the wall."), _side);
            _height = (Height)EditorGUILayout.EnumPopup(new GUIContent("Height", "Place the trim at the bottom or the top of the mesh."), _height);
            _offset = EditorGUILayout.FloatField(new GUIContent("Offset", "Moves the trim away from the wall surface. Negative values move it into the wall."), _offset);

            if (_output == Output.Trim)
                _statsGuid = StatsField("Trim Stats", "Trim Stats to give the new trims. Stats that store their own path are ignored.", _statsGuid,
                    typeof(TsitskiProBuilderTrimStats));
            else
                _arrayStatsGuid = StatsField("Array Stats",
                    "Array Stats to give the new arrays, including their source mesh. Stats that store their own path are ignored. Without stats each array gets a default cube source.",
                    _arrayStatsGuid, typeof(TsitskiArrayStats));
        }

        private static string StatsField(string label, string tooltip, string guid, System.Type type) {
            Object current = Load<Object>(guid);
            Object chosen = EditorGUILayout.ObjectField(new GUIContent(label, tooltip), current, type, false);
            return chosen == current ? guid : chosen ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(chosen)) : "";
        }

        private static T Load<T>(string guid) where T : Object =>
            string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

        protected override void Apply() {
            List<(Source source, List<Vector2> loop)> paths = new();
            foreach (Source source in _sources)
                foreach (List<Vector2> loop in Loops(source))
                    paths.Add((source, loop));

            string statsGuid = _output == Output.Trim ? _statsGuid : _arrayStatsGuid;
            if (_builtOutput != _output || _builtStats != statsGuid)
                DestroyCreated();
            _builtOutput = _output;
            _builtStats = statsGuid;

            while (_created.Count > paths.Count) {
                if (_created[^1])
                    Object.DestroyImmediate(_created[^1].gameObject);
                _created.RemoveAt(_created.Count - 1);
            }

            for (int i = 0; i < paths.Count; i++) {
                (Source source, List<Vector2> loop) = paths[i];
                if (i >= _created.Count || !_created[i])
                    Create(source, i);

                Component created = _created[i];
                Transform meshTransform = source.mesh.transform;
                created.transform.SetPositionAndRotation(meshTransform.position, meshTransform.rotation);
                Matrix4x4 toLocal = created.transform.worldToLocalMatrix * meshTransform.localToWorldMatrix;

                float y = _height == Height.Top ? source.top : source.bottom;
                List<Vector3> path = loop.Select(p => toLocal.MultiplyPoint3x4(new Vector3(p.x, y, p.y))).ToList();

                if (created is TsitskiProBuilderTrim trim) {
                    trim.path = path;
                    trim.pathRadii = new List<float>();
                    trim.closed = true;
                    trim.Rebuild();
                }
                else if (created is TsitskiArray array) {
                    array.path = path;
                    array.pathRadii = new List<float>();
                    array.closed = true;
                    array.Rebuild();
                }
            }
        }

        private IEnumerable<List<Vector2>> Loops(Source source) {
            switch (_side) {
                case Side.Outside:
                    return source.outer.Select(l => Offset(l, _offset));
                case Side.Inside:
                    if (source.inner.Count > 0)
                        return source.inner.Select(l => Offset(l, _offset));
                    return source.outer.Select(l => {
                        List<Vector2> reversed = new(l);
                        reversed.Reverse();
                        return Offset(reversed, _offset);
                    });
                default:
                    return source.outer.Select(l => Offset(l, _offset - source.halfThickness));
            }
        }

        private static List<Vector2> Offset(List<Vector2> loop, float distance) {
            if (Mathf.Abs(distance) < 1e-6f)
                return new List<Vector2>(loop);

            List<Vector2> result = new(loop.Count);
            for (int i = 0; i < loop.Count; i++) {
                Vector2 previous = loop[(i - 1 + loop.Count) % loop.Count];
                Vector2 next = loop[(i + 1) % loop.Count];
                Vector2 n0 = Right((loop[i] - previous).normalized);
                Vector2 n1 = Right((next - loop[i]).normalized);

                Vector2 mitre = n0 + n1;
                mitre = mitre.sqrMagnitude > 1e-8f ? mitre.normalized : n0;
                float dot = Mathf.Max(Vector2.Dot(mitre, n0), MinMitre);
                result.Add(loop[i] + mitre * (distance / dot));
            }

            return result;
        }

        private static Vector2 Right(Vector2 direction) => new(direction.y, -direction.x);

        private void Create(Source source, int index) {
            GameObject createdObject = new(ObjectNames.GetUniqueName(
                source.mesh.transform.parent
                    ? source.mesh.transform.parent.Cast<Transform>().Select(t => t.name).ToArray()
                    : source.mesh.gameObject.scene.GetRootGameObjects().Select(g => g.name).ToArray(),
                $"{source.mesh.name} Outline {_output}"));
            createdObject.transform.SetParent(source.mesh.transform.parent, false);
            createdObject.transform.SetSiblingIndex(source.mesh.transform.GetSiblingIndex() + 1);
            if (source.mesh.gameObject.scene != createdObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(createdObject, source.mesh.gameObject.scene);

            Component created = _output == Output.Trim ? CreateTrim(createdObject) : CreateArray(createdObject);

            if (index < _created.Count)
                _created[index] = created;
            else
                _created.Add(created);
        }

        private TsitskiProBuilderTrim CreateTrim(GameObject createdObject) {
            TsitskiProBuilderTrim trim = createdObject.AddComponent<TsitskiProBuilderTrim>();
            if (!createdObject.TryGetComponent(out MeshCollider _))
                createdObject.AddComponent<MeshCollider>();

            TsitskiProBuilderTrimStats stats = Load<TsitskiProBuilderTrimStats>(_statsGuid);
            trim.stats = stats && !stats.storePath ? stats : null;
            return trim;
        }

        private TsitskiArray CreateArray(GameObject createdObject) {
            TsitskiArray array = createdObject.AddComponent<TsitskiArray>();
            TsitskiArrayStats stats = Load<TsitskiArrayStats>(_arrayStatsGuid);
            if (stats && stats.storePath)
                stats = null;

            array.stats = stats;
            bool statsSource = stats && stats.piece;
            ArraySourceData stored = ProBuilderArrayPieces.StoredSource(stats);
            ProBuilderMesh localSource = ProBuilderArrayPieces.LocalSource(array);

            if ((statsSource || stored != null) && localSource) {
                Object.DestroyImmediate(localSource.gameObject);
                array.piece = null;
            }

            if (stored != null)
                ProBuilderArrayPieces.CreateSourceFrom(array, stored);
            else if (!statsSource && !array.piece)
                ProBuilderArrayPieces.CreateSource(array);

            return array;
        }

        protected override void Cancel() => DestroyCreated();

        protected override void Abandon() => DestroyCreated();

        protected override void Complete() {
            List<GameObject> objects = _created.Where(t => t).Select(t => t.gameObject).ToList();
            foreach (GameObject createdObject in objects)
                Undo.RegisterCreatedObjectUndo(createdObject, $"Outline {_output}");
            _created.Clear();

            if (objects.Count > 0)
                Selection.objects = objects.Cast<Object>().ToArray();
        }

        private void DestroyCreated() {
            foreach (Component created in _created)
                if (created)
                    Object.DestroyImmediate(created.gameObject);
            _created.Clear();
        }
    }
}
