using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class QuitButtonScript : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Sound Effects")]
    public AudioClip buttonPressSound;
    public AudioClip buttonReleaseSound;
    public AudioSource audioSource;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float pressVolume = 1f;
    [Range(0f, 1f)]
    public float releaseVolume = 1f;

    [Header("Quit Settings")]
    public float quitDelay = 0.1f; // Small delay to let the release sound play

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
            button.onClick.AddListener(QuitGame);
        }
    }

    public void QuitGame()
    {
        // Add a small delay to let the button release sound play before quitting
        StartCoroutine(QuitWithDelay());
    }

    private IEnumerator QuitWithDelay()
    {
        yield return new WaitForSeconds(quitDelay);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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