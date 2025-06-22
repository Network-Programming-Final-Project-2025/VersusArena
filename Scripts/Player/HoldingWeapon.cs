using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class HoldingWeapon : MonoBehaviour
{
    [SerializeField] private TwoBoneIKConstraint rightHandIK;
    [SerializeField] private TwoBoneIKConstraint leftHandIK;
    [SerializeField] private Transform currentRightGrip;
    [SerializeField] private Transform currentLeftGrip;
    [SerializeField] private bool debugMode = false;

    public void SwitchWeapon(GameObject newWeapon)
    {
        if (newWeapon == null)
        {
            DisableIK();
            return;
        }

        // Find grip points for both hands
        Transform rightGrip = newWeapon.transform.Find("RightHandGrip");
        Transform leftGrip = newWeapon.transform.Find("LeftHandGrip");

        // Alternative names to search for
        if (rightGrip == null) rightGrip = newWeapon.transform.Find("GripPoint_Right");
        if (leftGrip == null) leftGrip = newWeapon.transform.Find("GripPoint_Left");

        // Setup right hand IK
        if (rightGrip != null)
        {
            rightHandIK.data.target = rightGrip; // Use target, not hint
            currentRightGrip = rightGrip;
            rightHandIK.weight = 1f;
            rightHandIK.Reset();

            if (debugMode) Debug.Log($"Right hand IK set to {rightGrip.name}");
        }
        else
        {
            rightHandIK.weight = 0f;
            Debug.LogWarning($"No right hand grip point found on {newWeapon.name}");
        }

        // Setup left hand IK
        if (leftGrip != null)
        {
            leftHandIK.data.target = leftGrip; // Use target, not hint
            currentLeftGrip = leftGrip;
            leftHandIK.weight = 1f;
            leftHandIK.Reset();

            if (debugMode) Debug.Log($"Left hand IK set to {leftGrip.name}");
        }
        else
        {
            leftHandIK.weight = 0f;
            Debug.LogWarning($"No left hand grip point found on {newWeapon.name}");
        }
    }

    public void DisableIK()
    {
        if (rightHandIK != null)
        {
            rightHandIK.weight = 0f;
            currentRightGrip = null;
        }

        if (leftHandIK != null)
        {
            leftHandIK.weight = 0f;
            currentLeftGrip = null;
        }
    }

    public void EnableIK()
    {
        if (rightHandIK != null && currentRightGrip != null)
        {
            rightHandIK.weight = 1f;
        }

        if (leftHandIK != null && currentLeftGrip != null)
        {
            leftHandIK.weight = 1f;
        }
    }

    // For weapons that only need one hand (like pistols)
    public void SwitchToOneHandedWeapon(GameObject newWeapon, bool useRightHand = true)
    {
        Transform grip = newWeapon.transform.Find("GripPoint");

        if (grip != null)
        {
            if (useRightHand)
            {
                rightHandIK.data.target = grip;
                rightHandIK.weight = 1f;
                rightHandIK.Reset();

                // Disable left hand
                leftHandIK.weight = 0f;
            }
            else
            {
                leftHandIK.data.target = grip;
                leftHandIK.weight = 1f;
                leftHandIK.Reset();

                // Disable right hand
                rightHandIK.weight = 0f;
            }
        }
    }
}