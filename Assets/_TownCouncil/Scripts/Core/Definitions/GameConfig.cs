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
