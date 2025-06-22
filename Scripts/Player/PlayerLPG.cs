using System.Collections;
using System.Collections.Generic;
using TMPro.Examples;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class PlayerLPG : NetworkBehaviour
{
    [Header("Gun Settings")]
    public GameObject bullet;
    public float force, forceUpward, timeShooting, timeShots, spread, reloadTime;
    public int ammo, bulletsPerTap;
    public bool allowButtonHold;
    private int bulletsLeft, bulletsShot;

    [Header("Gun State")]
    bool shooting, readyToShoot, reloading;

    // Network variables for gun transform synchronization
    private NetworkVariable<Vector3> networkGunPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkGunRotation = new NetworkVariable<Quaternion>();

    [Header("References")]
    public Camera cam;
    public Transform attackPoint;
    public GameObject muzzleFlash;
    public TextMeshProUGUI ammoDisplay;

    [Header("Gun Positioning")]
    public Transform playerTransform; // Reference to the Player object
    public Transform mainCamera; // Reference to the Main Camera
    public Vector3 gunOffset = new Vector3(0.5f, -0.3f, 0.8f); // Offset from camera
    public float followSpeed = 15f;

    [Header("Reload Animation")]
    public Vector3 reloadPositionOffset = new Vector3(0f, -0.8f, 0.5f); // Position offset during reload (forward and down)
    public Vector3 reloadRotationOffset = new Vector3(0f, 0f, 90f); // Rotation offset during reload (tilted sideways)
    public float reloadAnimationSpeed = 8f; // Speed of the reload animation
    public AnimationCurve reloadCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Animation curve for smooth transitions

    [Header("Audio")]
    public AudioSource audioSource; // Assign this in inspector
    public AudioClip shootSFX; // Drag your shooting sound here
    public AudioClip reloadSFX; // Optional: reload sound
    public AudioClip emptyClipSFX; // Optional: empty clip sound
    [Range(0f, 1f)]
    public float sfxVolume = 0.8f;

    private InputManager inputManager;
    private bool allowInvoke;

    // Reload animation variables
    private bool isReloadAnimating = false;
    private float reloadAnimationProgress = 0f;
    private Vector3 baseGunOffset;
    private Vector3 baseGunRotation;

    void Awake()
    {
        inputManager = FindObjectOfType<InputManager>();
        bulletsLeft = ammo;
        readyToShoot = true;
        allowInvoke = true;

        // Store the base gun offset and rotation
        baseGunOffset = gunOffset;
        baseGunRotation = Vector3.zero;

        // Get AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                // Create AudioSource if it doesn't exist
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Configure AudioSource
        audioSource.playOnAwake = false;
        audioSource.volume = sfxVolume;
    }

    public override void OnNetworkSpawn()
    {
        // Initialize network variables with current transform
        if (IsServer)
        {
            networkGunPosition.Value = transform.position;
            networkGunRotation.Value = transform.rotation;
        }

        base.OnNetworkSpawn();
    }

    private void Update()
    {
        if (IsOwner)
        {
            // Update reload animation
            UpdateReloadAnimation();

            // Owner updates gun position and syncs to network
            UpdateGunPosition();
            MyInput();

            if (ammoDisplay != null)
            {
                ammoDisplay.SetText(bulletsLeft / bulletsPerTap + "");
            }
        }
        else
        {
            // Non-owners apply networked gun transform
            ApplyNetworkedGunTransform();
        }
    }

    private void UpdateReloadAnimation()
    {
        if (isReloadAnimating)
        {
            // Update animation progress
            reloadAnimationProgress += Time.deltaTime * reloadAnimationSpeed;
            reloadAnimationProgress = Mathf.Clamp01(reloadAnimationProgress);

            // If animation is complete, stop animating
            if (reloadAnimationProgress >= 1f)
            {
                isReloadAnimating = false;
                reloadAnimationProgress = 0f;
            }
        }
    }

    private void UpdateGunPosition()
    {
        if (mainCamera != null && playerTransform != null)
        {
            Vector3 currentGunOffset = baseGunOffset;
            Vector3 currentGunRotation = baseGunRotation;

            // Apply reload animation if reloading
            if (reloading)
            {
                float animationValue = 0f;

                if (isReloadAnimating)
                {
                    // Use animation curve for smooth transitions
                    float curveValue = reloadCurve.Evaluate(reloadAnimationProgress);

                    // Create a smooth in-out animation (goes to reload position, then back)
                    if (reloadAnimationProgress <= 0.5f)
                    {
                        // First half: move to reload position
                        animationValue = curveValue * 2f;
                    }
                    else
                    {
                        // Second half: return to normal position
                        animationValue = (1f - curveValue) * 2f;
                    }
                }
                else
                {
                    // Stay in reload position if not animating
                    animationValue = 1f;
                }

                // Interpolate between normal and reload positions
                currentGunOffset = Vector3.Lerp(baseGunOffset, baseGunOffset + reloadPositionOffset, animationValue);
                currentGunRotation = Vector3.Lerp(baseGunRotation, reloadRotationOffset, animationValue);
            }

            // Calculate target position: Player position + camera-relative offset
            Vector3 targetPosition = playerTransform.position + mainCamera.TransformDirection(currentGunOffset);

            // Calculate target rotation: camera rotation + reload rotation offset
            Quaternion additionalRotation = Quaternion.Euler(currentGunRotation);
            Quaternion targetRotation = mainCamera.rotation * additionalRotation;

            // Apply position and rotation with smooth interpolation
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, followSpeed * Time.deltaTime);

            // Sync to network (only if values changed significantly to reduce network traffic)
            if (Vector3.Distance(transform.position, networkGunPosition.Value) > 0.01f ||
                Quaternion.Angle(transform.rotation, networkGunRotation.Value) > 1f)
            {
                UpdateGunTransformServerRpc(transform.position, transform.rotation);
            }
        }
    }

    private void ApplyNetworkedGunTransform()
    {
        // Check if network variables have valid values
        if (networkGunPosition.Value == Vector3.zero && networkGunRotation.Value == Quaternion.identity)
        {
            // Network variables haven't been initialized yet, skip interpolation
            return;
        }

        // Validate quaternions before interpolation
        if (IsValidQuaternion(networkGunRotation.Value) && IsValidQuaternion(transform.rotation))
        {
            // Non-owners smoothly interpolate to networked transform
            transform.position = Vector3.Lerp(transform.position, networkGunPosition.Value, followSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkGunRotation.Value, followSpeed * Time.deltaTime);
        }
        else
        {
            // If quaternions are invalid, directly set the values without interpolation
            if (IsValidQuaternion(networkGunRotation.Value))
            {
                transform.rotation = networkGunRotation.Value;
            }
            transform.position = networkGunPosition.Value;
        }
    }

    // Helper method to validate quaternions
    private bool IsValidQuaternion(Quaternion q)
    {
        // Check if quaternion has valid magnitude (not zero or NaN)
        float magnitude = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        return magnitude > 0.001f && !float.IsNaN(magnitude) && !float.IsInfinity(magnitude);
    }

    [ServerRpc]
    private void UpdateGunTransformServerRpc(Vector3 position, Quaternion rotation)
    {
        // Validate the rotation before setting network variable
        if (IsValidQuaternion(rotation))
        {
            // Update network variables on server
            networkGunPosition.Value = position;
            networkGunRotation.Value = rotation;
        }
    }

    private void MyInput()
    {
        if (allowButtonHold)
        {
            shooting = inputManager.walk.Shoot.IsPressed();
        }
        else
        {
            shooting = inputManager.walk.Shoot.WasPressedThisFrame();
        }

        if (inputManager.walk.Reload.WasPressedThisFrame() && bulletsLeft < ammo && !reloading)
        {
            Reload();
        }

        if (readyToShoot && shooting && !reloading && bulletsLeft <= 0)
        {
            // Play empty clip sound
            PlayEmptyClipSound();
            Reload();
        }

        if (readyToShoot && shooting && !reloading && bulletsLeft > 0)
        {
            bulletsShot = 0;
            Shoot();
        }
    }

    private void Shoot()
    {
        readyToShoot = false;

        // Play shoot sound immediately for owner
        PlayShootSound();
        PlayerUIManager.TriggerAmmoGearAnimation();

        // Calculate shooting direction on client
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(75);
        }

        Vector3 directionWithoutSpread = targetPoint - attackPoint.position;

        float x = Random.Range(-spread, spread);
        float y = Random.Range(-spread, spread);

        Vector3 directionWithSpread = directionWithoutSpread + new Vector3(x, y, 0);

        // Send shoot command to server
        ShootServerRpc(attackPoint.position, directionWithSpread.normalized, cam.transform.up);

        // Update local UI immediately for responsiveness
        bulletsLeft--;
        bulletsShot++;

        if (allowInvoke)
        {
            Invoke("ResetShot", timeShots);
            allowInvoke = false;
        }

        if (bulletsShot < bulletsPerTap && bulletsLeft > 0)
        {
            Invoke("Shoot", timeShots);
        }
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 shootPosition, Vector3 shootDirection, Vector3 upDirection)
    {
        // Create bullet on server
        GameObject currentBullet = Instantiate(bullet, shootPosition, Quaternion.LookRotation(shootDirection));
        currentBullet.transform.Rotate(90f, 0f, 0f);

        // Get NetworkObject and spawn it
        NetworkObject bulletNetObj = currentBullet.GetComponent<NetworkObject>();
        if (bulletNetObj != null)
        {
            bulletNetObj.Spawn();
        }

        // Apply forces using ServerRpc
        BulletCollision bulletScript = currentBullet.GetComponent<BulletCollision>();
        if (bulletScript != null)
        {
            bulletScript.ApplyForceServerRpc(shootDirection * force, upDirection * forceUpward);
        }

        // Show muzzle flash to all clients and play sound for other players
        ShowMuzzleFlashAndSoundClientRpc(shootPosition, Quaternion.LookRotation(shootDirection));
    }

    [ClientRpc]
    private void ShowMuzzleFlashAndSoundClientRpc(Vector3 position, Quaternion rotation)
    {
        // Show muzzle flash
        if (muzzleFlash != null)
        {
            GameObject flash = Instantiate(muzzleFlash, position, rotation, attackPoint);
            // Destroy flash after a short time
            Destroy(flash, 0.3f);
        }

        // Play shoot sound for other players (not the owner, they already heard it)
        if (!IsOwner)
        {
            PlayShootSound();
        }
    }

    private void PlayShootSound()
    {
        if (audioSource != null && shootSFX != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f); // Add slight pitch variation
            audioSource.PlayOneShot(shootSFX, sfxVolume);
        }
    }

    private void PlayReloadSound()
    {
        if (audioSource != null && reloadSFX != null)
        {
            audioSource.PlayOneShot(reloadSFX, sfxVolume);
        }
    }

    private void PlayEmptyClipSound()
    {
        if (audioSource != null && emptyClipSFX != null)
        {
            audioSource.PlayOneShot(emptyClipSFX, sfxVolume * 0.7f); // Slightly quieter
        }
    }

    private void ResetShot()
    {
        readyToShoot = true;
        allowInvoke = true;
    }

    private void Reload()
    {
        reloading = true;
        isReloadAnimating = true;
        reloadAnimationProgress = 0f;

        PlayReloadSound(); // Play reload sound
        Invoke("ReloadFinished", reloadTime);
    }

    private void ReloadFinished()
    {
        bulletsLeft = ammo;
        reloading = false;
        // Animation will automatically return to normal position
    }
}