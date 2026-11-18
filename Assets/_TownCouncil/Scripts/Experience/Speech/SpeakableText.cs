using TMPro;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// Marks a TextMeshPro text as important: it is read aloud (with word highlighting) when it appears
    /// or changes, if speech is installed and switched on. Does nothing otherwise.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class SpeakableText : MonoBehaviour
    {
        public SpeechChannel channel = SpeechChannel.Narration;
        public string speakerId = "Narrator";
        public bool speakWhenShown = true;
        public bool speakWhenTextChanges = true;
        [Tooltip("Wait this long after the text changes, so quickly flipping pages doesn't queue speech.")]
        [Min(0f)] public float delay = 0.5f;
        [Tooltip("Optional: where the voice comes from. Empty = not positional.")]
        public Transform emitter;

        TMP_Text m_Text;
        string m_LastText;
        float m_SpeakAt = -1f;

        void Awake() => m_Text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            m_LastText = m_Text.text;
            if (speakWhenShown) m_SpeakAt = Time.unscaledTime + delay;
        }

        void OnDisable()
        {
            m_SpeakAt = -1f;
            SpeechHub.Stop(speakerId);
        }

        void LateUpdate()
        {
            if (speakWhenTextChanges && m_Text.text != m_LastText)
            {
                m_LastText = m_Text.text;
                SpeechHub.Stop(speakerId);
                m_SpeakAt = Time.unscaledTime + delay;
            }

            if (m_SpeakAt >= 0f && Time.unscaledTime >= m_SpeakAt)
            {
                m_SpeakAt = -1f;
                SpeakNow();
            }
        }

        /// <summary>Read the text aloud now (e.g. from a "Read aloud" button).</summary>
        public void SpeakNow()
        {
            SpeechHub.Stop(speakerId);
            SpeechHub.Speak(speakerId, channel, SpeechHub.PlainText(m_Text.text), m_Text, emitter);
        }
    }
}
