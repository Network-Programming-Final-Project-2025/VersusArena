using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

public class NetworkButton : MonoBehaviour
{
    public static bool hasBeenLoaded = false;
    private GameRelay gameRelay;
    private string relayCode;

    [SerializeField] private Button startHost;
    [SerializeField] private Button startClient;
    [SerializeField] private TMP_InputField relayCodeInput; // Changed to TMP_InputField
    [SerializeField] private TMP_Text relayCodeDisplay; // To show the generated relay code

    private void Awake()
    {
        // Find the GameRelay component in the scene
        gameRelay = FindObjectOfType<GameRelay>();

        if (gameRelay == null)
        {
            Debug.LogError("GameRelay component not found in the scene! Please add GameRelay script to a GameObject.");
            return;
        }

        if (hasBeenLoaded)
        {
            // This logic seems wrong - you shouldn't start both host and client
            // NetworkManager.Singleton.StartHost();
            // NetworkManager.Singleton.StartClient();
            Hide(); // Just hide the buttons if already loaded
        }
        else
        {
            startHost.onClick.AddListener(() =>
            {
                gameRelay.CreateRelay(OnRelayCodeGenerated);
                Hide(); // Hide buttons after starting
            });

            startClient.onClick.AddListener(() =>
            {
                // Get relay code from input field or use the stored one
                string codeToUse = relayCodeInput != null ? relayCodeInput.text : relayCode;

                if (!string.IsNullOrEmpty(codeToUse))
                {
                    gameRelay.JoinRelay(codeToUse);
                    Hide(); // Hide buttons after joining
                }
                else
                {
                    Debug.LogError("Relay code is empty! Please enter a relay code.");
                }
            });

            hasBeenLoaded = true;
        }
    }

    // Callback method to display the relay code
    private void OnRelayCodeGenerated(string code)
    {
        if (relayCodeDisplay != null)
        {
            relayCodeDisplay.text = code;
            relayCodeDisplay.gameObject.SetActive(true);
        }
        Debug.Log(code);
    }

    // Public method to set relay code from external sources
    public void SetRelayCode(string code)
    {
        relayCode = code;
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}