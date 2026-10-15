using UnityEngine;

/// <summary>
/// Procedural breathing loops (no audio assets needed). The louder / faster loop fades in as the player tires:
/// Mild -> slightly stronger breathing, Moderate -> heavy breathing, Severe -> hard gasping.
/// </summary>
[RequireComponent(typeof(StaminaSystem))]
public class BreathingAudio : MonoBehaviour
{
    [Header("Max volume per fatigue level")]
    [Range(0f, 1f)] public float mildVolume = 0.22f;
    [Range(0f, 1f)] public float moderateVolume = 0.50f;
    [Range(0f, 1f)] public float severeVolume = 0.85f;
    public float fadeSpeed = 1.2f;

    StaminaSystem stamina;
    AudioSource mild, moderate, severe;
    float vMild, vModerate, vSevere;

    void Awake()
    {
        stamina = GetComponent<StaminaSystem>();

        mild     = MakeSource("Breath_Mild",     BuildBreath("BreathMild",     0.80f, 1.10f, 0.90f,  900f, 0.15f, 11));
        moderate = MakeSource("Breath_Moderate", BuildBreath("BreathModerate", 0.60f, 0.80f, 0.30f, 1400f, 0.30f, 23));
        severe   = MakeSource("Breath_Severe",   BuildBreath("BreathSevere",   0.45f, 0.55f, 0.12f, 2200f, 0.55f, 37));
    }

    AudioSource MakeSource(string name, AudioClip clip)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        src.Play();
        return src;
    }

    void Update()
    {
        float tMild = 0f, tModerate = 0f, tSevere = 0f;
        switch (stamina.Level)
        {
            case StaminaSystem.FatigueLevel.Mild: tMild = mildVolume; break;
            case StaminaSystem.FatigueLevel.Moderate: tModerate = moderateVolume; break;
            case StaminaSystem.FatigueLevel.Severe: tSevere = severeVolume; break;
        }

        float step = fadeSpeed * Time.deltaTime;
        vMild = Mathf.MoveTowards(vMild, tMild, step);
        vModerate = Mathf.MoveTowards(vModerate, tModerate, step);
        vSevere = Mathf.MoveTowards(vSevere, tSevere, step);

        mild.volume = vMild;
        moderate.volume = vModerate;
        severe.volume = vSevere;
    }

    /// <summary>One seamless breath cycle: inhale, exhale, pause. Starts and ends in silence so it loops cleanly.</summary>
    static AudioClip BuildBreath(string name, float inhale, float exhale, float gap, float cutoffHz, float rasp, int seed)
    {
        const int sampleRate = 44100;
        int n = Mathf.CeilToInt((inhale + exhale + gap) * sampleRate);
        float[] data = new float[n];
        System.Random rng = new System.Random(seed);

        float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / sampleRate);
        float lp1 = 0f, lp2 = 0f, peak = 0.0001f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sampleRate;
            float env;
            if (t < inhale)
            {
                float u = t / inhale;
                env = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.6f) * 0.75f;
            }
            else if (t < inhale + exhale)
            {
                float u = (t - inhale) / exhale;
                float onset = Mathf.Clamp01(u / 0.12f);
                env = onset * Mathf.Pow(1f - u, 1.4f);
            }
            else
            {
                env = 0f;
            }

            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp1 += a * (white - lp1);
            lp2 += a * (lp1 - lp2);
            float hiss = white - lp1;
            float flutter = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 7f * t);

            float s = (lp2 * 6f + hiss * rasp * env) * env * flutter;
            data[i] = s;
            float abs = Mathf.Abs(s);
            if (abs > peak) peak = abs;
        }

        float gain = 0.9f / peak;
        for (int i = 0; i < n; i++) data[i] *= gain;

        int fade = Mathf.Min(256, n / 2);
        for (int i = 0; i < fade; i++)
        {
            float f = (float)i / fade;
            data[i] *= f;
            data[n - 1 - i] *= f;
        }

        AudioClip clip = AudioClip.Create(name, n, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
