using System;
using TMPro;
using TownCouncil.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TownCouncil.Experience
{
    /// <summary>
    /// First thing the player sees with the headset on: a few pages explaining the game,
    /// taken from the scenario's intro pages. The last page leads to joining a council.
    /// </summary>
    public class WelcomePanel : MonoBehaviour
    {
        [Header("References")]
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public TMP_Text pageText;
        public Button backButton;
        public Button nextButton;
        public TMP_Text nextButtonLabel;

        [Header("Wording")]
        public string nextLabel = "Next";
        public string joinLabel = "Join the council";

        public event Action JoinRequested;

        ScenarioDefinition m_Scenario;
        int m_Page;

        void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
        }

        public void Show(ScenarioDefinition scenario)
        {
            m_Scenario = scenario;
            m_Page = 0;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Next()
        {
            if (m_Page >= PageCount - 1)
            {
                JoinRequested?.Invoke();
                return;
            }
            m_Page++;
            Refresh();
        }

        public void Back()
        {
            if (m_Page <= 0) return;
            m_Page--;
            Refresh();
        }

        int PageCount => m_Scenario != null && m_Scenario.introPages.Count > 0 ? m_Scenario.introPages.Count : 1;

        void Refresh()
        {
            string title = "Town Council 2045";
            string body = "Add intro pages to the Scenario asset.";
            if (m_Scenario != null && m_Scenario.introPages.Count > 0)
            {
                var page = m_Scenario.introPages[Mathf.Clamp(m_Page, 0, m_Scenario.introPages.Count - 1)];
                title = page.title;
                body = page.body;
            }

            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
            if (pageText != null) pageText.text = $"{m_Page + 1} / {PageCount}";
            if (backButton != null) backButton.interactable = m_Page > 0;
            if (nextButtonLabel != null) nextButtonLabel.text = m_Page >= PageCount - 1 ? joinLabel : nextLabel;
        }
    }
}
