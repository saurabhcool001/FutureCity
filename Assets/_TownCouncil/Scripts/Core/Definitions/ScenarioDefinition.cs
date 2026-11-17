using System.Collections.Generic;
using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>
    /// The whole story: roles, rounds in order, values tracked and the welcome pages.
    /// Swap this asset to run a different town or a different set of dilemmas.
    /// </summary>
    [CreateAssetMenu(menuName = "Town Council/Scenario", fileName = "Scenario_")]
    public class ScenarioDefinition : ScriptableObject
    {
        public string townName = "Newhaven";
        public int startYear = 2045;

        [Header("Welcome sequence (headset on, before joining)")]
        public List<IntroPage> introPages = new();

        [Header("Content")]
        [Tooltip("One role per seat. The number of roles sets how many seats the council has.")]
        public List<RoleDefinition> roles = new();
        public List<RoundDefinition> rounds = new();
        public List<ValueMeterDefinition> meters = new();
        public List<BuildingId> buildings = new();

        public int SeatCount => roles?.Count ?? 0;

        public RoleDefinition GetRole(int index) =>
            roles != null && index >= 0 && index < roles.Count ? roles[index] : null;

        public int IndexOf(RoleDefinition role) => roles?.IndexOf(role) ?? -1;

        public int TotalPolicyCount
        {
            get
            {
                int n = 0;
                if (rounds != null)
                    foreach (var r in rounds) n += r != null ? r.PolicyCount : 0;
                return n;
            }
        }
    }
}
