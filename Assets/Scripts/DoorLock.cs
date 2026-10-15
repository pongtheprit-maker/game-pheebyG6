using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Add next to a SchoolDoor (on the hinge object) to make that door need a key item.
/// With locked = true the door cannot be opened until the player holds requiredItemId and presses E.
/// </summary>
[RequireComponent(typeof(SchoolDoor))]
public class DoorLock : MonoBehaviour
{
    public bool locked = true;
    [Tooltip("Item id the player must hold, for example key_staff_room.")]
    public string requiredItemId = "";
    public string lockedMessage = "ล็อกอยู่ ต้องใช้กุญแจ";
    public string unlockedMessage = "ไขประตูแล้ว";
    [Tooltip("Remove the key from the inventory when used.")]
    public bool consumeItem = false;
    public UnityEvent onUnlocked;

    public static event Action<DoorLock> Unlocked;

    /// <summary>Returns true if the door is now unlocked.</summary>
    public bool TryUnlock(PlayerInventory inventory)
    {
        if (!locked) return true;
        bool hasKey = inventory != null && inventory.Has(requiredItemId);
        if (!hasKey)
        {
            if (InteractionPrompt.Instance != null) InteractionPrompt.Instance.ShowMessage(lockedMessage);
            return false;
        }

        locked = false;
        if (consumeItem) inventory.Remove(requiredItemId);
        if (onUnlocked != null) onUnlocked.Invoke();
        if (Unlocked != null) Unlocked(this);
        if (InteractionPrompt.Instance != null) InteractionPrompt.Instance.ShowMessage(unlockedMessage);
        return true;
    }
}
