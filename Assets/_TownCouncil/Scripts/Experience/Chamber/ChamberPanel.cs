using System.Collections.Generic;
using System.Text;
using TMPro;
using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// The council board floating above the table. Before the game it shows who is seated; during
    /// the game it shows the year, the policy on the table, the timer, who has voted, the results,
    /// the future view's headlines and diaries, and in the epilogue the town's value fingerprint.
    /// </summary>
    public class ChamberPanel : MonoBehaviour
    {
        [Header("Before the game")]
        public GameObject preGameRoot;
        public TMP_Text statusText;
        public TMP_Text seatsText;
        [TextArea] public string completeMessage = "The council is complete.\nCIVIC is waking up…";

        [Header("During the game")]
        public GameObject gameRoot;
        public TMP_Text headerText;
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public TMP_Text timerText;
        public TMP_Text footerText;

        [Header("Epilogue")]
        public GameObject epilogueRoot;
        public RadarChartGraphic radar;
        public float labelDistance = 1.22f;

        [Header("Future view")]
        public float yearRollSeconds = 2.5f;
        public float diarySeconds = 4.5f;

        const float k_RefreshInterval = 0.1f;
        float m_NextRefresh;
        GamePhase m_LastPhase = (GamePhase)255;
        float m_PhaseStartedAt;
        readonly List<TextMeshProUGUI> m_RadarLabels = new();

        void Update()
        {
            if (Time.unscaledTime < m_NextRefresh) return;
            m_NextRefresh = Time.unscaledTime + k_RefreshInterval;

            var session = CouncilSession.Instance;
            if (session == null || !session.IsSpawned) return;

            var game = CouncilGame.Instance;
            bool running = game != null && game.IsRunning;
            SetActive(preGameRoot, !running);
            SetActive(gameRoot, running);
            SetActive(epilogueRoot, running && game.Phase == GamePhase.Epilogue);

            if (!running)
            {
                ShowPreGame(session);
                return;
            }

            if (game.Phase != m_LastPhase)
            {
                m_LastPhase = game.Phase;
                m_PhaseStartedAt = Time.time;
            }
            ShowGame(game);
        }

        // Before the game -----------------------------------------------------------------------

        void ShowPreGame(CouncilSession session)
        {
            if (seatsText != null) seatsText.text = CouncilText.SeatList(session);
            if (statusText == null) return;

            if (session.Phase == CouncilPhase.Complete)
            {
                statusText.text = completeMessage;
                return;
            }

            int seated = 0;
            for (int i = 0; i < session.SeatCount; i++)
                if (session.GetSeat(i).Stage == SeatStage.Seated && session.GetSeat(i).HasRole) seated++;
            statusText.text = $"{seated} of {session.SeatCount} councillors seated.\n{CouncilText.LobbyStatus(session)}";
        }

        // During the game -----------------------------------------------------------------------

        void ShowGame(CouncilGame game)
        {
            var scenario = game.Scenario;
            var round = game.CurrentRound;
            string town = scenario != null ? scenario.townName : "";
            int roundNumber = game.RoundIndex + 1;
            int roundCount = scenario != null ? scenario.rounds.Count : 0;

            string header = "", title = "", body = "", timer = "", footer = "";
            float left = game.SecondsLeft;

            switch (game.Phase)
            {
                case GamePhase.WakingUp:
                    header = $"{town} · {(scenario != null ? scenario.startYear : 0)}";
                    title = "The council is in session";
                    body = "CIVIC is waking up…";
                    break;

                case GamePhase.RoundIntro:
                    header = $"Round {roundNumber} of {roundCount}";
                    title = round != null ? $"{round.year}\n{round.title}" : "";
                    break;

                case GamePhase.Proposal:
                    header = RoundHeader(round);
                    title = ProposalTitle(game);
                    body = ProposalBody(game);
                    footer = "Listen to CIVIC. Touch a building on the model to hear the people it affects.";
                    break;

                case GamePhase.Deciding:
                    header = RoundHeader(round);
                    title = ProposalTitle(game);
                    body = ProposalBody(game);
                    timer = left >= 0f ? CouncilText.Countdown(left) : "";
                    footer = VoteProgressLine(game) + "\nDrop your token on Agree, Disagree or Amend. You can move it until time runs out.";
                    break;

                case GamePhase.Resolving:
                    header = RoundHeader(round);
                    title = "The council has decided";
                    body = ResultsBody(game);
                    break;

                case GamePhase.FutureView:
                    int from = round != null ? round.year : 0;
                    int to = game.NextYear;
                    float t = Mathf.Clamp01((Time.time - m_PhaseStartedAt) / Mathf.Max(0.1f, yearRollSeconds));
                    int year = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
                    header = "Time passes…";
                    title = $"{town}, {year}";
                    body = FutureBody(game);
                    break;

                case GamePhase.Epilogue:
                    header = $"{town}, {game.EpilogueYear}";
                    title = "The town you chose";
                    body = EpilogueBody(game);
                    footer = "There is no winning Newhaven. Only choosing what it values.";
                    ShowRadar(game);
                    break;
            }

            Set(headerText, header);
            Set(titleText, title);
            Set(bodyText, body);
            Set(timerText, timer);
            Set(footerText, footer);
        }

        static string RoundHeader(RoundDefinition round) => round != null ? $"{round.year} · {round.title}" : "";

        static string ProposalTitle(CouncilGame game)
        {
            var active = game.ActivePolicies;
            if (active.Count == 1)
            {
                var policy = game.GetPolicy(active[0]);
                return policy != null ? policy.title : "";
            }
            return $"{active.Count} policies on the table";
        }

        static string ProposalBody(CouncilGame game)
        {
            var sb = new StringBuilder();
            var active = game.ActivePolicies;
            foreach (var p in active)
            {
                var policy = game.GetPolicy(p);
                if (policy == null) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                if (active.Count > 1) sb.Append($"<b>{policy.title}</b>\n");
                sb.Append(policy.summary);
                if (!string.IsNullOrWhiteSpace(policy.amendWording))
                    sb.Append($"\n<size=85%><color=#F9B934>Amend:</color> {policy.amendWording}</size>");
                if (policy.requiresMajorityToOverride)
                    sb.Append("\n<size=85%><color=#FF6B29>To stop CIVIC, more than half the council must Disagree.</color></size>");
            }
            return sb.ToString();
        }

        static string VoteProgressLine(CouncilGame game)
        {
            var sb = new StringBuilder();
            foreach (var p in game.ActivePolicies)
            {
                var (voted, seats) = game.VoteProgress(p);
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(game.ActivePolicies.Count > 1 ? $"Policy {p + 1}: {voted}/{seats} voted" : $"{voted} of {seats} have voted");
            }
            return sb.ToString();
        }

        static string ResultsBody(CouncilGame game)
        {
            var sb = new StringBuilder();
            foreach (var p in game.ActivePolicies)
            {
                if (!game.TryGetResultEntry(game.RoundIndex, p, out var r)) continue;
                var policy = game.GetPolicy(p);
                if (sb.Length > 0) sb.Append("\n\n");
                if (policy != null) sb.Append($"<b>{policy.title}</b>\n");
                sb.Append($"<color=#4CC774>Agree {r.Agree}</color>   <color=#E64D47>Disagree {r.Disagree}</color>   <color=#F9B934>Amend {r.Amend}</color>");
                if (r.Abstain > 0) sb.Append($"   <color=#9A9A9A>No vote {r.Abstain}</color>");
                sb.Append('\n');
                sb.Append(r.Deadlock
                    ? "<b><color=#FF6B29>No decision: CIVIC proceeds with its plan.</color></b>"
                    : $"<b>Decision: {r.Choice}</b>");
            }
            return sb.ToString();
        }

        string FutureBody(CouncilGame game)
        {
            var headlines = new StringBuilder();
            var diaries = new List<string>();
            foreach (var r in game.Record.Results)
            {
                if (r.RoundIndex != game.RoundIndex) continue;
                var outcome = game.Record.GetPolicy(r.RoundIndex, r.PolicyIndex)?.OutcomeFor(r.Choice);
                if (outcome == null) continue;
                if (!string.IsNullOrWhiteSpace(outcome.headline))
                    headlines.Append($"<b>“{outcome.headline}”</b>\n");
                if (!string.IsNullOrWhiteSpace(outcome.diaryText))
                    diaries.Add($"<i>{outcome.diaryText}</i>\n<size=80%><color=#9A9A9A>— {outcome.diaryAuthor}, {outcome.diaryYear}</color></size>");
            }

            if (diaries.Count == 0) return headlines.ToString();
            // After the year has rolled, cycle through the diary entries.
            float since = Time.time - m_PhaseStartedAt - yearRollSeconds;
            int index = since < 0f ? 0 : Mathf.FloorToInt(since / Mathf.Max(1f, diarySeconds)) % diaries.Count;
            return $"{headlines}\n{diaries[index]}";
        }

        static string EpilogueBody(CouncilGame game)
        {
            var record = game.Record;
            int deadlocks = record.Deadlocks;
            return $"Your council matched CIVIC's recommendations {record.AgreementPercent}% of the time " +
                   $"and deadlocked {deadlocks} {(deadlocks == 1 ? "time" : "times")}.\n" +
                   "Every secret is now revealed above its seat.";
        }

        void ShowRadar(CouncilGame game)
        {
            var scenario = game.Scenario;
            if (radar == null || scenario == null) return;

            var values = new List<float>();
            foreach (var meter in scenario.meters) values.Add(game.Record.Normalized(meter));
            radar.SetValues(values);

            while (m_RadarLabels.Count < scenario.meters.Count)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(radar.transform, false);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 26f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                ((RectTransform)go.transform).sizeDelta = new Vector2(220f, 60f);
                m_RadarLabels.Add(tmp);
            }

            for (int i = 0; i < m_RadarLabels.Count; i++)
            {
                var meter = i < scenario.meters.Count ? scenario.meters[i] : null;
                m_RadarLabels[i].gameObject.SetActive(meter != null);
                if (meter == null) continue;
                m_RadarLabels[i].text = $"{meter.displayName}\n<b>{game.Record.ValueOf(meter)}</b>/{meter.maxValue}";
                m_RadarLabels[i].color = meter.color;
                ((RectTransform)m_RadarLabels[i].transform).anchoredPosition = radar.AxisPoint(i, labelDistance);
            }
        }

        static void Set(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
