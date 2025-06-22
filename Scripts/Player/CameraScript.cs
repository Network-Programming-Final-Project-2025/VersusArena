using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class CameraScript : NetworkBehaviour
{

    [SerializeField] private Transform camPosition;
    private Camera playerCamera;
    private AudioListener audioListener;

    // Start is called before the first frame update
    void Start()
    {

        playerCamera = GetComponent<Camera>();
        audioListener = GetComponent<AudioListener>();

        if (IsOwner)
        {
            if (playerCamera != null) playerCamera.enabled = true;
            if (audioListener != null) audioListener.enabled = true;
        }
        else
        {
            if (playerCamera != null) playerCamera.enabled = false;
            if (audioListener != null) audioListener.enabled = false;
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner || camPosition == null) return;

        transform.position = camPosition.position;
    }
}
