using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerNetworkAnimator : MonoBehaviour
{

    public Animator animator;

    private NetworkAnimator networkAnimator;

    // Start is called before the first frame update
    void Start()
    {
        networkAnimator = GetComponent<NetworkAnimator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
