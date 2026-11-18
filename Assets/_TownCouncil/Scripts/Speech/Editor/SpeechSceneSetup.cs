using SaurabhFutureCity.OfflineTTS;
using TTSModelSetup = SaurabhFutureCity.OfflineTTS.Editor.TTSModelSetup;
using TownCouncil.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TownCouncil.Speech.EditorTools
{
    /// <summary>
    /// Adds speech to the game scene: the TTS service (with the project's settings asset) and the
    /// <see cref="OfflineSpeechOutput"/> that the game talks to. Runs automatically after
    /// "Town Council > 2. Build Game Scene" and is available from the menu for an open scene.
    /// Only exists when the FutureCity Offline TTS package is installed.
    /// </summary>
    [InitializeOnLoad]
    public static class SpeechSceneSetup
    {
        static SpeechSceneSetup() => SceneBuilder.SceneBuilt += OnSceneBuilt;

        static void OnSceneBuilt(Scene scene) => AddTo(scene, false);

        [MenuItem(TCPaths.MenuRoot + "Speech/Add Speech To Open Scene", priority = 40)]
        static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (AddTo(scene, true)) EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem(TCPaths.MenuRoot + "Speech/Remove Speech From Open Scene", priority = 41)]
        static void RemoveFromOpenScene()
        {
            var output = Object.FindFirstObjectByType<OfflineSpeechOutput>(FindObjectsInactive.Include);
            if (output == null)
            {
                Debug.Log("[Town Council] The open scene has no speech output.");
                return;
            }
            Undo.DestroyObjectImmediate(output.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Town Council] Speech removed from the scene; the game shows captions only. " +
                      "The TTS service object (if any) was left in place.");
        }

        /// <summary>Returns true if something was added.</summary>
        static bool AddTo(Scene scene, bool interactive)
        {
            bool changed = false;

            if (Object.FindFirstObjectByType<OfflineTTS>(FindObjectsInactive.Include) == null)
            {
                var serviceGo = new GameObject("[OfflineTTS]");
                SceneManager.MoveGameObjectToScene(serviceGo, scene);
                var service = serviceGo.AddComponent<OfflineTTS>();
                TTSModelSetup.AssignSettings(service, TTSModelSetup.FindSettings());
                Undo.RegisterCreatedObjectUndo(serviceGo, "Add TTS service");
                changed = true;
            }

            var output = Object.FindFirstObjectByType<OfflineSpeechOutput>(FindObjectsInactive.Include);
            if (output == null)
            {
                var go = new GameObject("Speech (TTS)");
                SceneManager.MoveGameObjectToScene(go, scene);
                var root = GameObject.Find("Town Council");
                if (root != null) go.transform.SetParent(root.transform, false);
                output = go.AddComponent<OfflineSpeechOutput>();
                Undo.RegisterCreatedObjectUndo(go, "Add speech output");
                changed = true;
            }

            if (TTSModelSetup.FindSettings() == null)
                Debug.LogWarning("[Town Council] Speech added, but there's no TTS settings asset yet. " +
                                 "Run FutureCity > Plugins > OfflineTTS > Create or Locate Settings Asset (defaults are used until then).");
            if (!TTSModelSetup.ModelsPresent())
                Debug.LogWarning("[Town Council] Speech added, but the voice models aren't downloaded yet. " +
                                 "Run FutureCity > Plugins > OfflineTTS > Download Supertonic 3 Models.");

            if (interactive)
            {
                Selection.activeObject = output.gameObject;
                Debug.Log(changed ? "[Town Council] Speech added to the open scene." : "[Town Council] The open scene already has speech.");
            }
            return changed;
        }
    }
}
