using System.Collections.Generic;
using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>
    /// One year of the story. Holds any number of policies: add or remove entries in the list
    /// (or use the "Add policy" button in the inspector) and the game adapts.
    /// </summary>
    [CreateAssetMenu(menuName = "Town Council/Round", fileName = "Round_")]
    public class RoundDefinition : ScriptableObject
    {
        public int year = 2046;
        public string title = "Round title";
        [Tooltip("The lesson this round teaches (shown to teachers, used in the debrief).")]
        [TextArea(1, 3)] public string lesson;
        public DialogueLine civicIntro;

        [Header("Policies")]
        public PolicyPresentation presentation = PolicyPresentation.OneAfterAnother;
        public List<PolicyDefinition> policies = new();

        [Header("Perspectives (touch a building to hear them)")]
        public List<PerspectiveClip> perspectives = new();

        public string DisplayName => $"{year} · {title}";
        public int PolicyCount => policies?.Count ?? 0;
    }
}
