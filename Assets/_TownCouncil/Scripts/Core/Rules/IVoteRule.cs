using System.Collections.Generic;

namespace TownCouncil.Core
{
    /// <summary>Turns the votes on one policy into a result. Add a class to add a new rule.</summary>
    public interface IVoteRule
    {
        VoteResult Resolve(IReadOnlyList<VoteChoice> votes, int seatCount);
    }

    /// <summary>Picks the rule a policy asks for.</summary>
    public static class VoteRules
    {
        static readonly IVoteRule s_Plurality = new PluralityCivicTieBreak();
        static readonly IVoteRule s_Majority = new MajorityToOverride();

        public static IVoteRule For(PolicyDefinition policy) =>
            policy != null && policy.requiresMajorityToOverride ? s_Majority : s_Plurality;

        internal static void Count(IReadOnlyList<VoteChoice> votes, int seatCount,
            out int agree, out int disagree, out int amend, out int abstain)
        {
            agree = disagree = amend = 0;
            int cast = 0;
            if (votes != null)
            {
                foreach (var v in votes)
                {
                    switch (v)
                    {
                        case VoteChoice.Agree: agree++; cast++; break;
                        case VoteChoice.Disagree: disagree++; cast++; break;
                        case VoteChoice.Amend: amend++; cast++; break;
                    }
                }
            }
            abstain = System.Math.Max(0, seatCount - cast);
        }
    }

    /// <summary>
    /// Default rule: most votes wins. A tie for the top, or no votes at all,
    /// means the council deadlocked and CIVIC proceeds with its own plan (Agree).
    /// </summary>
    public sealed class PluralityCivicTieBreak : IVoteRule
    {
        public VoteResult Resolve(IReadOnlyList<VoteChoice> votes, int seatCount)
        {
            VoteRules.Count(votes, seatCount, out var a, out var d, out var m, out var abs);
            int top = System.Math.Max(a, System.Math.Max(d, m));
            int leaders = (a == top ? 1 : 0) + (d == top ? 1 : 0) + (m == top ? 1 : 0);

            if (top == 0 || leaders > 1)
                return new VoteResult(VoteChoice.Agree, true, a, d, m, abs);

            var choice = a == top ? VoteChoice.Agree : d == top ? VoteChoice.Disagree : VoteChoice.Amend;
            return new VoteResult(choice, false, a, d, m, abs);
        }
    }

    /// <summary>
    /// "Who decides?" rule: Disagree only wins with a strict majority of all seats (3 of 4).
    /// Amend wins with a strict majority too. Anything else: CIVIC proceeds.
    /// </summary>
    public sealed class MajorityToOverride : IVoteRule
    {
        public VoteResult Resolve(IReadOnlyList<VoteChoice> votes, int seatCount)
        {
            VoteRules.Count(votes, seatCount, out var a, out var d, out var m, out var abs);
            int needed = seatCount / 2 + 1;
            if (d >= needed) return new VoteResult(VoteChoice.Disagree, false, a, d, m, abs);
            if (m >= needed) return new VoteResult(VoteChoice.Amend, false, a, d, m, abs);
            bool deadlock = a < needed;
            return new VoteResult(VoteChoice.Agree, deadlock, a, d, m, abs);
        }
    }
}
