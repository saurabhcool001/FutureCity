using System;
using TMPro;
using TownCouncil.Core;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>What kind of speech a line is. Each kind can be switched on or off in Game Config.</summary>
    public enum SpeechChannel
    {
        /// <summary>CIVIC, the AI.</summary>
        Civic,
        /// <summary>Residents' views, heard by touching buildings.</summary>
        Resident,
        /// <summary>Important on-screen text read aloud (welcome pages and similar).</summary>
        Narration
    }

    /// <summary>
    /// Something that can speak text aloud. The TTS module (TownCouncil.Speech) provides one when the
    /// FutureCity Offline TTS package is installed; without it the game simply shows captions.
    /// </summary>
    public interface ISpeechOutput
    {
        /// <summary>True once the voice model is loaded.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Speak a line as the given speaker. If <paramref name="highlight"/> is set, its words are highlighted as
        /// they are spoken. <paramref name="emitter"/> is where the voice comes from (null = non-positional).
        /// </summary>
        void Speak(string speakerId, SpeechChannel channel, string text, TMP_Text highlight, Transform emitter);

        void Stop(string speakerId);
        void StopAll();

        /// <summary>True while the speaker is generating or playing a line.</summary>
        bool IsSpeaking(string speakerId);
    }

    /// <summary>
    /// The one place game code asks for speech. Decides whether a line may be spoken (package installed,
    /// master switch, channel switch, runtime mute) and forwards it to the speech output.
    /// Game code never references the TTS package directly, so it can be removed at any time.
    /// </summary>
    public static class SpeechHub
    {
        const string k_MutedPref = "TownCouncil.SpeechMuted";

        static bool? s_Muted;

        /// <summary>The installed speech output, or null when the TTS module isn't present.</summary>
        public static ISpeechOutput Output { get; private set; }

        /// <summary>Settings with the speech switches. Set by the experience director.</summary>
        public static GameConfig Config { get; set; }

        /// <summary>Raised when speech becomes available/unavailable or is muted/unmuted.</summary>
        public static event Action Changed;

        /// <summary>Raised for each word as it is spoken (speaker id, word index).</summary>
        public static event Action<string, int> WordSpoken;

        public static bool IsInstalled => Output != null;
        public static bool IsReady => Output != null && Output.IsReady;

        /// <summary>Runtime switch for everyone at this headset (remembered between sessions).</summary>
        public static bool Muted
        {
            get => s_Muted ??= PlayerPrefs.GetInt(k_MutedPref, 0) == 1;
            set
            {
                if (Muted == value) return;
                s_Muted = value;
                PlayerPrefs.SetInt(k_MutedPref, value ? 1 : 0);
                if (value) Output?.StopAll();
                Changed?.Invoke();
            }
        }

        public static bool HighlightWords => Config == null || Config.highlightSpokenWords;

        public static void Register(ISpeechOutput output)
        {
            Output = output;
            Changed?.Invoke();
        }

        public static void Unregister(ISpeechOutput output)
        {
            if (Output != output) return;
            Output = null;
            Changed?.Invoke();
        }

        /// <summary>Whether a line of this kind would be spoken right now.</summary>
        public static bool CanSpeak(SpeechChannel channel)
        {
            if (Output == null || Muted) return false;
            if (Config == null) return true;
            if (!Config.speechEnabled) return false;
            return channel switch
            {
                SpeechChannel.Civic => Config.speakCivic,
                SpeechChannel.Resident => Config.speakResidents,
                _ => Config.speakNarration
            };
        }

        /// <summary>Speak if allowed. Returns false when the line won't be spoken (show the caption as usual).</summary>
        public static bool Speak(string speakerId, SpeechChannel channel, string text, TMP_Text highlight = null, Transform emitter = null)
        {
            if (string.IsNullOrWhiteSpace(text) || !CanSpeak(channel)) return false;
            Output.Speak(speakerId, channel, text, HighlightWords ? highlight : null, emitter);
            return true;
        }

        public static void Stop(string speakerId) => Output?.Stop(speakerId);
        public static void StopAll() => Output?.StopAll();
        public static bool IsSpeaking(string speakerId) => Output != null && Output.IsSpeaking(speakerId);

        /// <summary>Called by the speech output for each spoken word.</summary>
        public static void RaiseWordSpoken(string speakerId, int wordIndex) => WordSpoken?.Invoke(speakerId, wordIndex);

        /// <summary>Strips TextMeshPro rich-text tags so only the words are spoken.</summary>
        public static string PlainText(string richText) =>
            string.IsNullOrEmpty(richText) ? richText : System.Text.RegularExpressions.Regex.Replace(richText, "<[^>]+>", " ").Trim();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Output = null;
            s_Muted = null;
            Changed = null;
            WordSpoken = null;
        }
    }
}
