using System.Text;
using TownCouncil.Networking;

namespace TownCouncil.Experience
{
    /// <summary>Shared wording for seat lists and countdowns, so every panel says the same thing.</summary>
    public static class CouncilText
    {
        public static string Countdown(float seconds)
        {
            int s = UnityEngine.Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        public static string SeatLine(CouncilSession session, int index)
        {
            var seat = session.GetSeat(index);
            var role = seat.HasRole ? session.GetRole(seat.RoleIndex) : null;
            string who = seat.Occupant switch
            {
                SeatOccupant.Empty => "Empty seat",
                SeatOccupant.AI => role != null ? $"{role.CardTitle} · AI" : "AI citizen (choosing)",
                _ => seat.Name.ToString()
            };

            if (seat.IsHuman && role != null) who += $" as {role.displayName}";

            string where = seat.Occupant switch
            {
                SeatOccupant.Empty => "",
                SeatOccupant.AI => seat.HasRole ? "seated" : "waiting for people to pick",
                _ => seat.Stage switch
                {
                    SeatStage.Joined => "choosing a card",
                    SeatStage.RoleChosen => "card picked",
                    SeatStage.InRoleRoom => "reading their role",
                    _ => "seated"
                }
            };

            bool isLocal = index == session.LocalSeatIndex;
            var line = $"{index + 1}. {who}";
            if (where.Length > 0) line += $"  <color=#9A9A9A>— {where}</color>";
            if (isLocal) line = $"<b>{line}  (you)</b>";
            return line;
        }

        public static string SeatList(CouncilSession session)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < session.SeatCount; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(SeatLine(session, i));
            }
            return sb.ToString();
        }

        public static string LobbyStatus(CouncilSession session)
        {
            switch (session.Phase)
            {
                case CouncilPhase.Gathering:
                    float left = session.SecondsUntilAIFill;
                    int empty = session.SeatCount - session.HumanCount;
                    if (left < 0f) return "Waiting for players…";
                    return empty == 1
                        ? $"Waiting for 1 more person. AI fills the empty seat in {Countdown(left)}"
                        : $"Waiting for {empty} more people. AI fills the empty seats in {Countdown(left)}";
                case CouncilPhase.Locked:
                    return session.AICount > 0
                        ? $"The session is set: {session.HumanCount} citizens and {session.AICount} AI's."
                        : "The session is set: every seat has a citizen.";
                default:
                    return "The session is complete.";
            }
        }
    }
}
