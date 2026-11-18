using System;
using TownCouncil.Core;
using Unity.Netcode;

namespace TownCouncil.Networking
{
    /// <summary>Where the council is in the game. Phases only run while the council is complete.</summary>
    public enum GamePhase : byte
    {
        /// <summary>Waiting for every seat to be filled and seated.</summary>
        NotStarted = 0,
        /// <summary>CIVIC wakes up and greets the council.</summary>
        WakingUp = 1,
        /// <summary>The year and round title; CIVIC's introduction.</summary>
        RoundIntro = 2,
        /// <summary>CIVIC pitches the policy (or all of the round's policies); buildings light up.</summary>
        Proposal = 3,
        /// <summary>Timer running: councillors debate and drop their tokens.</summary>
        Deciding = 4,
        /// <summary>Votes are counted and shown; CIVIC reacts.</summary>
        Resolving = 5,
        /// <summary>Time jump: the city model shows the future, with headlines and diaries.</summary>
        FutureView = 6,
        /// <summary>The town's fingerprint, every secret and each role's ending.</summary>
        Epilogue = 7
    }

    /// <summary>One seat's vote on one policy of the current round.</summary>
    public struct VoteEntry : INetworkSerializable, IEquatable<VoteEntry>
    {
        public byte Seat;
        /// <summary>Index of the policy inside the current round.</summary>
        public byte PolicyIndex;
        public VoteChoice Choice;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seat);
            serializer.SerializeValue(ref PolicyIndex);
            serializer.SerializeValue(ref Choice);
        }

        public bool Equals(VoteEntry other) => Seat == other.Seat && PolicyIndex == other.PolicyIndex && Choice == other.Choice;
        public override bool Equals(object obj) => obj is VoteEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Seat, PolicyIndex, (byte)Choice);
    }

    /// <summary>A resolved policy in the game's history.</summary>
    public struct PolicyResultEntry : INetworkSerializable, IEquatable<PolicyResultEntry>
    {
        public byte Round;
        public byte Policy;
        public VoteChoice Choice;
        public bool Deadlock;
        public byte Agree;
        public byte Disagree;
        public byte Amend;
        public byte Abstain;

        public PolicyResult ToResult() => new(Round, Policy, Choice, Deadlock);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Round);
            serializer.SerializeValue(ref Policy);
            serializer.SerializeValue(ref Choice);
            serializer.SerializeValue(ref Deadlock);
            serializer.SerializeValue(ref Agree);
            serializer.SerializeValue(ref Disagree);
            serializer.SerializeValue(ref Amend);
            serializer.SerializeValue(ref Abstain);
        }

        public bool Equals(PolicyResultEntry o) =>
            Round == o.Round && Policy == o.Policy && Choice == o.Choice && Deadlock == o.Deadlock &&
            Agree == o.Agree && Disagree == o.Disagree && Amend == o.Amend && Abstain == o.Abstain;

        public override bool Equals(object obj) => obj is PolicyResultEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Round, Policy, (byte)Choice, Deadlock, Agree, Disagree, Amend);
    }
}
