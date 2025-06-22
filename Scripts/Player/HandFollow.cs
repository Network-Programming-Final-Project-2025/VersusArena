using System.Collections;
using System.Collections.Generic;
using TMPro.Examples;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class HandFollow : NetworkBehaviour
{
    [Header("References")]
    public Camera cam;
    public Transform attackPoint;

    [Header("Gun Positioning")]
    public Transform playerTransform; // Reference to the Player object
    public Transform mainCamera; // Reference to the Main Camera
    public Vector3 gunOffset = new Vector3(0.5f, -0.3f, 0.8f); // Offset from camera
    public float followSpeed = 15f;

    private void Update()
    {
        if (IsOwner)
        {
            // ONLY update gun position to follow player hands
            UpdateGunPosition();
        }
    }

    private void UpdateGunPosition()
    {
        if (mainCamera != null && playerTransform != null)
        {
            // Calculate target position: Player position + camera-relative offset
            Vector3 targetPosition = playerTransform.position + mainCamera.TransformDirection(gunOffset);

            // Calculate target rotation: camera rotation
            Quaternion targetRotation = mainCamera.rotation;

            // Apply position and rotation with smooth interpolation
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, followSpeed * Time.deltaTime);
        }
    }
}