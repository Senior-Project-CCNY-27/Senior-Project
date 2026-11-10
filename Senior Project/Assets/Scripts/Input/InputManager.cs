using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputManager : Singleton<InputManager>
{
    public PlayerInputActions Actions { get; private set; }
    
    /// <summary>
    /// We are using C# generated inputs, so this is just a dummy playerInput to detect controlScheme changes
    /// </summary>
    [SerializeField, ReadOnly] private PlayerInput _playerInput;
        
    public enum ControlScheme
    {
        KBM,
        Gamepad
    }
    
    public ControlScheme CurrentControlScheme { get; private set; }
        
    /// <summary>
    /// Action that is invoked when the current control scheme is changed based on the input device used.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>ControlScheme controlScheme</c>: The new controlScheme that was changed to.</description></item>
    /// </list>
    /// </remarks>
    public event Action<ControlScheme> OnControlSchemeChanged = delegate { };
        
    /// <summary>
    /// The intended lock state.
    /// Controllers will always be locked, but when switching back to mouse, this will be the applied lock state
    /// </summary>
    private CursorLockMode _desiredCursorLockState;
    private CursorLockMode? _cursorLockOverride;
    private bool _desiredPlayerActionsEnabled;
    private bool? _actionsOverride;

    private void OnValidate()
    {
        _playerInput = GetComponent<PlayerInput>();
    }

    private protected override void Awake()
    {
        base.Awake();
        
        _playerInput = GetComponent<PlayerInput>();
        
        Actions = new PlayerInputActions();
        
        _playerInput.onControlsChanged += PlayerInput_OnControlsChanged;
    }

    private void OnEnable()
    {
        if (Actions == null)
            return;

        ApplyActionsState();
    }

    private void OnDisable()
    {
        if (Actions == null)
            return;
        
        Actions.Disable();
    }

    private protected override void OnDestroy()
    {
        _playerInput.onControlsChanged -= PlayerInput_OnControlsChanged;
        Actions?.Dispose();
        
        base.OnDestroy();
    }
    
    /// <summary>
    /// Detects whether the control scheme has changed based on the input device used.
    /// </summary>
    /// <param name="input">The PlayerInput component that detected the control scheme change</param>
    private void PlayerInput_OnControlsChanged(PlayerInput input)
    {
        ControlScheme newScheme = ControlScheme.KBM;
        if (input.currentControlScheme == "Gamepad")
            newScheme = ControlScheme.Gamepad;

        if (newScheme != CurrentControlScheme)
        {
            CurrentControlScheme = newScheme;
            OnControlSchemeChanged.Invoke(CurrentControlScheme);
                
            ApplyCursorLockState();
        }
    }
        
    /// <summary>
    /// Locks or unlocks the cursor based on the user's preference.
    /// Keeps track of the desired cursor lock state, which is used when switching between control schemes.
    /// Gamepad always locks the cursor.
    /// </summary>
    /// <param name="locked"></param>
    public void LockCursor(bool locked)
    {
        // Always remember the user's preference
        _desiredCursorLockState = locked ? CursorLockMode.Locked : CursorLockMode.None;

        ApplyCursorLockState();
    }

    /// <summary>
    /// Temporarily overrides the applied cursor lock state without changing the desired state.
    /// Pass null to remove the override and reapply the desired state.
    /// </summary>
    public void SetCursorLockOverride(CursorLockMode? lockState)
    {
        _cursorLockOverride = lockState;
        ApplyCursorLockState();
    }

    /// <summary>
    /// Sets whether gameplay input should normally be enabled. This desired state is retained
    /// while a temporary override, such as the developer console, disables the input asset.
    /// </summary>
    public void SetPlayerActionsEnabled(bool enabled)
    {
        _desiredPlayerActionsEnabled = enabled;
        ApplyActionsState();
    }

    /// <summary>
    /// Temporarily overrides whether the generated input actions are enabled without changing
    /// the desired player action map state. Pass null to follow this component's active state.
    /// </summary>
    public void SetActionsOverride(bool? enabled)
    {
        _actionsOverride = enabled;
        ApplyActionsState();
    }

    private void ApplyActionsState()
    {
        if (Actions == null)
            return;

        bool shouldEnable = _actionsOverride ?? isActiveAndEnabled;
        if (!shouldEnable)
        {
            Actions.Player.Disable();
            Actions.Disable();
            return;
        }

        Actions.Enable();
        if (_desiredPlayerActionsEnabled)
            Actions.Player.Enable();
        else
            Actions.Player.Disable();
    }
        
    /// <summary>
    /// Helper method to apply the desired cursor lock state based on the current control scheme.
    /// </summary>
    private void ApplyCursorLockState()
    {
        if (_cursorLockOverride.HasValue)
        {
            Cursor.lockState = _cursorLockOverride.Value;
            Cursor.visible = _cursorLockOverride.Value != CursorLockMode.Locked;
            return;
        }

        if (CurrentControlScheme == ControlScheme.Gamepad)
        {
            // Gamepad always locks
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        Cursor.lockState = _desiredCursorLockState;
        Cursor.visible = _desiredCursorLockState != CursorLockMode.Locked;
    }
}
