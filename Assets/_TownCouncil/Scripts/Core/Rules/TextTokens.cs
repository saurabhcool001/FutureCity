using System;

namespace TownCouncil.Core
{
    /// <summary>Replaces {tokens} in text, e.g. "matched {agreementPercent}% of the time".</summary>
    public static class TextTokens
    {
        public static string Format(string text, Func<string, string> resolver)
        {
            if (string.IsNullOrEmpty(text) || resolver == null) return text;
            int searchFrom = 0;
            while (searchFrom < text.Length)
            {
                int start = text.IndexOf('{', searchFrom);
                if (start < 0) break;
                int end = text.IndexOf('}', start);
                if (end < 0) break;
                var token = text.Substring(start + 1, end - start - 1);
                var value = resolver(token) ?? "{" + token + "}";
                text = text.Substring(0, start) + value + text.Substring(end + 1);
                searchFrom = start + value.Length;
            }
            return text;
        }
    }
}
