using TMPro;
using TownCouncil.Networking;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

namespace TownCouncil.Experience
{
    /// <summary>
    /// The player's personal ending, shown in front of their seat in the epilogue: what became of
    /// their character, chosen from the role's endings by what the council decided.
    /// </summary>
    public class EndingPanel : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text titleText;
        public TMP_Text endingText;
        public TMP_Text footerText;
        public Button leaveButton;
        [Tooltip("Seat spawn points, same order as the seats. The panel appears in front of the local seat.")]
        public Transform[] seatSpawns = new Transform[0];
        public Vector3 offsetFromSeat = new(0f, 1.3f, 0.55f);

        [TextArea] public string footer = "Take off your headset. Your teacher will lead the debrief.";
        [TextArea] public string noEnding = "Your story in Newhaven continues.";

        bool m_Shown;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            if (leaveButton != null) leaveButton.onClick.AddListener(() =>
            {
                if (XRINetworkGameManager.Instance != null) XRINetworkGameManager.Instance.Disconnect();
            });
        }

        void Update()
        {
            var game = CouncilGame.Instance;
            bool show = game != null && game.IsSpawned && game.Phase == GamePhase.Epilogue;
            if (show == m_Shown) return;
            m_Shown = show;

            if (!show)
            {
                if (root != null) root.SetActive(false);
                return;
            }

            var session = game.Session;
            if (!session.TryGetLocalSeat(out var seat) || !seat.HasRole) return;
            var role = session.GetRole(seat.RoleIndex);

            int index = session.LocalSeatIndex;
            if (index >= 0 && index < seatSpawns.Length && seatSpawns[index] != null)
            {
                var spawn = seatSpawns[index];
                transform.SetPositionAndRotation(spawn.TransformPoint(offsetFromSeat), spawn.rotation);
            }

            var ending = game.Record.EndingFor(role);
            if (titleText != null) titleText.text = role != null ? $"{role.CardTitle} · {game.EpilogueYear}" : "";
            if (endingText != null) endingText.text = string.IsNullOrWhiteSpace(ending) ? noEnding : ending;
            if (footerText != null) footerText.text = footer;
            if (root != null) root.SetActive(true);
        }
    }
}
