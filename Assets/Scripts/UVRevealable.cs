using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A hidden mark / clue (writing on the blackboard, scratches on a desk, a handprint on a wall ...)
/// that only becomes visible while the purple UV flashlight shines on it.
///
/// Put it on a Renderer object (a Quad works well) placed ~1-2 cm in front of the surface and remove
/// its collider. Menu: GameObject > UV Light > Create UV Mark (Quad).
///
/// Puzzle systems can listen to <see cref="Revealed"/> (static event) or <see cref="onFirstRevealed"/>.
/// </summary>
[DisallowMultipleComponent]
public class UVRevealable : MonoBehaviour
{
    /// <summary>Fired once per mark the first time it is fully revealed. Argument = the mark.</summary>
    public static event Action<UVRevealable> Revealed;

    [Header("Identity (for the puzzle system)")]
    [Tooltip("Free text id, e.g. 'blackboard_date', 'desk_engraving'. Puzzle scripts can read it.")]
    public string clueId = "";

    [Header("Look")]
    [Tooltip("Optional mask / handwriting texture (alpha = shape). Empty = solid glowing rectangle.")]
    public Texture2D markTexture;
    [ColorUsage(true, true)]
    public Color glowColor = new Color(0.75f, 0.45f, 1f, 1f);
    [Range(0f, 1f)] public float maxAlpha = 0.9f;
    [Tooltip("True: build a transparent unlit glow material automatically. False: keep your own material and just show/hide the renderer.")]
    public bool autoMaterial = true;

    [Header("Reveal")]
    public float fadeInSpeed = 5f;
    [Tooltip("Slow fade-out leaves a short afterglow when the beam moves away.")]
    public float fadeOutSpeed = 1.2f;
    [Tooltip("0 = use the beam range. Otherwise the mark only reacts within this distance.")]
    public float maxDistance = 0f;
    [Range(0.1f, 1f)]
    [Tooltip("Share of the beam cone that counts as lit (1 = full spot angle).")]
    public float coneUse = 0.9f;
    [Tooltip("True: once revealed, the mark stays visible forever.")]
    public bool stayRevealedOnce = false;
    public UnityEvent onFirstRevealed;

    public bool HasBeenRevealed { get; private set; }
    public float Amount { get { return amount; } }

    Renderer[] renderers;
    Material runtimeMat;
    float amount;
    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        if (autoMaterial)
        {
            runtimeMat = MakeGlowMaterial(markTexture, glowColor);
            foreach (Renderer r in renderers)
            {
                r.sharedMaterial = runtimeMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
        SetRenderersEnabled(false);
    }

    void OnDestroy()
    {
        if (runtimeMat != null) Destroy(runtimeMat);
    }

    void Update()
    {
        if (renderers == null || renderers.Length == 0) return;

        float target = 0f;
        if (stayRevealedOnce && HasBeenRevealed) target = 1f;
        else if (IsLitByUV()) target = 1f;

        float speed = target > amount ? fadeInSpeed : fadeOutSpeed;
        amount = Mathf.MoveTowards(amount, target, speed * Time.deltaTime);
        ApplyVisual();

        if (!HasBeenRevealed && amount >= 0.95f)
        {
            HasBeenRevealed = true;
            if (onFirstRevealed != null) onFirstRevealed.Invoke();
            if (Revealed != null) Revealed(this);
        }
    }

    void ApplyVisual()
    {
        bool visible = amount > 0.003f;
        if (autoMaterial)
        {
            SetRenderersEnabled(visible);
            if (visible && runtimeMat != null)
            {
                Color c = glowColor;
                c.a = amount * maxAlpha;
                runtimeMat.SetColor("_BaseColor", c);
                runtimeMat.SetColor("_Color", c);
            }
        }
        else
        {
            SetRenderersEnabled(amount > 0.5f);
        }
    }

    void SetRenderersEnabled(bool on)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].enabled != on) renderers[i].enabled = on;
    }

    /// <summary>Is the purple beam currently shining on this mark (range, cone and line of sight)?</summary>
    bool IsLitByUV()
    {
        FlashlightController fl = FlashlightController.Active;
        if (fl == null || !fl.IsUVOn) return false;

        Transform b = fl.Beam.transform;
        float range = fl.Beam.range;
        if (maxDistance > 0f) range = Mathf.Min(range, maxDistance);
        float halfAngle = fl.Beam.spotAngle * 0.5f * coneUse;

        Bounds bounds = GetBounds();
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;

        // Test the centre and six points on the bounds so large marks react when only partly lit.
        if (SamplePoint(fl, b, c, range, halfAngle)) return true;
        if (SamplePoint(fl, b, c + new Vector3(e.x, 0f, 0f), range, halfAngle)) return true;
        if (SamplePoint(fl, b, c - new Vector3(e.x, 0f, 0f), range, halfAngle)) return true;
        if (SamplePoint(fl, b, c + new Vector3(0f, e.y, 0f), range, halfAngle)) return true;
        if (SamplePoint(fl, b, c - new Vector3(0f, e.y, 0f), range, halfAngle)) return true;
        if (SamplePoint(fl, b, c + new Vector3(0f, 0f, e.z), range, halfAngle)) return true;
        if (SamplePoint(fl, b, c - new Vector3(0f, 0f, e.z), range, halfAngle)) return true;
        return false;
    }

    bool SamplePoint(FlashlightController fl, Transform beamT, Vector3 point, float range, float halfAngle)
    {
        Vector3 to = point - beamT.position;
        float dist = to.magnitude;
        if (dist > range || dist < 0.01f) return false;
        if (Vector3.Angle(beamT.forward, to) > halfAngle) return false;

        // Line of sight: stop 5 cm in front of the mark so the surface it sits on never blocks it.
        Vector3 dir = to / dist;
        float checkDist = Mathf.Max(0f, dist - 0.05f);
        int n = Physics.RaycastNonAlloc(beamT.position, dir, hitBuffer, checkDist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform t = hitBuffer[i].transform;
            if (t.IsChildOf(transform)) continue;       // own colliders
            if (t.IsChildOf(fl.transform)) continue;    // the player's own collider
            return false;                               // something solid is in the way
        }
        return true;
    }

    Bounds GetBounds()
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Material MakeGlowMaterial(Texture2D tex, Color color)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.name = "UV Glow (runtime)";

        if (sh.name.StartsWith("Universal"))
        {
            m.SetFloat("_Surface", 1f);   // transparent
            m.SetFloat("_Blend", 0f);     // alpha blend
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);      // visible from both sides
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            if (tex != null) m.SetTexture("_BaseMap", tex);
        }
        else if (tex != null)
        {
            m.mainTexture = tex;
        }

        Color c = color;
        c.a = 0f;
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        return m;
    }
}
