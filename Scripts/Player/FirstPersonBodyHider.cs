using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class FirstPersonBodyHider : NetworkBehaviour
{
    [Header("Body Parts to Hide")]
    public GameObject[] bodyParts; // Assign head, body, arms, etc. in inspector

    [Header("Auto-Find Settings")]
    public bool autoFindBodyParts = true;
    public string[] bodyPartNames = { "Head", "Body", "Chest", "Torso", "Arms", "Arm_L", "Arm_R" };

    private Camera playerCamera;
    private int originalLayer;
    private int hiddenLayer;

    void Start()
    {
        if (!IsOwner) return;

        // Set up layers
        originalLayer = 0; // Default layer
        hiddenLayer = 31; // Use layer 31 for hidden body parts (you can change this)

        // Find the player's camera
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Auto-find body parts if enabled
        if (autoFindBodyParts)
        {
            FindBodyParts();
        }

        // Hide body parts from this player's view
        HideBodyFromOwner();
    }

    private void FindBodyParts()
    {
        List<GameObject> foundParts = new List<GameObject>();

        // Search through all children for body parts
        Transform[] allChildren = GetComponentsInChildren<Transform>();

        foreach (Transform child in allChildren)
        {
            foreach (string partName in bodyPartNames)
            {
                if (child.name.ToLower().Contains(partName.ToLower()))
                {
                    foundParts.Add(child.gameObject);
                    break;
                }
            }
        }

        // Also look for renderers on the main object
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer.gameObject != gameObject) // Don't include the main player object
            {
                foundParts.Add(renderer.gameObject);
            }
        }

        bodyParts = foundParts.ToArray();
    }

    private void HideBodyFromOwner()
    {
        if (playerCamera == null) return;

        // Set camera to not render the hidden layer
        playerCamera.cullingMask &= ~(1 << hiddenLayer);

        // Move body parts to hidden layer
        foreach (GameObject bodyPart in bodyParts)
        {
            if (bodyPart != null)
            {
                SetLayerRecursively(bodyPart, hiddenLayer);
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        // Also set layer for all children
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public override void OnNetworkDespawn()
    {
        // Reset layers when player despawns (cleanup)
        if (IsOwner)
        {
            foreach (GameObject bodyPart in bodyParts)
            {
                if (bodyPart != null)
                {
                    SetLayerRecursively(bodyPart, originalLayer);
                }
            }
        }

        base.OnNetworkDespawn();
    }

    // Manual method to add body parts at runtime
    public void AddBodyPart(GameObject bodyPart)
    {
        if (!IsOwner) return;

        List<GameObject> partsList = new List<GameObject>(bodyParts);
        partsList.Add(bodyPart);
        bodyParts = partsList.ToArray();

        // Hide the new body part
        SetLayerRecursively(bodyPart, hiddenLayer);
    }

    // Manual method to remove body parts at runtime
    public void RemoveBodyPart(GameObject bodyPart)
    {
        if (!IsOwner) return;

        List<GameObject> partsList = new List<GameObject>(bodyParts);
        partsList.Remove(bodyPart);
        bodyParts = partsList.ToArray();

        // Show the body part again
        SetLayerRecursively(bodyPart, originalLayer);
    }

    // Toggle body visibility (useful for third-person switching)
    public void ToggleBodyVisibility(bool visible)
    {
        if (!IsOwner) return;

        int targetLayer = visible ? originalLayer : hiddenLayer;

        foreach (GameObject bodyPart in bodyParts)
        {
            if (bodyPart != null)
            {
                SetLayerRecursively(bodyPart, targetLayer);
            }
        }

        // Update camera culling mask
        if (visible)
        {
            playerCamera.cullingMask |= (1 << hiddenLayer);
        }
        else
        {
            playerCamera.cullingMask &= ~(1 << hiddenLayer);
        }
    }
}