using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Singleton;
    [SerializeField] ChatMessage chatMessage;
    [SerializeField] CanvasGroup chatContent;
    [SerializeField] TMP_InputField chatInput;
    [SerializeField] GameObject chatPanel; // Reference to the chat panel GameObject
    public string playerName;

    private bool isChatActive = false;
    private float chatHideTimer = 0f;
    private const float CHAT_DISPLAY_TIME = 5f; // Time to show chat after receiving message

    void Awake()
    {
        ChatManager.Singleton = this;
    }

    void Start()
    {
        // Initially hide the chat
        SetChatVisibility(false);
        // Don't lock cursor here - assume it's already locked by the game
    }

    void Update()
    {
        HandleChatInput();
        HandleChatVisibility();
    }

    void HandleChatInput()
    {
        // Toggle chat when pressing Enter
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!isChatActive)
            {
                // Open chat
                OpenChat();
            }
            else
            {
                // Send message and close chat
                if (!string.IsNullOrWhiteSpace(chatInput.text))
                {
                    SendChatMessage(chatInput.text, playerName);
                    chatInput.text = "";
                }
                CloseChat();
            }
        }

        // Close chat with Escape
        if (Input.GetKeyDown(KeyCode.Escape) && isChatActive)
        {
            chatInput.text = "";
            CloseChat();
        }
    }

    void HandleChatVisibility()
    {
        // Auto-hide chat after receiving messages
        if (!isChatActive && chatHideTimer > 0f)
        {
            chatHideTimer -= Time.deltaTime;
            if (chatHideTimer <= 0f)
            {
                SetChatVisibility(false);
            }
        }
    }

    void OpenChat()
    {
        isChatActive = true;
        SetChatVisibility(true);
        SetCursorLock(false); // Unlock cursor for chat input
        chatInput.ActivateInputField();
        chatInput.Select();
        chatHideTimer = 0f; // Cancel auto-hide timer
    }

    void CloseChat()
    {
        isChatActive = false;
        SetCursorLock(true); // Re-lock cursor for game
        chatInput.DeactivateInputField();

        // Start auto-hide timer if there are recent messages
        if (chatContent.transform.childCount > 0)
        {
            chatHideTimer = CHAT_DISPLAY_TIME;
        }
        else
        {
            SetChatVisibility(false);
        }
    }

    void SetChatVisibility(bool visible)
    {
        if (chatPanel != null)
        {
            chatPanel.SetActive(visible);
        }
        else
        {
            // Fallback to using CanvasGroup alpha
            chatContent.alpha = visible ? 1f : 0f;
            chatContent.interactable = visible;
            chatContent.blocksRaycasts = visible;
        }
    }

    void SetCursorLock(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void SendChatMessage(string message, string player = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }
        string formattedMessage = player + " > " + message;
        SendChatMessageServerRpc(formattedMessage);
    }

    void AddMessage(string msg)
    {
        ChatMessage CM = Instantiate(chatMessage, chatContent.transform);
        CM.SetText(msg);

        // Show chat when receiving a message
        if (!isChatActive)
        {
            SetChatVisibility(true);
            chatHideTimer = CHAT_DISPLAY_TIME;
        }

        // Optional: Limit number of messages displayed
        if (chatContent.transform.childCount > 50)
        {
            DestroyImmediate(chatContent.transform.GetChild(0).gameObject);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendChatMessageServerRpc(string msg)
    {
        ReceiveChatMessageClientRpc(msg);
    }

    [ClientRpc]
    void ReceiveChatMessageClientRpc(string msg)
    {
        ChatManager.Singleton.AddMessage(msg);
    }

    // Public method to check if chat is active (useful for other scripts)
    public bool IsChatActive()
    {
        return isChatActive;
    }
}