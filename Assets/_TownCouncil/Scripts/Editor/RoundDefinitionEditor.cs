using TownCouncil.Core;
using UnityEditor;
using UnityEngine;

namespace TownCouncil.EditorTools
{
    /// <summary>Round inspector with one-click "Add policy": a round can hold as many policies as you like.</summary>
    [CustomEditor(typeof(RoundDefinition))]
    public class RoundDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var round = (RoundDefinition)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Policies", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"{round.PolicyCount} {(round.PolicyCount == 1 ? "policy" : "policies")} in this round. " +
                "Add as many as you like; the game plays them in list order.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add policy"))
                {
                    var policy = PolicyFactory.CreatePolicy($"{round.name}_Policy{round.PolicyCount + 1}");
                    Undo.RecordObject(round, "Add policy");
                    round.policies.Add(policy);
                    EditorUtility.SetDirty(round);
                    AssetDatabase.SaveAssets();
                    EditorGUIUtility.PingObject(policy);
                }

                if (GUILayout.Button("Create missing outcomes"))
                {
                    foreach (var p in round.policies) PolicyFactory.CreateMissingOutcomes(p);
                }
            }
        }
    }

    /// <summary>Policy inspector helpers.</summary>
    [CustomEditor(typeof(PolicyDefinition))]
    public class PolicyDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var policy = (PolicyDefinition)target;

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create missing outcomes"))
                    PolicyFactory.CreateMissingOutcomes(policy);

                if (GUILayout.Button("Add AI preference for every role"))
                {
                    var config = AssetDatabase.LoadAssetAtPath<GameConfig>(TCPaths.GameConfig);
                    PolicyFactory.AddMissingRolePreferences(policy, config != null ? config.scenario : null);
                }
            }
        }
    }
}
