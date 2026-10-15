using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Place on a real enemy or scare source. Proximity drains sanity and gradually reveals distorted reality.
/// The object's physical colliders and damage logic remain under the enemy's own control.
/// </summary>
[DisallowMultipleComponent]
public class SanityThreat : MonoBehaviour
{
    public static readonly HashSet<SanityThreat> ActiveThreats = new HashSet<SanityThreat>();

    [Min(0.1f)] public float fearRadius = 8f;
    [Min(0f)] public float sanityDrainPerSecond = 16f;

    void OnEnable() { ActiveThreats.Add(this); }
    void OnDisable() { ActiveThreats.Remove(this); }
    void OnDestroy() { ActiveThreats.Remove(this); }

    internal float GetFearAt(Vector3 playerPosition)
    {
        float radius = Mathf.Max(0.1f, fearRadius);
        float distance = Vector3.Distance(transform.position, playerPosition);
        return distance >= radius ? 0f : (1f - distance / radius) * sanityDrainPerSecond;
    }
}
