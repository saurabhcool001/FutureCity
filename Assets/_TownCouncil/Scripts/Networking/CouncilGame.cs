using System;
using System.Collections.Generic;
using TownCouncil.Core;
using TownCouncil.Core.AI;
using Unity.Netcode;
using UnityEngine;

namespace TownCouncil.Networking
{
    /// <summary>
    /// Runs the rounds once the council is complete:
    /// CIVIC wakes → for each round: intro → proposal → decision window → result
    /// (repeated per policy, or once for all of them, as the round asks) → future view → epilogue.
    ///
    /// Only the authority advances phases, counts votes and plays AI councillors. Every headset
    /// gets the same replicated phase, timer, votes and result history, and derives the town
    /// (meters, buildings, flags) from that history through <see cref="CouncilRecord"/>.
    /// Lives on the same NetworkObject as <see cref="CouncilSession"/>.
    /// </summary>
    [RequireComponent(typeof(CouncilSession))]
    public class CouncilGame : NetworkBehaviour
    {
        // Replicated state ---------------------------------------------------------------------
        readonly NetworkVariable<GamePhase> m_Phase = new(GamePhase.NotStarted,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<int> m_Round = new(-1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Policy being handled in one-after-another rounds; -1 = all of the round's policies.</summary>
        readonly NetworkVariable<int> m_Policy = new(-1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<double> m_PhaseEndsAt = new(-1d,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        NetworkList<VoteEntry> m_Votes;
        NetworkList<PolicyResultEntry> m_Results;

        // Authority-only scheduling ------------------------------------------------------------
        readonly Dictionary<(int seat, int policy), double> m_AIVoteAt = new();
        readonly Dictionary<int, double> m_AIRevealAt = new();
        bool m_ShortcutApplied;

        // Cached derived state -----------------------------------------------------------------
        CouncilSession m_Session;
        CouncilRecord m_Record;

        public static CouncilGame Instance { get; private set; }

        public event Action<GamePhase> PhaseChanged;
        public event Action VotesChanged;
        public event Action ResultsChanged;

        public CouncilSession Session => m_Session != null ? m_Session : m_Session = GetComponent<CouncilSession>();
        public GameConfig Config => Session.Config;
        public ScenarioDefinition Scenario => Session.Scenario;

        public GamePhase Phase => m_Phase.Value;
        public bool IsRunning => IsSpawned && m_Phase.Value != GamePhase.NotStarted;
        public int RoundIndex => m_Round.Value;
        public RoundDefinition CurrentRound =>
            Scenario != null && m_Round.Value >= 0 && m_Round.Value < Scenario.rounds.Count ? Scenario.rounds[m_Round.Value] : null;

        /// <summary>Indices (inside the current round) of the policies on the table right now. A fresh copy each call.</summary>
        public IReadOnlyList<int> ActivePolicies
        {
            get
            {
                var list = new List<int>();
                var round = CurrentRound;
                if (round == null) return list;
                if (m_Policy.Value >= 0)
                {
                    if (m_Policy.Value < round.policies.Count) list.Add(m_Policy.Value);
                }
                else
                {
                    for (int i = 0; i < round.policies.Count; i++) list.Add(i);
                }
                return list;
            }
        }

        public bool IsActive(int policyIndex)
        {
            foreach (var p in ActivePolicies) if (p == policyIndex) return true;
            return false;
        }

        public PolicyDefinition GetPolicy(int policyIndex)
        {
            var round = CurrentRound;
            return round != null && policyIndex >= 0 && policyIndex < round.policies.Count ? round.policies[policyIndex] : null;
        }

        /// <summary>Seconds left in the current phase, or -1 if it doesn't time out.</summary>
        public float SecondsLeft => !IsSpawned || m_PhaseEndsAt.Value < 0d
            ? -1f
            : Mathf.Max(0f, (float)(m_PhaseEndsAt.Value - NetworkManager.ServerTime.Time));

        /// <summary>Everything that follows from the decisions so far (meters, buildings, flags...).</summary>
        public CouncilRecord Record => m_Record ??= BuildRecord();

        public IReadOnlyList<PolicyResultEntry> ResultEntries
        {
            get
            {
                var list = new List<PolicyResultEntry>();
                if (m_Results != null) foreach (var r in m_Results) list.Add(r);
                return list;
            }
        }

        public bool TryGetResultEntry(int round, int policy, out PolicyResultEntry entry)
        {
            if (m_Results != null)
                foreach (var r in m_Results)
                    if (r.Round == round && r.Policy == policy) { entry = r; return true; }
            entry = default;
            return false;
        }

        public VoteChoice GetVote(int seat, int policyIndex)
        {
            if (m_Votes == null) return VoteChoice.None;
            foreach (var v in m_Votes)
                if (v.Seat == seat && v.PolicyIndex == policyIndex) return v.Choice;
            return VoteChoice.None;
        }

        /// <summary>How many seats have voted on a policy, out of the seats with a role.</summary>
        public (int voted, int seats) VoteProgress(int policyIndex)
        {
            int voted = 0, seats = 0;
            for (int s = 0; s < Session.SeatCount; s++)
            {
                if (!Session.GetSeat(s).HasRole) continue;
                seats++;
                if (GetVote(s, policyIndex) != VoteChoice.None) voted++;
            }
            return (voted, seats);
        }

        /// <summary>The year shown for the current phase (rolls forward in the future view and epilogue).</summary>
        public int DisplayYear
        {
            get
            {
                var s = Scenario;
                if (s == null) return 0;
                if (m_Phase.Value == GamePhase.Epilogue) return EpilogueYear;
                var round = CurrentRound;
                return round != null ? round.year : s.startYear;
            }
        }

        public int NextYear
        {
            get
            {
                var s = Scenario;
                if (s == null) return 0;
                int next = m_Round.Value + 1;
                return next < s.rounds.Count && s.rounds[next] != null ? s.rounds[next].year : EpilogueYear;
            }
        }

        public int EpilogueYear
        {
            get
            {
                var s = Scenario;
                if (s == null || s.rounds.Count == 0) return s != null ? s.startYear : 0;
                var last = s.rounds[s.rounds.Count - 1];
                return (last != null ? last.year : s.startYear) + (Config != null ? Config.epilogueYearsAfterLastRound : 1);
            }
        }

        // Requests from this headset ------------------------------------------------------------

        /// <summary>Cast, change or withdraw (None) this player's vote on a policy of the current round.</summary>
        public void CastVote(int policyIndex, VoteChoice choice) => CastVoteRpc((byte)policyIndex, choice);

        /// <summary>Debug/teacher: end the current phase now.</summary>
        public void RequestSkipPhase() => SkipPhaseRpc();

        // Lifecycle -----------------------------------------------------------------------------

        void Awake()
        {
            m_Votes = new NetworkList<VoteEntry>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            m_Results = new NetworkList<PolicyResultEntry>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            m_Phase.OnValueChanged += OnPhaseValueChanged;
            m_Votes.OnListChanged += OnVotesListChanged;
            m_Results.OnListChanged += OnResultsListChanged;
            m_Record = null;
        }

        public override void OnNetworkDespawn()
        {
            m_Phase.OnValueChanged -= OnPhaseValueChanged;
            m_Votes.OnListChanged -= OnVotesListChanged;
            m_Results.OnListChanged -= OnResultsListChanged;
            m_AIVoteAt.Clear();
            m_AIRevealAt.Clear();
        }

        void OnPhaseValueChanged(GamePhase _, GamePhase phase) => PhaseChanged?.Invoke(phase);
        void OnVotesListChanged(NetworkListEvent<VoteEntry> _) => VotesChanged?.Invoke();

        void OnResultsListChanged(NetworkListEvent<PolicyResultEntry> _)
        {
            m_Record = null;
            ResultsChanged?.Invoke();
        }

        CouncilRecord BuildRecord()
        {
            var results = new List<PolicyResult>();
            if (m_Results != null) foreach (var r in m_Results) results.Add(r.ToResult());
            return new CouncilRecord(Scenario, results);
        }

        double Now => NetworkManager.ServerTime.Time;

        // Authority: the state machine ----------------------------------------------------------

        void Update()
        {
            if (!IsSpawned || !HasAuthority || Scenario == null) return;

            switch (m_Phase.Value)
            {
                case GamePhase.NotStarted:
                    if (Session.Phase == CouncilPhase.Complete)
                        Enter(GamePhase.WakingUp, Config.councilWakeSeconds);
                    return;

                case GamePhase.Epilogue:
                    return;

                case GamePhase.Deciding:
                    TickAI();
                    TickAllVotedShortcut();
                    break;
            }

            if (m_PhaseEndsAt.Value >= 0d && Now >= m_PhaseEndsAt.Value) Advance();
        }

        void Advance()
        {
            var round = CurrentRound;
            switch (m_Phase.Value)
            {
                case GamePhase.WakingUp:
                    StartRound(0);
                    break;

                case GamePhase.RoundIntro:
                    StartProposal(round.presentation == PolicyPresentation.AllAtOnce ? -1 : 0);
                    break;

                case GamePhase.Proposal:
                    StartDeciding();
                    break;

                case GamePhase.Deciding:
                    ResolveActivePolicies();
                    Enter(GamePhase.Resolving, Config.resolveSeconds);
                    break;

                case GamePhase.Resolving:
                    int next = m_Policy.Value + 1;
                    if (m_Policy.Value >= 0 && round != null && next < round.policies.Count)
                        StartProposal(next);
                    else
                        Enter(GamePhase.FutureView, Config.futureViewSeconds);
                    break;

                case GamePhase.FutureView:
                    if (m_Round.Value + 1 < Scenario.rounds.Count) StartRound(m_Round.Value + 1);
                    else Enter(GamePhase.Epilogue, -1f);
                    break;
            }
        }

        void Enter(GamePhase phase, float seconds)
        {
            m_PhaseEndsAt.Value = seconds >= 0f ? Now + seconds : -1d;
            m_Phase.Value = phase;
            Debug.Log($"[Town Council] Phase: {phase} (round {m_Round.Value + 1}, policy {(m_Policy.Value < 0 ? "all" : (m_Policy.Value + 1).ToString())})");
        }

        void StartRound(int roundIndex)
        {
            // Skip empty rounds so a half-written scenario still plays.
            while (roundIndex < Scenario.rounds.Count &&
                   (Scenario.rounds[roundIndex] == null || Scenario.rounds[roundIndex].PolicyCount == 0))
                roundIndex++;

            if (roundIndex >= Scenario.rounds.Count)
            {
                Enter(GamePhase.Epilogue, -1f);
                return;
            }

            m_Round.Value = roundIndex;
            m_Policy.Value = -1;
            m_Votes.Clear();
            Enter(GamePhase.RoundIntro, Config.roundIntroSeconds);
        }

        void StartProposal(int policyIndex)
        {
            m_Policy.Value = policyIndex;
            int count = ActivePolicies.Count;
            Enter(GamePhase.Proposal, Config.proposalSeconds * Mathf.Max(1, count));
        }

        void StartDeciding()
        {
            // Clear old votes for the policies now on the table.
            var active = new List<int>(ActivePolicies);
            for (int i = m_Votes.Count - 1; i >= 0; i--)
                if (active.Contains(m_Votes[i].PolicyIndex)) m_Votes.RemoveAt(i);

            float seconds = 0f;
            foreach (var p in active) seconds = Mathf.Max(seconds, GetPolicy(p).GetDecisionSeconds(Config));
            ScheduleAI(active, seconds);
            m_ShortcutApplied = false;
            Enter(GamePhase.Deciding, seconds);
        }

        void ResolveActivePolicies()
        {
            int seatCount = 0;
            for (int s = 0; s < Session.SeatCount; s++) if (Session.GetSeat(s).HasRole) seatCount++;

            foreach (var p in ActivePolicies)
            {
                var policy = GetPolicy(p);
                var votes = new List<VoteChoice>();
                for (int s = 0; s < Session.SeatCount; s++)
                    if (Session.GetSeat(s).HasRole) votes.Add(GetVote(s, p));

                var result = VoteRules.For(policy).Resolve(votes, seatCount);
                m_Results.Add(new PolicyResultEntry
                {
                    Round = (byte)m_Round.Value,
                    Policy = (byte)p,
                    Choice = result.Choice,
                    Deadlock = result.Deadlock,
                    Agree = (byte)result.Agree,
                    Disagree = (byte)result.Disagree,
                    Amend = (byte)result.Amend,
                    Abstain = (byte)result.Abstain
                });
                Debug.Log($"[Town Council] {policy.title}: {result}");
            }
        }

        // Authority: AI councillors -------------------------------------------------------------

        void ScheduleAI(List<int> active, float seconds)
        {
            m_AIVoteAt.Clear();
            m_AIRevealAt.Clear();
            var window = Config.aiVoteWindow;
            double start = Now;

            for (int s = 0; s < Session.SeatCount; s++)
            {
                var seat = Session.GetSeat(s);
                if (!seat.IsAI || !seat.HasRole) continue;

                foreach (var p in active)
                    m_AIVoteAt[(s, p)] = start + seconds * UnityEngine.Random.Range(window.x, window.y);

                if (seat.SecretRevealed) continue;
                var role = Session.GetRole(seat.RoleIndex);
                foreach (var p in active)
                {
                    var policy = GetPolicy(p);
                    if (policy != null && policy.aiRevealRoles.Contains(role))
                    {
                        m_AIRevealAt[s] = start + seconds * UnityEngine.Random.Range(0.1f, 0.35f);
                        break;
                    }
                }
            }
        }

        void TickAI()
        {
            double now = Now;
            if (m_AIVoteAt.Count > 0)
            {
                var due = new List<(int seat, int policy)>();
                foreach (var kv in m_AIVoteAt)
                    if (now >= kv.Value) due.Add(kv.Key);

                foreach (var key in due)
                {
                    m_AIVoteAt.Remove(key);
                    var brain = Session.GetBrain(key.seat);
                    var policy = GetPolicy(key.policy);
                    if (brain == null || policy == null) continue;
                    var context = new CouncillorContext { OtherVotes = OtherVotes(key.seat, key.policy), MeterValues = Record.Meters };
                    SetVote(key.seat, key.policy, brain.DecideVote(policy, context));
                }
            }

            if (m_AIRevealAt.Count > 0)
            {
                var reveal = new List<int>();
                foreach (var kv in m_AIRevealAt)
                    if (now >= kv.Value) reveal.Add(kv.Key);
                foreach (var seat in reveal)
                {
                    m_AIRevealAt.Remove(seat);
                    Session.RevealSeatSecret(seat);
                }
            }
        }

        List<VoteChoice> OtherVotes(int seat, int policyIndex)
        {
            var list = new List<VoteChoice>();
            for (int s = 0; s < Session.SeatCount; s++)
                if (s != seat && Session.GetSeat(s).HasRole) list.Add(GetVote(s, policyIndex));
            return list;
        }

        void TickAllVotedShortcut()
        {
            if (m_ShortcutApplied) return;
            foreach (var p in ActivePolicies)
            {
                var (voted, seats) = VoteProgress(p);
                if (voted < seats) return;
            }

            m_ShortcutApplied = true;
            double shortcutEnd = Now + Config.allVotedShortcutSeconds;
            if (shortcutEnd < m_PhaseEndsAt.Value) m_PhaseEndsAt.Value = shortcutEnd;
        }

        void SetVote(int seat, int policyIndex, VoteChoice choice)
        {
            for (int i = 0; i < m_Votes.Count; i++)
            {
                var v = m_Votes[i];
                if (v.Seat != seat || v.PolicyIndex != policyIndex) continue;
                if (choice == VoteChoice.None) m_Votes.RemoveAt(i);
                else if (v.Choice != choice)
                {
                    v.Choice = choice;
                    m_Votes[i] = v;
                }
                return;
            }
            if (choice != VoteChoice.None)
                m_Votes.Add(new VoteEntry { Seat = (byte)seat, PolicyIndex = (byte)policyIndex, Choice = choice });
        }

        // RPCs (run on the authority) -----------------------------------------------------------

        [Rpc(SendTo.Authority)]
        void CastVoteRpc(byte policyIndex, VoteChoice choice, RpcParams rpcParams = default)
        {
            if (m_Phase.Value != GamePhase.Deciding) return;
            int seat = Session.SeatOfClientId(rpcParams.Receive.SenderClientId);
            if (seat < 0 || !Session.GetSeat(seat).HasRole) return;
            if (!IsActive(policyIndex)) return;

            SetVote(seat, policyIndex, choice);
            if (choice == VoteChoice.None) m_ShortcutApplied = false;
        }

        [Rpc(SendTo.Authority)]
        void SkipPhaseRpc()
        {
            if (m_Phase.Value == GamePhase.NotStarted || m_Phase.Value == GamePhase.Epilogue) return;
            m_PhaseEndsAt.Value = Now;
        }
    }
}
