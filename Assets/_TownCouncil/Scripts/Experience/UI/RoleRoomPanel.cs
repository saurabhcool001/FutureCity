using System;
using TMPro;
using TownCouncil.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TownCouncil.Experience
{
    /// <summary>
    /// Shown in the role's own room: who you are, your hidden goal and your secret,
    /// then a button leading to the council chamber.
    /// </summary>
    public class RoleRoomPanel : MonoBehaviour
    {
        [Header("References")]
        public TMP_Text titleText;
        public TMP_Text jobText;
        public TMP_Text introText;
        public TMP_Text goalText;
        public TMP_Text secretText;
        public Button continueButton;
        public Image portraitImage;

        [Tooltip("Seconds before Continue becomes active, so the player reads the card first.")]
        [Min(0f)] public float readingDelay = 3f;

        public event Action ContinueRequested;

        float m_EnableAt;

        void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
        }

        public void Show(RoleDefinition role)
        {
            if (role == null)
            {
                Debug.LogError("[Town Council] Role room panel got no role to show. Check the Council Session's Game Config (Town Council > Repair Open Scene).", this);
                return;
            }
            if (titleText != null) titleText.text = role.CardTitle;
            if (jobText != null) jobText.text = role.job;
            if (introText != null) introText.text = role.roomIntro;
            if (goalText != null) goalText.text = role.hiddenGoal;
            if (secretText != null) secretText.text = role.secret;
            if (portraitImage != null)
            {
                portraitImage.sprite = role.portrait;
                portraitImage.enabled = role.portrait != null;
            }
            m_EnableAt = Time.unscaledTime + readingDelay;
        }

        public void MoveTo(Transform anchor)
        {
            if (anchor == null) return;
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        void Update()
        {
            if (continueButton != null) continueButton.interactable = Time.unscaledTime >= m_EnableAt;
        }
    }
}
