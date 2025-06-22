using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    public PlayerInput.WalkActions walk;

    public static InputManager Instance { get; private set; }

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;

        playerInput = new PlayerInput();
        walk = playerInput.Walk;
    }

    public bool getJump()
    {
        return walk.Jump.IsPressed();
    }

    public Vector2 getMovement()
    {
        Vector2 input = playerInput.Walk.Movement.ReadValue<Vector2>();

        return input;
    }
    public Vector2 getLook()
    {
        Vector2 input = playerInput.Walk.Look.ReadValue<Vector2>();

        return input;
    }

    private void OnEnable()
    {
        walk.Enable();
    }

    private void OnDisable()
    {
        walk.Disable();
    }
}