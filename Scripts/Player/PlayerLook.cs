using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerLook : NetworkBehaviour
{
    [Header("References")]
    public Transform orientation;

    [Header("Sensitivity")]
    public float xSensitivity = 30f;
    public float ySensitivity = 30f;

    private float xRotation = 0f;
    private float yRotation = 0f;

    // Network variables for synchronization
    private NetworkVariable<float> networkXRotation = new NetworkVariable<float>();
    private NetworkVariable<float> networkYRotation = new NetworkVariable<float>();

    // Smoothing for non-owners
    private float targetXRotation;
    private float targetYRotation;
    public float rotationSmoothSpeed = 15f;

    void Start()
    {
        // Only lock cursor for the owner
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Subscribe to network variable changes for non-owners
        if (!IsOwner)
        {
            networkXRotation.OnValueChanged += OnXRotationChanged;
            networkYRotation.OnValueChanged += OnYRotationChanged;

            // Initialize target rotations
            targetXRotation = networkXRotation.Value;
            targetYRotation = networkYRotation.Value;
        }
    }

    public override void OnDestroy()
    {
        // Unsubscribe from network variable changes
        if (!IsOwner)
        {
            networkXRotation.OnValueChanged -= OnXRotationChanged;
            networkYRotation.OnValueChanged -= OnYRotationChanged;
        }
        base.OnDestroy();
    }

    void Update()
    {
        if (IsOwner)
        {
            ProcessLookOwner();
        }
        else
        {
            ProcessLookNonOwner();
        }
    }

    private void ProcessLookOwner()
    {
        Vector2 input = InputManager.Instance.getLook();

        // Apply look rotation locally for immediate response
        ApplyLookRotation(input);

        // Update network variables if we're the server
        if (IsServer)
        {
            networkXRotation.Value = xRotation;
            networkYRotation.Value = yRotation;
        }
        else
        {
            // Send to server for validation and sync
            SendLookToServerRpc(input, xRotation, yRotation);
        }
    }

    private void ProcessLookNonOwner()
    {
        // Smoothly interpolate to the target rotation for non-owners
        xRotation = Mathf.LerpAngle(xRotation, targetXRotation, Time.deltaTime * rotationSmoothSpeed);
        yRotation = Mathf.LerpAngle(yRotation, targetYRotation, Time.deltaTime * rotationSmoothSpeed);

        // Apply the interpolated rotation
        ApplyRotationToTransforms();
    }

    private void ApplyLookRotation(Vector2 input)
    {
        float mouseX = input.x;
        float mouseY = input.y;

        xRotation -= mouseY * xSensitivity * 0.001f;
        yRotation += mouseX * ySensitivity * 0.001f;

        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        ApplyRotationToTransforms();
    }

    private void ApplyRotationToTransforms()
    {
        // Main Camera handles BOTH vertical and horizontal rotation
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);

        // Player Camera Holder stays unrotated for proper movement
        // Only the separate orientation transform rotates for movement reference
        if (orientation != null)
        {
            orientation.rotation = Quaternion.Euler(0, yRotation, 0);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendLookToServerRpc(Vector2 input, float clientXRotation, float clientYRotation)
    {
        // Server validation (optional - you can add anti-cheat logic here)
        xRotation = clientXRotation;
        yRotation = clientYRotation;

        // Apply rotation on server
        ApplyRotationToTransforms();

        // Update network variables for other clients
        networkXRotation.Value = xRotation;
        networkYRotation.Value = yRotation;

        // Broadcast to other clients (excluding the sender)
        UpdateOtherClientsLookClientRpc(xRotation, yRotation, NetworkManager.Singleton.LocalClientId);
    }

    [ClientRpc]
    private void UpdateOtherClientsLookClientRpc(float serverXRotation, float serverYRotation, ulong senderClientId)
    {
        // Don't update the client that sent the data or the owner
        if (NetworkManager.Singleton.LocalClientId == senderClientId || IsOwner)
            return;

        // Set target rotations for smooth interpolation
        targetXRotation = serverXRotation;
        targetYRotation = serverYRotation;
    }

    // Network variable change handlers for non-owners
    private void OnXRotationChanged(float previousValue, float newValue)
    {
        if (!IsOwner)
        {
            targetXRotation = newValue;
        }
    }

    private void OnYRotationChanged(float previousValue, float newValue)
    {
        if (!IsOwner)
        {
            targetYRotation = newValue;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetLookRotationServerRpc(float xRot, float yRot)
    {
        xRotation = Mathf.Clamp(xRot, -80f, 80f);
        yRotation = yRot;

        ApplyRotationToTransforms();

        // Update network variables
        networkXRotation.Value = xRotation;
        networkYRotation.Value = yRotation;

        // Broadcast to all clients
        ApplyLookRotationClientRpc(xRotation, yRotation);
    }

    [ClientRpc]
    private void ApplyLookRotationClientRpc(float xRot, float yRot)
    {
        if (!IsOwner) // Only apply to non-owners
        {
            targetXRotation = xRot;
            targetYRotation = yRot;
        }
    }

    // Method to get current look direction (useful for other scripts)
    public Vector3 GetLookDirection()
    {
        return orientation != null ? orientation.forward : transform.forward;
    }

    // Method to get look rotation values
    public Vector2 GetLookRotation()
    {
        return new Vector2(xRotation, yRotation);
    }
}