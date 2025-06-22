using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class StartButtonScript : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scene Name to Load")]
    public string sceneName = "SampleScene";
    public GameObject mainMenuUI;
    
    [Header("Sound Effects")]
    public AudioClip buttonPressSound;
    public AudioClip buttonReleaseSound;
    public AudioSource audioSource;
    
    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float pressVolume = 1f;
    [Range(0f, 1f)]
    public float releaseVolume = 1f;
    
    private void Start()
    {
        // If no AudioSource is assigned, try to get one from this GameObject
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            
            // If still null, create one
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
        
        // Get the Button component and add the click listener
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(ChangeScene);
        }
    }
    
    public void ChangeScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            HideMainMenu();
        }
        else
        {
            Debug.LogWarning("Scene name is not set!");
        }
    }
    
    public void HideMainMenu()
    {
        if (mainMenuUI != null)
        {
            mainMenuUI.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Main menu UI reference is not set!");
        }
    }
    
    // Called when the button is pressed down
    public void OnPointerDown(PointerEventData eventData)
    {
        PlayButtonPressSound();
    }
    
    // Called when the button is released
    public void OnPointerUp(PointerEventData eventData)
    {
        PlayButtonReleaseSound();
    }
    
    private void PlayButtonPressSound()
    {
        if (audioSource != null && buttonPressSound != null)
        {
            audioSource.PlayOneShot(buttonPressSound, pressVolume);
        }
    }
    
    private void PlayButtonReleaseSound()
    {
        if (audioSource != null && buttonReleaseSound != null)
        {
            audioSource.PlayOneShot(buttonReleaseSound, releaseVolume);
        }
    }
    
    // Alternative method if you want to call these manually
    public void PlayPressSound()
    {
        PlayButtonPressSound();
    }
    
    public void PlayReleaseSound()
    {
        PlayButtonReleaseSound();
    }
}