using System;
using Animancer;
using Sirenix.OdinInspector;
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
    [field: SerializeField, ReadOnly] public float CurrentStamina { get; private set; }
    [field: SerializeField] public float MaxStamina { get; private set; } = 100f;
    [SerializeField] private float _minStaminaToSprint = 10f;
    [SerializeField] private ClipTransition _sprintClip;

    [Header("Stamina Regen Parameters")]
    [SerializeField] private float _drainRate = 20f;
    [SerializeField] private float _regenRate = 10f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _groundSphereCheckRadius = 0.2f;
    [SerializeField] private float _groundSphereCheckOffset = 0.1f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField, ReadOnly] private bool _isGrounded;

    public enum State
    {
        Idle,
        Walk,
        Sprint
    }
    
    private PlayerInputActions _input;
    private CharacterController _controller;
    private AnimancerComponent _animator;
    
    [field: Header("Debug")]
    [field: SerializeField, ReadOnly] public State CurrentState { get; private set; }
    [field: SerializeField, ReadOnly] public float CurrentSpeed { get; private set; }
    public event Action<State, State> OnStateChanged = delegate { };

    private Vector3 _moveInput;
    private float _yVelocity;

    private void Awake()
    {
        _input = InputManager.Instance.Actions;
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<AnimancerComponent>();
    }

    private void Start()
    {
        CurrentStamina = MaxStamina;
        ChangeState(State.Idle, true);
    }

    private void OnEnable()
    {
        InputManager.Instance.Actions.Player.Jump.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        if(InputManager.Instance == null)
            return;

        InputManager.Instance.Actions.Player.Jump.performed -= OnJumpPerformed;
    }

    private void Update()
    {
        ReadMoveInput();
        
        UpdateState(CurrentState);

        CheckGrounded();
        UpdateGravity();
        ApplyRotation();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position - Vector3.up * _groundSphereCheckOffset, _groundSphereCheckRadius);
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
                RegenStamina();
                break;
            case State.Walk:
                if (_moveInput == Vector3.zero)
                {
                    ChangeState(State.Idle);
                    return;
                }

                if (_input.Player.Sprint.IsPressed() && CurrentStamina > _minStaminaToSprint)
                {
                    ChangeState(State.Sprint);
                    return;
                }

                RegenStamina();
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
                
                DrainStamina();
                if (CurrentStamina <= 0)
                {
                    ChangeState(State.Idle);
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
        
        _controller.Move(targetMoveDirection * CurrentSpeed * Time.deltaTime);
    }

    private void UpdateGravity()
    {
        if (_controller.isGrounded && _yVelocity < 0f)
            _yVelocity = 0f;

        _yVelocity += Physics.gravity.y * Time.deltaTime;
        _controller.Move(Vector3.up * (_yVelocity * Time.deltaTime));
    }

    private void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
    }

    private void DrainStamina()
    {
        if (CurrentStamina <= 0)
        {
            CurrentStamina = 0;
            return;
        }

        CurrentStamina -= _drainRate * Time.deltaTime;
    }

    private void RegenStamina()
    {
        if(CurrentStamina >= MaxStamina)
        {
            CurrentStamina = MaxStamina;
            return;
        }

        CurrentStamina += _regenRate * Time.deltaTime;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if(!_isGrounded)
            return;

        float jumpVelocity = Mathf.Sqrt(_jumpHeight * -2f * Physics.gravity.y);
        _yVelocity = jumpVelocity;
    }

    private void CheckGrounded()
    {
        Vector3 spherePosition = transform.position - Vector3.up * _groundSphereCheckOffset;
        _isGrounded = Physics.CheckSphere(spherePosition, _groundSphereCheckRadius, _groundLayer);
    }
}
