using TMPro;
using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TownCouncil.Experience
{
    /// <summary>
    /// A role card on the foyer table. Grab it (or press its button) to ask for that role.
    /// Shows only public info; the goal and secret appear in the role's room.
    /// </summary>
    public class RoleCardView : MonoBehaviour
    {
        [Header("Card")]
        public int roleIndex;
        public GameConfig config;

        [Header("References")]
        public TMP_Text titleText;
        public TMP_Text jobText;
        public TMP_Text stateText;
        public Button chooseButton;
        public Renderer cardRenderer;
        [Tooltip("Optional: grabbing or selecting this picks the card.")]
        public XRBaseInteractable interactable;

        [Header("Colours")]
        public Color takenColor = new(0.25f, 0.25f, 0.25f);
        public Color mineColor = new(1f, 0.85f, 0.4f);

        const float k_RefreshInterval = 0.2f;
        float m_NextRefresh;
        MaterialPropertyBlock m_Block;

        RoleDefinition Role => config != null && config.scenario != null ? config.scenario.GetRole(roleIndex) : null;

        void Awake()
        {
            if (chooseButton != null) chooseButton.onClick.AddListener(Choose);
            if (interactable == null) interactable = GetComponent<XRBaseInteractable>();
            if (interactable != null) interactable.selectEntered.AddListener(OnSelected);
        }

        void OnDestroy()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnSelected);
        }

        void OnEnable()
        {
            var role = Role;
            if (titleText != null) titleText.text = role != null ? role.CardTitle : $"Role {roleIndex + 1}";
            if (jobText != null) jobText.text = role != null ? role.job : "";
            m_NextRefresh = 0f;
        }

        void OnSelected(SelectEnterEventArgs _) => Choose();

        /// <summary>Ask the council for this card. Ignored if it's taken or you already have one.</summary>
        public void Choose()
        {
            var session = CouncilSession.Instance;
            if (session == null || !session.IsSpawned) return;
            if (!session.TryGetLocalSeat(out var seat) || seat.HasRole) return;
            if (session.IsRoleTaken(roleIndex)) return;
            session.RequestRole(roleIndex);
            if (stateText != null) stateText.text = "Asking…";
        }

        void Update()
        {
            if (Time.unscaledTime < m_NextRefresh) return;
            m_NextRefresh = Time.unscaledTime + k_RefreshInterval;

            var session = CouncilSession.Instance;
            if (session == null || !session.IsSpawned) return;

            int holder = session.SeatOfRole(roleIndex);
            bool mine = holder >= 0 && holder == session.LocalSeatIndex;
            bool taken = holder >= 0 && !mine;
            bool canPick = !taken && session.TryGetLocalSeat(out var seat) && !seat.HasRole;

            if (stateText != null)
            {
                if (mine) stateText.text = "Your card";
                else if (taken) stateText.text = session.GetSeat(holder).IsAI ? "Taken by AI" : $"Taken by {session.GetSeat(holder).Name}";
                else stateText.text = canPick ? "Pick me" : "";
            }

            if (chooseButton != null) chooseButton.interactable = canPick;
            if (interactable != null) interactable.enabled = canPick;

            var role = Role;
            Tint(mine ? mineColor : taken ? takenColor : role != null ? role.accentColor : Color.white);
        }

        void Tint(Color color)
        {
            if (cardRenderer == null) return;
            m_Block ??= new MaterialPropertyBlock();
            cardRenderer.GetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", color);
            m_Block.SetColor("_Color", color);
            cardRenderer.SetPropertyBlock(m_Block);
        }
    }
}
