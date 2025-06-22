using System.Collections;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class DisableMovement : NetworkBehaviour
{

    public TextMeshProUGUI countdownText;
    public GameObject blackPanel;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        if (IsServer)
        {
            CheckPlayersAndEnableInput();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            CheckPlayersAndEnableInput();
        }
    }

    private void CheckPlayersAndEnableInput()
    {
        if (!IsServer) return;

        int playerCount = NetworkManager.Singleton.ConnectedClients.Count;

        if (playerCount >= 2)
        {
            StartCountdownClientRpc(); // trigger countdown on all clients
        }
        else
        {
            DisableAllInputClientRpc();
        }
    }

    [ClientRpc]
    private void StartCountdownClientRpc()
    {
        StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }

        if (blackPanel != null)
        {
            blackPanel.SetActive(true);
        }

        if (countdownText != null) countdownText.text = "3";
        yield return new WaitForSeconds(1f);

        if (countdownText != null) countdownText.text = "2";
        yield return new WaitForSeconds(1f);

        if (countdownText != null) countdownText.text = "1";
        yield return new WaitForSeconds(1f);

        if (countdownText != null) countdownText.text = "GO!";
        yield return new WaitForSeconds(0.5f);

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (blackPanel != null) blackPanel.SetActive(false);

        EnableAllInputClientRpc();
    }

    [ClientRpc]
    private void EnableAllInputClientRpc()
    {
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth player in allPlayers)
        {
            InputManager input = player.GetComponent<InputManager>();
            if (input != null)
            {
                input.enabled = true;
            }
        }
    }

    [ClientRpc]
    private void DisableAllInputClientRpc()
    {
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth player in allPlayers)
        {
            InputManager input = player.GetComponent<InputManager>();
            if (input != null)
            {
                input.enabled = false;
            }
        }
    }
}
