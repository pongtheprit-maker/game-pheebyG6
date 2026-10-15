using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Add to visual props that should exist only in the player's sane or distorted perception.
/// It only changes renderers/materials; colliders and gameplay remain present.
/// </summary>
[DisallowMultipleComponent]
public class SanityPerceptionTarget : MonoBehaviour
{
    public static readonly HashSet<SanityPerceptionTarget> ActiveTargets = new HashSet<SanityPerceptionTarget>();

    public enum AppearanceMode { AlwaysVisible, NormalOnly, DistortedOnly }

    public AppearanceMode appearance = AppearanceMode.DistortedOnly;
    [Range(0f, 1f)] public float appearAtDistortion = 0.18f;
    [Range(0f, 1f)] public float disappearFromNormalAt = 0.72f;
    [Tooltip("Optional clean materials for this renderer set.")]
    public Material[] normalMaterials;
    [Tooltip("Optional decayed/haunted materials for this renderer set.")]
    public Material[] distortedMaterials;
    [Range(0f, 1f)] public float materialSwitchAt = 0.5f;

    Renderer[] renderers;
    bool[] initialEnabled;
    Material[][] initialMaterials;
    bool hasCaptured;
    bool distortedMaterialsActive;

    void Awake() { CaptureRenderers(); }

void OnEnable()
    {
        if (!hasCaptured) CaptureRenderers();
        ActiveTargets.Add(this);
        SanitySystem system = SanitySystem.Instance;
        ApplyDistortion(system != null ? system.Distortion : 0f);
    }

void OnDisable()
    {
        ActiveTargets.Remove(this);
        if (hasCaptured) RestoreInitialState();
    }

    void CaptureRenderers()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        initialEnabled = new bool[renderers.Length];
        initialMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            initialEnabled[i] = renderers[i] != null && renderers[i].enabled;
            initialMaterials[i] = renderers[i] != null ? renderers[i].sharedMaterials : null;
        }

        hasCaptured = true;
        distortedMaterialsActive = false;
    }

public void ApplyDistortion(float blend)
    {
        if (!hasCaptured) CaptureRenderers();
        blend = Mathf.Clamp01(blend);

        bool visible = true;
        switch (appearance)
        {
            case AppearanceMode.NormalOnly:
                visible = blend < disappearFromNormalAt;
                break;
            case AppearanceMode.DistortedOnly:
                visible = blend >= appearAtDistortion;
                break;
        }

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = initialEnabled[i] && visible;

        bool useDistorted = blend >= materialSwitchAt;
        if (useDistorted != distortedMaterialsActive)
        {
            Material[] chosen = useDistorted ? distortedMaterials : normalMaterials;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (chosen != null && chosen.Length > 0) renderers[i].sharedMaterials = chosen;
                else if (initialMaterials[i] != null) renderers[i].sharedMaterials = initialMaterials[i];
            }
            distortedMaterialsActive = useDistorted;
        }
    }

    void RestoreInitialState()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].enabled = initialEnabled[i];
            if (initialMaterials[i] != null) renderers[i].sharedMaterials = initialMaterials[i];
        }
    }
}
