using System.Collections;
using System.Collections.Generic;
using TMPro;
using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// What CIVIC says in each phase: its greeting, each round's introduction, each policy's pitch,
    /// its reaction to every result and its closing words. Shows a caption, plays the line's audio
    /// when there is one, and makes the orb pulse and breathe faster while it speaks.
    /// Lines with {tokens} (e.g. {agreementPercent}) are filled from the council's real record.
    /// </summary>
    public class CivicVoice : MonoBehaviour
    {
        public CivicOrbView orb;
        public AudioSource voice;
        public GameObject captionRoot;
        public TMP_Text captionText;
        [Tooltip("Caption time for lines without audio: seconds per word.")]
        public float secondsPerWord = 0.36f;
        public float minLineSeconds = 2.5f;

        [Header("Speech (when the TTS module is installed and switched on)")]
        [Tooltip("Speaker id used for CIVIC's voice; voices are set on the speech output.")]
        public string speakerId = "CIVIC";
        [Tooltip("Longest wait for one spoken line before moving on.")]
        public float maxSpokenLineSeconds = 30f;
        [Tooltip("Pulse the orb on each spoken word.")]
        public bool pulseOnWords = true;

        CouncilGame m_Game;
        Coroutine m_Speaking;

        struct Line
        {
            public string Text;
            public AudioClip Audio;
        }

        void Awake()
        {
            if (captionRoot != null) captionRoot.SetActive(false);
        }

        void Update()
        {
            if (m_Game != null || CouncilGame.Instance == null || !CouncilGame.Instance.IsSpawned) return;
            m_Game = CouncilGame.Instance;
            m_Game.PhaseChanged += OnPhaseChanged;
        }

        void OnEnable() => SpeechHub.WordSpoken += OnWordSpoken;

        void OnWordSpoken(string id, int _)
        {
            if (pulseOnWords && id == speakerId && orb != null) orb.Pulse(0.45f);
        }

        void OnDisable()
        {
            SpeechHub.WordSpoken -= OnWordSpoken;
            SpeechHub.Stop(speakerId);
            if (orb != null) orb.IsSpeaking = false;
            if (captionRoot != null) captionRoot.SetActive(false);
            m_Speaking = null;
        }

        void OnDestroy()
        {
            if (m_Game != null) m_Game.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (!isActiveAndEnabled) return;
            if (m_Speaking != null) StopCoroutine(m_Speaking);
            SpeechHub.Stop(speakerId);
            m_Speaking = StartCoroutine(SpeakFor(phase));
        }

        IEnumerator SpeakFor(GamePhase phase)
        {
            // Results and phase arrive together; give the results list a moment to land.
            if (phase == GamePhase.Resolving) yield return new WaitForSeconds(0.3f);

            var lines = LinesFor(phase);
            foreach (var line in lines)
                yield return Say(line);

            if (orb != null) orb.IsSpeaking = false;
            if (captionRoot != null) captionRoot.SetActive(false);
            m_Speaking = null;
        }

        IEnumerator Say(Line line)
        {
            if (string.IsNullOrWhiteSpace(line.Text)) yield break;

            if (captionText != null) captionText.text = $"<color=#FF6B29><b>CIVIC</b></color>\n{line.Text}";
            if (captionRoot != null) captionRoot.SetActive(true);
            if (orb != null)
            {
                orb.IsSpeaking = true;
                orb.Pulse(1.2f);
            }

            float duration = Mathf.Max(minLineSeconds, line.Text.Split(' ').Length * secondsPerWord);
            if (line.Audio != null && voice != null)
            {
                // Recorded or baked audio always wins over live speech.
                voice.clip = line.Audio;
                voice.Play();
                duration = Mathf.Max(duration, line.Audio.length + 0.3f);
            }
            else if (SpeechHub.Speak(speakerId, SpeechChannel.Civic, line.Text, captionText, voice != null ? voice.transform : transform))
            {
                // Spoken by the TTS module: wait until it finishes (generation + playback), within limits.
                float start = Time.time;
                yield return null;
                while (SpeechHub.IsSpeaking(speakerId) && Time.time - start < maxSpokenLineSeconds) yield return null;
                float elapsed = Time.time - start;
                yield return new WaitForSeconds(Mathf.Max(0.3f, minLineSeconds - elapsed));
                yield break;
            }
            yield return new WaitForSeconds(duration);
        }

        List<Line> LinesFor(GamePhase phase)
        {
            var lines = new List<Line>();
            var config = m_Game.Config;
            var record = m_Game.Record;

            switch (phase)
            {
                case GamePhase.WakingUp:
                    lines.Add(Text(config.civicOnWake));
                    break;

                case GamePhase.RoundIntro:
                    var round = m_Game.CurrentRound;
                    if (round != null && round.civicIntro != null) lines.Add(From(round.civicIntro, record));
                    break;

                case GamePhase.Proposal:
                    foreach (var p in m_Game.ActivePolicies)
                    {
                        var policy = m_Game.GetPolicy(p);
                        if (policy == null) continue;
                        lines.Add(policy.civicPitch != null ? From(policy.civicPitch, record) : Text(policy.summary));
                    }
                    break;

                case GamePhase.Resolving:
                    bool several = m_Game.ActivePolicies.Count > 1;
                    foreach (var p in m_Game.ActivePolicies)
                    {
                        if (!m_Game.TryGetResultEntry(m_Game.RoundIndex, p, out var result)) continue;
                        string reaction = result.Deadlock ? config.civicOnDeadlock : result.Choice switch
                        {
                            VoteChoice.Disagree => config.civicOnDisagree,
                            VoteChoice.Amend => config.civicOnAmend,
                            _ => config.civicOnAgree
                        };
                        var policy = m_Game.GetPolicy(p);
                        lines.Add(Text(several && policy != null ? $"{policy.title}: {reaction}" : reaction));
                    }
                    break;

                case GamePhase.Epilogue:
                    lines.Add(Text(record.Format(config.civicOnEpilogue)));
                    break;
            }
            return lines;
        }

        static Line Text(string text) => new() { Text = text };

        static Line From(DialogueLine line, CouncilRecord record) =>
            new() { Text = line.Format(record.ResolveToken), Audio = line.audio };
    }
}
