using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person walking / sprinting controller with a heavy, grounded camera feel (Resident Evil 7 style).
/// The player has no visible body or legs: only a CharacterController capsule with no renderer.
/// Hierarchy expected:  Player (this + CharacterController)  ->  Head  ->  Camera
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    public Transform head;          // pitch pivot at eye height
    public Camera playerCamera;     // child of head, receives bob / sway
    public StaminaSystem stamina;
    public Animator characterAnimator; // optional visual character model animator
   // optional: limits and slows sprinting

    [Header("Movement")]
    public float walkSpeed = 2.0f;
    public float sprintSpeed = 4.0f;
    public float backwardSpeedMultiplier = 0.6f;
    public float strafeSpeedMultiplier = 0.8f;
    public float acceleration = 7f;
    public float deceleration = 9f;
    public float gravity = -15f;

    [Header("Look")]
    public float lookSensitivity = 0.08f;   // degrees per mouse pixel
    public float lookSmoothing = 20f;       // higher = snappier
    public float maxPitch = 80f;
    public float defaultPitch = 0f;

    [Header("Camera")]
    public float fieldOfView = 70f;
    public float nearClip = 0.05f;

    [Header("Head bob / sway")]
    public float bobCyclesPerSecond = 0.95f;
    public float bobVertical = 0.04f;
    public float bobHorizontal = 0.025f;
    public float bobRollDegrees = 0.7f;
    public float strafeRollDegrees = 1.4f;
    public float breathVertical = 0.005f;
    public float breathPitchDegrees = 0.18f;
    public float breathCyclesPerSecond = 0.22f;
    public float cameraFollowSharpness = 14f;

    public bool IsSprinting { get; private set; }

    CharacterController controller;
    float yaw, pitch;
    float smoothYaw, smoothPitch;
    Vector3 planarVelocity;
    float verticalVelocity;
    float bobPhase;
    float bobAmount;
    float rollCurrent;
    Vector3 camOffsetCurrent;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (stamina == null) stamina = GetComponent<StaminaSystem>();

        if (head == null)
        {
            Transform h = transform.Find("Head");
            if (h != null) head = h;
        }
        if (playerCamera == null && head != null)
            playerCamera = head.GetComponentInChildren<Camera>();

        if (playerCamera != null)
        {
            playerCamera.fieldOfView = fieldOfView;
            playerCamera.nearClipPlane = nearClip;
        }

        yaw = smoothYaw = transform.eulerAngles.y;
        pitch = smoothPitch = defaultPitch;
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        HandleLook(dt);
        HandleMove(dt);
        HandleCamera(dt);
        UpdateCharacterAnimation();
    }

void UpdateCharacterAnimation()
    {
        if (characterAnimator == null) return;
        characterAnimator.SetFloat("Speed", planarVelocity.magnitude);
        characterAnimator.SetBool("Grounded", controller.isGrounded);
    }


    void HandleLook(float dt)
    {
        Vector2 delta = Vector2.zero;
        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            delta = Mouse.current.delta.ReadValue();

        yaw += delta.x * lookSensitivity;
        pitch -= delta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        float k = 1f - Mathf.Exp(-lookSmoothing * dt);
        smoothYaw = Mathf.LerpAngle(smoothYaw, yaw, k);
        smoothPitch = Mathf.Lerp(smoothPitch, pitch, k);

        transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
        if (head != null)
            head.localRotation = Quaternion.Euler(smoothPitch, 0f, 0f);
    }

    void HandleMove(float dt)
    {
        float x = 0f, z = 0f;
        bool shift = false;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) z += 1f;
            if (kb.sKey.isPressed) z -= 1f;
            if (kb.dKey.isPressed) x += 1f;
            if (kb.aKey.isPressed) x -= 1f;
            shift = kb.leftShiftKey.isPressed;
        }

        bool moving = (x != 0f || z != 0f);
        bool canSprint = stamina == null || stamina.CanSprint;
        IsSprinting = shift && z > 0f && moving && canSprint;

        Vector3 wish = Vector3.zero;
        if (moving)
        {
            float forward = z > 0f ? 1f : backwardSpeedMultiplier;
            Vector3 dir = (transform.right * x * strafeSpeedMultiplier) +
                          (transform.forward * z * (z != 0f ? forward : 1f));

            float speed = walkSpeed;
            if (IsSprinting)
            {
                float mult = stamina != null ? stamina.SprintSpeedMultiplier : 1f;
                speed = Mathf.Lerp(walkSpeed, sprintSpeed, mult);
            }
            wish = Vector3.ClampMagnitude(dir, 1f) * speed;
        }

        if (stamina != null)
            stamina.Tick(dt, IsSprinting, moving);

        float rate = wish.sqrMagnitude > 0.001f ? acceleration : deceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, wish, rate * dt);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        Vector3 motion = planarVelocity + Vector3.up * verticalVelocity;
        controller.Move(motion * dt);
    }

    void HandleCamera(float dt)
    {
        if (playerCamera == null) return;

        // 0 = standing, 1 = walking pace, ~2 = full sprint
        float speedRatio = planarVelocity.magnitude / Mathf.Max(0.01f, walkSpeed);
        bobAmount = Mathf.Lerp(bobAmount, Mathf.Min(speedRatio, 2.2f), 1f - Mathf.Exp(-6f * dt));
        bobPhase += dt * bobCyclesPerSecond * Mathf.PI * 2f * Mathf.Min(speedRatio, 2.2f);

        // Amplitude grows more slowly than cadence once running.
        float amp = Mathf.Min(bobAmount, 1f) + Mathf.Max(0f, bobAmount - 1f) * 0.7f;
        float walkFactor = Mathf.Clamp01(bobAmount);

        float t = Time.time;

        // Footstep bob: vertical at twice the horizontal frequency.
        float bobY = Mathf.Sin(bobPhase * 2f) * bobVertical * amp;
        float bobX = Mathf.Sin(bobPhase) * bobHorizontal * amp;
        float bobRoll = Mathf.Sin(bobPhase) * bobRollDegrees * amp;

        // Idle breathing so the view is never perfectly still.
        float breath = Mathf.Sin(t * Mathf.PI * 2f * breathCyclesPerSecond);
        float breathY = breath * breathVertical * (1f - walkFactor * 0.6f);
        float breathPitch = breath * breathPitchDegrees * (1f - walkFactor * 0.6f);

        // Lean into strafing.
        float strafeInput = Vector3.Dot(planarVelocity, transform.right) / Mathf.Max(0.01f, walkSpeed);
        float targetRoll = -strafeInput * strafeRollDegrees + bobRoll;
        rollCurrent = Mathf.Lerp(rollCurrent, targetRoll, 1f - Mathf.Exp(-8f * dt));

        Vector3 targetOffset = new Vector3(bobX, bobY + breathY, 0f);
        camOffsetCurrent = Vector3.Lerp(camOffsetCurrent, targetOffset, 1f - Mathf.Exp(-cameraFollowSharpness * dt));

        playerCamera.transform.localPosition = camOffsetCurrent;
        playerCamera.transform.localRotation = Quaternion.Euler(breathPitch, 0f, rollCurrent);
    }
}
