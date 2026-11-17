using System;
using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>A recorded resident's view, heard by touching a building.</summary>
    [Serializable]
    public class PerspectiveClip
    {
        public BuildingId building;
        public string speaker = "Resident";
        [TextArea(2, 4)] public string subtitle;
        public AudioClip audio;
    }

    /// <summary>A page of the welcome sequence shown when the headset goes on.</summary>
    [Serializable]
    public class IntroPage
    {
        public string title;
        [TextArea(3, 8)] public string body;
    }

    /// <summary>Change to one value meter, e.g. Fairness +2.</summary>
    [Serializable]
    public struct MeterDelta
    {
        public ValueMeterDefinition meter;
        public int amount;
    }

    /// <summary>How much a role cares about a value. Used by dynamic AI councillors.</summary>
    [Serializable]
    public struct ValuePriority
    {
        public ValueMeterDefinition meter;
        [UnityEngine.Range(-2f, 2f)] public float weight;
    }

    /// <summary>One building moving to a new state as a result of a decision.</summary>
    [Serializable]
    public struct CityChange
    {
        public BuildingId building;
        public BuildingStateId state;
        [Tooltip("Optional: a designer-made transition (Timeline, animation...) played instead of the default.")]
        public GameObject customTransitionPrefab;
    }

    /// <summary>What a role's AI councillor votes on a policy in Fixed AI mode.</summary>
    [Serializable]
    public struct RoleVotePreference
    {
        public RoleDefinition role;
        public VoteChoice choice;
    }

    /// <summary>Text shown to the player whose role reaches the epilogue with these flags.</summary>
    [Serializable]
    public class RoleEnding
    {
        [Tooltip("All of these flags must have been set by outcomes during the game. Leave empty for the default ending.")]
        public string[] requiredFlags = Array.Empty<string>();
        [TextArea(2, 6)] public string text;
    }
}
