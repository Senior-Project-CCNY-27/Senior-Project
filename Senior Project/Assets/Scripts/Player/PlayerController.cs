using System;
using Animancer;
using NaughtyAttributes;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AnimancerComponent))]
public class PlayerController : MonoBehaviour
{
    [Header("Idle")]
    [SerializeField] private ClipTransition _idleClip;
    
    [Header("Walk")]
    [SerializeField] private float _walkSpeed = 3f;
    [SerializeField] private ClipTransition _walkClip;
    
    [Header("Sprint")]
    [SerializeField] private float _sprintSpeed = 4.5f;
    [SerializeField] private ClipTransition _sprintClip;
    
    public enum State
    {
        Idle,
        Walk,
        Sprint
    }
    
    private PlayerInputActions _input;
    private CharacterController _characterController;
    private AnimancerComponent _animator;
    
    [field: Header("Debug")]
    [field: SerializeField, ReadOnly] public State CurrentState { get; private set; }
    [field: SerializeField, ReadOnly] public float CurrentSpeed { get; private set; }
    public event Action<State, State> OnStateChanged = delegate { };

    private Vector3 _moveInput;

    private void Awake()
    {
        _input = InputManager.Instance.Actions;
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<AnimancerComponent>();
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

        State previousState = CurrentState;
        
        OnStateExit(CurrentState);
        CurrentState = newState;
        OnStateEnter(CurrentState);
        
        OnStateChanged(newState, previousState);
    }

    private void OnStateEnter(State state)
    {
        switch (state)
        {
            case State.Idle:
                _animator.Play(_idleClip, 0.1f);
                CurrentSpeed = 0f;
                break;
            case State.Walk:
                _animator.Play(_walkClip, 0.1f);
                CurrentSpeed = _walkSpeed;
                break;
            case State.Sprint:
                _animator.Play(_sprintClip, 0.1f);
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
