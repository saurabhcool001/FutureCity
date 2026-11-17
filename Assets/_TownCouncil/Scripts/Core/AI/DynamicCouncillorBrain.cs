using System;

namespace TownCouncil.Core.AI
{
    /// <summary>
    /// Scores each choice from the role's value priorities and the outcome's meter changes,
    /// leans toward the humans' majority by the role's persuadability, and adds a little noise
    /// so two playthroughs don't feel identical.
    /// </summary>
    public sealed class DynamicCouncillorBrain : ICouncillorBrain
    {
        const float k_Noise = 0.35f;
        const float k_MajorityPull = 2f;
        const float k_PreferenceBonus = 1.5f;

        readonly Random m_Random;

        public RoleDefinition Role { get; }

        public DynamicCouncillorBrain(RoleDefinition role, int seed)
        {
            Role = role;
            m_Random = new Random(seed);
        }

        public VoteChoice DecideVote(PolicyDefinition policy, CouncillorContext context)
        {
            if (policy == null || Role == null) return VoteChoice.Agree;

            var best = VoteChoice.Agree;
            float bestScore = float.NegativeInfinity;
            foreach (var choice in new[] { VoteChoice.Agree, VoteChoice.Disagree, VoteChoice.Amend })
            {
                var outcome = policy.OutcomeFor(choice);
                if (outcome == null) continue;

                float score = ValueScore(outcome)
                              + MajorityScore(choice, context)
                              + PreferenceScore(policy, choice)
                              + (float)(m_Random.NextDouble() * 2 - 1) * k_Noise;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = choice;
                }
            }
            return best;
        }

        float ValueScore(PolicyOutcome outcome)
        {
            float score = 0f;
            foreach (var priority in Role.valuePriorities)
                if (priority.meter != null)
                    score += priority.weight * outcome.DeltaFor(priority.meter);
            return score;
        }

        float MajorityScore(VoteChoice choice, CouncillorContext context)
        {
            if (context?.OtherVotes == null || Role.persuadability <= 0f) return 0f;
            int same = 0, cast = 0;
            foreach (var v in context.OtherVotes)
            {
                if (v == VoteChoice.None) continue;
                cast++;
                if (v == choice) same++;
            }
            return cast == 0 ? 0f : Role.persuadability * k_MajorityPull * same / cast;
        }

        float PreferenceScore(PolicyDefinition policy, VoteChoice choice) =>
            policy.TryGetPreference(Role, out var preferred) && preferred == choice ? k_PreferenceBonus : 0f;
    }
}
