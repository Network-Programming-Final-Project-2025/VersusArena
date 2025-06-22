using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BulletExplosive : NetworkBehaviour
{
    [Header("Bullet Settings")]
    public float damage = 20f;
    public float bulletLifetime = 5f;
    public float bulletSpeed = 50f;
    public float clientSyncRate = 120f; // How often to sync position (per second)

    public GameObject struckParticle;

    private Rigidbody rb;
    private Vector3 moveDirection;
    private Vector3 lastPosition;
    private bool hasCollided = false;

    // Network variables for synchronization
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> networkVelocity = new NetworkVariable<Vector3>();
    private NetworkVariable<bool> networkHasCollided = new NetworkVariable<bool>();
    private NetworkVariable<Vector3> networkCollisionPoint = new NetworkVariable<Vector3>();

    // Client-side prediction variables
    private Vector3 clientPredictedPosition;
    private Vector3 clientVelocity;
    private float lastSyncTime;
    private float syncInterval;
    private bool isClientPredicting = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        syncInterval = 1f / clientSyncRate;

        // Set rigidbody to Continuous collision detection for fast-moving objects
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // Store initial position for raycast collision detection
        lastPosition = transform.position;
        clientPredictedPosition = transform.position;
    }

    public override void OnNetworkSpawn()
    {
        // Start the lifetime countdown on all clients
        StartCoroutine(DestroyAfterTime());

        // Subscribe to network variable changes
        networkPosition.OnValueChanged += OnPositionChanged;
        networkVelocity.OnValueChanged += OnVelocityChanged;
        networkHasCollided.OnValueChanged += OnCollisionChanged;
        networkCollisionPoint.OnValueChanged += OnCollisionPointChanged;

        // Initialize client prediction
        if (!IsServer)
        {
            isClientPredicting = true;
            clientVelocity = networkVelocity.Value;
            clientPredictedPosition = transform.position;
        }

        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        networkPosition.OnValueChanged -= OnPositionChanged;
        networkVelocity.OnValueChanged -= OnVelocityChanged;
        networkHasCollided.OnValueChanged -= OnCollisionChanged;
        networkCollisionPoint.OnValueChanged -= OnCollisionPointChanged;

        base.OnNetworkDespawn();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyForceServerRpc(Vector3 mainForce, Vector3 upwardForce)
    {
        if (rb != null)
        {
            Vector3 totalForce = mainForce + upwardForce;
            rb.AddForce(totalForce, ForceMode.Impulse);

            // Store the direction for consistent movement on all clients
            moveDirection = totalForce.normalized;

            // Initialize last position for raycast collision detection
            lastPosition = transform.position;

            // Sync initial velocity and position to all clients immediately
            networkVelocity.Value = rb.velocity;
            networkPosition.Value = transform.position;

            // Start syncing position regularly
            StartCoroutine(SyncPositionRegularly());
        }
    }

    private IEnumerator SyncPositionRegularly()
    {
        while (!hasCollided && !networkHasCollided.Value && IsServer)
        {
            yield return new WaitForSeconds(syncInterval);

            if (rb != null && !hasCollided)
            {
                networkPosition.Value = transform.position;
                networkVelocity.Value = rb.velocity;
            }
        }
    }

    // Raycast-based collision detection to catch fast-moving bullet collisions
    private void PerformRaycastCollisionCheck()
    {
        if (hasCollided || lastPosition == Vector3.zero) return;

        Vector3 currentPosition = transform.position;
        Vector3 direction = (currentPosition - lastPosition);
        float distance = direction.magnitude;

        if (distance > 0.01f) // Only check if bullet has moved significantly
        {
            // Perform raycast from last position to current position
            RaycastHit[] hits = Physics.RaycastAll(lastPosition, direction.normalized, distance);

            foreach (RaycastHit hit in hits)
            {
                // Skip if hit our own collider
                if (hit.collider == GetComponent<Collider>()) continue;

                // Check if we hit a player
                PlayerHealth playerHealth = hit.collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    Debug.Log($"Raycast detected hit on player {playerHealth.OwnerClientId}");
                    HandleCollision(hit.collider.gameObject, hit.point);
                    return;
                }

                // Check for other collision types (walls, obstacles, etc.)
                if (hit.collider.CompareTag("Wall") || hit.collider.CompareTag("Obstacle"))
                {
                    Debug.Log($"Raycast detected hit on {hit.collider.name}");
                    HandleCollision(hit.collider.gameObject, hit.point);
                    return;
                }
            }
        }
    }

    private void HandleCollision(GameObject hitObject, Vector3 hitPoint)
    {
        if (hasCollided) return;

        hasCollided = true;

        // Move bullet to collision point
        transform.position = hitPoint;

        // Stop bullet movement BEFORE making it kinematic
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Sync collision state and point to all clients
        networkHasCollided.Value = true;
        networkCollisionPoint.Value = hitPoint;
        networkPosition.Value = hitPoint; // Ensure position is synced to collision point

        // Handle particle effects
        SpawnHitParticleClientRpc(hitPoint);

        // Check if hit a player
        PlayerHealth playerHealth = hitObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            // Apply damage directly on server
            playerHealth.TakeDamageOnServer(damage);
            Debug.Log($"Bullet hit player {playerHealth.OwnerClientId} for {damage} damage");
        }

        // Destroy bullet with slight delay to ensure clients see the collision
        StartCoroutine(DestroyBulletDelayed(0.15f)); // Slightly longer delay
    }

    [ClientRpc]
    private void SpawnHitParticleClientRpc(Vector3 hitPoint)
    {
        if (struckParticle != null)
        {
            GameObject particle = Instantiate(struckParticle, hitPoint, Quaternion.identity);
            Destroy(particle, 2f);
        }
    }

    private void FixedUpdate()
    {
        if (hasCollided || networkHasCollided.Value) return;

        if (IsServer)
        {
            // Server handles physics simulation with additional raycast collision detection
            if (rb != null)
            {
                // Perform raycast collision detection for fast-moving bullets
                PerformRaycastCollisionCheck();

                // Update last position for next frame's raycast
                lastPosition = transform.position;
            }
        }
        else
        {
            // Client-side prediction for smoother movement
            UpdateClientPrediction();
        }
    }

    private void UpdateClientPrediction()
    {
        if (!isClientPredicting || networkVelocity.Value == Vector3.zero) return;

        // Predict bullet movement on client
        clientPredictedPosition += clientVelocity * Time.fixedDeltaTime;

        // Apply gravity if the bullet has it
        if (rb != null && !rb.useGravity == false)
        {
            clientVelocity += Physics.gravity * Time.fixedDeltaTime;
        }

        // Smoothly blend between predicted position and networked position
        float blendFactor = Mathf.Clamp01(Time.fixedDeltaTime * 10f);
        Vector3 targetPosition = Vector3.Lerp(clientPredictedPosition, networkPosition.Value, blendFactor);

        transform.position = targetPosition;

        // Update predicted position to match actual position to reduce drift
        clientPredictedPosition = transform.position;
    }

    private void OnPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsServer && !hasCollided)
        {
            // Update client prediction with server position
            clientPredictedPosition = newValue;

            // Smoothly move towards the networked position
            float distance = Vector3.Distance(transform.position, newValue);

            // If the distance is too large, teleport (likely a collision occurred)
            if (distance > bulletSpeed * Time.fixedDeltaTime * 3f)
            {
                transform.position = newValue;
                clientPredictedPosition = newValue;
            }
            else
            {
                // Smooth interpolation for small differences
                transform.position = Vector3.Lerp(transform.position, newValue, Time.deltaTime * 15f);
            }
        }
    }

    private void OnVelocityChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsServer)
        {
            clientVelocity = newValue;

            if (rb != null && !rb.isKinematic && !hasCollided)
            {
                rb.velocity = newValue;
            }
        }
    }

    private void OnCollisionChanged(bool previousValue, bool newValue)
    {
        if (newValue && !hasCollided)
        {
            hasCollided = true;
            isClientPredicting = false;

            // Stop movement on all clients when collision occurs
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }

    private void OnCollisionPointChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsServer && newValue != Vector3.zero)
        {
            // Move bullet to the exact collision point on clients
            transform.position = newValue;
            clientPredictedPosition = newValue;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || hasCollided) return;

        Debug.Log($"OnCollisionEnter triggered with {collision.gameObject.name}");
        HandleCollision(collision.gameObject, collision.contacts[0].point);
    }

    private IEnumerator DestroyBulletDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        DestroyBullet();
    }

    private void DestroyBullet()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    private IEnumerator DestroyAfterTime()
    {
        yield return new WaitForSeconds(bulletLifetime);
        if (!hasCollided && !networkHasCollided.Value)
        {
            DestroyBullet();
        }
    }
}