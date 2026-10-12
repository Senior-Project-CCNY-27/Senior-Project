using System;
using NaughtyAttributes;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Walk")]
    [SerializeField] private float _walkSpeed = 3f;
    
    [Header("Sprint")]
    [SerializeField] private float _sprintSpeed = 4.5f;
    
    public enum State
    {
        Idle,
        Walk,
        Sprint
    }
    
    private PlayerInputActions _input;
    private CharacterController _characterController;
    
    [field: Header("Debug")]
    [field: SerializeField, ReadOnly] public State CurrentState { get; private set; }
    [field: SerializeField, ReadOnly] public float CurrentSpeed { get; private set; }

    private Vector3 _moveInput;

    private void Awake()
    {
        _input = InputManager.Instance.Actions;
        _characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        ChangeState(State.Idle);
    }

    private void Update()
    {
        ReadMoveInput();
        
        UpdateState(CurrentState);  
        
        ApplyRotation();
    }
    
    public void ChangeState(State newState, bool forceChange = false)
    {
        if (CurrentState == newState && !forceChange)
            return;

        OnStateExit(CurrentState);
        CurrentState = newState;
        OnStateEnter(CurrentState);
    }

    private void OnStateEnter(State state)
    {
        switch (state)
        {
            case State.Idle:
                break;
            case State.Walk:
                CurrentSpeed = _walkSpeed;
                break;
            case State.Sprint:
                CurrentSpeed = _sprintSpeed;
                break;
        }
    }

    private void OnStateExit(State state)
    {
        switch (state)
        {
            case State.Idle:
                break;
            case State.Walk:
                break;
            case State.Sprint:
                break;
        }
    }

    private void UpdateState(State state)
    {
        switch (state)
        {
            case State.Idle:
                if (_moveInput != Vector3.zero)
                {
                    ChangeState(State.Walk);
                    return;
                }
                
                break;
            case State.Walk:
                if (_moveInput == Vector3.zero)
                {
                    ChangeState(State.Idle);
                    return;
                }

                if (_input.Player.Sprint.IsPressed())
                {
                    ChangeState(State.Sprint);
                    return;
                }
                
                ApplyMovement();
                break;
            case State.Sprint:
                if (_moveInput == Vector3.zero)
                {
                    ChangeState(State.Idle);
                    return;
                }
                
                if (!_input.Player.Sprint.IsPressed())
                {
                    ChangeState(State.Walk);
                    return;
                }
                
                ApplyMovement();
                break;
        }
    }

    private void ReadMoveInput()
    {
        Vector2 input = _input.Player.Move.ReadValue<Vector2>();
        _moveInput = new Vector3(input.x, 0f, input.y);
    }

    private void ApplyMovement()
    {
        Vector3 normalizedMoveInput = _moveInput.normalized;
        Quaternion cameraYRotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);

        Vector3 targetMoveDirection = cameraYRotation * normalizedMoveInput;
        
        _characterController.Move(targetMoveDirection * CurrentSpeed * Time.deltaTime);
    }

    private void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
    }
}
