using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownCouncil.Experience
{
    /// <summary>
    /// A "Voice: On / Off" button for players. Hidden when speech isn't installed or is switched off
    /// in Game Config. Muting is remembered on this headset.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SpeechToggleButton : MonoBehaviour
    {
        public TMP_Text label;
        public string onText = "Voice: On";
        public string offText = "Voice: Off";

        Button m_Button;
        Graphic m_Graphic;

        void Awake()
        {
            m_Button = GetComponent<Button>();
            m_Graphic = GetComponent<Graphic>();
            m_Button.onClick.AddListener(() => SpeechHub.Muted = !SpeechHub.Muted);
        }

        void OnEnable()
        {
            SpeechHub.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => SpeechHub.Changed -= Refresh;

        void Update()
        {
            // Speech can register after this button wakes up; keep visibility in step cheaply.
            bool available = Available();
            if (m_Graphic != null && m_Graphic.enabled != available) Refresh();
        }

        static bool Available() =>
            SpeechHub.IsInstalled && (SpeechHub.Config == null || SpeechHub.Config.speechEnabled);

        void Refresh()
        {
            bool available = Available();
            if (m_Graphic != null) m_Graphic.enabled = available;
            m_Button.enabled = available;
            if (label != null)
            {
                label.enabled = available;
                label.text = SpeechHub.Muted ? offText : onText;
            }
        }
    }
}
