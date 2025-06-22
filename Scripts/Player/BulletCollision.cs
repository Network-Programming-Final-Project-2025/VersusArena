using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BulletCollision : NetworkBehaviour
{
    [Header("Projectile Settings")]
    public float bulletSpeed = 30f; // Speed for bullets/rockets
    public float clientSyncRate = 60f; // Sync rate for networking
    public bool isExplosive = true; // Toggle for explosive behavior

    [Header("Explosion Settings")]
    public GameObject explosion; // Used for both explosions and bullet strike effects
    public LayerMask playerLayers = -1; // Set to "Everything" by default, configure in inspector
    public int explodeDamage = 100;
    public float explodeRange = 5f;
    public float maxLifetime = 5f; // 5 seconds as you wanted
    public bool explodeOnTouch = true;

    [Header("Direct Hit Settings (Non-Explosive)")]
    public int directHitDamage = 50; // Damage for direct hits when not explosive
    public bool penetrateThroughTargets = false; // Whether bullet continues after hitting a target

    [Header("Physics Settings")]
    [Range(0f, 1f)] public float bounce = 0.3f;
    public bool useGravity = true;
    public int maxCollisions = 3;

    // Core components
    public Rigidbody rb;
    private Vector3 moveDirection;
    private Vector3 lastPosition;
    private bool hasExploded = false;
    private bool hasLaunched = false; // Add this to track if projectile has been launched
    private bool hasHitTarget = false; // Track if non-explosive bullet has hit a target
    private int collisions = 0;
    private PhysicMaterial physicMaterial;

    // Network variables for synchronization
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> networkVelocity = new NetworkVariable<Vector3>();
    private NetworkVariable<bool> networkHasExploded = new NetworkVariable<bool>();
    private NetworkVariable<bool> networkHasHitTarget = new NetworkVariable<bool>();
    private NetworkVariable<Vector3> networkExplosionPoint = new NetworkVariable<Vector3>();

    // Client-side prediction variables
    private Vector3 clientPredictedPosition;
    private Vector3 clientVelocity;
    private float syncInterval;
    private bool isClientPredicting = false;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        syncInterval = 1f / clientSyncRate;

        // Set rigidbody settings
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = useGravity;

            // Make sure rigidbody is not kinematic initially
            rb.isKinematic = false;

            // Set mass based on projectile type
            if (rb.mass < 0.1f)
                rb.mass = isExplosive ? 0.5f : 0.1f; // Rockets heavier than bullets
        }

        // Store initial position
        lastPosition = transform.position;
        clientPredictedPosition = transform.position;

        Setup();
    }

    public override void OnNetworkSpawn()
    {
        // Start the lifetime countdown on all clients
        StartCoroutine(ExplodeAfterTime());

        // Subscribe to network variable changes
        networkPosition.OnValueChanged += OnPositionChanged;
        networkVelocity.OnValueChanged += OnVelocityChanged;
        networkHasExploded.OnValueChanged += OnExplosionChanged;
        networkHasHitTarget.OnValueChanged += OnHitTargetChanged;
        networkExplosionPoint.OnValueChanged += OnExplosionPointChanged;

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
        networkHasExploded.OnValueChanged -= OnExplosionChanged;
        networkHasHitTarget.OnValueChanged -= OnHitTargetChanged;
        networkExplosionPoint.OnValueChanged -= OnExplosionPointChanged;

        base.OnNetworkDespawn();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyForceServerRpc(Vector3 mainForce, Vector3 upwardForce)
    {
        if (rb != null && !hasExploded && !hasHitTarget && !hasLaunched)
        {
            // Make sure rigidbody is ready for physics
            rb.isKinematic = false;
            rb.useGravity = useGravity;

            Vector3 totalForce = mainForce + upwardForce;

            // Debug log to check force values
            Debug.Log($"Applying force to {(isExplosive ? "rocket" : "bullet")}: mainForce={mainForce}, upwardForce={upwardForce}, totalForce={totalForce}");

            // Apply the force
            rb.AddForce(totalForce, ForceMode.Impulse);

            // Mark as launched
            hasLaunched = true;

            // Store the direction for consistent movement
            moveDirection = totalForce.normalized;
            lastPosition = transform.position;

            // Wait one physics frame to get accurate velocity, then sync
            StartCoroutine(SyncPositionRegularly());
        }
    }

    private IEnumerator SyncAfterPhysicsStep()
    {
        // Wait for physics to process the force
        yield return new WaitForFixedUpdate();

        // Now sync the actual velocity and position
        if (rb != null && !hasExploded && !hasHitTarget)
        {
            networkVelocity.Value = rb.velocity;
            networkPosition.Value = transform.position;

            Debug.Log($"Projectile velocity after force applied: {rb.velocity}");

            // Start regular position syncing
            StartCoroutine(SyncPositionRegularly());
        }
    }

    private IEnumerator SyncPositionRegularly()
    {
        while (!hasExploded && !hasHitTarget && !networkHasExploded.Value && !networkHasHitTarget.Value && IsServer && hasLaunched)
        {
            yield return new WaitForSeconds(syncInterval);

            if (rb != null && !hasExploded && !hasHitTarget)
            {
                networkPosition.Value = transform.position;
                networkVelocity.Value = rb.velocity;
            }
        }
    }

    private void Setup()
    {
        physicMaterial = new PhysicMaterial();
        physicMaterial.bounciness = bounce;
        physicMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
        physicMaterial.bounceCombine = PhysicMaterialCombine.Maximum;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.material = physicMaterial;
        }
    }

    private void Update()
    {
        if (hasExploded || hasHitTarget || networkHasExploded.Value || networkHasHitTarget.Value) return;

        // Check collision count (only on server)
        if (IsServer && collisions >= maxCollisions)
        {
            if (isExplosive)
            {
                Explode();
            }
            else
            {
                DestroyProjectile();
            }
        }
    }

    private void FixedUpdate()
    {
        if (hasExploded || hasHitTarget || networkHasExploded.Value || networkHasHitTarget.Value) return;

        if (IsServer)
        {
            // Server handles physics simulation
            if (rb != null && hasLaunched)
            {
                // Debug current velocity to see if projectile is moving
                if (Time.fixedTime % 0.5f < Time.fixedDeltaTime) // Log every 0.5 seconds
                {
                    Debug.Log($"{(isExplosive ? "Rocket" : "Bullet")} velocity: {rb.velocity}, position: {transform.position}");
                }

                // Update last position for tracking
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

        // Predict projectile movement on client
        clientPredictedPosition += clientVelocity * Time.fixedDeltaTime;

        // Apply gravity if enabled
        if (rb != null && rb.useGravity)
        {
            clientVelocity += Physics.gravity * Time.fixedDeltaTime;
        }

        // Smoothly blend between predicted position and networked position
        float blendFactor = Mathf.Clamp01(Time.fixedDeltaTime * (isExplosive ? 8f : 12f)); // Bullets blend faster
        Vector3 targetPosition = Vector3.Lerp(clientPredictedPosition, networkPosition.Value, blendFactor);

        transform.position = targetPosition;
        clientPredictedPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || hasExploded || hasHitTarget) return;

        collisions++;
        Debug.Log($"{(isExplosive ? "Rocket" : "Bullet")} collision #{collisions} with {collision.gameObject.name}");

        if (isExplosive)
        {
            // Explosive behavior
            if (explodeOnTouch)
            {
                Explode(collision.contacts[0].point);
            }
        }
        else
        {
            // Non-explosive behavior - check for direct hit on player
            bool hitPlayer = HandleDirectHit(collision);

            // Always spawn hit effect for bullets (whether they hit player or wall)
            HandleExplosionEffects(collision.contacts[0].point);

            if (hitPlayer && !penetrateThroughTargets)
            {
                // Stop the bullet if it hit a player and doesn't penetrate
                hasHitTarget = true;
                networkHasHitTarget.Value = true;

                // Stop bullet movement
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                // Destroy bullet with slight delay
                StartCoroutine(DestroyProjectileDelayed(0.1f));
            }
            else if (!hitPlayer)
            {
                // Hit a wall or other object - stop the bullet
                hasHitTarget = true;
                networkHasHitTarget.Value = true;

                // Stop bullet movement
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                // Destroy bullet with slight delay
                StartCoroutine(DestroyProjectileDelayed(0.1f));
            }
        }
    }

    private bool HandleDirectHit(Collision collision)
    {
        // Try to find PlayerHealth on the hit object
        PlayerHealth playerHealth = collision.collider.GetComponent<PlayerHealth>();

        // If not found on the collider itself, try the parent or children
        if (playerHealth == null)
        {
            playerHealth = collision.collider.GetComponentInParent<PlayerHealth>();
        }
        if (playerHealth == null)
        {
            playerHealth = collision.collider.GetComponentInChildren<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            Debug.Log($"Direct hit on {collision.collider.name}! Applying {directHitDamage} damage");

            // Apply direct hit damage
            playerHealth.TakeDamageOnServer(directHitDamage);

            // Trigger hit effect on clients
            TriggerHitEffectClientRpc(collision.contacts[0].point);

            return true; // Hit a player
        }

        return false; // Didn't hit a player
    }

    [ClientRpc]
    private void TriggerHitEffectClientRpc(Vector3 hitPoint)
    {
        // You can add hit effects here (sparks, blood, etc.)
        // For now, just log the hit
        Debug.Log($"Bullet hit effect at {hitPoint}");
    }

    private void Explode(Vector3? explosionPoint = null)
    {
        if (hasExploded) return;

        hasExploded = true;
        Vector3 explodePos = explosionPoint ?? transform.position;

        Debug.Log($"Rocket exploding at position: {explodePos}");

        // Stop rocket movement
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Sync explosion state to all clients
        networkHasExploded.Value = true;
        networkExplosionPoint.Value = explodePos;

        // Handle explosion effects and damage
        HandleExplosionEffects(explodePos);
        if (isExplosive)
        {
            HandleExplosionDamage(explodePos);
        }

        // Destroy rocket with slight delay
        StartCoroutine(DestroyProjectileDelayed(0.2f));
    }

    private void HandleExplosionEffects(Vector3 effectPos)
    {
        // Spawn explosion/hit effect particle on all clients
        SpawnEffectClientRpc(effectPos);
    }

    [ClientRpc]
    private void SpawnEffectClientRpc(Vector3 effectPos)
    {
        if (explosion != null)
        {
            GameObject effectInstance = Instantiate(explosion, effectPos, Quaternion.identity);
            Destroy(effectInstance, 5f);
        }
    }

    private void HandleExplosionDamage(Vector3 explosionPos)
    {
        // Only handle damage on server
        if (!IsServer) return;

        Debug.Log($"Handling explosion damage at {explosionPos} with range {explodeRange}");

        // Method 1: Try with layer mask first
        Collider[] hitColliders = Physics.OverlapSphere(explosionPos, explodeRange, playerLayers);
        Debug.Log($"Found {hitColliders.Length} colliders in explosion range with layer mask");

        // Method 2: If layer mask doesn't work, try without it and filter manually
        if (hitColliders.Length == 0)
        {
            hitColliders = Physics.OverlapSphere(explosionPos, explodeRange);
            Debug.Log($"Found {hitColliders.Length} total colliders in explosion range (no layer mask)");
        }

        foreach (Collider hitCollider in hitColliders)
        {
            Debug.Log($"Checking collider: {hitCollider.name} on layer {hitCollider.gameObject.layer}");

            // Try to find PlayerHealth on the hit object
            PlayerHealth playerHealth = hitCollider.GetComponent<PlayerHealth>();

            // If not found on the collider itself, try the parent or children
            if (playerHealth == null)
            {
                playerHealth = hitCollider.GetComponentInParent<PlayerHealth>();
            }
            if (playerHealth == null)
            {
                playerHealth = hitCollider.GetComponentInChildren<PlayerHealth>();
            }

            if (playerHealth != null)
            {
                // Calculate distance-based damage
                float distance = Vector3.Distance(explosionPos, hitCollider.transform.position);
                float damageFalloff = Mathf.Clamp01(1 - (distance / explodeRange));
                int finalDamage = Mathf.RoundToInt(explodeDamage * damageFalloff);

                Debug.Log($"Found PlayerHealth on {hitCollider.name}! Applying {finalDamage} damage (distance: {distance}, falloff: {damageFalloff})");

                // Apply explosion damage
                playerHealth.TakeDamageOnServer(finalDamage);
            }
            else
            {
                Debug.Log($"No PlayerHealth found on {hitCollider.name}");
            }
        }
    }

    // Network variable change handlers
    private void OnPositionChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsServer && !hasExploded && !hasHitTarget)
        {
            clientPredictedPosition = newValue;
            float distance = Vector3.Distance(transform.position, newValue);

            if (distance > bulletSpeed * Time.fixedDeltaTime * 3f)
            {
                transform.position = newValue;
                clientPredictedPosition = newValue;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, newValue, Time.deltaTime * 10f);
            }
        }
    }

    private void OnVelocityChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsServer)
        {
            clientVelocity = newValue;
            if (rb != null && !rb.isKinematic && !hasExploded && !hasHitTarget)
            {
                rb.velocity = newValue;
            }
        }
    }

    private void OnExplosionChanged(bool previousValue, bool newValue)
    {
        if (newValue && !hasExploded)
        {
            hasExploded = true;
            isClientPredicting = false;

            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }

    private void OnHitTargetChanged(bool previousValue, bool newValue)
    {
        if (newValue && !hasHitTarget)
        {
            hasHitTarget = true;
            isClientPredicting = false;

            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }

    private void OnExplosionPointChanged(Vector3 previousValue, Vector3 newValue)
    {
        if (!IsServer && newValue != Vector3.zero)
        {
            transform.position = newValue;
            clientPredictedPosition = newValue;
        }
    }

    private IEnumerator ExplodeAfterTime()
    {
        yield return new WaitForSeconds(maxLifetime);
        if (!hasExploded && !hasHitTarget && !networkHasExploded.Value && !networkHasHitTarget.Value)
        {
            if (IsServer)
            {
                if (isExplosive)
                {
                    Debug.Log("Rocket exploded after maximum lifetime");
                    Explode();
                }
                else
                {
                    Debug.Log("Bullet destroyed after maximum lifetime");
                    DestroyProjectile();
                }
            }
        }
    }

    private IEnumerator DestroyProjectileDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (isExplosive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explodeRange);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}