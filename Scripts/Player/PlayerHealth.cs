using System.Collections;
using System.Collections.Generic;
using TMPro.Examples;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float followUpSpeed = 2f;
    public GameObject randomizer;

    [Header("UI References")]
    [HideInInspector] public Image healthBar;
    [HideInInspector] public Image followUpHealthBar;
    [HideInInspector] public TextMeshProUGUI healthNumber;
    public TextMeshProUGUI countdownText;
    public GameObject blackPanel;

    // Network variable for health synchronization
    private NetworkVariable<float> networkHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float lerpTimer;

    public override void OnNetworkSpawn()
    {
        // Initialize health
        if (IsServer)
        {
            networkHealth.Value = maxHealth;
        }

        // Subscribe to health changes
        networkHealth.OnValueChanged += OnHealthChanged;

        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from health changes
        networkHealth.OnValueChanged -= OnHealthChanged;
        base.OnNetworkDespawn();
    }

    void Start()
    {
        // Set initial health if not networked yet
        if (!IsSpawned)
        {
            networkHealth.Value = maxHealth;
        }
    }

    void Update()
    {
        // Only update UI for the owner or if this is a local single-player setup
        if (IsOwner || !IsSpawned)
        {
            UpdateHealthUI();
        }
    }

    private void OnHealthChanged(float previousHealth, float newHealth)
    {
        // Reset lerp timer when health changes
        lerpTimer = 0f;
        Debug.Log($"Health changed for player {OwnerClientId}: {previousHealth} -> {newHealth}");
    }

    private void UpdateHealthUI()
    {
        float currentHealth = IsSpawned ? networkHealth.Value : maxHealth;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (healthBar == null || followUpHealthBar == null || healthNumber == null)
            return;

        float fillHealth = healthBar.fillAmount;
        float fillFollowUp = followUpHealthBar.fillAmount;
        float healthFraction = currentHealth / maxHealth;

        if (fillFollowUp > healthFraction)
        {
            healthBar.fillAmount = healthFraction;
            followUpHealthBar.color = Color.red;
            lerpTimer += Time.deltaTime;
            float percentComplete = lerpTimer / followUpSpeed;
            percentComplete = percentComplete * percentComplete;
            followUpHealthBar.fillAmount = Mathf.Lerp(fillFollowUp, healthFraction, percentComplete);
        }

        if (fillHealth < healthFraction)
        {
            followUpHealthBar.color = Color.green;
            followUpHealthBar.fillAmount = healthFraction;
            lerpTimer += Time.deltaTime;
            float percentComplete = lerpTimer / followUpSpeed;
            percentComplete = percentComplete * percentComplete;
            healthBar.fillAmount = Mathf.Lerp(fillHealth, followUpHealthBar.fillAmount, percentComplete);
        }

        healthNumber.text = Mathf.Round(currentHealth) + "";
    }

    public void TakeDamage(float damage)
    {
        if (!IsSpawned)
        {
            // Local damage for non-networked objects
            float newHealth = Mathf.Max(0, networkHealth.Value - damage);
            networkHealth.Value = newHealth;
            lerpTimer = 0f;
            return;
        }

        // Network damage
        TakeDamageServerRpc(damage);
    }

    // New public method for server-side damage (called directly by bullets on server)
    public void TakeDamageOnServer(float damage)
    {
        if (!IsServer) return;

        float newHealth = Mathf.Max(0, networkHealth.Value - damage);
        networkHealth.Value = newHealth;

        Debug.Log($"Player {OwnerClientId} took {damage} damage. Health: {newHealth}");

        // Check if player died
        if (newHealth <= 0)
        {
            StartCoroutine(DelayedDeathRpc());
        }
    }

    private IEnumerator DelayedDeathRpc()
    {
        DisableAllInputClientRpc();
        // Disable movement/input
        yield return new WaitForSeconds(3f);

        // Only server should trigger the death sequence
        if (IsServer)
        {
            OnPlayerDiedClientRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float damage)
    {
        TakeDamageOnServer(damage);
    }

    [ClientRpc]
    private void OnPlayerDiedClientRpc()
    {
        Debug.Log($"Player {OwnerClientId} died!");
        RestartRandomizerOnAllClients();
        RestoreAllPlayersHealthOnAllClients();
        RespawnAllPlayersClientRpc();

        // Only start countdown once, not on every client
        if (IsServer)
        {
            StartCountdownClientRpc();
        }
    }

    [ClientRpc]
    public void StartCountdownClientRpc()
    {
        // Find all players and show countdown on each player's own UI
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth player in allPlayers)
        {
            if (player.IsOwner)
            {
                player.StartCoroutine(player.DelayedEnableInput());
            }
        }
    }

    private IEnumerator DelayedEnableInput()
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
    private void DisableAllInputClientRpc()
    {
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth player in allPlayers)
        {
            InputManager input = player.GetComponent<InputManager>();
            if (input != null)
                input.enabled = false;
        }
    }

    [ClientRpc]
    private void EnableAllInputClientRpc()
    {
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth player in allPlayers)
        {
            InputManager input = player.GetComponent<InputManager>();
            if (input != null)
                input.enabled = true;
        }
    }

    [ClientRpc]
    private void RespawnAllPlayersClientRpc()
    {
        // Get spawn positions
        GameObject spawnOrange = GameObject.Find("Spawn Orange");
        GameObject spawnGreen = GameObject.Find("Spawn Green");

        // Find all players and reset their positions on all clients
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();

        foreach (PlayerHealth player in allPlayers)
        {
            if (player.IsSpawned)
            {
                Vector3 spawnPosition = Vector3.zero;

                // Determine spawn position based on client ID
                if (player.OwnerClientId == 0)
                {
                    // Host spawns at orange spawn
                    if (spawnOrange != null)
                    {
                        spawnPosition = spawnOrange.transform.position;
                    }
                }
                else
                {
                    // Client spawns at green spawn
                    if (spawnGreen != null)
                    {
                        spawnPosition = spawnGreen.transform.position;
                    }
                }

                // Reset position for this player
                player.transform.position = spawnPosition;

                // Reset physics
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Debug.Log($"Player {player.OwnerClientId} moved to spawn position: {spawnPosition} on client {NetworkManager.Singleton.LocalClientId}");
            }
        }
    }

    private void RestartRandomizerOnAllClients()
    {
        // Find the Randomizer component in the scene
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth player in allPlayers)
        {
            if (player.randomizer != null)
            {
                Randomizer r = player.randomizer.GetComponent<Randomizer>();
                if (r != null)
                {
                    r.restart();
                    Debug.Log($"Randomizer restarted for player {player.OwnerClientId} on client {NetworkManager.Singleton.LocalClientId}");
                }
            }
        }
    }

    private void RestoreAllPlayersHealthOnAllClients()
    {
        // Find all PlayerHealth components in the scene
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth player in allPlayers)
        {
            if (player.IsSpawned)
            {
                // Reset health to maximum for each player
                if (IsServer)
                {
                    player.networkHealth.Value = player.maxHealth;
                }

                // Reset lerp timer for smooth UI transition
                player.lerpTimer = 0f;

                Debug.Log($"Health restored to {player.maxHealth} for player {player.OwnerClientId} on client {NetworkManager.Singleton.LocalClientId}");
            }
        }
    }

    public void HealHealth(float heal)
    {
        if (!IsSpawned)
        {
            // Local heal for non-networked objects
            float newHealth = Mathf.Min(maxHealth, networkHealth.Value + heal);
            networkHealth.Value = newHealth;
            lerpTimer = 0f;
            return;
        }

        // Network heal
        HealHealthServerRpc(heal);
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealHealthServerRpc(float heal)
    {
        float newHealth = Mathf.Min(maxHealth, networkHealth.Value + heal);
        networkHealth.Value = newHealth;
    }

    // Public getter for current health
    public float GetCurrentHealth()
    {
        return IsSpawned ? networkHealth.Value : maxHealth;
    }

    // Public getter for health percentage
    public float GetHealthPercentage()
    {
        return GetCurrentHealth() / maxHealth;
    }
}