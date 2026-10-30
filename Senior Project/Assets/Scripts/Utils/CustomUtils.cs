using UnityEngine;

public static class CustomUtils 
{
    /// <summary>
    /// Helper method to set the alpha value of a Unity UI Image component.
    /// Needed because Image.color.a = alpha does not work.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="alpha"></param>
    public static void SetImageAlpha(this UnityEngine.UI.Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    /// <summary>
    /// Returns a new Vector3 with one component (x, y, or z) set to a new value.
    /// Vector3.x = x does not work as expected in Unity, so this is a workaround.
    /// </summary>
    public static Vector3 WithX(this Vector3 vector, float x) => new Vector3(x, vector.y, vector.z);
    /// <summary>
    /// Returns a new Vector3 with one component (x, y, or z) set to a new value.
    /// Vector3.z = z does not work as expected in Unity, so this is a workaround.
    /// </summary>
    public static Vector3 WithY(this Vector3 vector, float y) => new Vector3(vector.x, y, vector.z);
    /// <summary>
    /// Returns a new Vector3 with one component (x, y, or z) set to a new value.
    /// Vector3.z = z does not work as expected in Unity, so this is a workaround.
    /// </summary>
    public static Vector3 WithZ(this Vector3 vector, float z) => new Vector3(vector.x, vector.y, z);

    /// <summary>
    /// Sets the x component of a Vector3 to a new value.
    /// Vector3.x = x does not work as expected in Unity, so this is a workaround.
    /// </summary>
    public static void SetX(this ref Vector3 vector, float x) => vector = vector.WithX(x);
    /// <summary>
    /// Sets the y component of a Vector3 to a new value.
    /// Vector3.y = y does not work as expected in Unity, so this is a workaround.
    /// </summary>
    public static void SetY(this ref Vector3 vector, float y) => vector = vector.WithY(y);
    /// <summary>
    /// Sets the z component of a Vector3 to a new value.
    /// Vector3.z = z does not work as expected in Unity, so this is a workaround.
    /// </summary>
    public static void SetZ(this ref Vector3 vector, float z) => vector = vector.WithZ(z);

    public static string FloatToString(float value, int decimalPlaces = 2)
    {
        string format = "F" + decimalPlaces;
        return value.ToString(format);
    }

    /// <summary>
    /// Calculates the movement input relative to the camera's orientation.
    /// The return result is normalized.
    /// </summary>
    public static Vector3 GetCameraBasedMoveInput(Transform cameraTransform, Vector2 moveInput)
    {
        if (cameraTransform == null)
            return new Vector3(moveInput.x, 0, moveInput.y);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0; // Ignore vertical component
        right.y = 0; // Ignore vertical component

        forward.Normalize();
        right.Normalize();

        return (forward * moveInput.y + right * moveInput.x).normalized;
    }

    /// <summary>
    /// Calculates the jump force from the jump height using the physics equation
    /// </summary>
    public static float GetJumpForce(float jumpHeight, float gravity) => Mathf.Sqrt(2f * gravity * jumpHeight);
        
    /// <summary>
    /// Checks to see if a layer exists in the layer mask
    /// </summary>
    public static bool Contains(this LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;
    
    /// <summary>
    /// Checks to see if a position is visible from the camera screen.
    /// </summary>
    public static bool IsPositionOnScreen(Camera camera, Vector3 worldPosition)
    {
        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f &&
               viewport.x >= 0f && viewport.x <= 1f &&
               viewport.y >= 0f && viewport.y <= 1f;
    }
    
    /// <summary>
    /// Checks to see if a collider is within the camera's view.
    /// </summary>
    public static bool IsColliderOnScreen(Camera camera, Collider collider)
    {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, collider.bounds);
    }
    
    /// <summary>
    /// Gets the screen-space distance in pixels from the camera's center to a world position.
    /// </summary>
    public static float GetScreenDistanceFromCenter(Camera camera, Vector3 worldPosition)
    {
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        return Vector2.Distance(screenPosition, camera.pixelRect.center);
    }
    
    /// <summary>
    /// Returns a new Color with its red component set to a new value.
    /// </summary>
    public static Color WithR(this Color color, float r) =>
        new Color(r, color.g, color.b, color.a);

    /// <summary>
    /// Returns a new Color with its green component set to a new value.
    /// </summary>
    public static Color WithG(this Color color, float g) =>
        new Color(color.r, g, color.b, color.a);

    /// <summary>
    /// Returns a new Color with its blue component set to a new value.
    /// </summary>
    public static Color WithB(this Color color, float b) =>
        new Color(color.r, color.g, b, color.a);

    /// <summary>
    /// Returns a new Color with its alpha component set to a new value.
    /// </summary>
    public static Color WithA(this Color color, float a) =>
        new Color(color.r, color.g, color.b, a);
}