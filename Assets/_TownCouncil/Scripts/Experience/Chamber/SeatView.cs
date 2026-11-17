using TMPro;
using TownCouncil.Networking;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// One chair at the round table. Shows the nameplate, tints to the role's colour and shows a
    /// stand-in avatar when an AI councillor sits here. Human players appear as their own avatars.
    /// </summary>
    public class SeatView : MonoBehaviour
    {
        public int seatIndex;
        [Tooltip("Where the local player stands/sits when this is their seat. Forward faces the table.")]
        public Transform playerSpawn;
        public TMP_Text nameplate;
        [Tooltip("Shown while an AI councillor holds this seat. Replace with a proper AI avatar.")]
        public GameObject aiAvatar;
        public Renderer chairRenderer;

        const float k_RefreshInterval = 0.25f;
        float m_NextRefresh;
        MaterialPropertyBlock m_Block;

        public Transform PlayerSpawn => playerSpawn != null ? playerSpawn : transform;

        void Update()
        {
            if (Time.unscaledTime < m_NextRefresh) return;
            m_NextRefresh = Time.unscaledTime + k_RefreshInterval;

            var session = CouncilSession.Instance;
            bool live = session != null && session.IsSpawned && seatIndex < session.SeatCount;
            var seat = live ? session.GetSeat(seatIndex) : SeatState.Empty;
            var role = live && seat.HasRole ? session.GetRole(seat.RoleIndex) : null;

            if (aiAvatar != null && aiAvatar.activeSelf != (seat.IsAI && seat.HasRole))
                aiAvatar.SetActive(seat.IsAI && seat.HasRole);

            if (nameplate != null)
            {
                if (seat.IsEmpty) nameplate.text = "";
                else if (role == null) nameplate.text = seat.IsAI ? "AI councillor" : seat.Name.ToString();
                else nameplate.text = seat.IsAI ? $"{role.CardTitle}\n<size=60%>{role.job} · AI</size>"
                                                : $"{role.CardTitle}\n<size=60%>{role.job} · {seat.Name}</size>";
            }

            if (chairRenderer != null)
            {
                m_Block ??= new MaterialPropertyBlock();
                var color = role != null ? role.accentColor : new Color(0.35f, 0.35f, 0.38f);
                chairRenderer.GetPropertyBlock(m_Block);
                m_Block.SetColor("_BaseColor", color);
                m_Block.SetColor("_Color", color);
                chairRenderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
