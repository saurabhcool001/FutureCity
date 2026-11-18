using System;
using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>A spoken or captioned line (CIVIC, a resident, a narrator).</summary>
    [CreateAssetMenu(menuName = "Town Council/Dialogue Line", fileName = "Line_")]
    public class DialogueLine : ScriptableObject
    {
        public string speaker = "CIVIC";
        [TextArea(2, 6)] public string subtitle;
        public AudioClip audio;

        /// <summary>Subtitle with {tokens} replaced, e.g. {agreementPercent}.</summary>
        public string Format(Func<string, string> tokenResolver) => TextTokens.Format(subtitle, tokenResolver);
    }
}
