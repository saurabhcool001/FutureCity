using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>A named state a building can be in: Normal, Closed, Upgraded, Robots...</summary>
    [CreateAssetMenu(menuName = "Town Council/Building State", fileName = "BuildingState_")]
    public class BuildingStateId : ScriptableObject
    {
        public string displayName = "State";

        [Header("Placeholder look (used when a building has no art for this state)")]
        [Tooltip("Tint applied to the placeholder building.")]
        public Color tint = Color.white;
        [Tooltip("Height of the placeholder building relative to normal.")]
        [Min(0.1f)] public float heightMultiplier = 1f;
        [Tooltip("Glow strength, e.g. for upgraded or automated buildings.")]
        [Min(0f)] public float glow = 0f;
    }
}
