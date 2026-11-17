using System.Collections.Generic;
using TownCouncil.Core;
using TownCouncil.Core.AI;
using TownCouncil.Experience;
using TownCouncil.Networking;
using UnityEditor;
using UnityEngine;
using XRMultiplayer;

namespace TownCouncil.EditorTools
{
    /// <summary>
    /// Play-mode window for testing without a full group: see every seat, skip the lobby
    /// countdown, jump between areas and preview how AI councillors would vote.
    /// </summary>
    public class TownCouncilDebugWindow : EditorWindow
    {
        Vector2 m_Scroll;
        int m_RoundIndex;

        [MenuItem(TCPaths.MenuRoot + "Debug Window", priority = 21)]
        public static void Open() => GetWindow<TownCouncilDebugWindow>("Town Council Debug");

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see the live council. Tip: the template's XR Device Simulator " +
                                        "lets you play without a headset, and Multiplayer Play Mode adds extra players.", MessageType.Info);
                DrawSetupButtons();
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawConnection();
            var session = CouncilSession.Instance;
            if (session != null && session.IsSpawned)
            {
                DrawSession(session);
                DrawAIPreview(session);
            }
            DrawDirector();
            EditorGUILayout.EndScrollView();
        }

        static void DrawSetupButtons()
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("1. Create Sample Data")) SampleDataBuilder.CreateSampleData();
            if (GUILayout.Button("2. Build Game Scene")) SceneBuilder.BuildScene();
            if (GUILayout.Button("Validate Scenario")) ScenarioValidatorWindow.Open();
            if (GUILayout.Button("Select Game Config"))
            {
                var config = AssetDatabase.LoadAssetAtPath<GameConfig>(TCPaths.GameConfig);
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
        }

        static void DrawConnection()
        {
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State", XRINetworkGameManager.CurrentConnectionState.Value.ToString());
            EditorGUILayout.LabelField("Council code", string.IsNullOrEmpty(XRINetworkGameManager.ConnectedRoomCode) ? "—" : XRINetworkGameManager.ConnectedRoomCode);
        }

        static void DrawSession(CouncilSession session)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Council", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Phase", session.Phase.ToString());
            EditorGUILayout.LabelField("This headset runs the rules", session.HasAuthority ? "Yes" : "No");
            float left = session.SecondsUntilAIFill;
            EditorGUILayout.LabelField("AI fills empty seats in", left < 0 ? "—" : $"{left:0.0} s");

            for (int i = 0; i < session.SeatCount; i++)
            {
                var seat = session.GetSeat(i);
                var role = seat.HasRole ? session.GetRole(seat.RoleIndex) : null;
                EditorGUILayout.LabelField($"Seat {i + 1}{(i == session.LocalSeatIndex ? " (you)" : "")}",
                    $"{seat.Occupant} · {(role != null ? role.displayName : "no role")} · {seat.Stage} · {seat.Name}");
            }

            using (new EditorGUI.DisabledScope(session.Phase != CouncilPhase.Gathering))
                if (GUILayout.Button("Fill empty seats with AI now")) session.RequestAIFillNow();
        }

        void DrawAIPreview(CouncilSession session)
        {
            var scenario = session.Scenario;
            if (scenario == null || scenario.rounds.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"AI vote preview ({session.Config.aiMode} mode)", EditorStyles.boldLabel);

            var names = new string[scenario.rounds.Count];
            for (int i = 0; i < names.Length; i++) names[i] = scenario.rounds[i] != null ? scenario.rounds[i].DisplayName : "(empty)";
            m_RoundIndex = Mathf.Clamp(EditorGUILayout.Popup("Round", m_RoundIndex, names), 0, names.Length - 1);

            var round = scenario.rounds[m_RoundIndex];
            if (round == null) return;

            var context = new CouncillorContext { OtherVotes = new List<VoteChoice>() };
            foreach (var policy in round.policies)
            {
                if (policy == null) continue;
                var line = new List<string>();
                for (int i = 0; i < session.SeatCount; i++)
                {
                    var brain = session.GetBrain(i);
                    if (brain != null) line.Add($"{brain.Role.displayName}: {brain.DecideVote(policy, context)}");
                }
                EditorGUILayout.LabelField(policy.title, line.Count > 0 ? string.Join(", ", line) : "No AI seats yet", EditorStyles.wordWrappedLabel);
            }
        }

        static void DrawDirector()
        {
            var director = ExperienceDirector.Instance;
            if (director == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Local player", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stage", director.CurrentStage + (director.IsTransitioning ? " (moving)" : ""));
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (ExperienceDirector.Stage stage in System.Enum.GetValues(typeof(ExperienceDirector.Stage)))
                    if (GUILayout.Button(stage.ToString())) director.GoTo(stage);
            }
        }
    }
}
