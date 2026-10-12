using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    //Setting variables for look sensivity
    [Header("Look")]
    public float sensitivity = 1f;
    public float maxYRot = 90f;
    public float minYRot = -75f;

    //This is public variables to store the cameraFollow target
    [Header("Follow")]
    public Transform targetTransform;
    public UpdateMode followUpdateMode = UpdateMode.FixedUpdate;

    private float xRot;
    private float yRot;

    public enum UpdateMode
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    private void Awake()
    {
        LockCursor(true);
    }

    private void Update()
    {
        HandleLook();

        if (followUpdateMode != UpdateMode.Update)
            return;

        FollowTarget();
    }

    private void FixedUpdate()
    {
        if (followUpdateMode != UpdateMode.FixedUpdate)
            return;

        FollowTarget();
    }

    private void LateUpdate()
    {
        if (followUpdateMode != UpdateMode.LateUpdate)
            return;

        FollowTarget();
    }

    private void FollowTarget()
    {
        transform.position = targetTransform.position;
    }

    private void HandleLook()
    {
        float xInput = Input.GetAxisRaw("Mouse X");
        float yInput = Input.GetAxisRaw("Mouse Y");

        xRot -= yInput * sensitivity;
        yRot += xInput * sensitivity;

        xRot = Mathf.Clamp(xRot, minYRot, maxYRot);

        transform.rotation = Quaternion.Euler(xRot, yRot, 0f);
    }

    public static void LockCursor(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}