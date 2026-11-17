using System.Collections.Generic;
using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>
    /// What happens when a policy resolves one way: value changes, city changes, headline, diary and ending flags.
    /// Outcomes only describe what happens; the City and UI modules decide how to show it.
    /// </summary>
    [CreateAssetMenu(menuName = "Town Council/Policy Outcome", fileName = "Outcome_")]
    public class PolicyOutcome : ScriptableObject
    {
        public List<MeterDelta> meterDeltas = new();
        public List<CityChange> cityChanges = new();
        [TextArea(1, 3)] public string headline;

        [Header("Diary entry shown in the future view")]
        public string diaryAuthor;
        public string diaryYear;
        [TextArea(2, 5)] public string diaryText;

        [Tooltip("Flags remembered for role endings, e.g. 'library_closed'.")]
        public List<string> endingFlags = new();

        public int DeltaFor(ValueMeterDefinition meter)
        {
            int total = 0;
            foreach (var d in meterDeltas)
                if (d.meter == meter) total += d.amount;
            return total;
        }
    }
}
