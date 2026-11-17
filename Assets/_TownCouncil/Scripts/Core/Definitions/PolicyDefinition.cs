using System.Collections.Generic;
using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>
    /// One policy CIVIC puts in front of the council, with one outcome per possible decision.
    /// A round can hold any number of these.
    /// </summary>
    [CreateAssetMenu(menuName = "Town Council/Policy", fileName = "Policy_")]
    public class PolicyDefinition : ScriptableObject
    {
        public string title = "New policy";
        [TextArea(2, 5)] public string summary;
        [Tooltip("CIVIC's pitch, spoken when the policy is proposed.")]
        public DialogueLine civicPitch;
        [Tooltip("Buildings that light up on the city model while this policy is discussed.")]
        public List<BuildingId> affectedBuildings = new();

        [Header("Decision")]
        [Tooltip("Seconds the council has to decide. 0 = use the default from Game Config.")]
        [Min(0f)] public float decisionSeconds = 0f;
        [Tooltip("When ticked, Disagree needs a strict majority of all seats or CIVIC proceeds (Round 4 style).")]
        public bool requiresMajorityToOverride;
        [Tooltip("The amended version of the policy, shown on the Amend slot.")]
        [TextArea(2, 4)] public string amendWording;

        [Header("Outcomes")]
        public PolicyOutcome onAgree;
        public PolicyOutcome onDisagree;
        public PolicyOutcome onAmend;

        [Header("AI councillors (Fixed mode)")]
        public List<RoleVotePreference> aiPreferences = new();

        /// <summary>The outcome for a resolved choice. None (no votes) counts as CIVIC proceeding.</summary>
        public PolicyOutcome OutcomeFor(VoteChoice choice) => choice switch
        {
            VoteChoice.Disagree => onDisagree,
            VoteChoice.Amend => onAmend,
            _ => onAgree
        };

        public bool TryGetPreference(RoleDefinition role, out VoteChoice choice)
        {
            foreach (var p in aiPreferences)
            {
                if (p.role == role && p.choice != VoteChoice.None)
                {
                    choice = p.choice;
                    return true;
                }
            }
            choice = VoteChoice.None;
            return false;
        }

        public float GetDecisionSeconds(GameConfig config) =>
            decisionSeconds > 0f ? decisionSeconds : (config != null ? config.defaultDecisionSeconds : 60f);
    }
}
