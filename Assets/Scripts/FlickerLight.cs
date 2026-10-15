using UnityEngine;

/// <summary>
/// Unstable fluorescent tube: the light (and the tube's emissive glow) stutters and drops out at random.
/// </summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    public Renderer tubeRenderer;
    [ColorUsage(false, true)] public Color tubeEmission = new Color(1.6f, 1.7f, 1.5f);
    public float minOnTime = 0.6f;
    public float maxOnTime = 5f;
    public float burstMinCount = 2;
    public float burstMaxCount = 7;
    public float burstStepMin = 0.025f;
    public float burstStepMax = 0.11f;
    [Range(0f, 1f)] public float offLevel = 0.02f;

    Light lamp;
    MaterialPropertyBlock block;
    float baseIntensity;
    float nextChange;
    int burstRemaining;
    bool lit = true;
    float noiseSeed;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        block = new MaterialPropertyBlock();
        noiseSeed = Random.value * 100f;
        nextChange = Time.time + Random.Range(minOnTime, maxOnTime);
    }

    void Update()
    {
        if (Time.time >= nextChange)
        {
            if (burstRemaining > 0)
            {
                lit = !lit;
                burstRemaining--;
                nextChange = Time.time + Random.Range(burstStepMin, burstStepMax);
                if (burstRemaining == 0) lit = true;
            }
            else
            {
                burstRemaining = Mathf.RoundToInt(Random.Range(burstMinCount, burstMaxCount));
                lit = false;
                nextChange = Time.time + Random.Range(burstStepMin, burstStepMax);
                // Sometimes the tube stays dead for a moment before coming back.
                if (Random.value < 0.25f) nextChange += Random.Range(0.3f, 1.2f);
            }

            if (burstRemaining == 0 && lit)
                nextChange = Time.time + Random.Range(minOnTime, maxOnTime);
        }

        float hum = 0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 18f, noiseSeed);
        float level = lit ? hum : offLevel;
        lamp.intensity = baseIntensity * level;

        if (tubeRenderer != null)
        {
            tubeRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, tubeEmission * level);
            tubeRenderer.SetPropertyBlock(block);
        }
    }
}
