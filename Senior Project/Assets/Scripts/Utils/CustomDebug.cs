using System.Drawing;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class CustomDebug
{
#if UNITY_EDITOR
    private static int GIZMOS_SEGMENTS = 16;

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
        float angleStep = 360f / GIZMOS_SEGMENTS;
        Vector3 lastPoint = tip + Vector3.up * radius;  // Start from a point on the circle

        // Loop to draw the circle around the base of the cone
        for (int i = 1; i <= GIZMOS_SEGMENTS; i++)
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
    /// Draws a wireframe sector cut from a sphere with <paramref name="maxDistance"/>
    /// as its radius. The sector is defined by the horizontal XZ angle around
    /// <paramref name="direction"/>; the sphere's vertical bounds are preserved.
    /// </summary>
    /// <param name="center">The center of the sector.</param>
    /// <param name="direction">The forward direction used to orient the XZ sector.</param>
    /// <param name="halfAngle">The sector's horizontal half-angle in degrees.</param>
    /// <param name="maxDistance">The radius of the sphere.</param>
    public static void DrawWireSphereSectorIgnoringY(Vector3 center, Vector3 direction, float halfAngle, float maxDistance)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontalDirection.sqrMagnitude < 0.0001f || maxDistance <= 0f)
            return;

        horizontalDirection.Normalize();
        halfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
        int azimuthSegments = GIZMOS_SEGMENTS * 2;
        int elevationSegments = GIZMOS_SEGMENTS;
        float azimuthStep = (halfAngle * 2f) / azimuthSegments;
        float elevationStep = 180f / elevationSegments;
        Vector3 right = Vector3.Cross(Vector3.up, horizontalDirection).normalized;

        Vector3 PointOnSphere(float azimuth, float elevation)
        {
            float azimuthRadians = azimuth * Mathf.Deg2Rad;
            float elevationRadians = elevation * Mathf.Deg2Rad;
            Vector3 horizontal = Mathf.Cos(azimuthRadians) * horizontalDirection +
                Mathf.Sin(azimuthRadians) * right;
            Vector3 radialDirection = Mathf.Cos(elevationRadians) * horizontal +
                Mathf.Sin(elevationRadians) * Vector3.up;
            return center + radialDirection * maxDistance;
        }

        // Draw horizontal latitude arcs across the spherical sector.
        for (int elevationIndex = 0; elevationIndex <= elevationSegments; elevationIndex++)
        {
            float elevation = -90f + elevationIndex * elevationStep;
            Vector3 lastPoint = PointOnSphere(-halfAngle, elevation);

            for (int azimuthIndex = 1; azimuthIndex <= azimuthSegments; azimuthIndex++)
            {
                float azimuth = -halfAngle + azimuthIndex * azimuthStep;
                Vector3 nextPoint = PointOnSphere(azimuth, elevation);
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }
        }

        // Draw the longitude arcs that form the rest of the spherical wire mesh.
        for (int azimuthIndex = 0; azimuthIndex <= azimuthSegments; azimuthIndex++)
        {
            float azimuth = -halfAngle + azimuthIndex * azimuthStep;
            Vector3 lastPoint = PointOnSphere(azimuth, -90f);

            for (int elevationIndex = 1; elevationIndex <= elevationSegments; elevationIndex++)
            {
                float elevation = -90f + elevationIndex * elevationStep;
                Vector3 nextPoint = PointOnSphere(azimuth, elevation);
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }
        }

        // Draw the vertical diameter passing through the sphere's center.
        Gizmos.DrawLine(
            center - Vector3.up * maxDistance,
            center + Vector3.up * maxDistance);

        // Draw the sector's radial boundaries at the equator.
        Gizmos.DrawLine(center, PointOnSphere(-halfAngle, 0f));
        Gizmos.DrawLine(center, PointOnSphere(halfAngle, 0f));
    }

    /// <summary>
    /// Draws a wireframe capsule shape using Handles.
    /// </summary>
    public static void DrawWireCapsule(Vector3 point1, Vector3 point2, float radius)
    {
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
    }

    /// <summary>
    /// Draws a wireframe circle shape using Handles.
    /// </summary>
    public static void DrawWireCircle(Vector3 center, float radius)
    {
        Handles.color = Gizmos.color;

        // Draw the wire circle using Handles
        Handles.DrawWireArc(center, Vector3.up, Vector3.right, 360f, radius);
    }
#endif

    /// <summary>
    /// Instantiates a temporary sphere GameObject with the specified center, radius, expire duration, and color.
    /// The sphere is created as a primitive sphere with a Collider set as a trigger.
    /// The sphere's material is set to be transparent using the Universal Render Pipeline/Unlit shader.
    /// The sphere is destroyed after the specified expire duration.
    /// </summary>
    /// <param name="center">The center position of the sphere.</param>
    /// <param name="radius">The radius of the sphere.</param>
    /// <param name="expireDuration">The duration in seconds before the sphere is destroyed.</param>
    /// <param name="color">The color of the sphere.</param>
    public static GameObject InstantiateTemporarySphere(Vector3 center, float radius, float expireDuration, UnityEngine.Color color)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.GetComponent<Collider>().enabled = false;
        sphere.name = "Temporary Sphere";
        sphere.transform.position = center;
        sphere.transform.localScale = radius * 2f * Vector3.one;

        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        sphereRenderer.material = GetTransparentMaterial();
        sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sphereRenderer.material.color = color;
        GameObject.Destroy(sphere, expireDuration);

        return sphere;
    }

    /// <summary>
    /// Generates a transparent material.
    /// </summary>
    /// <returns>The transparent material.</returns>
    public static Material GetTransparentMaterial()
    {
        Material transparentMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        // Change Surface Type to Transparent
        transparentMaterial.SetFloat("_Surface", 1); // 1 = Transparent, 0 = Opaque

        // Enable required shader keywords
        transparentMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        transparentMaterial.DisableKeyword("_SURFACE_TYPE_OPAQUE");

        // Set rendering mode for transparency
        transparentMaterial.SetOverrideTag("RenderType", "Transparent");
        transparentMaterial.SetInt("_ZWrite", 0); // Disable ZWrite for transparency
        transparentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        transparentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        // Apply the changes to the material
        transparentMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return transparentMaterial;
    }
}