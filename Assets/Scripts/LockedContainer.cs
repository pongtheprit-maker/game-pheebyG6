using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A locker or cabinet that needs an item to open: a crowbar to pry it ("งัด") or a key to unlock it ("ไข").
/// On success it can give a reward item and optionally swing a door leaf open.
/// Put it on the object that has the Collider the player aims at.
/// </summary>
public class LockedContainer : MonoBehaviour
{
    [Tooltip("Item the player must hold, for example crowbar or key_lounge_locker.")]
    public string requiredItemId = "crowbar";
    [Tooltip("Verb in the prompt, for example งัด or ไข.")]
    public string actionVerb = "งัด";
    public string displayName = "ล็อกเกอร์";
    public string lockedMessage = "ติดแน่น ต้องมีของมางัด";
    public string openedMessage = "เปิดแล้ว";
    public bool consumeRequiredItem = false;

    [Header("Reward")]
    public string rewardItemId = "";
    public string rewardDisplayName = "";

    [Header("Optional door swing (the leaf pivot must be at its hinge)")]
    public Transform doorLeaf;
    public Vector3 openEuler = new Vector3(0f, -100f, 0f);

    public UnityEvent onOpened;
    public static event Action<LockedContainer> Opened;

    public bool IsOpen { get; private set; }

    public string GetPrompt(PlayerInventory inventory, out bool available)
    {
        available = inventory != null && inventory.Has(requiredItemId);
        if (IsOpen) return null;
        return actionVerb + displayName;
    }

    public bool TryOpen(PlayerInventory inventory)
    {
        if (IsOpen) return true;
        if (inventory == null || !inventory.Has(requiredItemId))
        {
            if (InteractionPrompt.Instance != null) InteractionPrompt.Instance.ShowMessage(lockedMessage);
            return false;
        }

        IsOpen = true;
        if (consumeRequiredItem) inventory.Remove(requiredItemId);
        if (!string.IsNullOrEmpty(rewardItemId)) inventory.Add(rewardItemId, rewardDisplayName);
        if (doorLeaf != null) doorLeaf.localRotation = doorLeaf.localRotation * Quaternion.Euler(openEuler);

        string msg = openedMessage;
        if (!string.IsNullOrEmpty(rewardItemId))
            msg += "  ได้รับ: " + (string.IsNullOrEmpty(rewardDisplayName) ? rewardItemId : rewardDisplayName);
        if (InteractionPrompt.Instance != null) InteractionPrompt.Instance.ShowMessage(msg, 3f);

        if (onOpened != null) onOpened.Invoke();
        if (Opened != null) Opened(this);
        return true;
    }
}
