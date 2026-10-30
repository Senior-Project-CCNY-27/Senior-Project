using System;
using Unity.Cinemachine;
using UnityEngine;

public class CameraInputController : InputAxisControllerBase<CameraInputController.Reader>
{
    [SerializeField] private float _sensitivity = 1f;
    public static float Sensitivity { get; private set; }
    
    void Update()
    { 
        if (!Application.isPlaying) 
            return;
        
        Sensitivity = _sensitivity;
        
        UpdateControllers();
    }

    [Serializable]
    public class Reader : IInputAxisReader
    {
        public float GetValue(UnityEngine.Object context, IInputAxisOwner.AxisDescriptor.Hints hint)
        {
            Vector2 lookDirection = InputManager.Instance.Actions.Player.Look.ReadValue<Vector2>();
            
            if (hint == IInputAxisOwner.AxisDescriptor.Hints.X)
                return lookDirection.x * Sensitivity;

            if (hint == IInputAxisOwner.AxisDescriptor.Hints.Y)
                return -lookDirection.y * Sensitivity;

            return 0f;
        }
    }
}