using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tsitski.Core {
    [Serializable]
    public struct OrientedBounds {
        public Vector3 center;
        public Vector3 size;
        public Vector3 forward;
        public Vector3 right;
        [HideInInspector] public bool initialized;

        public Vector3 eulerAngles {
            get => forward.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f
                ? Quaternion.LookRotation(forward, up).eulerAngles
                : Vector3.zero;
            set {
                Quaternion rotation = Quaternion.Euler(value);
                forward = rotation * Vector3.forward;
                right = rotation * Vector3.right;
            }
        }

        public Vector3 up => Vector3.Cross(forward, right);
        public Quaternion rotation => forward.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f
            ? Quaternion.LookRotation(forward, up)
            : Quaternion.identity;
        public Vector3 halfSize => size / 2;
        public Vector3[] corners => CalculateCorners(center, size, forward, right);

        public OrientedBounds(BoxCollider boxCollider) {
            center = boxCollider.transform.TransformPoint(boxCollider.center);
            size = boxCollider.size;
            forward = boxCollider.transform.forward;
            right = boxCollider.transform.right;
            initialized = true;
        }

        public OrientedBounds(Vector3 center, Vector3 size, Vector3 forward, Vector3 right) {
            this.center = center;
            this.size = size;
            this.forward = forward;
            this.right = right;
            initialized = true;
        }

        public OrientedBounds(Vector3 center, Vector3 size, Transform transform) {
            this.center = center;
            this.size = size;
            forward = transform.forward;
            right = transform.right;
            initialized = true;
        }

        public OrientedBounds(OrientedBounds bounds, Vector3 newCenter) {
            center = newCenter;
            size = bounds.size;
            forward = bounds.forward;
            right = bounds.right;
            initialized = true;
        }

        public OrientedBounds(Vector3 size, Transform transform) {
            this.center = transform.position;
            this.size = size;
            forward = transform.forward;
            right = transform.right;
            initialized = true;
        }

        private Vector3[] CalculateCorners(Vector3 cornersCenter, Vector3 cornersSize, Vector3 cornersForward,
            Vector3 cornersRight) {
            Vector3[] newCorners = new Vector3[8];


            Vector3 halfSize = cornersSize * 0.5f;

            newCorners[0] = cornersCenter - cornersRight * halfSize.x - up * halfSize.y - cornersForward * halfSize.z;
            newCorners[1] = cornersCenter + cornersRight * halfSize.x - up * halfSize.y - cornersForward * halfSize.z;
            newCorners[2] = cornersCenter - cornersRight * halfSize.x - up * halfSize.y + cornersForward * halfSize.z;
            newCorners[3] = cornersCenter + cornersRight * halfSize.x - up * halfSize.y + cornersForward * halfSize.z;
            newCorners[4] = cornersCenter - cornersRight * halfSize.x + up * halfSize.y - cornersForward * halfSize.z;
            newCorners[5] = cornersCenter + cornersRight * halfSize.x + up * halfSize.y - cornersForward * halfSize.z;
            newCorners[6] = cornersCenter - cornersRight * halfSize.x + up * halfSize.y + cornersForward * halfSize.z;
            newCorners[7] = cornersCenter + cornersRight * halfSize.x + up * halfSize.y + cornersForward * halfSize.z;

            return newCorners;
        }

        //negative values are shrinking the box!
        public bool Contains(Vector3 point, float margin = 0) {
            Vector3 differenceDirection = point - center;

            float xDistance = Mathf.Abs(Vector3.Dot(right, differenceDirection));
            float yDistance = Mathf.Abs(Vector3.Dot(up, differenceDirection));
            float zDistance = Mathf.Abs(Vector3.Dot(forward, differenceDirection));

            if (xDistance > halfSize.x + margin ||
                yDistance > halfSize.y + margin ||
                zDistance > halfSize.z + margin)
                return false;

            return true;
        }

        public bool IntersectsSegment(Vector3 start, Vector3 end) {
            Vector3 localStart = ToLocal(start);
            Vector3 delta = ToLocal(end) - localStart;
            Vector3 half = halfSize;

            float enter = 0f;
            float exit = 1f;

            for (int axis = 0; axis < 3; axis++) {
                if (Mathf.Abs(delta[axis]) < 1e-6f) {
                    if (Mathf.Abs(localStart[axis]) > half[axis])
                        return false;

                    continue;
                }

                float near = (-half[axis] - localStart[axis]) / delta[axis];
                float far = (half[axis] - localStart[axis]) / delta[axis];

                if (near > far)
                    (near, far) = (far, near);

                enter = Mathf.Max(enter, near);
                exit = Mathf.Min(exit, far);

                if (enter > exit)
                    return false;
            }

            return true;
        }

        public Vector3 ClampToBounds(Vector3 position) {
            Vector3 difference = position - center;

            float x = Vector3.Dot(right, difference);
            float y = Vector3.Dot(up, difference);
            float z = Vector3.Dot(forward, difference);

            x = Mathf.Clamp(x, -halfSize.x, halfSize.x);
            y = Mathf.Clamp(y, -halfSize.y, halfSize.y);
            z = Mathf.Clamp(z, -halfSize.z, halfSize.z);

            return center + right * x + up * y + forward * z;
        }

        public Vector3 ToLocal(Vector3 position) {
            Vector3 difference = position - center;
            return new Vector3(Vector3.Dot(right, difference), Vector3.Dot(up, difference), Vector3.Dot(forward, difference));
        }

        public Vector3 ToWorld(Vector3 local) => center + right * local.x + up * local.y + forward * local.z;

        public OrientedBounds Transformed(Transform transform) =>
            new(transform.TransformPoint(center), size, transform.TransformDirection(forward).normalized,
                transform.TransformDirection(right).normalized);

        public Vector3 GetClosestPoint(Vector3 position) {
            return ClampToBounds(position);
        }

        public OrientedBounds Encapsulate(Vector3 position) {
            Vector3 f = forward.normalized;
            Vector3 r = right.normalized;
            Vector3 u = Vector3.Cross(f, r).normalized;
            r = Vector3.Cross(u, f).normalized;

            Vector3 delta = position - center;

            float px = Vector3.Dot(r, delta);
            float py = Vector3.Dot(u, delta);
            float pz = Vector3.Dot(f, delta);

            float minX = -halfSize.x;
            float maxX = halfSize.x;
            float minY = -halfSize.y;
            float maxY = halfSize.y;
            float minZ = -halfSize.z;
            float maxZ = halfSize.z;

            if (px < minX) minX = px;
            if (px > maxX) maxX = px;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
            if (pz < minZ) minZ = pz;
            if (pz > maxZ) maxZ = pz;

            Vector3 newCenter =
                center +
                r * ((minX + maxX) * 0.5f) +
                u * ((minY + maxY) * 0.5f) +
                f * ((minZ + maxZ) * 0.5f);

            Vector3 newSize = new Vector3(
                maxX - minX,
                maxY - minY,
                maxZ - minZ
            );

            return new OrientedBounds(newCenter, newSize, f, r);
        }

        public static OrientedBounds GetBestFit(IReadOnlyList<Collider> colliders) {
            if (colliders == null || colliders.Count == 0)
                return default;

            int start = -1;

            for (int i = 0; i < colliders.Count; i++) {
                if (colliders[i]) {
                    start = i;
                    break;
                }
            }

            if (start == -1)
                return default;

            var first = colliders[start];

            Vector3 f = first.transform.forward;
            Vector3 r = first.transform.right;

            if (f.sqrMagnitude < 1e-8f) f = Vector3.forward;
            if (r.sqrMagnitude < 1e-8f) r = Vector3.right;

            var orientedBounds = new OrientedBounds(first.bounds.center, Vector3.zero, f, r);

            for (int i = start; i < colliders.Count; i++) {
                var col = colliders[i];
                if (!col)
                    continue;

                orientedBounds = orientedBounds.Encapsulate(col.ClosestPoint(orientedBounds.center + orientedBounds.forward * 3));
                orientedBounds = orientedBounds.Encapsulate(col.ClosestPoint(orientedBounds.center + orientedBounds.right * 3));
                orientedBounds = orientedBounds.Encapsulate(col.ClosestPoint(orientedBounds.center + orientedBounds.up * 3));
                orientedBounds = orientedBounds.Encapsulate(col.ClosestPoint(orientedBounds.center + -orientedBounds.forward * 3));
                orientedBounds = orientedBounds.Encapsulate(col.ClosestPoint(orientedBounds.center + -orientedBounds.right * 3));
                orientedBounds = orientedBounds.Encapsulate(col.ClosestPoint(orientedBounds.center + -orientedBounds.up * 3));
            }

            return orientedBounds;
        }

        private float resolvingIterations => 25;

        public Vector3 GetSafePosition(int layerMask) {
            Vector3 f = forward.normalized;
            if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;

            Vector3 r = right.normalized;
            if (r.sqrMagnitude < 1e-6f || Mathf.Abs(Vector3.Dot(f, r)) > 0.999f)
                r = Vector3.Cross(Vector3.up, f);

            Vector3 u = Vector3.Cross(f, r).normalized;
            r = Vector3.Cross(u, f).normalized;

            Quaternion rotation = Quaternion.LookRotation(f, u);

            var tempGO = new GameObject("TEMP_OBB_SAFE_POSITION");
            tempGO.hideFlags = HideFlags.HideAndDontSave;

            tempGO.transform.SetPositionAndRotation(center, rotation);

            var box = tempGO.AddComponent<BoxCollider>();
            box.size = size;
            box.center = Vector3.zero;
            box.isTrigger = true;

            Vector3 position = center;

            for (int iteration = 0; iteration < resolvingIterations; iteration++) {
                var overlaps = Physics.OverlapBox(
                    position,
                    halfSize,
                    rotation,
                    layerMask,
                    QueryTriggerInteraction.Ignore
                );

                bool moved = false;

                foreach (var other in overlaps) {
                    if (!other || other == box)
                        continue;

                    if (Physics.ComputePenetration(
                            box, position, rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 dir, out float dist)) {
                        position += dir * (dist + 0.001f);
                        moved = true;
                    }
                }

                if (!moved)
                    break;
            }

            Object.DestroyImmediate(tempGO);

            return position;
        }
    }
}
