using UnityEngine;

/// <summary>
/// Hidden stamina model. Nothing here is ever drawn on screen; other systems (breathing sound,
/// sprint speed, screen darkening) read the fatigue level from this component.
/// </summary>
public class StaminaSystem : MonoBehaviour
{
    public enum FatigueLevel { None, Mild, Moderate, Severe }

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float drainPerSecond = 14f;
    public float regenStanding = 16f;
    public float regenWalking = 10f;
    public float regenDelay = 1.0f;

    [Header("Fatigue thresholds (fraction of max stamina remaining)")]
    [Range(0f, 1f)] public float mildThreshold = 0.70f;
    [Range(0f, 1f)] public float moderateThreshold = 0.40f;
    [Range(0f, 1f)] public float severeThreshold = 0.15f;

    [Header("Exhaustion")]
    [Tooltip("After stamina hits zero the player cannot sprint until this fraction has recovered.")]
    [Range(0f, 1f)] public float resumeSprintAt = 0.30f;

    [Header("Sprint speed while tired (1 = full sprint, 0 = walk speed)")]
    public float moderateSprintFactor = 0.60f;
    public float severeSprintFactor = 0.25f;
    public float speedChangeRate = 1.5f;

    float stamina;
    float timeSinceSprint = 99f;
    bool exhausted;
    float sprintFactor = 1f;

    /// <summary>1 = fully rested, 0 = empty.</summary>
    public float Normalized { get { return stamina / Mathf.Max(0.01f, maxStamina); } }

    public bool CanSprint { get { return !exhausted; } }

    /// <summary>Blend between walk speed (0) and full sprint speed (1).</summary>
    public float SprintSpeedMultiplier { get { return sprintFactor; } }

    /// <summary>0 when not severely tired, rising to 1 as stamina empties.</summary>
    public float SevereAmount
    {
        get { return Mathf.Clamp01(1f - Normalized / Mathf.Max(0.01f, severeThreshold)); }
    }

    public FatigueLevel Level
    {
        get
        {
            float n = Normalized;
            if (n < severeThreshold) return FatigueLevel.Severe;
            if (n < moderateThreshold) return FatigueLevel.Moderate;
            if (n < mildThreshold) return FatigueLevel.Mild;
            return FatigueLevel.None;
        }
    }

    void Awake()
    {
        stamina = maxStamina;
    }

    /// <summary>Called once per frame by the movement controller.</summary>
    public void Tick(float dt, bool sprinting, bool moving)
    {
        if (sprinting)
        {
            stamina -= drainPerSecond * dt;
            timeSinceSprint = 0f;
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
            }
        }
        else
        {
            timeSinceSprint += dt;
            if (timeSinceSprint >= regenDelay)
            {
                float regen = moving ? regenWalking : regenStanding;
                stamina = Mathf.Min(maxStamina, stamina + regen * dt);
            }
        }

        if (exhausted && Normalized >= resumeSprintAt)
            exhausted = false;

        float target = 1f;
        switch (Level)
        {
            case FatigueLevel.Moderate: target = moderateSprintFactor; break;
            case FatigueLevel.Severe: target = severeSprintFactor; break;
        }
        sprintFactor = Mathf.MoveTowards(sprintFactor, target, speedChangeRate * dt);
    }
}
