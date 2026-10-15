using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Inverse horror perception: high sanity masks marked horrors; low sanity reveals them.
/// Damage and threat objects call into this component. Health is five hits.
/// </summary>
[DisallowMultipleComponent]
public class SanitySystem : MonoBehaviour
{
    public static SanitySystem Instance { get; private set; }

    [Header("State")]
    [Min(1)] public int maxHealth = 5;
    [Min(1f)] public float maxSanity = 100f;
    [Min(0f)] public float sanityLossPerHit = 10f;
    [Min(0f)] public float recoveryPerSecond = 1.5f;
    [Min(0f)] public float revealAtSanity = 75f;
    [Min(0f)] public float fullDistortionAtSanity = 20f;

    [Header("Distorted reality post processing")]
    [Range(-100f, 100f)] public float distortedSaturation = -55f;
    [Range(-100f, 100f)] public float distortedContrast = 18f;
    [Range(0f, 1f)] public float distortedChromaticAberration = 0.12f;
    [Range(0f, 1f)] public float distortedFilmGrain = 0.28f;
    [Min(0.01f)] public float transitionSpeed = 2.2f;

    public int Health { get; private set; }
    public float CurrentSanity { get; private set; }
    public float NormalizedSanity { get { return CurrentSanity / Mathf.Max(1f, maxSanity); } }
    public float Distortion { get; private set; }
    public bool IsDead { get { return Health <= 0; } }

    SanityHUD hud;
    StaminaSystem stamina;

    Volume volume;
    VolumeProfile runtimeProfile;
    ColorAdjustments colorAdjustments;
    ChromaticAberration chromaticAberration;
    FilmGrain filmGrain;
    float baseSaturation, baseContrast, distortionVelocity;
    float baseChromaticAberration, baseFilmGrain;
    Color originalAmbientLight, originalFogColor;
    float originalFogDensity;


    float threatScanTimer;
    const float ThreatScanInterval = 0.20f;

    void Awake()
    {
        Instance = this;
        stamina = GetComponent<StaminaSystem>();

        Health = maxHealth;
        CurrentSanity = maxSanity;
        originalAmbientLight = RenderSettings.ambientLight;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;


        FirstPersonController firstPerson = GetComponent<FirstPersonController>();
        Camera camera = firstPerson != null ? firstPerson.playerCamera : null;
        if (camera == null) camera = GetComponentInChildren<Camera>();
        if (camera == null) camera = Camera.main;

        volume = FindGlobalVolume();
        if (volume != null)
        {
            runtimeProfile = volume.profile;
            if (runtimeProfile.TryGet(out colorAdjustments))
            {
                baseSaturation = colorAdjustments.saturation.value;
                baseContrast = colorAdjustments.contrast.value;
                colorAdjustments.saturation.overrideState = true;
                colorAdjustments.contrast.overrideState = true;
            }

            if (!runtimeProfile.TryGet(out chromaticAberration))
                chromaticAberration = runtimeProfile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.overrideState = true;
            baseChromaticAberration = chromaticAberration.intensity.value;


            if (!runtimeProfile.TryGet(out filmGrain))
                filmGrain = runtimeProfile.Add<FilmGrain>(true);
            filmGrain.intensity.overrideState = true;
            baseFilmGrain = filmGrain.intensity.value;

        }

        hud = gameObject.AddComponent<SanityHUD>();
        hud.Initialize(camera, this);
        UpdateHealthPulse();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (stamina == null) stamina = GetComponent<StaminaSystem>();
        if (hud != null) hud.SetFatigue(stamina != null ? stamina.Normalized : 1f);

        threatScanTimer += dt;
        if (threatScanTimer >= ThreatScanInterval)
        {
            float scanDelta = threatScanTimer;
            threatScanTimer = 0f;
            float fear = 0f;
            Vector3 playerPosition = transform.position;
            foreach (SanityThreat threat in SanityThreat.ActiveThreats)
            {
                if (threat == null || !threat.isActiveAndEnabled) continue;
                fear = Mathf.Max(fear, threat.GetFearAt(playerPosition));
            }

            float sanityChange = fear > 0f ? -fear * scanDelta : recoveryPerSecond * scanDelta;
            ChangeSanity(sanityChange);
        }

        float targetDistortion = Mathf.InverseLerp(revealAtSanity, fullDistortionAtSanity, CurrentSanity);
        Distortion = Mathf.SmoothDamp(Distortion, targetDistortion, ref distortionVelocity,
                                      1f / Mathf.Max(0.01f, transitionSpeed), Mathf.Infinity, dt);

        ApplyPostProcessing();
        ApplyPerceptionTargets();
    }

    static Volume FindGlobalVolume()
    {
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
            if (volumes[i] != null && volumes[i].isGlobal) return volumes[i];
        return null;
    }

    void ChangeSanity(float amount)
    {
        CurrentSanity = Mathf.Clamp(CurrentSanity + amount, 0f, maxSanity);
        if (hud != null) hud.SetSanity(CurrentSanity, maxSanity, Distortion);
    }

void ApplyPostProcessing()
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = Mathf.Lerp(baseSaturation, distortedSaturation, Distortion);
            colorAdjustments.contrast.value = Mathf.Lerp(baseContrast, distortedContrast, Distortion);
        }

        if (chromaticAberration != null)
            chromaticAberration.intensity.value = Mathf.Lerp(baseChromaticAberration, distortedChromaticAberration, Distortion);

        if (filmGrain != null)
            filmGrain.intensity.value = Mathf.Lerp(baseFilmGrain, distortedFilmGrain, Distortion);

        Color saneAmbient = new Color(.16f, .17f, .16f, 1f);
        RenderSettings.ambientLight = Color.Lerp(saneAmbient, originalAmbientLight, Distortion);
        if (RenderSettings.fog)
        {
            Color saneFog = new Color(.17f, .18f, .17f, 1f);
            RenderSettings.fogColor = Color.Lerp(saneFog, originalFogColor, Distortion);
            RenderSettings.fogDensity = Mathf.Lerp(0f, originalFogDensity, Distortion);
        }
    }

void ApplyPerceptionTargets()
    {
        foreach (SanityPerceptionTarget target in SanityPerceptionTarget.ActiveTargets)
            if (target != null) target.ApplyDistortion(Distortion);
    }

void OnDisable()
    {
        if (!Application.isPlaying) return;
        RenderSettings.ambientLight = originalAmbientLight;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = baseSaturation;
            colorAdjustments.contrast.value = baseContrast;
        }
        if (chromaticAberration != null) chromaticAberration.intensity.value = baseChromaticAberration;
        if (filmGrain != null) filmGrain.intensity.value = baseFilmGrain;
    }

void UpdateHealthPulse()
    {
        if (hud != null) hud.SetHealth(Health, maxHealth);
    }

    /// <summary>Call from an enemy or hazard when a hit lands. One hit removes one of five health points.</summary>
    public void TakeDamage(int hits = 1)
    {
        if (hits <= 0 || IsDead) return;
        int appliedHits = Mathf.Min(hits, Health);
        Health = Mathf.Max(0, Health - appliedHits);
        ChangeSanity(-sanityLossPerHit * appliedHits);
        UpdateHealthPulse();
    }

    /// <summary>Compatibility alias for damage events.</summary>
    public void ApplyDamage(int hits = 1) { TakeDamage(hits); }

    /// <summary>Call when a scripted scare directly affects the player's sanity.</summary>
    public void DrainSanity(float amount)
    {
        if (amount > 0f) ChangeSanity(-amount);
    }

    public void RestoreSanity(float amount)
    {
        if (amount > 0f && !IsDead) ChangeSanity(amount);
    }

    public void SetSanity(float value)
    {
        CurrentSanity = Mathf.Clamp(value, 0f, maxSanity);
        if (hud != null) hud.SetSanity(CurrentSanity, maxSanity, Distortion);
    }

internal void NotifyDistortion(float amount)
    {
        if (hud != null) hud.SetSanity(CurrentSanity, maxSanity, amount);
    }
}
