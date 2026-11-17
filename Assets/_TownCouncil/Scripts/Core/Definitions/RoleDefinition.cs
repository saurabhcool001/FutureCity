using System.Collections.Generic;
using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>
    /// A role card: public identity, hidden goal, secret, personal room and AI personality.
    /// </summary>
    [CreateAssetMenu(menuName = "Town Council/Role", fileName = "Role_")]
    public class RoleDefinition : ScriptableObject
    {
        [Header("Public (everyone sees this)")]
        public string displayName = "Name";
        public int age = 30;
        public string job = "Job";
        public Sprite portrait;
        public Color accentColor = Color.white;

        [Header("Private (only this player sees this)")]
        [TextArea(2, 4)] public string hiddenGoal;
        [TextArea(2, 4)] public string secret;

        [Header("Personal room (shown after the card is picked)")]
        [Tooltip("Environment prefab placed in this role's room. Leave empty to use the generated placeholder room.")]
        public GameObject roomEnvironmentPrefab;
        [Tooltip("Short scene-setting line shown in the room, e.g. 'Your evening shift at A&E is ending.'")]
        [TextArea(2, 4)] public string roomIntro;

        [Header("AI councillor (used when an AI plays this role)")]
        [Tooltip("Dynamic AI: how much this role cares about each value. Positive = wants it to go up.")]
        public List<ValuePriority> valuePriorities = new();
        [Tooltip("Dynamic AI: 0 = never moved by the humans' votes, 1 = always follows the majority.")]
        [Range(0f, 1f)] public float persuadability = 0.3f;
        [Tooltip("Used by both AI modes when a policy gives no preference for this role.")]
        public VoteChoice defaultStance = VoteChoice.Agree;

        [Header("Epilogue")]
        public List<RoleEnding> endings = new();

        public string CardTitle => age > 0 ? $"{displayName}, {age}" : displayName;
    }
}
