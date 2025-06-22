using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBodyRotation : MonoBehaviour
{
    [SerializeField]
    private Transform cameraTransform;

    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;
        
        if (cameraForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = targetRotation;
        }
    }
}
