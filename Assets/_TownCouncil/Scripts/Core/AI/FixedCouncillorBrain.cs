namespace TownCouncil.Core.AI
{
    /// <summary>
    /// Votes exactly as the designer wrote on the policy for this role, else the role's default stance.
    /// Predictable: good for demos, the expo and testing a specific story path.
    /// </summary>
    public sealed class FixedCouncillorBrain : ICouncillorBrain
    {
        public RoleDefinition Role { get; }

        public FixedCouncillorBrain(RoleDefinition role) => Role = role;

        public VoteChoice DecideVote(PolicyDefinition policy, CouncillorContext context)
        {
            if (policy != null && policy.TryGetPreference(Role, out var choice))
                return choice;
            return Role != null && Role.defaultStance != VoteChoice.None ? Role.defaultStance : VoteChoice.Agree;
        }
    }
}
