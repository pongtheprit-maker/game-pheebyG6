using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Very small item bag for story items (keys, crowbar, photo frame ...).
/// Items are identified by a string id such as "crowbar" or "key_staff_room".
/// Added automatically to the Player by PlayerDoorInteractor if missing.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    /// <summary>Raised with (itemId, displayName) when an item is added.</summary>
    public static event Action<string, string> ItemAdded;
    /// <summary>Raised with itemId when an item is removed.</summary>
    public static event Action<string> ItemRemoved;

    readonly Dictionary<string, string> names = new Dictionary<string, string>();
    readonly List<string> order = new List<string>();

    public int Count { get { return order.Count; } }

    void Awake() { Instance = this; }
    void OnEnable() { Instance = this; }

    public bool Has(string id)
    {
        return !string.IsNullOrEmpty(id) && names.ContainsKey(id);
    }

    public void Add(string id, string displayName)
    {
        if (string.IsNullOrEmpty(id) || names.ContainsKey(id)) return;
        names[id] = string.IsNullOrEmpty(displayName) ? id : displayName;
        order.Add(id);
        if (ItemAdded != null) ItemAdded(id, names[id]);
    }

    public bool Remove(string id)
    {
        if (!Has(id)) return false;
        names.Remove(id);
        order.Remove(id);
        if (ItemRemoved != null) ItemRemoved(id);
        return true;
    }

    public string NameOf(string id)
    {
        string n;
        return names.TryGetValue(id, out n) ? n : id;
    }

    public IList<string> ItemIds { get { return order; } }
}
