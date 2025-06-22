using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    public float moveSpeed = 3f;

    // biar lantai gak licin
    public float groundDrag;
    public float playerHeight = 2.5f;
    public LayerMask isGround;
    private bool isGrounded;

    public float jumpForce = 10f;
    public float airMultiplier = 0.4f;

    private bool jumping;
    private float lastJumpTime = 0f;
    public float jumpCooldown = 0.2f;

    public Animator animator;

    [SerializeField]
    private Transform orientation;

    private float inputHorizon;
    private float inputGurt;

    Vector3 moveDirection;
    Vector3 currentVelocity;

    public float maxSlopeAngle;
    private RaycastHit slopeCheck;
    private bool slopeExit;

    private Rigidbody rb;

    // Network variables for synchronization
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> networkVelocity = new NetworkVariable<Vector3>();
    private NetworkVariable<float> networkAnimationVelocity = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        // Subscribe to network variable changes
        if (!IsOwner)
        {
            networkPosition.OnValueChanged += OnPositionChanged;
            networkVelocity.OnValueChanged += OnVelocityChanged;
        }

        // Subscribe to animation velocity changes for all clients
        networkAnimationVelocity.OnValueChanged += OnAnimationVelocityChanged;
    }

    public override void OnDestroy()
    {
        // Unsubscribe from network variable changes
        if (!IsOwner)
        {
            networkPosition.OnValueChanged -= OnPositionChanged;
            networkVelocity.OnValueChanged -= OnVelocityChanged;
        }

        networkAnimationVelocity.OnValueChanged -= OnAnimationVelocityChanged;
        base.OnDestroy();
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;

        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight + 0.3f, isGround);

        SetJumping(InputManager.Instance.getJump());
        SpeedControl();

        currentVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        // Update animation velocity for owner
        float velocityMagnitude = currentVelocity.magnitude;
        animator.SetFloat("Velocity", velocityMagnitude);

        // Update network variable for animation sync
        networkAnimationVelocity.Value = velocityMagnitude;

        if (isGrounded)
        {
            rb.drag = groundDrag;
        }
        else
        {
            rb.drag = 0;
        }

        if (jumping && isGrounded && Time.time - lastJumpTime > jumpCooldown)
        {
            Jump();
            lastJumpTime = Time.time;
        }

        // Update network variables for other clients
        if (IsServer)
        {
            networkPosition.Value = transform.position;
            networkVelocity.Value = rb.velocity;
        }

        //debugMode();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        ProcessMovement();
    }

    private void OnAnimationVelocityChanged(float previousValue, float newValue)
    {
        // Update animation for non-owner clients
        if (!IsOwner && animator != null)
        {
            animator.SetFloat("Velocity", newValue);
        }
    }

    private void ProcessMovement()
    {
        Vector2 input = InputManager.Instance.getMovement();

        inputHorizon = input.x;
        inputGurt = input.y;

        moveDirection = orientation.forward * inputGurt + orientation.right * inputHorizon;

        // Apply forces locally on the owner
        ApplyMovementForces();

        // Send the movement data to server for validation and sync
        if (!IsServer)
        {
            SendMovementToServerRpc(input, transform.position, rb.velocity);
        }
    }

    private void ApplyMovementForces()
    {
        // Don't apply slope forces if we're in the process of jumping off a slope
        if (SlopeWalk() && !slopeExit)
        {
            // On slope - use slope movement only with same force multiplier as ground
            rb.AddForce(SlopeMoveDirection() * moveSpeed * 15f, ForceMode.Force);
            rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        else if (isGrounded && !slopeExit) // Use else if instead of if
        {
            // On flat ground - use regular movement
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        else if (!isGrounded)
        {
            // In air - use air movement
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

        rb.useGravity = !SlopeWalk() || slopeExit; // Enable gravity when jumping off slopes
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendMovementToServerRpc(Vector2 input, Vector3 clientPosition, Vector3 clientVelocity)
    {
        // Server validation (optional - you can add anti-cheat logic here)
        float distance = Vector3.Distance(clientPosition, transform.position);

        // If the distance is reasonable, accept the client's position
        if (distance < moveSpeed * 2f) // Adjust threshold as needed
        {
            transform.position = clientPosition;
            rb.velocity = clientVelocity;
        }

        // Update network variables for other clients
        networkPosition.Value = transform.position;
        networkVelocity.Value = rb.velocity;

        // Broadcast to other clients (excluding the sender)
        UpdateOtherClientRpc(clientPosition, clientVelocity, NetworkManager.Singleton.LocalClientId);
    }

    [ClientRpc]
    private void UpdateOtherClientRpc(Vector3 serverPosition, Vector3 serverVelocity, ulong senderClientId)
    {
        // Don't update the client that sent the data
        if (NetworkManager.Singleton.LocalClientId == senderClientId || IsOwner)
            return;

        StartCoroutine(SmoothPositionUpdate(serverPosition, serverVelocity));
    }

    private IEnumerator SmoothPositionUpdate(Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 startPosition = transform.position;
        Vector3 startVelocity = rb.velocity;
        float duration = 0.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            rb.velocity = Vector3.Lerp(startVelocity, targetVelocity, t);

            yield return null;
        }

        transform.position = targetPosition;
        rb.velocity = targetVelocity;
    }

    private void OnPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsOwner)
        {
            transform.position = newValue;
        }
    }

    private void OnVelocityChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsOwner)
        {
            rb.velocity = newValue;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdAddForceServerRpc(Vector3 force, ForceMode forceMode = ForceMode.Force)
    {
        rb.AddForce(force, forceMode);

        networkPosition.Value = transform.position;
        networkVelocity.Value = rb.velocity;

        ApplyForceClientRpc(force, forceMode);
    }

    [ClientRpc]
    private void ApplyForceClientRpc(Vector3 force, ForceMode forceMode)
    {
        if (!IsOwner)
        {
            rb.AddForce(force, forceMode);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void JumpServerRpc(Vector3 jumpForce)
    {
        // Server validates and applies the jump
        rb.AddForce(jumpForce, ForceMode.Impulse);

        // Update network variables
        networkPosition.Value = transform.position;
        networkVelocity.Value = rb.velocity;

        // Notify other clients (excluding the sender)
        ApplyJumpForceClientRpc(jumpForce);
    }

    [ClientRpc]
    private void ApplyJumpForceClientRpc(Vector3 jumpForce)
    {
        // Apply jump force to non-owner clients only
        if (!IsOwner)
        {
            rb.AddForce(jumpForce, ForceMode.Impulse);
        }
    }

    private void SpeedControl()
    {
        if (SlopeWalk() && !slopeExit)
        {
            // For slopes, limit the magnitude of the entire velocity vector
            if (rb.velocity.magnitude > moveSpeed)
            {
                rb.velocity = rb.velocity.normalized * moveSpeed;
            }
        }
        else
        {
            // For ground and air, only limit horizontal velocity (preserve Y for jumping/falling)
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            if (horizontalVelocity.magnitude > moveSpeed)
            {
                Vector3 limitedHorizontal = horizontalVelocity.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedHorizontal.x, rb.velocity.y, limitedHorizontal.z);
            }
        }
    }

    private void debugMode()
    {
        currentVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Debug.Log("Current Velocity Magnitude: " + currentVelocity.magnitude);

        // Raycast for ground check
        Vector3 rayOrigin = transform.position;
        Vector3 rayDir = Vector3.down * (playerHeight + 0.3f);
        Debug.DrawRay(rayOrigin, rayDir, Color.red);

        // Raycast for slope check
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, playerHeight + 0.3f))
        {
            Debug.DrawLine(hit.point, hit.point + hit.normal, Color.green); // Surface normal
            Debug.Log("Slope Angle: " + Vector3.Angle(Vector3.up, hit.normal));
        }
    }

    private void Jump()
    {
        if (!isGrounded) return;

        slopeExit = true;

        // Reset Y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        Vector3 jumpDirection;

        // Check if we're on a slope
        if (SlopeWalk())
        {
            // Jump perpendicular to the slope surface
            jumpDirection = slopeCheck.normal;

            // Add some upward bias to make the jump feel more natural
            jumpDirection = (slopeCheck.normal + Vector3.up * 0.5f).normalized;
        }
        else
        {
            // Normal jump straight up
            jumpDirection = Vector3.up;
        }

        Vector3 jumpForceVector = jumpDirection * jumpForce;

        // Apply jump force locally first for immediate feedback
        rb.AddForce(jumpForceVector, ForceMode.Impulse);

        // Then sync with network
        if (IsServer)
        {
            // Update network variables for other clients
            networkPosition.Value = transform.position;
            networkVelocity.Value = rb.velocity;

            // Notify other clients (but not the owner)
            ApplyJumpForceClientRpc(jumpForceVector);
        }
        else
        {
            // Send to server for validation and sync
            JumpServerRpc(jumpForceVector);
        }
    }

    public void SetJumping(bool jump)
    {
        jumping = jump;

        // Only reset slopeExit if we're not jumping anymore and we're grounded
        if (!jump && isGrounded)
        {
            slopeExit = false;
        }
    }

    private bool SlopeWalk()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeCheck, playerHeight + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeCheck.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    private Vector3 SlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeCheck.normal).normalized;
    }
}