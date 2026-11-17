using System;
using TMPro;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>On-panel keypad for typing a council's join code (no system keyboard needed in VR).</summary>
    public class CodeKeypad : MonoBehaviour
    {
        public TMP_Text display;
        [Min(1)] public int maxLength = 6;
        public string placeholder = "Enter code";

        public event Action<string> Submitted;

        public string Code { get; private set; } = string.Empty;

        void OnEnable() => Refresh();

        public void AddCharacter(string character)
        {
            if (string.IsNullOrEmpty(character) || Code.Length >= maxLength) return;
            Code += character.ToUpperInvariant();
            Refresh();
        }

        public void Backspace()
        {
            if (Code.Length == 0) return;
            Code = Code.Substring(0, Code.Length - 1);
            Refresh();
        }

        public void Clear()
        {
            Code = string.Empty;
            Refresh();
        }

        public void Submit()
        {
            if (Code.Length > 0) Submitted?.Invoke(Code);
        }

        void Refresh()
        {
            if (display != null) display.text = Code.Length > 0 ? Code : $"<color=#8A8A8A>{placeholder}</color>";
        }
    }
}
