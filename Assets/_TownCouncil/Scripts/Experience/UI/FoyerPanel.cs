using TMPro;
using TownCouncil.Networking;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

namespace TownCouncil.Experience
{
    /// <summary>
    /// The lobby board in the foyer: council code, who has joined, the AI countdown and what to do next.
    /// </summary>
    public class FoyerPanel : MonoBehaviour
    {
        [Header("References")]
        public TMP_Text codeText;
        public TMP_Text seatsText;
        public TMP_Text statusText;
        public TMP_Text hintText;
        public Button startNowButton;
        public Button leaveButton;
        public Button watchButton;

        [Header("Wording")]
        public string hintPickCard = "Pick up a role card from the table.";
        public string hintWaiting = "Card accepted. Heading to your room…";
        public string hintWatching = "This council has already started. You can watch from the gallery.";

        const float k_RefreshInterval = 0.25f;
        float m_NextRefresh;

        void Awake()
        {
            if (startNowButton != null) startNowButton.onClick.AddListener(StartNowWithAI);
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
            if (watchButton != null) watchButton.onClick.AddListener(Watch);
        }

        void Update()
        {
            if (Time.unscaledTime < m_NextRefresh) return;
            m_NextRefresh = Time.unscaledTime + k_RefreshInterval;
            Refresh();
        }

        void Refresh()
        {
            var session = CouncilSession.Instance;
            bool live = session != null && session.IsSpawned;

            if (codeText != null)
            {
                var code = XRINetworkGameManager.ConnectedRoomCode;
                codeText.text = string.IsNullOrEmpty(code) ? "" : $"Council code  <b>{code}</b>";
            }

            if (!live)
            {
                if (seatsText != null) seatsText.text = "";
                if (statusText != null) statusText.text = "Connecting…";
                SetVisible(startNowButton, false);
                SetVisible(watchButton, false);
                return;
            }

            if (seatsText != null) seatsText.text = CouncilText.SeatList(session);
            if (statusText != null) statusText.text = CouncilText.LobbyStatus(session);

            bool seated = session.TryGetLocalSeat(out var seat);
            bool watching = !seated && session.Phase != CouncilPhase.Gathering;

            if (hintText != null)
                hintText.text = watching ? hintWatching : seated && seat.HasRole ? hintWaiting : hintPickCard;

            // Whoever runs the session can stop waiting and fill the empty seats with AI.
            SetVisible(startNowButton, session.HasAuthority && session.Phase == CouncilPhase.Gathering);
            SetVisible(watchButton, watching);
        }

        void StartNowWithAI()
        {
            var session = CouncilSession.Instance;
            if (session != null && session.IsSpawned) session.RequestAIFillNow();
        }

        static void Leave()
        {
            if (XRINetworkGameManager.Instance != null) XRINetworkGameManager.Instance.Disconnect();
        }

        static void Watch()
        {
            if (ExperienceDirector.Instance != null) ExperienceDirector.Instance.WatchFromGallery();
        }

        static void SetVisible(Component c, bool visible)
        {
            if (c != null && c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
        }
    }
}
