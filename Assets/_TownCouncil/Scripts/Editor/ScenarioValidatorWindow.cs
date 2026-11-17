using System.Collections.Generic;
using TownCouncil.Core;
using TownCouncil.Experience;
using UnityEditor;
using UnityEngine;

namespace TownCouncil.EditorTools
{
    /// <summary>
    /// Checks the scenario for gaps before anyone plays it: missing outcomes, empty text,
    /// buildings without a view on the city model, meters outside the scenario, and so on.
    /// Click a row to select the asset.
    /// </summary>
    public class ScenarioValidatorWindow : EditorWindow
    {
        enum Severity { Error, Warning, Info }

        struct Issue
        {
            public Severity Severity;
            public string Message;
            public Object Context;
        }

        readonly List<Issue> m_Issues = new();
        Vector2 m_Scroll;
        GameConfig m_Config;

        [MenuItem(TCPaths.MenuRoot + "Validate Scenario", priority = 20)]
        public static void Open()
        {
            var w = GetWindow<ScenarioValidatorWindow>("Scenario Validator");
            w.minSize = new Vector2(520, 320);
            w.Run();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                m_Config = (GameConfig)EditorGUILayout.ObjectField(m_Config, typeof(GameConfig), false, GUILayout.Width(260));
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(80))) Run();
                GUILayout.FlexibleSpace();
                int errors = 0, warnings = 0;
                foreach (var i in m_Issues)
                {
                    if (i.Severity == Severity.Error) errors++;
                    else if (i.Severity == Severity.Warning) warnings++;
                }
                GUILayout.Label($"{errors} errors · {warnings} warnings", EditorStyles.miniLabel);
            }

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            if (m_Issues.Count == 0) EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
            foreach (var issue in m_Issues)
            {
                var type = issue.Severity switch
                {
                    Severity.Error => MessageType.Error,
                    Severity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.Message, type);
                    if (issue.Context != null && GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(38)))
                    {
                        Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void Run()
        {
            m_Issues.Clear();
            if (m_Config == null) m_Config = AssetDatabase.LoadAssetAtPath<GameConfig>(TCPaths.GameConfig);
            if (m_Config == null) { Add(Severity.Error, "No Game Config found. Run Town Council > 1. Create Sample Data.", null); return; }

            var s = m_Config.scenario;
            if (s == null) { Add(Severity.Error, "Game Config has no Scenario.", m_Config); return; }

            if (s.introPages.Count == 0) Add(Severity.Warning, "The scenario has no welcome pages.", s);
            if (s.roles.Count == 0) Add(Severity.Error, "The scenario has no roles (roles set the number of seats).", s);
            if (m_Config.minHumansToStart > s.roles.Count) Add(Severity.Error, "Min humans to start is more than the number of seats.", m_Config);

            foreach (var role in s.roles)
            {
                if (role == null) { Add(Severity.Error, "The scenario has an empty role slot.", s); continue; }
                if (string.IsNullOrWhiteSpace(role.hiddenGoal)) Add(Severity.Warning, $"{role.name}: no hidden goal.", role);
                if (string.IsNullOrWhiteSpace(role.secret)) Add(Severity.Warning, $"{role.name}: no secret.", role);
                if (m_Config.aiMode == AIMode.Dynamic && role.valuePriorities.Count == 0)
                    Add(Severity.Warning, $"{role.name}: Dynamic AI is on but the role has no value priorities.", role);
            }

            if (s.rounds.Count == 0) Add(Severity.Error, "The scenario has no rounds.", s);
            foreach (var round in s.rounds)
            {
                if (round == null) { Add(Severity.Error, "The scenario has an empty round slot.", s); continue; }
                if (round.PolicyCount == 0) Add(Severity.Error, $"{round.name}: no policies.", round);
                foreach (var policy in round.policies) CheckPolicy(s, round, policy);
            }

            CheckSceneBuildings(s);
            Add(Severity.Info, $"{s.roles.Count} seats, {s.rounds.Count} rounds, {s.TotalPolicyCount} policies.", s);
        }

        void CheckPolicy(ScenarioDefinition s, RoundDefinition round, PolicyDefinition policy)
        {
            if (policy == null) { Add(Severity.Error, $"{round.name}: empty policy slot.", round); return; }
            var label = $"{round.name} › {policy.name}";
            if (string.IsNullOrWhiteSpace(policy.title)) Add(Severity.Warning, $"{label}: no title.", policy);
            if (policy.civicPitch == null) Add(Severity.Warning, $"{label}: no CIVIC pitch line.", policy);
            if (string.IsNullOrWhiteSpace(policy.amendWording)) Add(Severity.Warning, $"{label}: no amend wording.", policy);
            if (policy.title != null && policy.title.StartsWith("[Draft]")) Add(Severity.Info, $"{label}: still marked as a draft.", policy);

            CheckOutcome(s, label, "Agree", policy.onAgree, policy);
            CheckOutcome(s, label, "Disagree", policy.onDisagree, policy);
            CheckOutcome(s, label, "Amend", policy.onAmend, policy);

            if (m_Config.aiMode == AIMode.Fixed)
                foreach (var role in s.roles)
                    if (role != null && !policy.TryGetPreference(role, out _))
                        Add(Severity.Info, $"{label}: no Fixed-AI vote for {role.displayName}; uses their default stance.", policy);
        }

        void CheckOutcome(ScenarioDefinition s, string label, string which, PolicyOutcome outcome, Object context)
        {
            if (outcome == null) { Add(Severity.Error, $"{label}: missing {which} outcome.", context); return; }
            if (string.IsNullOrWhiteSpace(outcome.headline)) Add(Severity.Warning, $"{label} › {which}: no headline.", outcome);
            foreach (var d in outcome.meterDeltas)
                if (d.meter == null || !s.meters.Contains(d.meter))
                    Add(Severity.Error, $"{label} › {which}: a meter change uses a meter that isn't in the scenario.", outcome);
            foreach (var c in outcome.cityChanges)
            {
                if (c.building == null || !s.buildings.Contains(c.building))
                    Add(Severity.Error, $"{label} › {which}: a city change uses a building that isn't in the scenario.", outcome);
                if (c.state == null)
                    Add(Severity.Warning, $"{label} › {which}: a city change has no target state.", outcome);
            }
        }

        void CheckSceneBuildings(ScenarioDefinition s)
        {
            var views = FindObjectsByType<BuildingView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (views.Length == 0)
            {
                Add(Severity.Info, "Open the game scene to also check the city model's buildings.", null);
                return;
            }
            var present = new HashSet<BuildingId>();
            foreach (var v in views) if (v.building != null) present.Add(v.building);
            foreach (var b in s.buildings)
                if (b != null && !present.Contains(b))
                    Add(Severity.Warning, $"{b.displayName} has no BuildingView on the city model in the open scene.", b);
        }

        void Add(Severity severity, string message, Object context) =>
            m_Issues.Add(new Issue { Severity = severity, Message = message, Context = context });
    }
}
