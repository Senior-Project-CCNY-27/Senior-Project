using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    [InitializeOnLoad]
    public static class OrientedBoundsGizmoDrawer {
        static readonly Dictionary<MonoBehaviour, Vector3> _lastPos      = new();
        static readonly Dictionary<MonoBehaviour, Quaternion> _lastRot      = new();
        // Tracks which component IDs were selected on the previous frame —
        // prevents a false delta when re-selecting an object that was moved while deselected
        static readonly HashSet<MonoBehaviour>           _selectedPrev = new();

        static OrientedBoundsGizmoDrawer() {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView _) {
            var go = Selection.activeGameObject;

            var selectedThisFrame = new HashSet<MonoBehaviour>();

            if (go) {
                foreach (var component in go.GetComponents<MonoBehaviour>()) {
                    if (!component) continue;

                    MonoBehaviour id         = component;
                    Vector3    currentPos = component.transform.position;
                    Quaternion currentRot = component.transform.rotation;

                    selectedThisFrame.Add(id);

                    // Only apply deltas when continuously selected (prevents false jump on re-select)
                    if (_selectedPrev.Contains(id)) {
                        if (_lastPos.TryGetValue(id, out Vector3 lastPos)) {
                            Vector3 posDelta = currentPos - lastPos;
                            if (posDelta.sqrMagnitude > 1e-8f)
                                ApplyPositionDelta(component, posDelta);
                        }
                        if (_lastRot.TryGetValue(id, out Quaternion lastRot)) {
                            Quaternion rotDelta = currentRot * Quaternion.Inverse(lastRot);
                            if (Quaternion.Angle(Quaternion.identity, rotDelta) > 0.001f)
                                ApplyRotationDelta(component, rotDelta);
                        }
                    }

                    _lastPos[id] = currentPos;
                    _lastRot[id] = currentRot;

                    // Draw handles for every annotated OrientedBounds field
                    var type = component.GetType();
                    while (type != null && type != typeof(MonoBehaviour)) {
                        foreach (var field in type.GetFields(
                            BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                            if (!field.IsDefined(typeof(OrientedBoundsGizmoAttribute), false)) continue;

                            if (field.FieldType == typeof(OrientedBounds)) {
                                var bounds = (OrientedBounds)field.GetValue(component);
                                DrawHandle(component, IsLocal(field), bounds, b => field.SetValue(component, b));
                            }
                            else if (field.FieldType == typeof(OrientedBounds[]) &&
                                     field.GetValue(component) is OrientedBounds[] array) {
                                for (int i = 0; i < array.Length; i++) {
                                    int index = i;
                                    DrawHandle(component, IsLocal(field), array[i], b => array[index] = b);
                                }
                            }
                        }
                        type = type.BaseType;
                    }
                }
            }

            _selectedPrev.Clear();
            foreach (MonoBehaviour id in selectedThisFrame) _selectedPrev.Add(id);
        }

        // Shift every annotated OrientedBounds on a component by delta (world space)
        static void ApplyPositionDelta(MonoBehaviour component, Vector3 delta) {
            bool dirty = false;
            var  type  = component.GetType();

            while (type != null && type != typeof(MonoBehaviour)) {
                foreach (var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                    if (!field.IsDefined(typeof(OrientedBoundsGizmoAttribute), false)) continue;
                    if (IsLocal(field)) continue; // local bounds follow the transform already
                    if (field.FieldType != typeof(OrientedBounds) && field.FieldType != typeof(OrientedBounds[])) continue;

                    if (!dirty) {
                        Undo.RecordObject(component, "Move Oriented Bounds");
                        dirty = true;
                    }

                    ApplyToBounds(component, field, b => new OrientedBounds(b.center + delta, b.size, b.forward, b.right));
                }
                type = type.BaseType;
            }

            if (dirty) EditorUtility.SetDirty(component);
        }

        // Rotate every annotated OrientedBounds forward/right by a world-space rotation delta
        static void ApplyRotationDelta(MonoBehaviour component, Quaternion delta) {
            bool dirty = false;
            var  type  = component.GetType();

            while (type != null && type != typeof(MonoBehaviour)) {
                foreach (var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                    if (!field.IsDefined(typeof(OrientedBoundsGizmoAttribute), false)) continue;
                    if (IsLocal(field)) continue; // local bounds follow the transform already
                    if (field.FieldType != typeof(OrientedBounds) && field.FieldType != typeof(OrientedBounds[])) continue;

                    if (!dirty) {
                        Undo.RecordObject(component, "Rotate Oriented Bounds");
                        dirty = true;
                    }

                    ApplyToBounds(component, field, b => new OrientedBounds(b.center, b.size, Round(delta * b.forward), Round(delta * b.right)));
                }
                type = type.BaseType;
            }

            if (dirty) EditorUtility.SetDirty(component);
        }

        static void ApplyToBounds(MonoBehaviour component, FieldInfo field, System.Func<OrientedBounds, OrientedBounds> change) {
            if (field.GetValue(component) is OrientedBounds[] array) {
                for (int i = 0; i < array.Length; i++)
                    array[i] = change(array[i]);
                return;
            }

            field.SetValue(component, change((OrientedBounds)field.GetValue(component)));
        }

        static Vector3 Round(Vector3 v) =>
            new((float)System.Math.Round(v.x, 3), (float)System.Math.Round(v.y, 3), (float)System.Math.Round(v.z, 3));

        static bool IsLocal(FieldInfo field) {
            return field.GetCustomAttribute<OrientedBoundsGizmoAttribute>(false).local;
        }

        static OrientedBounds ToWorld(OrientedBounds b, Transform t) {
            return new OrientedBounds(t.TransformPoint(b.center), b.size,
                t.TransformDirection(b.forward), t.TransformDirection(b.right));
        }

        static void DrawHandle(MonoBehaviour component, bool local, OrientedBounds b, System.Action<OrientedBounds> write) {
            Transform transform = component.transform;

            // First-time initialization: place at transform position/rotation with size 1
            if (!b.initialized) {
                b = local
                    ? new OrientedBounds(Vector3.zero, Vector3.one, Vector3.forward, Vector3.right)
                    : new OrientedBounds(transform.position, Vector3.one, transform.forward, transform.right);
                Undo.RecordObject(component, "Initialize Oriented Bounds");
                write(b);
                EditorUtility.SetDirty(component);
            }

            // Handles work in world space, local bounds are converted back when written
            OrientedBounds stored = b;
            if (local) b = ToWorld(b, transform);

            Vector3 f = b.forward;
            Vector3 r = b.right;

            // Defensive fix: zero directions (shouldn't happen after init, but guard anyway)
            if (f.sqrMagnitude < 0.01f) f = b.forward;
            if (r.sqrMagnitude < 0.01f) r = b.right;

            Vector3 u  = b.up;
            Vector3 c  = b.center;
            Vector3 hs = b.halfSize;

            Handles.color = HandleTheme.Path;
            DrawWireCube(b);

            if (Tools.current == Tool.Rotate) {
                EditorGUI.BeginChangeCheck();
                Quaternion rotation = Handles.RotationHandle(Quaternion.LookRotation(f, u), c);

                if (EditorGUI.EndChangeCheck()) {
                    Vector3 newForward = rotation * Vector3.forward;
                    Vector3 newRight = rotation * Vector3.right;
                    OrientedBounds rotated = local
                        ? new OrientedBounds(stored.center, stored.size,
                            transform.InverseTransformDirection(newForward).normalized,
                            transform.InverseTransformDirection(newRight).normalized)
                        : new OrientedBounds(c, b.size, newForward, newRight);

                    Undo.RecordObject(component, "Rotate Oriented Bounds");
                    write(rotated);
                    EditorUtility.SetDirty(component);
                    return;
                }
            }

            if (Tools.current == Tool.Move) {
                Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local
                    ? Quaternion.LookRotation(f, u)
                    : Quaternion.identity;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(c, handleRotation);

                if (EditorGUI.EndChangeCheck()) {
                    OrientedBounds shifted = local
                        ? new OrientedBounds(transform.InverseTransformPoint(moved), stored.size, stored.forward, stored.right)
                        : new OrientedBounds(moved, b.size, b.forward, b.right);

                    Undo.RecordObject(component, "Move Oriented Bounds");
                    write(shifted);
                    EditorUtility.SetDirty(component);
                    return;
                }
            }

            float dotSize = HandleUtility.GetHandleSize(c) * 0.04f;
            Handles.color = HandleTheme.Path;

            Vector3 fp  = c + f *  hs.z;
            Vector3 fn  = c - f *  hs.z;
            Vector3 rp  = c + r *  hs.x;
            Vector3 rn  = c - r *  hs.x;
            Vector3 up_ = c + u *  hs.y;
            Vector3 dn  = c - u *  hs.y;

            EditorGUI.BeginChangeCheck();

            Vector3 nfp = Handles.Slider(fp,   f, dotSize, Handles.DotHandleCap, 0f);
            Vector3 nfn = Handles.Slider(fn,  -f, dotSize, Handles.DotHandleCap, 0f);
            Vector3 nrp = Handles.Slider(rp,   r, dotSize, Handles.DotHandleCap, 0f);
            Vector3 nrn = Handles.Slider(rn,  -r, dotSize, Handles.DotHandleCap, 0f);
            Vector3 nup = Handles.Slider(up_,  u, dotSize, Handles.DotHandleCap, 0f);
            Vector3 ndn = Handles.Slider(dn,  -u, dotSize, Handles.DotHandleCap, 0f);

            if (!EditorGUI.EndChangeCheck()) return;

            // Delta-based: untouched handles contribute exactly 0, avoiding float drift
            float hzP = Mathf.Max(0, hs.z + Vector3.Dot(nfp - fp,  f));
            float hzN = Mathf.Max(0, hs.z + Vector3.Dot(fn  - nfn,  f));
            float hxP = Mathf.Max(0, hs.x + Vector3.Dot(nrp - rp,  r));
            float hxN = Mathf.Max(0, hs.x + Vector3.Dot(rn  - nrn,  r));
            float hyP = Mathf.Max(0, hs.y + Vector3.Dot(nup - up_,  u));
            float hyN = Mathf.Max(0, hs.y + Vector3.Dot(dn  - ndn,  u));

            Vector3 newSize   = new Vector3(hxP + hxN, hyP + hyN, hzP + hzN);
            Vector3 newCenter = c
                + f * (hzP - hzN) * 0.5f
                + r * (hxP - hxN) * 0.5f
                + u * (hyP - hyN) * 0.5f;

            OrientedBounds edited = local
                ? new OrientedBounds(transform.InverseTransformPoint(newCenter), newSize, stored.forward, stored.right)
                : new OrientedBounds(newCenter, newSize, b.forward, b.right);

            Undo.RecordObject(component, "Edit Oriented Bounds");
            write(edited);
            EditorUtility.SetDirty(component);
        }

        // Corner layout (from OrientedBounds.CalculateCorners):
        //   0 = -r -u -f   1 = +r -u -f   2 = -r -u +f   3 = +r -u +f
        //   4 = -r +u -f   5 = +r +u -f   6 = -r +u +f   7 = +r +u +f
        static void DrawWireCube(OrientedBounds b) {
            var v = b.corners;

            Handles.DrawAAPolyLine(2f, v[0], v[1], v[3], v[2], v[0]);
            Handles.DrawAAPolyLine(2f, v[4], v[5], v[7], v[6], v[4]);
            Handles.DrawAAPolyLine(2f, v[0], v[4]);
            Handles.DrawAAPolyLine(2f, v[1], v[5]);
            Handles.DrawAAPolyLine(2f, v[2], v[6]);
            Handles.DrawAAPolyLine(2f, v[3], v[7]);
        }
    }
}
