using System;
using System.Collections.Generic;
using TownCouncil.Core;
using TownCouncil.Core.AI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;

namespace TownCouncil.Networking
{
    /// <summary>
    /// The council's shared state: who sits where, which role card each seat holds, the lobby
    /// countdown and AI councillors. One instance lives in the game scene.
    ///
    /// Authority: all rules run on whoever holds authority over this object (the host in a
    /// host/Relay session, the session owner in a Distributed Authority session). Everyone else
    /// reads the replicated state and sends requests through RPCs. Views never change state.
    /// </summary>
    [DisallowMultipleComponent]
    public class CouncilSession : NetworkBehaviour
    {
        [SerializeField] GameConfig m_Config;

        // Replicated state ---------------------------------------------------------------
        // Created in Awake: NGO warns about native-memory leaks for lists made in field initializers.
        NetworkList<SeatState> m_Seats;

        readonly NetworkVariable<CouncilPhase> m_Phase = new(CouncilPhase.Gathering,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Server time at which empty seats become AI. Negative = no countdown.</summary>
        readonly NetworkVariable<double> m_AIFillAt = new(-1d,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        readonly Dictionary<int, ICouncillorBrain> m_Brains = new();

        // Public API ---------------------------------------------------------------------
        public static CouncilSession Instance { get; private set; }

        /// <summary>Raised on every client when this session spawns or despawns on the network.</summary>
        public static event Action<CouncilSession> Spawned;
        public static event Action<CouncilSession> Despawned;

        /// <summary>Any seat changed (someone joined, picked a card, sat down, left...).</summary>
        public event Action SeatsChanged;
        public event Action<CouncilPhase> PhaseChanged;

        public GameConfig Config => m_Config;
        public ScenarioDefinition Scenario => m_Config != null ? m_Config.scenario : null;
        public CouncilPhase Phase => m_Phase.Value;
        public int SeatCount => m_Seats != null ? m_Seats.Count : 0;

        public SeatState GetSeat(int index) => m_Seats[index];

        public RoleDefinition GetRole(int roleIndex) => Scenario != null ? Scenario.GetRole(roleIndex) : null;

        public int RoleCount => Scenario != null ? Scenario.roles.Count : m_Seats.Count;
        public int HumanCount => Count(s => s.IsHuman);
        public int AICount => Count(s => s.IsAI);

        /// <summary>Seconds left before AI fills the empty seats, or -1 when no countdown is running.</summary>
        public float SecondsUntilAIFill
        {
            get
            {
                if (!IsSpawned || m_AIFillAt.Value < 0d || Phase != CouncilPhase.Gathering) return -1f;
                return Mathf.Max(0f, (float)(m_AIFillAt.Value - NetworkManager.ServerTime.Time));
            }
        }

        /// <summary>Index of the seat held by this headset, or -1 (not registered yet, or watching).</summary>
        public int LocalSeatIndex
        {
            get
            {
                if (!IsSpawned) return -1;
                var localId = NetworkManager.LocalClientId;
                for (int i = 0; i < m_Seats.Count; i++)
                    if (m_Seats[i].IsHuman && m_Seats[i].ClientId == localId) return i;
                return -1;
            }
        }

        public bool TryGetLocalSeat(out SeatState seat)
        {
            int i = LocalSeatIndex;
            seat = i >= 0 ? m_Seats[i] : SeatState.Empty;
            return i >= 0;
        }

        /// <summary>The seat holding a role, or -1.</summary>
        public int SeatOfRole(int roleIndex)
        {
            for (int i = 0; i < m_Seats.Count; i++)
                if (m_Seats[i].RoleIndex == roleIndex) return i;
            return -1;
        }

        public bool IsRoleTaken(int roleIndex) => SeatOfRole(roleIndex) >= 0;

        /// <summary>The AI brain for an AI seat (created on demand), or null for human/empty seats.</summary>
        public ICouncillorBrain GetBrain(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= m_Seats.Count) return null;
            var seat = m_Seats[seatIndex];
            if (!seat.IsAI || !seat.HasRole) return null;

            if (!m_Brains.TryGetValue(seatIndex, out var brain) || brain.Role != GetRole(seat.RoleIndex))
            {
                var mode = m_Config != null ? m_Config.aiMode : AIMode.Fixed;
                brain = CouncillorBrainFactory.Create(mode, GetRole(seat.RoleIndex), seatIndex * 7919 + seat.RoleIndex);
                m_Brains[seatIndex] = brain;
            }
            return brain;
        }

        // Requests from this headset -------------------------------------------------------

        /// <summary>Ask to take a role card. The authority accepts it if the card is still free.</summary>
        public void RequestRole(int roleIndex) => ClaimRoleRpc(roleIndex);

        /// <summary>Tell everyone how far this player has got (role room, seated...).</summary>
        public void ReportStage(SeatStage stage) => ReportStageRpc(stage);

        /// <summary>Show this player's secret to the whole council. Can't be undone.</summary>
        public void RevealSecret() => RevealSecretRpc();

        /// <summary>Seat held by a client, or -1.</summary>
        public int SeatOfClientId(ulong clientId) => SeatOfClient(clientId);

        /// <summary>Authority only: reveal a seat's secret (used for AI councillors).</summary>
        internal void RevealSeatSecret(int seatIndex)
        {
            if (!HasAuthority || seatIndex < 0 || seatIndex >= m_Seats.Count) return;
            var seat = m_Seats[seatIndex];
            if (!seat.HasRole || seat.SecretRevealed) return;
            seat.SecretRevealed = true;
            m_Seats[seatIndex] = seat;
        }

        /// <summary>Debug/teacher: stop waiting and fill empty seats with AI now.</summary>
        public void RequestAIFillNow() => ForceAIFillRpc();

        // Lifecycle --------------------------------------------------------------------------

        void Awake()
        {
            m_Seats = new NetworkList<SeatState>(null,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

            if (Instance != null && Instance != this)
                Debug.LogWarning("[Town Council] More than one CouncilSession in the scene; using the newest.", this);
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            m_Seats.OnListChanged += OnSeatsListChanged;
            m_Phase.OnValueChanged += OnPhaseValueChanged;
            NetworkManager.OnConnectionEvent += OnConnectionEvent;

            if (HasAuthority) EnsureSeats();

            RegisterHumanRpc(new FixedString64Bytes(LocalPlayerName()));
            Spawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            m_Seats.OnListChanged -= OnSeatsListChanged;
            m_Phase.OnValueChanged -= OnPhaseValueChanged;
            if (NetworkManager != null) NetworkManager.OnConnectionEvent -= OnConnectionEvent;
            m_Brains.Clear();
            Despawned?.Invoke(this);
        }

        public override void OnGainedOwnership()
        {
            // Distributed Authority: the session owner left and we inherited the rules.
            if (HasAuthority) EnsureSeats();
        }

        void OnSeatsListChanged(NetworkListEvent<SeatState> _) => SeatsChanged?.Invoke();
        void OnPhaseValueChanged(CouncilPhase _, CouncilPhase value) => PhaseChanged?.Invoke(value);

        static string LocalPlayerName()
        {
            var name = XRINetworkGameManager.LocalPlayerName.Value;
            if (string.IsNullOrWhiteSpace(name)) name = "Player";
            return name.Length > 28 ? name.Substring(0, 28) : name;
        }

        // Authority rules ----------------------------------------------------------------------

        void Update()
        {
            if (!IsSpawned || !HasAuthority) return;

            switch (m_Phase.Value)
            {
                case CouncilPhase.Gathering:
                    TickGathering();
                    break;
                case CouncilPhase.Locked:
                    TickLocked();
                    break;
            }
        }

        void TickGathering()
        {
            int humans = HumanCount;
            if (humans <= 0) return;

            if (humans >= m_Seats.Count)
            {
                LockCouncil("every seat has a person");
                return;
            }

            int minHumans = m_Config != null ? m_Config.minHumansToStart : 1;
            if (m_AIFillAt.Value >= 0d && NetworkManager.ServerTime.Time >= m_AIFillAt.Value && humans >= minHumans)
                LockCouncil("countdown finished");
        }

        void TickLocked()
        {
            // AI picks only once every person has chosen, so nobody is left without a choice.
            if (AllHumansHaveRoles()) AssignAIRoles();

            for (int i = 0; i < m_Seats.Count; i++)
            {
                var s = m_Seats[i];
                if (s.IsEmpty || !s.HasRole || s.Stage != SeatStage.Seated) return;
            }

            m_Phase.Value = CouncilPhase.Complete;
            Debug.Log("[Town Council] Council complete: every seat is filled and seated.");
        }

        void EnsureSeats()
        {
            int seatCount = m_Config != null ? Mathf.Max(1, m_Config.SeatCount) : 4;
            if (m_Seats.Count == seatCount) return;

            m_Seats.Clear();
            for (int i = 0; i < seatCount; i++) m_Seats.Add(SeatState.Empty);
        }

        void LockCouncil(string reason)
        {
            for (int i = 0; i < m_Seats.Count; i++)
            {
                if (!m_Seats[i].IsEmpty) continue;
                var ai = SeatState.Empty;
                ai.Occupant = SeatOccupant.AI;
                ai.Name = new FixedString64Bytes("AI councillor");
                m_Seats[i] = ai;
            }

            m_AIFillAt.Value = -1d;
            m_Phase.Value = CouncilPhase.Locked;
            Debug.Log($"[Town Council] Council locked ({reason}): {HumanCount} people, {AICount} AI.");
        }

        bool AllHumansHaveRoles()
        {
            for (int i = 0; i < m_Seats.Count; i++)
                if (m_Seats[i].IsHuman && !m_Seats[i].HasRole) return false;
            return true;
        }

        void AssignAIRoles()
        {
            for (int i = 0; i < m_Seats.Count; i++)
            {
                var seat = m_Seats[i];
                if (!seat.IsAI || seat.HasRole) continue;

                int role = PickFreeRole();
                if (role < 0) return;

                seat.RoleIndex = role;
                seat.Stage = SeatStage.Seated;
                seat.Name = new FixedString64Bytes(AIName(role));
                m_Seats[i] = seat;
            }
        }

        int PickFreeRole()
        {
            var free = new List<int>();
            for (int r = 0; r < RoleCount; r++)
                if (!IsRoleTaken(r)) free.Add(r);
            return free.Count == 0 ? -1 : free[UnityEngine.Random.Range(0, free.Count)];
        }

        string AIName(int roleIndex)
        {
            var role = GetRole(roleIndex);
            return role != null ? $"{role.displayName} (AI)" : "AI councillor";
        }

        int SeatOfClient(ulong clientId)
        {
            for (int i = 0; i < m_Seats.Count; i++)
                if (m_Seats[i].IsHuman && m_Seats[i].ClientId == clientId) return i;
            return -1;
        }

        int Count(Predicate<SeatState> match)
        {
            int n = 0;
            for (int i = 0; i < m_Seats.Count; i++)
                if (match(m_Seats[i])) n++;
            return n;
        }

        void RestartCountdown()
        {
            float wait = m_Config != null ? m_Config.lobbyWaitSeconds : 60f;
            m_AIFillAt.Value = NetworkManager.ServerTime.Time + wait;
        }

        void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (!IsSpawned || !HasAuthority) return;
            if (data.EventType != ConnectionEvent.ClientDisconnected && data.EventType != ConnectionEvent.PeerDisconnected)
                return;
            if (data.ClientId == NetworkManager.LocalClientId) return;

            int i = SeatOfClient(data.ClientId);
            if (i < 0) return;

            if (m_Phase.Value == CouncilPhase.Gathering)
            {
                m_Seats[i] = SeatState.Empty;
                if (HumanCount == 0) m_AIFillAt.Value = -1d;
            }
            else
            {
                // Mid-game: an AI takes over the player's role so the council can carry on.
                var seat = m_Seats[i];
                seat.Occupant = SeatOccupant.AI;
                seat.ClientId = SeatState.NoClient;
                if (seat.HasRole)
                {
                    seat.Stage = SeatStage.Seated;
                    seat.Name = new FixedString64Bytes(AIName(seat.RoleIndex));
                }
                m_Seats[i] = seat;
                m_Brains.Remove(i);
            }
            Debug.Log($"[Town Council] Client {data.ClientId} left seat {i + 1}.");
        }

        // RPCs (run on the authority) ---------------------------------------------------------

        [Rpc(SendTo.Authority)]
        void RegisterHumanRpc(FixedString64Bytes playerName, RpcParams rpcParams = default)
        {
            EnsureSeats();
            ulong sender = rpcParams.Receive.SenderClientId;

            int existing = SeatOfClient(sender);
            if (existing >= 0)
            {
                var seat = m_Seats[existing];
                seat.Name = playerName;
                m_Seats[existing] = seat;
                return;
            }

            if (m_Phase.Value != CouncilPhase.Gathering)
            {
                Debug.Log($"[Town Council] Client {sender} joined after the council locked; watching only.");
                return;
            }

            for (int i = 0; i < m_Seats.Count; i++)
            {
                if (!m_Seats[i].IsEmpty) continue;

                var seat = SeatState.Empty;
                seat.Occupant = SeatOccupant.Human;
                seat.ClientId = sender;
                seat.Name = playerName;
                m_Seats[i] = seat;

                bool first = HumanCount == 1;
                if (first || m_AIFillAt.Value < 0d || (m_Config != null && m_Config.restartCountdownOnJoin))
                    RestartCountdown();

                Debug.Log($"[Town Council] {playerName} took seat {i + 1}.");
                return;
            }
        }

        [Rpc(SendTo.Authority)]
        void ClaimRoleRpc(int roleIndex, RpcParams rpcParams = default)
        {
            int seatIndex = SeatOfClient(rpcParams.Receive.SenderClientId);
            if (seatIndex < 0) return;

            var seat = m_Seats[seatIndex];
            if (seat.HasRole || roleIndex < 0 || roleIndex >= RoleCount || IsRoleTaken(roleIndex)) return;

            seat.RoleIndex = roleIndex;
            seat.Stage = SeatStage.RoleChosen;
            m_Seats[seatIndex] = seat;
        }

        [Rpc(SendTo.Authority)]
        void ReportStageRpc(SeatStage stage, RpcParams rpcParams = default)
        {
            int seatIndex = SeatOfClient(rpcParams.Receive.SenderClientId);
            if (seatIndex < 0) return;

            var seat = m_Seats[seatIndex];
            if (!seat.HasRole || stage <= seat.Stage) return;

            seat.Stage = stage;
            m_Seats[seatIndex] = seat;
        }

        [Rpc(SendTo.Authority)]
        void RevealSecretRpc(RpcParams rpcParams = default)
        {
            RevealSeatSecret(SeatOfClient(rpcParams.Receive.SenderClientId));
        }

        [Rpc(SendTo.Authority)]
        void ForceAIFillRpc()
        {
            if (m_Phase.Value == CouncilPhase.Gathering && HumanCount > 0)
                LockCouncil("requested from debug tools");
        }
    }
}
