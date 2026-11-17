using TMPro;
using TownCouncil.Networking;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>Status board in the chamber while the council gathers.</summary>
    public class ChamberPanel : MonoBehaviour
    {
        public TMP_Text statusText;
        public TMP_Text seatsText;
        [TextArea] public string completeMessage = "The council is complete.\nCIVIC is waking up…";

        const float k_RefreshInterval = 0.25f;
        float m_NextRefresh;

        void Update()
        {
            if (Time.unscaledTime < m_NextRefresh) return;
            m_NextRefresh = Time.unscaledTime + k_RefreshInterval;

            var session = CouncilSession.Instance;
            if (session == null || !session.IsSpawned) return;

            if (seatsText != null) seatsText.text = CouncilText.SeatList(session);
            if (statusText == null) return;

            if (session.Phase == CouncilPhase.Complete)
            {
                statusText.text = completeMessage;
                return;
            }

            int seated = 0;
            for (int i = 0; i < session.SeatCount; i++)
                if (session.GetSeat(i).Stage == SeatStage.Seated && session.GetSeat(i).HasRole) seated++;

            statusText.text = $"{seated} of {session.SeatCount} councillors seated.\n{CouncilText.LobbyStatus(session)}";
        }
    }
}
