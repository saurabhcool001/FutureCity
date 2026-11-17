using TownCouncil.Core;
using UnityEditor;
using UnityEngine;

namespace TownCouncil.EditorTools
{
    /// <summary>Creates policies together with their three outcome assets, so nothing is ever half set up.</summary>
    public static class PolicyFactory
    {
        public static PolicyDefinition CreatePolicy(string assetName, string title = null)
        {
            assetName = TCPaths.SafeFileName(assetName);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{TCPaths.Policies}/{assetName}.asset");
            TCPaths.EnsureFolder(TCPaths.Policies);

            var policy = ScriptableObject.CreateInstance<PolicyDefinition>();
            policy.title = string.IsNullOrEmpty(title) ? "New policy" : title;
            AssetDatabase.CreateAsset(policy, path);
            CreateMissingOutcomes(policy);
            return policy;
        }

        /// <summary>Adds any missing Agree / Disagree / Amend outcome assets next to the policy's name.</summary>
        public static bool CreateMissingOutcomes(PolicyDefinition policy)
        {
            if (policy == null) return false;
            bool changed = false;
            Undo.RecordObject(policy, "Create outcomes");
            if (policy.onAgree == null) { policy.onAgree = CreateOutcome(policy, "Agree"); changed = true; }
            if (policy.onDisagree == null) { policy.onDisagree = CreateOutcome(policy, "Disagree"); changed = true; }
            if (policy.onAmend == null) { policy.onAmend = CreateOutcome(policy, "Amend"); changed = true; }
            if (changed)
            {
                EditorUtility.SetDirty(policy);
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        /// <summary>Adds a Fixed-AI preference row for every scenario role the policy doesn't cover yet.</summary>
        public static void AddMissingRolePreferences(PolicyDefinition policy, ScenarioDefinition scenario)
        {
            if (policy == null || scenario == null) return;
            Undo.RecordObject(policy, "Add AI preferences");
            foreach (var role in scenario.roles)
            {
                if (role == null) continue;
                bool found = false;
                foreach (var p in policy.aiPreferences) if (p.role == role) { found = true; break; }
                if (!found) policy.aiPreferences.Add(new RoleVotePreference { role = role, choice = role.defaultStance });
            }
            EditorUtility.SetDirty(policy);
        }

        static PolicyOutcome CreateOutcome(PolicyDefinition policy, string suffix)
        {
            TCPaths.EnsureFolder(TCPaths.Outcomes);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{TCPaths.Outcomes}/{policy.name}_{suffix}.asset");
            var outcome = ScriptableObject.CreateInstance<PolicyOutcome>();
            AssetDatabase.CreateAsset(outcome, path);
            return outcome;
        }
    }
}
