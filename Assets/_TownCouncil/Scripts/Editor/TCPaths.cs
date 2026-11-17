using System.IO;
using UnityEditor;
using UnityEngine;

namespace TownCouncil.EditorTools
{
    /// <summary>Where generated assets live. Change here to move everything.</summary>
    public static class TCPaths
    {
        public const string Root = "Assets/_TownCouncil";
        public const string Data = Root + "/Data";
        public const string Meters = Data + "/Meters";
        public const string Buildings = Data + "/Buildings";
        public const string BuildingStates = Data + "/BuildingStates";
        public const string Roles = Data + "/Roles";
        public const string Rounds = Data + "/Rounds";
        public const string Policies = Data + "/Policies";
        public const string Outcomes = Data + "/Outcomes";
        public const string Dialogue = Data + "/Dialogue";
        public const string Materials = Root + "/Art/Materials";
        public const string Scenes = Root + "/Scenes";

        public const string GameConfig = Data + "/GameConfig.asset";
        public const string Scenario = Data + "/Scenario_Newhaven.asset";
        public const string GameScene = Scenes + "/TownCouncil.unity";

        public const string TemplateBaseScene = "Assets/Scenes/BasicScene.unity";

        public const string MenuRoot = "Town Council/";

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Loads the asset at path, or creates it. Returns true when it was just created.</summary>
        public static bool LoadOrCreate<T>(string path, out T asset) where T : ScriptableObject
        {
            asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return false;
            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return true;
        }

        public static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
