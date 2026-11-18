using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>Tuning that isn't story content: lobby rules, timers, AI mode, transitions.</summary>
    [CreateAssetMenu(menuName = "Town Council/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public ScenarioDefinition scenario;

        [Header("Lobby")]
        [Tooltip("Seconds to wait for more players before AI councillors fill the empty seats.")]
        [Min(5f)] public float lobbyWaitSeconds = 60f;
        [Tooltip("Restart the countdown each time another person joins.")]
        public bool restartCountdownOnJoin = true;
        [Tooltip("Fewest people needed before the AI may fill the rest. 1 = a single headset can play.")]
        [Min(1)] public int minHumansToStart = 1;
        [Tooltip("How AI councillors decide.")]
        public AIMode aiMode = AIMode.Fixed;

        [Header("Decisions")]
        [Tooltip("Used by policies that leave their own decision time at 0.")]
        [Min(5f)] public float defaultDecisionSeconds = 60f;
        [Tooltip("Once every seat has voted, cut the remaining time to this many seconds.")]
        [Min(0f)] public float allVotedShortcutSeconds = 5f;

        [Tooltip("Show which slot each councillor picked while the vote is open. Off = only show who has voted, reveal at the end.")]
        public bool showVotesLive = false;
        [Tooltip("AI councillors vote at a random moment within this part of the decision window (0-1).")]
        public Vector2 aiVoteWindow = new(0.2f, 0.7f);

        [Header("Round pacing (seconds)")]
        [Min(0f)] public float councilWakeSeconds = 5f;
        [Min(1f)] public float roundIntroSeconds = 8f;
        [Tooltip("Per policy. With 'All at once' presentation it's multiplied by the number of policies.")]
        [Min(1f)] public float proposalSeconds = 10f;
        [Min(1f)] public float resolveSeconds = 6f;
        [Min(1f)] public float futureViewSeconds = 14f;
        [Tooltip("Years added after the last round when no next round sets the year.")]
        [Min(1)] public int epilogueYearsAfterLastRound = 1;

        [Header("CIVIC's reactions")]
        [TextArea] public string civicOnAgree = "Thank you. I will proceed.";
        [TextArea] public string civicOnDisagree = "Noted. The policy is withdrawn.";
        [TextArea] public string civicOnAmend = "Understood. I will implement the amended version.";
        [TextArea] public string civicOnDeadlock = "You could not agree. I will proceed as recommended.";
        [TextArea] public string civicOnWake = "Good evening, council. Let us begin.";
        [TextArea] public string civicOnEpilogue = "Thank you for your service, council. This is the town you chose.";

        [Header("Speech (needs the FutureCity Offline TTS package)")]
        [Tooltip("Master switch. Off = captions only, even when the TTS package is installed.")]
        public bool speechEnabled = true;
        [Tooltip("CIVIC speaks its lines aloud.")]
        public bool speakCivic = true;
        [Tooltip("Residents speak when a building is touched (lines without recorded audio).")]
        public bool speakResidents = true;
        [Tooltip("Important on-screen text marked as speakable (e.g. the welcome pages) is read aloud.")]
        public bool speakNarration = true;
        [Tooltip("Highlight each word on screen as it is spoken.")]
        public bool highlightSpokenWords = true;

        [Header("Transitions")]
        [Min(0.05f)] public float fadeOutSeconds = 0.8f;
        [Min(0.05f)] public float fadeInSeconds = 1.0f;
        [Tooltip("Pause on black between areas, so the move feels deliberate.")]
        [Min(0f)] public float holdOnBlackSeconds = 0.4f;
        [Tooltip("Pause after a card is accepted before the fade to the role's room.")]
        [Min(0f)] public float cardRevealSeconds = 1.5f;

        public int SeatCount => scenario != null ? scenario.SeatCount : 4;
    }
}
