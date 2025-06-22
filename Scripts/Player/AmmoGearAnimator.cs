using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AmmoGearAnimator : MonoBehaviour
{
    [Header("Gear Image Reference")]
    [SerializeField] private Image gearImage;

    [Header("Animation Settings")]
    [SerializeField] private float spinDuration = 0.5f;
    [SerializeField] private float spinAmount = 180f; // Degrees to spin
    [SerializeField] private AnimationCurve spinCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Pulse Settings")]
    [SerializeField] private float pulseDuration = 0.3f;
    [SerializeField] private float pulseScale = 1.3f;
    [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Color Flash")]
    [SerializeField] private bool enableColorFlash = true;
    [SerializeField] private Color flashColor = Color.yellow;
    [SerializeField] private float flashDuration = 0.15f;

    private Color originalColor;
    private Vector3 originalScale;
    private Coroutine currentSpinCoroutine;

    void Start()
    {
        // Auto-find gear image if not assigned
        if (gearImage == null)
        {
            gearImage = GetComponent<Image>();
        }

        if (gearImage != null)
        {
            originalColor = gearImage.color;
            originalScale = gearImage.transform.localScale;
        }
    }

    public void PlayFireAnimation()
    {
        if (gearImage == null) return;

        StartCoroutine(FireAnimationCoroutine());
    }

    private IEnumerator FireAnimationCoroutine()
    {
        // Start spin animation (will continue from current rotation)
        if (currentSpinCoroutine != null)
        {
            StopCoroutine(currentSpinCoroutine);
        }
        currentSpinCoroutine = StartCoroutine(SpinAnimation());

        // Start pulse and flash animations
        Coroutine pulseCoroutine = StartCoroutine(PulseAnimation());
        Coroutine flashCoroutine = null;

        if (enableColorFlash)
        {
            flashCoroutine = StartCoroutine(ColorFlashAnimation());
        }

        // Wait for pulse and flash to complete (spin continues)
        yield return pulseCoroutine;

        if (flashCoroutine != null)
        {
            yield return flashCoroutine;
        }
    }

    private IEnumerator SpinAnimation()
    {
        float elapsed = 0f;
        float startRotation = gearImage.transform.eulerAngles.z;
        float targetRotation = startRotation + spinAmount;

        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / spinDuration;
            float curveValue = spinCurve.Evaluate(progress);

            float currentRotation = Mathf.Lerp(startRotation, targetRotation, curveValue);
            gearImage.transform.rotation = Quaternion.Euler(0, 0, currentRotation);

            yield return null;
        }

        // Ensure final rotation is set
        gearImage.transform.rotation = Quaternion.Euler(0, 0, targetRotation);
        currentSpinCoroutine = null;
    }

    private IEnumerator PulseAnimation()
    {
        float elapsed = 0f;

        // Scale up phase
        while (elapsed < pulseDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (pulseDuration / 2f);
            float curveValue = pulseCurve.Evaluate(progress);

            Vector3 currentScale = Vector3.Lerp(originalScale, originalScale * pulseScale, curveValue);
            gearImage.transform.localScale = currentScale;

            yield return null;
        }

        elapsed = 0f;

        // Scale down phase
        while (elapsed < pulseDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (pulseDuration / 2f);
            float curveValue = pulseCurve.Evaluate(progress);

            Vector3 currentScale = Vector3.Lerp(originalScale * pulseScale, originalScale, curveValue);
            gearImage.transform.localScale = currentScale;

            yield return null;
        }

        // Ensure original scale is restored
        gearImage.transform.localScale = originalScale;
    }

    private IEnumerator ColorFlashAnimation()
    {
        float elapsed = 0f;

        // Flash to color
        while (elapsed < flashDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (flashDuration / 2f);

            Color currentColor = Color.Lerp(originalColor, flashColor, progress);
            gearImage.color = currentColor;

            yield return null;
        }

        elapsed = 0f;

        // Flash back to original
        while (elapsed < flashDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (flashDuration / 2f);

            Color currentColor = Color.Lerp(flashColor, originalColor, progress);
            gearImage.color = currentColor;

            yield return null;
        }

        // Ensure original color is restored
        gearImage.color = originalColor;
    }

    // Public method to trigger animation from other scripts
    public static void TriggerFireAnimation()
    {
        // Find the AmmoGearAnimator in the scene
        AmmoGearAnimator animator = FindObjectOfType<AmmoGearAnimator>();
        if (animator != null)
        {
            animator.PlayFireAnimation();
        }
    }

    // Method to set custom animation parameters at runtime
    public void SetSpinSettings(float duration, float amount)
    {
        spinDuration = duration;
        spinAmount = amount;
    }

    public void SetPulseSettings(float duration, float scale)
    {
        pulseDuration = duration;
        pulseScale = scale;
    }

    public void SetFlashSettings(Color color, float duration)
    {
        flashColor = color;
        flashDuration = duration;
    }
}