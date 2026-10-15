using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player interaction: aims a ray from the camera and handles E for doors, locked doors,
/// pickup items and locked containers. Shows the [E] prompt through InteractionPrompt.
/// </summary>
public class PlayerDoorInteractor : MonoBehaviour
{
    [Tooltip("Camera used to aim at things. Automatically found under this GameObject if empty.")]
    public Camera viewCamera;
    public float interactionDistance = 3f;

    PlayerInventory inventory;
    InteractionPrompt prompt;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponentInChildren<Camera>();

        inventory = GetComponent<PlayerInventory>();
        if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();
        prompt = GetComponent<InteractionPrompt>();
        if (prompt == null) prompt = gameObject.AddComponent<InteractionPrompt>();
    }

    void Update()
    {
        if (viewCamera == null) return;

        PickupItem pickup = null;
        LockedContainer container = null;
        SchoolDoor door = null;
        DoorLock doorLock = null;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            pickup = hit.collider.GetComponentInParent<PickupItem>();
            if (pickup == null)
                container = hit.collider.GetComponentInParent<LockedContainer>();
            if (pickup == null && container == null)
            {
                door = hit.collider.GetComponentInParent<SchoolDoor>();
                if (door != null) doorLock = door.GetComponent<DoorLock>();
            }
        }

        string text = null;
        bool available = true;
        if (pickup != null)
        {
            text = pickup.PromptText;
        }
        else if (container != null)
        {
            text = container.GetPrompt(inventory, out available);
        }
        else if (door != null)
        {
            if (doorLock != null && doorLock.locked)
            {
                available = inventory.Has(doorLock.requiredItemId);
                text = available ? "ไขประตู" : "ประตูล็อกอยู่";
            }
            else
            {
                text = door.IsOpen ? "ปิดประตู" : "เปิดประตู";
            }
        }

        if (text != null) prompt.SetPrompt(text, available);
        else prompt.ClearPrompt();

        if (text == null || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (pickup != null)
        {
            pickup.Pickup(inventory);
        }
        else if (container != null)
        {
            container.TryOpen(inventory);
        }
        else if (door != null)
        {
            if (doorLock != null && doorLock.locked) doorLock.TryUnlock(inventory);
            else door.Toggle();
        }
    }
}
