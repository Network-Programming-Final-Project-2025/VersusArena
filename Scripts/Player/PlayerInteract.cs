using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerInteract : NetworkBehaviour
{
    private Camera cam;
    [SerializeField]
    private float lookDistance = 5f;
    [SerializeField]
    private LayerMask layerMask;
    private InputManager inputManager;

    void Start()
    {
        // Only initialize for the owner
        if (!IsOwner) return;

        cam = Camera.main;
        inputManager = GetComponent<InputManager>();
    }

    void Update()
    {
        // Only run interaction logic for the owner
        if (!IsOwner) return;

        // Clear prompt text first
        if (PlayerUIManager.LocalInstance != null)
        {
            PlayerUIManager.LocalInstance.UpdatePromptText(string.Empty);
        }

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hitInfo;

        if (Physics.Raycast(ray, out hitInfo, lookDistance, layerMask))
        {
            if (hitInfo.collider.GetComponent<Interactable>() != null)
            {
                Interactable interact = hitInfo.collider.GetComponent<Interactable>();

                // Update prompt text
                if (PlayerUIManager.LocalInstance != null)
                {
                    PlayerUIManager.LocalInstance.UpdatePromptText(interact.promptMessage);
                }

                if (inputManager.walk.Interact.triggered)
                {
                    interact.BaseInteract();
                }
            }
        }
    }
}