using System;
using System.Collections.Generic;
using SaurabhFutureCity.OfflineTTS;
using TMPro;
using TownCouncil.Core;
using TownCouncil.Experience;
using UnityEngine;

namespace TownCouncil.Speech
{
    /// <summary>
    /// Connects the game's <see cref="SpeechHub"/> to FutureCity Offline TTS. One TTS speaker per speaker id
    /// (CIVIC, the narrator, each resident), each with its own voice; words are highlighted in the caption
    /// that shows the line. This whole assembly only compiles when the TTS package is installed, so removing
    /// the package (or this component) leaves the game running with captions only.
    /// </summary>
    [DisallowMultipleComponent]
    public class OfflineSpeechOutput : MonoBehaviour, ISpeechOutput
    {
        [Serializable]
        public class VoiceProfile
        {
            public string speakerId = "CIVIC";
            [Tooltip("F1–F5 or M1–M5.")]
            public string voice = "M3";
            [Tooltip("Speaking rate. -1 = settings default.")]
            public float speed = -1f;
            [Tooltip("Positional voice (comes from the emitter). Off = same volume everywhere.")]
            public bool spatial = true;
        }

        public string language = "en";
        public List<VoiceProfile> voices = new()
        {
            new VoiceProfile { speakerId = "CIVIC", voice = "M3", speed = 0.95f, spatial = true },
            new VoiceProfile { speakerId = "Narrator", voice = "F2", speed = 1f, spatial = false }
        };
        [Tooltip("Residents get a voice from this list, chosen by name, so each resident always sounds the same.")]
        public string[] residentVoices = { "F1", "M1", "F4", "M2", "F5", "M4" };

        [Header("Start-up")]
        [Tooltip("Generate CIVIC's fixed lines in the background at start, so they play without delay.")]
        public bool prewarmCivicLines = true;

        readonly Dictionary<string, TTSSpeaker> m_Speakers = new();
        readonly Dictionary<string, TTSWordHighlighter> m_BoundHighlighters = new();

        public bool IsReady => OfflineTTS.Instance != null && OfflineTTS.Instance.IsReady;

        void OnEnable()
        {
            OfflineTTS.GetOrCreate();
            SpeechHub.Register(this);
        }

        void OnDisable()
        {
            StopAll();
            SpeechHub.Unregister(this);
        }

        async void Start()
        {
            if (!prewarmCivicLines) return;
            try
            {
                var service = OfflineTTS.GetOrCreate();
                await service.InitializeAsync();
                var lines = FixedCivicLines();
                if (lines.Count > 0 && this != null)
                    await SpeakerFor("CIVIC", SpeechChannel.Civic).PrewarmAsync(lines);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Town Council] Couldn't prepare CIVIC's lines: {e.Message}");
            }
        }

        // ISpeechOutput ------------------------------------------------------------------------

        public void Speak(string speakerId, SpeechChannel channel, string text, TMP_Text highlight, Transform emitter)
        {
            var speaker = SpeakerFor(speakerId, channel);
            if (speaker == null) return;

            if (emitter != null && speaker.transform.parent != emitter)
            {
                speaker.transform.SetParent(emitter, false);
                speaker.transform.localPosition = Vector3.zero;
            }

            Highlight(speakerId, speaker, highlight);
            speaker.Speak(text);
        }

        public void Stop(string speakerId)
        {
            if (m_Speakers.TryGetValue(speakerId, out var speaker) && speaker != null) speaker.Stop();
        }

        public void StopAll()
        {
            foreach (var speaker in m_Speakers.Values)
                if (speaker != null) speaker.Stop();
        }

        public bool IsSpeaking(string speakerId) =>
            m_Speakers.TryGetValue(speakerId, out var speaker) && speaker != null && (speaker.IsSpeaking || speaker.IsGenerating);

        // Speakers ------------------------------------------------------------------------------

        TTSSpeaker SpeakerFor(string speakerId, SpeechChannel channel)
        {
            if (m_Speakers.TryGetValue(speakerId, out var existing) && existing != null) return existing;

            var profile = ProfileFor(speakerId, channel);
            var go = new GameObject($"Voice · {speakerId}");
            go.transform.SetParent(transform, false);
            var speaker = TTSSpeaker.AddTo(go, profile.voice, profile.spatial);
            speaker.language = language;
            speaker.speed = profile.speed;
            speaker.interrupt = true;
            if (!profile.spatial) speaker.Source.spatialBlend = 0f;

            string id = speakerId;
            speaker.onWordChanged.AddListener(word => SpeechHub.RaiseWordSpoken(id, word));
            m_Speakers[speakerId] = speaker;
            return speaker;
        }

        VoiceProfile ProfileFor(string speakerId, SpeechChannel channel)
        {
            foreach (var v in voices)
                if (v != null && v.speakerId == speakerId) return v;

            if (channel == SpeechChannel.Resident && residentVoices.Length > 0)
            {
                int hash = 0;
                foreach (var c in speakerId) hash = hash * 31 + c;
                return new VoiceProfile { speakerId = speakerId, voice = residentVoices[Mathf.Abs(hash) % residentVoices.Length], spatial = true };
            }
            return new VoiceProfile { speakerId = speakerId, voice = "F1", spatial = channel != SpeechChannel.Narration };
        }

        void Highlight(string speakerId, TTSSpeaker speaker, TMP_Text text)
        {
            // Release the text this speaker highlighted last time.
            if (m_BoundHighlighters.TryGetValue(speakerId, out var previous) && previous != null &&
                (text == null || previous.gameObject != text.gameObject))
                previous.Bind(null);

            if (text == null)
            {
                m_BoundHighlighters.Remove(speakerId);
                return;
            }

            if (!text.TryGetComponent<TTSWordHighlighter>(out var highlighter))
                highlighter = text.gameObject.AddComponent<TTSWordHighlighter>();
            highlighter.listenTo = TTSWordHighlighter.ListenMode.BoundFromCode;
            highlighter.Bind(speaker);
            m_BoundHighlighters[speakerId] = highlighter;
        }

        List<string> FixedCivicLines()
        {
            var lines = new List<string>();
            var config = SpeechHub.Config;
            if (config == null) return lines;

            void Add(string line)
            {
                // Lines with {tokens} change every game, so they can't be prepared in advance.
                if (!string.IsNullOrWhiteSpace(line) && !line.Contains("{") && !lines.Contains(line)) lines.Add(line);
            }

            Add(config.civicOnWake);
            Add(config.civicOnAgree);
            Add(config.civicOnDisagree);
            Add(config.civicOnAmend);
            Add(config.civicOnDeadlock);
            Add(config.civicOnEpilogue);

            var scenario = config.scenario;
            if (scenario == null) return lines;
            foreach (var round in scenario.rounds)
            {
                if (round == null) continue;
                if (round.civicIntro != null) Add(round.civicIntro.subtitle);
                foreach (var policy in round.policies)
                    if (policy != null && policy.civicPitch != null) Add(policy.civicPitch.subtitle);
            }
            return lines;
        }
    }
}
