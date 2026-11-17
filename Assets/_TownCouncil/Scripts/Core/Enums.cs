namespace TownCouncil.Core
{
    /// <summary>A councillor's choice on one policy.</summary>
    public enum VoteChoice : byte
    {
        None = 0,
        Agree = 1,
        Disagree = 2,
        Amend = 3
    }

    /// <summary>How the policies inside one round are presented.</summary>
    public enum PolicyPresentation : byte
    {
        /// <summary>One policy at a time, each with its own decision window.</summary>
        OneAfterAnother = 0,
        /// <summary>All policies of the round on the table at once, one shared timer.</summary>
        AllAtOnce = 1
    }

    /// <summary>How AI councillors decide when they fill empty seats.</summary>
    public enum AIMode : byte
    {
        /// <summary>Votes come straight from each policy's per-role preference (predictable, good for demos).</summary>
        Fixed = 0,
        /// <summary>Votes are scored from the role's value priorities, the outcome's meter changes and the humans' votes.</summary>
        Dynamic = 1
    }
}
