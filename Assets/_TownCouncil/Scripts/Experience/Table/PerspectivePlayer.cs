using TMPro;
using TownCouncil.Core;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>Plays a resident's recorded view and shows the caption above the city model. Local to each headset.</summary>
    public class PerspectivePlayer : MonoBehaviour
    {
        public GameObject captionRoot;
        public TMP_Text captionText;
        [Tooltip("Caption time when a clip has no audio: seconds per word.")]
        public float secondsPerWord = 0.38f;

        public static PerspectivePlayer Instance { get; private set; }

        float m_HideAt;
        AudioSource m_Playing;

        void Awake()
        {
            Instance = this;
            if (captionRoot != null) captionRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Play(BuildingId building, PerspectiveClip clip, AudioSource source, Transform at)
        {
            if (m_Playing != null) m_Playing.Stop();

            string speaker = clip != null ? clip.speaker : building.displayName;
            string text = clip != null ? clip.subtitle : building.description;
            if (string.IsNullOrWhiteSpace(text)) text = building.displayName;

            float duration = Mathf.Max(3f, text.Split(' ').Length * secondsPerWord + 1.5f);
            if (clip != null && clip.audio != null && source != null)
            {
                source.clip = clip.audio;
                source.Play();
                m_Playing = source;
                duration = Mathf.Max(duration, clip.audio.length + 0.5f);
            }

            if (captionText != null) captionText.text = $"<b>{speaker}</b>\n“{text}”";
            if (captionRoot != null)
            {
                captionRoot.SetActive(true);
                if (at != null) captionRoot.transform.position = at.position + Vector3.up * 0.35f;
            }
            m_HideAt = Time.time + duration;
        }

        void Update()
        {
            if (captionRoot != null && captionRoot.activeSelf && Time.time >= m_HideAt) captionRoot.SetActive(false);
        }
    }
}
