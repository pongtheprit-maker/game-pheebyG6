using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Darkens the screen (tunnel vision + lowered exposure) when the player is severely exhausted.
/// Works on a runtime copy of the global volume profile, so the saved profile asset is never modified.
/// </summary>
[RequireComponent(typeof(StaminaSystem))]
public class FatigueVision : MonoBehaviour
{
    public Volume volume;

    [Header("Fully exhausted look")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.92f;
    [Range(0f, 1f)] public float vignetteSmoothness = 0.75f;
    public float postExposure = -2.6f;

    [Header("Response")]
    public float smoothTime = 0.45f;

    StaminaSystem stamina;
    Vignette vignette;
    ColorAdjustments colorAdjustments;
    float baseVigIntensity, baseVigSmoothness, baseExposure;
    float darkness, darknessVelocity;

    void Awake()
    {
        stamina = GetComponent<StaminaSystem>();

        if (volume == null)
        {
            Volume[] all = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].isGlobal) { volume = all[i]; break; }
            }
        }
        if (volume == null) { enabled = false; return; }

        // .profile returns a per-instance clone of the shared profile.
        VolumeProfile profile = volume.profile;

        if (profile.TryGet(out vignette))
        {
            baseVigIntensity = vignette.intensity.value;
            baseVigSmoothness = vignette.smoothness.value;
        }
        if (profile.TryGet(out colorAdjustments))
        {
            baseExposure = colorAdjustments.postExposure.value;
            colorAdjustments.postExposure.overrideState = true;
        }
    }

    void Update()
    {
        float target = stamina.SevereAmount;
        darkness = Mathf.SmoothDamp(darkness, target, ref darknessVelocity, smoothTime);

        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(baseVigIntensity, vignetteIntensity, darkness);
            vignette.smoothness.value = Mathf.Lerp(baseVigSmoothness, vignetteSmoothness, darkness);
        }
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = Mathf.Lerp(baseExposure, postExposure, darkness);
        }
    }
}
