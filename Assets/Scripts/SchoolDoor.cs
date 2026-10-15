using UnityEngine;

/// <summary>
/// Hinged school door. This component sits on the HINGE object (its origin is the hinge line);
/// the visible leaf is a child offset from it, so rotating the hinge swings the leaf around the
/// side edge instead of spinning it around its own centre. E-key interaction (PlayerDoorInteractor)
/// still calls Toggle().
/// </summary>
public class SchoolDoor : MonoBehaviour
{
    [Tooltip("The visible door leaf (child of the hinge object).")]
    public Transform leaf;
    [Tooltip("Swing angle in degrees while open. The sign sets the swing direction.")]
    public float openAngle = 90f;
    [Tooltip("Maximum swing speed in degrees per second.")]
    public float rotationSpeed = 180f;
    [Tooltip("Ease time at the start and end of a swing (seconds).")]
    public float smoothTime = 0.28f;
    [Tooltip("Begin in the open pose (used for doors left ajar).")]
    public bool startOpen;

    bool isOpen;
    public bool IsOpen { get { return isOpen; } }
    float angle;
    float angleVelocity;
    Quaternion closedRotation;
    Collider leafCollider;

    void Awake()
    {
        if (leaf == null && transform.childCount > 0)
            leaf = transform.GetChild(0);

        // The rotation stored in the scene is the CLOSED pose of the hinge.
        closedRotation = transform.localRotation;
        if (leaf != null)
            leafCollider = leaf.GetComponent<Collider>();

        isOpen = startOpen;
        angle = startOpen ? openAngle : 0f;
        ApplyAngle();
        SetSolid(!isOpen && Mathf.Abs(angle) < 0.05f);
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        // A moving or open door must never trap the player's CharacterController.
        SetSolid(false);
    }

    void Update()
    {
        float target = isOpen ? openAngle : 0f;
        if (Mathf.Abs(angle - target) > 0.02f || Mathf.Abs(angleVelocity) > 0.05f)
        {
            angle = Mathf.SmoothDamp(angle, target, ref angleVelocity, smoothTime, rotationSpeed);
            if (Mathf.Abs(angle - target) < 0.05f && Mathf.Abs(angleVelocity) < 1f)
            {
                angle = target;
                angleVelocity = 0f;
            }
            ApplyAngle();
        }

        // Only become solid again once fully closed and nobody is standing in the doorway.
        if (!isOpen && angle == 0f && leafCollider != null && leafCollider.isTrigger && !PlayerInDoorway())
            SetSolid(true);
    }

    void ApplyAngle()
    {
        transform.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
    }

    void SetSolid(bool solid)
    {
        if (leafCollider == null) return;
        leafCollider.enabled = true;
        leafCollider.isTrigger = !solid;
    }

    bool PlayerInDoorway()
    {
        Bounds b = leafCollider.bounds;
        Collider[] hits = Physics.OverlapBox(b.center, b.extents + new Vector3(0.35f, 0f, 0.35f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponent<CharacterController>() != null)
                return true;
        }
        return false;
    }
}
