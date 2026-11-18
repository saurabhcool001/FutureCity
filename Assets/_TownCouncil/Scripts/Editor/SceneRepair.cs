using TownCouncil.Core;
using TownCouncil.Experience;
using TownCouncil.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TownCouncil.EditorTools
{
    /// <summary>
    /// Fixes scenes whose Town Council components lost their Game Config reference: the council session,
    /// the experience director, the connect panel and the role cards. Safe to run any time.
    /// </summary>
    public static class SceneRepair
    {
        [MenuItem(TCPaths.MenuRoot + "Repair Open Scene", priority = 4)]
        static void RepairOpenScene()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(TCPaths.GameConfig);
            if (config == null)
            {
                EditorUtility.DisplayDialog("Town Council", "No Game Config found. Run Town Council > 1. Create Sample Data first.", "OK");
                return;
            }
            int fixedCount = AssignMissingConfig(config, true) + FixCylinderColliders(true);
            if (fixedCount > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Town Council",
                fixedCount > 0 ? $"Fixed {fixedCount} component(s). Save the scene to keep the fix." : "Nothing to fix: every component has its Game Config.",
                "OK");
        }

        /// <summary>
        /// Unity gives cylinders a capsule collider; on a wide flat cylinder (the round table, round floors) that
        /// capsule becomes a huge invisible ball that throws objects around. Swap them for the real shape.
        /// </summary>
        public static int FixCylinderColliders(bool log)
        {
            int count = 0;
            foreach (var capsule in Object.FindObjectsByType<CapsuleCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!capsule.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null || filter.sharedMesh.name != "Cylinder") continue;
                var go = capsule.gameObject;
                Undo.DestroyObjectImmediate(capsule);
                UseMeshCollider(go, false);
                if (log) Debug.Log($"[Town Council] Replaced the ball-shaped collider on {go.name} with its real shape.", go);
                count++;
            }
            return count;
        }

        /// <summary>Gives an object a mesh collider matching its mesh (removing any other collider).</summary>
        public static void UseMeshCollider(GameObject go, bool convex)
        {
            foreach (var c in go.GetComponents<Collider>())
                if (!(c is MeshCollider)) Object.DestroyImmediate(c);
            if (!go.TryGetComponent<MeshCollider>(out var mesh)) mesh = Undo.AddComponent<MeshCollider>(go);
            if (go.TryGetComponent<MeshFilter>(out var filter)) mesh.sharedMesh = filter.sharedMesh;
            mesh.convex = convex;
            EditorUtility.SetDirty(go);
        }

        /// <summary>Assigns the config wherever it's missing in the open scenes. Returns how many components changed.</summary>
        public static int AssignMissingConfig(GameConfig config, bool log)
        {
            int count = 0;

            foreach (var session in Object.FindObjectsByType<CouncilSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (session.Config == null) { session.AssignConfig(config); Mark(session, log); count++; }

            foreach (var director in Object.FindObjectsByType<ExperienceDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (director.Config == null) { director.AssignConfig(config); Mark(director, log); count++; }

            foreach (var panel in Object.FindObjectsByType<ConnectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.config == null) { panel.config = config; Mark(panel, log); count++; }

            foreach (var card in Object.FindObjectsByType<RoleCardView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (card.config == null) { card.config = config; Mark(card, log); count++; }

            return count;
        }

        static void Mark(Object target, bool log)
        {
            EditorUtility.SetDirty(target);
            if (log) Debug.Log($"[Town Council] Assigned Game Config to {target.name} ({target.GetType().Name}).", target);
        }
    }
}
