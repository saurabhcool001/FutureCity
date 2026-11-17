namespace TownCouncil.Core
{
    /// <summary>The resolved decision on one policy.</summary>
    public readonly struct VoteResult
    {
        public readonly VoteChoice Choice;
        /// <summary>True when the council failed to decide and CIVIC went ahead on its own.</summary>
        public readonly bool Deadlock;
        public readonly int Agree, Disagree, Amend, Abstain;

        public VoteResult(VoteChoice choice, bool deadlock, int agree, int disagree, int amend, int abstain)
        {
            Choice = choice;
            Deadlock = deadlock;
            Agree = agree;
            Disagree = disagree;
            Amend = amend;
            Abstain = abstain;
        }

        /// <summary>Counts toward CIVIC's "your votes matched my recommendations" figure.</summary>
        public bool MatchedCivic => Deadlock || Choice == VoteChoice.Agree;

        public override string ToString() =>
            $"{Choice}{(Deadlock ? " (deadlock: CIVIC proceeds)" : "")} — Agree {Agree}, Disagree {Disagree}, Amend {Amend}, Abstain {Abstain}";
    }
}
