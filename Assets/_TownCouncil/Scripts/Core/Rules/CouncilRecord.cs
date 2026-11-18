using System.Collections.Generic;
using System.Globalization;

namespace TownCouncil.Core
{
    /// <summary>The decision on one policy, as recorded in the game history.</summary>
    public readonly struct PolicyResult
    {
        public readonly int RoundIndex;
        public readonly int PolicyIndex;
        public readonly VoteChoice Choice;
        public readonly bool Deadlock;

        public PolicyResult(int roundIndex, int policyIndex, VoteChoice choice, bool deadlock)
        {
            RoundIndex = roundIndex;
            PolicyIndex = policyIndex;
            Choice = choice;
            Deadlock = deadlock;
        }

        public bool MatchedCivic => Deadlock || Choice == VoteChoice.Agree;
    }

    /// <summary>
    /// Everything that follows from the decisions so far: value meters, the city's buildings,
    /// ending flags and CIVIC's statistics. Built from the scenario plus the list of results,
    /// so every headset derives exactly the same town from the same history.
    /// </summary>
    public sealed class CouncilRecord
    {
        readonly ScenarioDefinition m_Scenario;
        readonly List<PolicyResult> m_Results = new();
        readonly Dictionary<ValueMeterDefinition, int> m_Meters = new();
        readonly Dictionary<BuildingId, BuildingStateId> m_City = new();
        readonly HashSet<string> m_Flags = new();

        public CouncilRecord(ScenarioDefinition scenario, IEnumerable<PolicyResult> results = null)
        {
            m_Scenario = scenario;
            Reset();
            if (results != null)
                foreach (var r in results) Add(r);
        }

        public ScenarioDefinition Scenario => m_Scenario;
        public IReadOnlyList<PolicyResult> Results => m_Results;
        public IReadOnlyDictionary<ValueMeterDefinition, int> Meters => m_Meters;
        public IReadOnlyDictionary<BuildingId, BuildingStateId> City => m_City;
        public IReadOnlyCollection<string> Flags => m_Flags;

        public int Deadlocks
        {
            get
            {
                int n = 0;
                foreach (var r in m_Results) if (r.Deadlock) n++;
                return n;
            }
        }

        /// <summary>How often the council ended up doing what CIVIC recommended, 0-100.</summary>
        public int AgreementPercent
        {
            get
            {
                if (m_Results.Count == 0) return 0;
                int matched = 0;
                foreach (var r in m_Results) if (r.MatchedCivic) matched++;
                return (int)System.Math.Round(100.0 * matched / m_Results.Count);
            }
        }

        public void Add(PolicyResult result)
        {
            m_Results.Add(result);
            var outcome = GetPolicy(result.RoundIndex, result.PolicyIndex)?.OutcomeFor(result.Choice);
            if (outcome == null) return;

            foreach (var d in outcome.meterDeltas)
            {
                if (d.meter == null) continue;
                int current = m_Meters.TryGetValue(d.meter, out var v) ? v : d.meter.startValue;
                m_Meters[d.meter] = System.Math.Clamp(current + d.amount, d.meter.minValue, d.meter.maxValue);
            }
            foreach (var c in outcome.cityChanges)
                if (c.building != null) m_City[c.building] = c.state;
            foreach (var f in outcome.endingFlags)
                if (!string.IsNullOrEmpty(f)) m_Flags.Add(f);
        }

        public PolicyDefinition GetPolicy(int roundIndex, int policyIndex)
        {
            if (m_Scenario == null || roundIndex < 0 || roundIndex >= m_Scenario.rounds.Count) return null;
            var round = m_Scenario.rounds[roundIndex];
            if (round == null || policyIndex < 0 || policyIndex >= round.policies.Count) return null;
            return round.policies[policyIndex];
        }

        public bool TryGetResult(int roundIndex, int policyIndex, out PolicyResult result)
        {
            foreach (var r in m_Results)
            {
                if (r.RoundIndex == roundIndex && r.PolicyIndex == policyIndex)
                {
                    result = r;
                    return true;
                }
            }
            result = default;
            return false;
        }

        /// <summary>Meter value scaled to 0-1 between its min and max.</summary>
        public float Normalized(ValueMeterDefinition meter)
        {
            if (meter == null) return 0f;
            int value = m_Meters.TryGetValue(meter, out var v) ? v : meter.startValue;
            float range = meter.maxValue - meter.minValue;
            return range <= 0f ? 0f : (value - meter.minValue) / range;
        }

        public int ValueOf(ValueMeterDefinition meter) =>
            meter == null ? 0 : m_Meters.TryGetValue(meter, out var v) ? v : meter.startValue;

        /// <summary>The role's ending whose required flags all happened (most specific first), else the default.</summary>
        public string EndingFor(RoleDefinition role)
        {
            if (role == null) return string.Empty;
            RoleEnding best = null;
            int bestCount = -1;
            foreach (var ending in role.endings)
            {
                if (ending == null) continue;
                var required = ending.requiredFlags ?? System.Array.Empty<string>();
                bool ok = true;
                foreach (var f in required)
                    if (!string.IsNullOrEmpty(f) && !m_Flags.Contains(f)) { ok = false; break; }
                if (ok && required.Length > bestCount)
                {
                    best = ending;
                    bestCount = required.Length;
                }
            }
            return best != null ? best.text : string.Empty;
        }

        /// <summary>Fills {tokens} in CIVIC's lines: agreementPercent, deadlocks, town, decisions.</summary>
        public string ResolveToken(string token) => token switch
        {
            "agreementPercent" => AgreementPercent.ToString(CultureInfo.InvariantCulture),
            "deadlocks" => Deadlocks.ToString(CultureInfo.InvariantCulture),
            "decisions" => m_Results.Count.ToString(CultureInfo.InvariantCulture),
            "town" => m_Scenario != null ? m_Scenario.townName : "the town",
            _ => null
        };

        public string Format(string text) => TextTokens.Format(text, ResolveToken);

        void Reset()
        {
            m_Results.Clear();
            m_Meters.Clear();
            m_City.Clear();
            m_Flags.Clear();
            if (m_Scenario == null) return;
            foreach (var m in m_Scenario.meters)
                if (m != null) m_Meters[m] = m.startValue;
        }
    }
}
