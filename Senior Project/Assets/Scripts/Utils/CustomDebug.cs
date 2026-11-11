using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class CustomDebug
{
    private const int GizmosSegments = 8;
    private const string TemporarySpherePrefabPath = "TransparentSphere";

    /// <summary>
    /// Draws a wireframe cone shape.
    /// </summary>
    public static void DrawWireCone(Vector3 center, Vector3 direction, float halfAngle, float maxDistance)
    {
        // Normalize the direction of the cone
        direction = direction.normalized;

        // Calculate the radius of the cone's base at maxDistance
        float radius = maxDistance * Mathf.Tan(halfAngle * Mathf.Deg2Rad);

        // Tip of the cone at the maxDistance along the direction vector
        Vector3 tip = center + direction * maxDistance;

        // Draw the line from the center to the tip of the cone (the cone's axis)
        Gizmos.DrawLine(center, tip);

        // Draw the base circle at maxDistance along the direction vector
        float angleStep = 360f / GizmosSegments;
        Vector3 lastPoint = tip + Vector3.up * radius;  // Start from a point on the circle

        // Loop to draw the circle around the base of the cone
        for (int i = 1; i <= GizmosSegments; i++)
        {
            float angle = angleStep * i;
            // Generate a point on the circle in the XZ plane at maxDistance
            Vector3 nextPoint = tip + Quaternion.AngleAxis(angle, direction) * Vector3.up * radius;
            Gizmos.DrawLine(lastPoint, nextPoint);
            Gizmos.DrawLine(center, nextPoint);  // Draw lines from center to the base
            lastPoint = nextPoint;
        }
    }

    /// <summary>
    /// Draws a flat XZ-plane oval sector oriented along <paramref name="direction"/>.
    /// The forward and lateral radii define the ellipse; the half-angle is measured
    /// from the supplied forward direction.
    /// </summary>
    public static void DrawWireEllipseSector(
        Vector3 center,
        Vector3 direction,
        float forwardRadius,
        float lateralRadius,
        float halfAngle)
    {
        Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f || forwardRadius <= 0f || lateralRadius <= 0f)
            return;

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        halfAngle = Mathf.Clamp(halfAngle, 0f, 180f);

        Vector3 PointOnEllipse(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            float forwardComponent = Mathf.Cos(radians);
            float lateralComponent = Mathf.Sin(radians);
            float ellipseRadius = 1f / Mathf.Sqrt(
                (forwardComponent * forwardComponent) / (forwardRadius * forwardRadius) +
                (lateralComponent * lateralComponent) / (lateralRadius * lateralRadius));

            Vector3 radialDirection = forward * forwardComponent + right * lateralComponent;
            return center + radialDirection * ellipseRadius;
        }

        const int segments = 32;
        Vector3 firstPoint = PointOnEllipse(-halfAngle);
        Vector3 previousPoint = firstPoint;

        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
            Vector3 nextPoint = PointOnEllipse(angle);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }

        Gizmos.DrawLine(center, firstPoint);
        Gizmos.DrawLine(center, previousPoint);
    }

    /// <summary>
    /// Draws a wireframe capsule shape using Handles.
    /// </summary>
    public static void DrawWireCapsule(Vector3 point1, Vector3 point2, float radius)
    {
#if UNITY_EDITOR
        Handles.color = Gizmos.color;

        Vector3 upOffset = point2 - point1;
        Vector3 up = upOffset.Equals(default) ? Vector3.up : upOffset.normalized;
        Quaternion orientation = Quaternion.FromToRotation(Vector3.up, up);
        Vector3 forward = orientation * Vector3.forward;
        Vector3 right = orientation * Vector3.right;
        // z axis
        Handles.DrawWireArc(point2, forward, right, 180, radius);
        Handles.DrawWireArc(point1, forward, right, -180, radius);
        Handles.DrawLine(point1 + right * radius, point2 + right * radius);
        Handles.DrawLine(point1 - right * radius, point2 - right * radius);
        // x axis
        Handles.DrawWireArc(point2, right, forward, -180, radius);
        Handles.DrawWireArc(point1, right, forward, 180, radius);
        Handles.DrawLine(point1 + forward * radius, point2 + forward * radius);
        Handles.DrawLine(point1 - forward * radius, point2 - forward * radius);
        // y axis
        Handles.DrawWireDisc(point2, up, radius);
        Handles.DrawWireDisc(point1, up, radius);
#endif
    }

    /// <summary>
    /// Draws a wireframe circle shape using Handles.
    /// </summary>
    public static void DrawWireCircle(Vector3 center, float radius)
    {
#if UNITY_EDITOR
        Handles.color = Gizmos.color;

        // Draw the wire circle using Handles
        Handles.DrawWireArc(center, Vector3.up, Vector3.right, 360f, radius);
#endif
    }

    /// <summary>
    /// Instantiates a temporary sphere GameObject with the specified center, radius, expire duration, and color.
    /// The sphere is instantiated from the TransparentSphere prefab in Resources.
    /// The sphere is destroyed after the specified expire duration.
    /// </summary>
    /// <param name="center">The center position of the sphere.</param>
    /// <param name="radius">The radius of the sphere.</param>
    /// <param name="expireDuration">The duration in seconds before the sphere is destroyed.</param>
    /// <param name="color">The color of the sphere.</param>
    public static GameObject InstantiateTemporarySphere(Vector3 center, float radius, float expireDuration, UnityEngine.Color? color)
    {
        GameObject spherePrefab = Resources.Load<GameObject>(TemporarySpherePrefabPath);
        if (spherePrefab == null)
        {
            Debug.LogError($"Can't find temporary sphere prefab at Resources/{TemporarySpherePrefabPath}");
            return null;
        }

        GameObject sphere = GameObject.Instantiate(spherePrefab, center, Quaternion.identity);
        sphere.name = "Temporary Sphere";
        sphere.transform.localScale = radius * 2f * Vector3.one;
        GameObject.Destroy(sphere, expireDuration);

        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        if (color.HasValue && sphereRenderer != null)
            sphereRenderer.material.color = color.Value;

        return sphere;
    }

}
