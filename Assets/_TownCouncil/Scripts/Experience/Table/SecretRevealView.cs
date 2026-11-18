using TMPro;
using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TownCouncil.Experience
{
    /// <summary>
    /// The role card at a seat and its reveal spot.
    /// - Local seat: your card lies beside your ballot, showing your goal and secret to you only.
    ///   Place it on the glowing reveal spot to show your secret to the whole council (no undo).
    /// - Every seat: once revealed (or in the epilogue) the secret appears above the seat for everyone.
    /// </summary>
    public class SecretRevealView : MonoBehaviour
    {
        public int seatIndex;
        public Transform cardAnchor;
        public Transform revealAnchor;
        [Tooltip("Text above the seat that shows the secret to everyone once revealed.")]
        public TMP_Text secretDisplay;
        public GameObject secretDisplayRoot;
        public Material cardMaterial;
        public Material glowMaterial;

        CouncilSession m_Session;
        CouncilGame m_Game;
        GameObject m_Kit;
        XRGrabInteractable m_Card;
        Renderer m_SpotRenderer;
        int m_BuiltForRole = -1;
        bool m_RequestedReveal;

        void Update()
        {
            m_Session ??= CouncilSession.Instance;
            m_Game ??= CouncilGame.Instance;
            if (m_Session == null || !m_Session.IsSpawned || seatIndex >= m_Session.SeatCount)
            {
                SetDisplay(null);
                return;
            }

            var seat = m_Session.GetSeat(seatIndex);
            var role = seat.HasRole ? m_Session.GetRole(seat.RoleIndex) : null;
            bool local = seatIndex == m_Session.LocalSeatIndex;
            bool inGame = m_Game != null && m_Game.IsRunning;
            bool epilogue = inGame && m_Game.Phase == GamePhase.Epilogue;

            // Personal card + reveal spot only at your own seat, once the game is on.
            bool wantKit = local && role != null && inGame && !epilogue;
            if (wantKit && (m_Kit == null || m_BuiltForRole != seat.RoleIndex)) BuildKit(role, seat.RoleIndex);
            if (!wantKit && m_Kit != null) { Destroy(m_Kit); m_Kit = null; m_BuiltForRole = -1; }

            if (m_Kit != null)
            {
                if (seat.SecretRevealed && m_Card != null) m_Card.enabled = false;
                float pulse = seat.SecretRevealed ? 0.4f : 1.2f + Mathf.Sin(Time.time * 3f) * 0.6f;
                TableKit.Tint(m_SpotRenderer, new Color(1f, 0.42f, 0.16f), pulse);
            }

            SetDisplay((seat.SecretRevealed || epilogue) && role != null ? role : null);
        }

        void SetDisplay(RoleDefinition role)
        {
            bool show = role != null && !string.IsNullOrEmpty(role.secret);
            if (secretDisplayRoot != null && secretDisplayRoot.activeSelf != show) secretDisplayRoot.SetActive(show);
            if (show && secretDisplay != null)
                secretDisplay.text = $"<size=60%><color=#FF6B29>{role.displayName.ToUpperInvariant()}'S SECRET</color></size>\n{role.secret}";
        }

        void BuildKit(RoleDefinition role, int roleIndex)
        {
            if (m_Kit != null) Destroy(m_Kit);
            m_BuiltForRole = roleIndex;
            m_RequestedReveal = false;
            m_Kit = new GameObject("Role Card Kit");
            m_Kit.transform.SetParent(transform, false);
            string key = $"secret-{seatIndex}";

            // Reveal spot on the table
            var spotParent = revealAnchor != null ? revealAnchor : transform;
            var spot = TableKit.Shape(PrimitiveType.Cylinder, "Reveal Spot", m_Kit.transform, Vector3.zero, new Vector3(0.16f, 0.002f, 0.16f), glowMaterial);
            spot.transform.SetPositionAndRotation(spotParent.position, spotParent.rotation);
            m_SpotRenderer = spot.GetComponent<Renderer>();
            var spotLabel = TableKit.FlatLabel("Reveal Label", m_Kit.transform, Vector3.zero, "PLACE CARD TO REVEAL YOUR SECRET", 0.12f, new Color(1f, 0.6f, 0.4f));
            spotLabel.transform.SetPositionAndRotation(spotParent.TransformPoint(new Vector3(0f, 0.003f, -0.1f)), spotParent.rotation * Quaternion.Euler(90f, 0f, 0f));
            var revealSocket = TableKit.Socket("Reveal Socket", m_Kit.transform, Vector3.zero, key, 0.07f);
            revealSocket.transform.SetPositionAndRotation(spotParent.position + spotParent.up * 0.01f, spotParent.rotation);
            revealSocket.Placed += (_, _) => RequestReveal();

            // Home socket + card
            var homeParent = cardAnchor != null ? cardAnchor : transform;
            var home = TableKit.Socket("Card Home", m_Kit.transform, Vector3.zero, key, 0.06f);
            home.transform.SetPositionAndRotation(homeParent.position + homeParent.up * 0.01f, homeParent.rotation);

            var card = TableKit.Shape(PrimitiveType.Cube, "Role Card", m_Kit.transform, Vector3.zero, new Vector3(0.12f, 0.004f, 0.17f), cardMaterial, true);
            card.transform.SetPositionAndRotation(home.transform.position, home.transform.rotation);
            TableKit.Tint(card.GetComponent<Renderer>(), role.accentColor * 0.6f, 0.2f);

            // Private face, readable from the seat, visible only on this headset (the card only exists here).
            var face = new GameObject("Face");
            face.transform.SetParent(card.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            face.transform.localScale = new Vector3(1f / 0.12f, 1f / 0.17f, 1f / 0.004f) * 0.001f;
            var tmp = face.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(110f, 160f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 30f;
            tmp.fontSizeMax = 110f;
            tmp.alignment = TextAlignmentOptions.Top;
            tmp.text = $"<b>{role.CardTitle}</b>\n<size=70%>{role.job}</size>\n\n<size=60%><color=#BBBBBB>GOAL</color>\n{role.hiddenGoal}\n\n<color=#FF6B29>SECRET</color>\n{role.secret}</size>";

            m_Card = TableKit.Grabbable(card, key);
            home.startingSelectedInteractable = m_Card;
        }

        void RequestReveal()
        {
            if (m_RequestedReveal || m_Session == null) return;
            m_RequestedReveal = true;
            m_Session.RevealSecret();
        }
    }
}
