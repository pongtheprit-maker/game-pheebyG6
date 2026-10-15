using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Q cycles the flashlight: lowered (off) -> normal light -> purple UV light -> lowered.
/// The flashlight is held in the right hand with the elbow bent (visible lower-right of the view).
/// The UV light reveals UVRevealable marks (blackboard, desks, walls ...).
/// The rig is a child of the camera, so it inherits head bob; extra hand lag, sprint lean and
/// wall-retract are added here. No UI is drawn.
/// </summary>
public class FlashlightController : MonoBehaviour
{
    /// <summary>The flashlight currently in the scene (used by UVRevealable).</summary>
    public static FlashlightController Active { get; private set; }

    [Header("References")]
    public Transform rig;               // arm + flashlight, child of the camera
    public Light beam;                  // spot light at the lens
    public FirstPersonController controller;

    [Header("Poses (camera-local)")]
    public Vector3 loweredPosition = new Vector3(0.12f, -0.75f, 0.05f);
    public Vector3 loweredEuler = new Vector3(35f, 10f, 0f);
    public Vector3 raisedPosition = Vector3.zero;
    public Vector3 raisedEuler = Vector3.zero;

    [Header("Motion")]
    public float raiseTime = 0.5f;
    public float handLagDegrees = 0.012f;
    public float maxLagDegrees = 4f;
    public Vector3 sprintOffset = new Vector3(0.025f, -0.045f, -0.03f);
    public Vector3 sprintEuler = new Vector3(6f, -4f, 3f);

    [Header("Wall retract")]
    public float retractStartDistance = 0.95f;
    public float retractRange = 0.55f;
    public LayerMask obstacleMask = ~0;

    [Header("Light modes (Q: Off -> Normal -> UV)")]
    [Tooltip("True: Q cycles Off -> Normal -> UV -> Off. False: after raising, Q only swaps Normal <-> UV.")]
    public bool cycleThroughOff = true;
    public Color uvColor = new Color(0.55f, 0.18f, 1f, 1f);
    public float uvIntensity = 70f;
    public float uvRange = 16f;
    public float uvSpotAngle = 48f;
    public float uvInnerSpotAngle = 14f;
    [Tooltip("Seconds to blend the beam between normal and UV.")]
    public float modeBlendTime = 0.15f;

    bool wantRaised;
    bool uvMode;
    float modeBlend;
    float blend;
    float prevYaw, prevPitch;
    Vector2 lagCurrent;
    float sprintBlend;
    float retractCurrent;
    Transform cam;

    // Beam values captured from the scene so the normal light stays exactly as authored.
    Color normalColor = Color.white;
    float normalIntensity, normalRange, normalSpot, normalInner;

    /// <summary>The light object (lens spot light). Used for UV reveal checks.</summary>
    public Light Beam { get { return beam; } }

    /// <summary>True only while the flashlight is fully raised AND set to purple UV.</summary>
    public bool IsUVOn { get { return wantRaised && uvMode && blend > 0.7f && beam != null && beam.enabled; } }

    void Awake()
    {
        if (controller == null) controller = GetComponent<FirstPersonController>();
    }

    void OnEnable() { Active = this; }

    void OnDisable() { if (Active == this) Active = null; }

    void Start()
    {
        if (controller != null && controller.playerCamera != null) cam = controller.playerCamera.transform;
        prevYaw = transform.eulerAngles.y;
        prevPitch = CurrentPitch();
        if (rig != null) rig.gameObject.SetActive(false);
        if (beam != null)
        {
            normalColor = beam.color;
            normalIntensity = beam.intensity;
            normalRange = beam.range;
            normalSpot = beam.spotAngle;
            normalInner = beam.innerSpotAngle;
            beam.enabled = false;
        }
    }

    float CurrentPitch()
    {
        if (controller != null && controller.head != null)
            return Mathf.DeltaAngle(0f, controller.head.localEulerAngles.x);
        return 0f;
    }

    void HandleToggleInput()
    {
        if (Keyboard.current == null || !Keyboard.current.qKey.wasPressedThisFrame) return;

        if (!wantRaised)
        {
            // Always start from the normal light when raising the flashlight.
            wantRaised = true;
            uvMode = false;
        }
        else if (!uvMode)
        {
            uvMode = true;
        }
        else if (cycleThroughOff)
        {
            wantRaised = false;
        }
        else
        {
            uvMode = false;
        }
    }

    void ApplyBeamMode(float dt)
    {
        if (beam == null) return;
        modeBlend = Mathf.MoveTowards(modeBlend, uvMode ? 1f : 0f, dt / Mathf.Max(0.01f, modeBlendTime));
        float m = Mathf.SmoothStep(0f, 1f, modeBlend);
        beam.color = Color.Lerp(normalColor, uvColor, m);
        beam.intensity = Mathf.Lerp(normalIntensity, uvIntensity, m);
        beam.range = Mathf.Lerp(normalRange, uvRange, m);
        beam.spotAngle = Mathf.Lerp(normalSpot, uvSpotAngle, m);
        beam.innerSpotAngle = Mathf.Lerp(normalInner, uvInnerSpotAngle, m);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || rig == null) return;

        HandleToggleInput();

        blend = Mathf.MoveTowards(blend, wantRaised ? 1f : 0f, dt / Mathf.Max(0.05f, raiseTime));
        float e = Mathf.SmoothStep(0f, 1f, blend);

        bool visible = blend > 0.001f;
        if (rig.gameObject.activeSelf != visible) rig.gameObject.SetActive(visible);

        // Light only while the flashlight is actually up.
        if (beam != null) beam.enabled = wantRaised && blend > 0.7f;
        ApplyBeamMode(dt);

        // Hand lags slightly behind camera rotation.
        float yaw = transform.eulerAngles.y;
        float pitch = CurrentPitch();
        float yawVel = Mathf.DeltaAngle(prevYaw, yaw) / dt;
        float pitchVel = (pitch - prevPitch) / dt;
        prevYaw = yaw;
        prevPitch = pitch;
        Vector2 lagTarget = new Vector2(
            Mathf.Clamp(-pitchVel * handLagDegrees, -maxLagDegrees, maxLagDegrees),
            Mathf.Clamp(-yawVel * handLagDegrees, -maxLagDegrees, maxLagDegrees));
        lagCurrent = Vector2.Lerp(lagCurrent, lagTarget, 1f - Mathf.Exp(-10f * dt));

        // Lean the arm back a little while sprinting.
        bool sprinting = controller != null && controller.IsSprinting;
        sprintBlend = Mathf.MoveTowards(sprintBlend, sprinting ? 1f : 0f, dt * 4f);

        // Pull the flashlight back when a wall is right in front of the player.
        float retractTarget = 0f;
        if (cam != null && visible)
        {
            RaycastHit hit;
            if (Physics.Raycast(cam.position, cam.forward, out hit, retractStartDistance, obstacleMask, QueryTriggerInteraction.Ignore))
                retractTarget = Mathf.Clamp01((retractStartDistance - hit.distance) / Mathf.Max(0.01f, retractRange));
        }
        retractCurrent = Mathf.Lerp(retractCurrent, retractTarget, 1f - Mathf.Exp(-12f * dt));

        // Compose the pose.
        Vector3 pos = Vector3.Lerp(loweredPosition, raisedPosition, e);
        Quaternion rot = Quaternion.Slerp(Quaternion.Euler(loweredEuler), Quaternion.Euler(raisedEuler), e);

        // Small arc so the hand sweeps up and out rather than sliding in a straight line.
        pos += new Vector3(0.04f, 0f, 0f) * Mathf.Sin(e * Mathf.PI);

        // Tiny idle sway.
        float t = Time.time;
        pos += new Vector3(Mathf.Sin(t * 0.9f) * 0.003f, Mathf.Sin(t * 1.3f) * 0.003f, 0f) * e;

        pos += sprintOffset * (sprintBlend * e);
        rot *= Quaternion.Euler(sprintEuler * (sprintBlend * e));

        pos += new Vector3(0f, -0.08f, -0.35f) * retractCurrent;
        rot *= Quaternion.Euler(25f * retractCurrent, 0f, 0f);

        rot = Quaternion.Euler(lagCurrent.x * e, lagCurrent.y * e, 0f) * rot;

        rig.localPosition = pos;
        rig.localRotation = rot;
    }
}
