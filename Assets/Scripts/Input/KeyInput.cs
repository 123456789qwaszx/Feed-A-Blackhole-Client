using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyInput : MonoBehaviour
{
    private InputSystem_Actions _actions;

    public event Action ContinuePressed;
    public event Action UpgradePressed;
    public event Action PausePressed;

    private void Awake()
    {
        _actions = new InputSystem_Actions();
    }

    private void Update()
    {
        //Vector2 move = _actions.UI.Navigate.ReadValue<Vector2>();
        //Debug.Log("방향키 입력");
    }

    private void OnEnable()
    {
        _actions.UI.Enable();
        _actions.UI.Upgrade.performed += OnUpgrade;
        _actions.UI.Continue.performed += OnContinue;
        _actions.UI.Pause.performed += OnPause;
    }

    private void OnDisable()
    {
        _actions.UI.Upgrade.performed -= OnUpgrade;
        _actions.UI.Continue.performed -= OnContinue;
        _actions.UI.Pause.performed -= OnPause;
        _actions.UI.Disable();
    }

    // Shift 키
    private void OnUpgrade(InputAction.CallbackContext context)
    {
        if (UpgradePressed != null) UpgradePressed();
    }

    // SpaceBar 키
    private void OnContinue(InputAction.CallbackContext context)
    {
        if (ContinuePressed != null) ContinuePressed();
    }

    // Esc 키
    private void OnPause(InputAction.CallbackContext context)
    {
        if (PausePressed != null) PausePressed();
    }
}
