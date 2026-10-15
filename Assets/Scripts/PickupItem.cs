using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put on an object that has a Collider. When the player aims at it within range, the prompt
/// shows "[E] verb displayName"; pressing E adds it to the PlayerInventory.
/// </summary>
public class PickupItem : MonoBehaviour
{
    [Tooltip("Unique id used by locks, for example crowbar or key_staff_room.")]
    public string itemId = "item";
    [Tooltip("Name shown to the player.")]
    public string displayName = "ไอเท็ม";
    public string verb = "หยิบ";
    [Tooltip("Shown after pickup. Leave empty for the default message.")]
    public string pickupMessage = "";
    [Tooltip("Hide the object after pickup.")]
    public bool hideOnPickup = true;
    public UnityEvent onPickedUp;

    public static event Action<PickupItem> PickedUp;

    public string PromptText { get { return verb + " " + displayName; } }

    public void Pickup(PlayerInventory inventory)
    {
        if (inventory == null) return;
        inventory.Add(itemId, displayName);
        if (onPickedUp != null) onPickedUp.Invoke();
        if (PickedUp != null) PickedUp(this);

        if (InteractionPrompt.Instance != null)
            InteractionPrompt.Instance.ShowMessage(string.IsNullOrEmpty(pickupMessage) ? "ได้รับ: " + displayName : pickupMessage);

        if (hideOnPickup) gameObject.SetActive(false);
    }
}
