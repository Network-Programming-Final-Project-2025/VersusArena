using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChatMessage : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI textMessage;

    public void SetText(string text)
    {
        textMessage.text = text;
    }

}
