using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    //Setting variables for look sensivity
    [Header("Look")]
    public float _sensitivity = 1f;
    public float _maxYRot = 90f;
    public float _minYRot = -75f;

    //This is public variables to store the cameraFollow target
    [Header("Follow")]
    public Transform _targetTransform;
    public UpdateMode _followUpdateMode = UpdateMode.FixedUpdate;

    private float _xRot;
    private float _yRot;

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

        if (_followUpdateMode != UpdateMode.Update)
            return;

        FollowTarget();
    }

    private void FixedUpdate()
    {
        if (_followUpdateMode != UpdateMode.FixedUpdate)
            return;

        FollowTarget();
    }

    private void LateUpdate()
    {
        if (_followUpdateMode != UpdateMode.LateUpdate)
            return;

        FollowTarget();
    }

    private void FollowTarget()
    {
        transform.position = _targetTransform.position;
    }

    private void HandleLook()
    {
        Vector2 mouseInput = InputManager.Instance.Actions.Player.Look.ReadValue<Vector2>();

        _xRot -= mouseInput.y * _sensitivity;
        _yRot += mouseInput.x * _sensitivity;

        _xRot = Mathf.Clamp(_xRot, _minYRot, _maxYRot);

        transform.rotation = Quaternion.Euler(_xRot, _yRot, 0f);
    }

    public static void LockCursor(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}