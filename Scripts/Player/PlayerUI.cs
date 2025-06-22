using UnityEngine;
using Unity.Netcode;

public class PlayerUI : NetworkBehaviour
{
    public void UpdateText(string promptText)
    {
        // Use the static reference to update UI
        if (PlayerUIManager.LocalInstance != null)
        {
            PlayerUIManager.LocalInstance.UpdatePromptText(promptText);
        }
    }

    public void ShowPrompt(string message)
    {
        if (PlayerUIManager.LocalInstance != null)
        {
            PlayerUIManager.LocalInstance.ShowPrompt(message);
        }
    }

    public void HidePrompt()
    {
        if (PlayerUIManager.LocalInstance != null)
        {
            PlayerUIManager.LocalInstance.HidePrompt();
        }
    }
}