using System;
using Unity.Collections;
using Unity.Netcode;

namespace TownCouncil.Networking
{
    public enum SeatOccupant : byte
    {
        Empty = 0,
        Human = 1,
        AI = 2
    }

    /// <summary>How far a seat's occupant has got through the pre-game flow. Only ever moves forward.</summary>
    public enum SeatStage : byte
    {
        Joined = 0,
        RoleChosen = 1,
        InRoleRoom = 2,
        Seated = 3
    }

    /// <summary>Lobby lifecycle.</summary>
    public enum CouncilPhase : byte
    {
        /// <summary>Waiting for people; the AI-fill countdown may be running.</summary>
        Gathering = 0,
        /// <summary>No more people can join. Empty seats are AI. Players finish picking and take their seats.</summary>
        Locked = 1,
        /// <summary>Every seat has a role and every councillor is seated. The game can start.</summary>
        Complete = 2
    }

    /// <summary>One seat at the council table, replicated to everyone.</summary>
    public struct SeatState : INetworkSerializable, IEquatable<SeatState>
    {
        public const ulong NoClient = ulong.MaxValue;

        public SeatOccupant Occupant;
        public ulong ClientId;
        public int RoleIndex;
        public SeatStage Stage;
        public FixedString64Bytes Name;
        /// <summary>The occupant has shown their secret to the council.</summary>
        public bool SecretRevealed;

        public static SeatState Empty => new SeatState
        {
            Occupant = SeatOccupant.Empty,
            ClientId = NoClient,
            RoleIndex = -1,
            Stage = SeatStage.Joined,
            Name = default,
            SecretRevealed = false
        };

        public bool IsEmpty => Occupant == SeatOccupant.Empty;
        public bool IsHuman => Occupant == SeatOccupant.Human;
        public bool IsAI => Occupant == SeatOccupant.AI;
        public bool HasRole => RoleIndex >= 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Occupant);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref RoleIndex);
            serializer.SerializeValue(ref Stage);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref SecretRevealed);
        }

        public bool Equals(SeatState other) =>
            Occupant == other.Occupant && ClientId == other.ClientId && RoleIndex == other.RoleIndex &&
            Stage == other.Stage && Name.Equals(other.Name) && SecretRevealed == other.SecretRevealed;

        public override bool Equals(object obj) => obj is SeatState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Occupant, ClientId, RoleIndex, (int)Stage, SecretRevealed);
    }
}
