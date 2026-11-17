using System.Collections.Generic;

namespace TownCouncil.Core.AI
{
    /// <summary>What an AI councillor knows when it decides.</summary>
    public sealed class CouncillorContext
    {
        /// <summary>Votes already cast by the other seats on this policy (None = not voted yet).</summary>
        public IReadOnlyList<VoteChoice> OtherVotes;
        /// <summary>Current value of each meter, if tracked.</summary>
        public IReadOnlyDictionary<ValueMeterDefinition, int> MeterValues;
    }

    /// <summary>
    /// Decides how an AI councillor votes. New AI behaviours are new implementations;
    /// the seat and vote code never changes.
    /// </summary>
    public interface ICouncillorBrain
    {
        RoleDefinition Role { get; }
        VoteChoice DecideVote(PolicyDefinition policy, CouncillorContext context);
    }

    public static class CouncillorBrainFactory
    {
        public static ICouncillorBrain Create(AIMode mode, RoleDefinition role, int seed) => mode switch
        {
            AIMode.Dynamic => new DynamicCouncillorBrain(role, seed),
            _ => new FixedCouncillorBrain(role)
        };
    }
}
