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
        public string Format(Func<string, string> tokenResolver)
        {
            if (string.IsNullOrEmpty(subtitle) || tokenResolver == null) return subtitle;
            var text = subtitle;
            int searchFrom = 0;
            while (searchFrom < text.Length)
            {
                int start = text.IndexOf('{', searchFrom);
                if (start < 0) break;
                int end = text.IndexOf('}', start);
                if (end < 0) break;
                var token = text.Substring(start + 1, end - start - 1);
                var value = tokenResolver(token) ?? "{" + token + "}";
                text = text.Substring(0, start) + value + text.Substring(end + 1);
                searchFrom = start + value.Length;
            }
            return text;
        }
    }
}
