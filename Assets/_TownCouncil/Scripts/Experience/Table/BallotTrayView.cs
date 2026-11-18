using System.Collections.Generic;
using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// The ballot area in front of one seat. Shows one <see cref="BallotRow"/> per policy on the
    /// table (one for one-after-another rounds, several for all-at-once rounds) and keeps the lamps
    /// in sync with the replicated votes. At the local seat it sends the player's token moves as votes.
    /// </summary>
    public class BallotTrayView : MonoBehaviour
    {
        public int seatIndex;
        [Tooltip("Where the first row sits; further rows step toward the table centre.")]
        public Transform trayAnchor;
        public float rowSpacing = 0.14f;
        public Material surfaceMaterial;
        public Material glowMaterial;

        readonly List<BallotRow> m_Rows = new();
        CouncilGame m_Game;
        string m_LayoutKey;
        float m_NextCheck;

        void Update()
        {
            if (Time.unscaledTime < m_NextCheck) return;
            m_NextCheck = Time.unscaledTime + 0.1f;

            if (m_Game == null)
            {
                m_Game = CouncilGame.Instance;
                if (m_Game == null) return;
                m_Game.PhaseChanged += OnPhaseChanged;
                m_Game.VotesChanged += RefreshLamps;
                m_Game.ResultsChanged += RefreshLamps;
            }

            // Rebuild only when the set of policies on the table (or who sits here) changes.
            var key = LayoutKey();
            if (key != m_LayoutKey)
            {
                m_LayoutKey = key;
                Rebuild();
            }
        }

        void OnDestroy()
        {
            if (m_Game == null) return;
            m_Game.PhaseChanged -= OnPhaseChanged;
            m_Game.VotesChanged -= RefreshLamps;
            m_Game.ResultsChanged -= RefreshLamps;
        }

        bool Visible(GamePhase phase) =>
            phase == GamePhase.Proposal || phase == GamePhase.Deciding || phase == GamePhase.Resolving;

        string LayoutKey()
        {
            if (m_Game == null || !m_Game.IsSpawned || !Visible(m_Game.Phase)) return "hidden";
            var session = m_Game.Session;
            if (seatIndex >= session.SeatCount || !session.GetSeat(seatIndex).HasRole) return "hidden";
            bool local = session.LocalSeatIndex == seatIndex;
            return $"{m_Game.RoundIndex}:{string.Join(",", m_Game.ActivePolicies)}:{local}";
        }

        void Rebuild()
        {
            foreach (var row in m_Rows) if (row != null) Destroy(row.gameObject);
            m_Rows.Clear();
            if (m_LayoutKey == "hidden") return;

            var session = m_Game.Session;
            var seat = session.GetSeat(seatIndex);
            var role = session.GetRole(seat.RoleIndex);
            bool local = session.LocalSeatIndex == seatIndex;
            var anchor = trayAnchor != null ? trayAnchor : transform;

            int i = 0;
            foreach (var policyIndex in m_Game.ActivePolicies)
            {
                var go = new GameObject($"Ballot · policy {policyIndex + 1}");
                go.transform.SetParent(anchor, false);
                go.transform.localPosition = new Vector3(0f, 0f, i * rowSpacing);
                var row = go.AddComponent<BallotRow>();
                row.Build(seatIndex, policyIndex, local, role != null ? role.accentColor : Color.white, surfaceMaterial, glowMaterial);
                var policy = m_Game.GetPolicy(policyIndex);
                row.SetLabel(policy != null ? policy.title : "");
                if (local) row.TokenMoved += OnTokenMoved;
                m_Rows.Add(row);
                i++;
            }
            OnPhaseChanged(m_Game.Phase);
        }

        void OnPhaseChanged(GamePhase phase)
        {
            bool open = phase == GamePhase.Deciding;
            foreach (var row in m_Rows)
            {
                if (row == null) continue;
                row.SetOpen(open);
                if (phase == GamePhase.Proposal) row.ReturnToken();
            }
            RefreshLamps();
        }

        void OnTokenMoved(BallotRow row, VoteChoice choice)
        {
            if (m_Game != null && m_Game.Phase == GamePhase.Deciding) m_Game.CastVote(row.PolicyIndex, choice);
        }

        void RefreshLamps()
        {
            if (m_Game == null || !m_Game.IsSpawned) return;
            bool resolving = m_Game.Phase == GamePhase.Resolving;
            bool live = m_Game.Config != null && m_Game.Config.showVotesLive;

            foreach (var row in m_Rows)
            {
                if (row == null) continue;
                var vote = m_Game.GetVote(seatIndex, row.PolicyIndex);
                bool isMine = row.IsLocal;
                bool winner = false;
                if (resolving && m_Game.TryGetResultEntry(m_Game.RoundIndex, row.PolicyIndex, out var result))
                    winner = !result.Deadlock && result.Choice == vote;
                row.ShowLamps(vote, resolving || live || isMine, winner);
            }
        }
    }
}
