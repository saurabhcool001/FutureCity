using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using TownCouncil.Core;
using TownCouncil.Experience;
using TownCouncil.Networking;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TownCouncil.EditorTools
{
    /// <summary>
    /// Builds a playable Town Council scene from the template's BasicScene (which already has the
    /// XR rig, the network managers and the session setup): welcome space, foyer with role cards,
    /// one room per role and the council chamber with the round table and city model.
    /// Everything it makes is placeholder geometry for the designer to replace.
    /// </summary>
    public static class SceneBuilder
    {
        static readonly string[] k_TemplateRootsToRemove = { "Environment", "NetworkInteractables", "Connection Canvas" };

        static readonly Vector3 k_WelcomePos = Vector3.zero;
        static readonly Vector3 k_FoyerPos = new(0f, 0f, 40f);
        static readonly Vector3 k_ChamberPos = new(0f, 0f, -40f);
        // Everything stays within 75 m of the origin: the template's CharacterResetter resets players beyond that.
        static readonly Vector3 k_RoleRoomsStart = new(55f, 0f, -45f);
        const float k_RoleRoomSpacing = 30f;

        [MenuItem(TCPaths.MenuRoot + "2. Build Game Scene", priority = 2)]
        public static void BuildScene()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(TCPaths.GameConfig);
            if (config == null || config.scenario == null)
            {
                if (!EditorUtility.DisplayDialog("Town Council", "There's no sample data yet. Create it now?", "Create", "Cancel"))
                    return;
                SampleDataBuilder.CreateSampleData();
                config = AssetDatabase.LoadAssetAtPath<GameConfig>(TCPaths.GameConfig);
                if (config == null) return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TCPaths.TemplateBaseScene) == null)
            {
                EditorUtility.DisplayDialog("Town Council", $"Can't find the template scene at {TCPaths.TemplateBaseScene}.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TCPaths.GameScene) != null)
            {
                if (!EditorUtility.DisplayDialog("Town Council",
                        $"{TCPaths.GameScene} already exists. Rebuild it? Changes made inside that scene will be lost.",
                        "Rebuild", "Cancel"))
                    return;
                AssetDatabase.DeleteAsset(TCPaths.GameScene);
            }

            TCPaths.EnsureFolder(TCPaths.Scenes);
            if (!AssetDatabase.CopyAsset(TCPaths.TemplateBaseScene, TCPaths.GameScene))
            {
                Debug.LogError("[Town Council] Couldn't copy the template scene.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TCPaths.GameScene, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (k_TemplateRootsToRemove.Contains(root.name)) Object.DestroyImmediate(root);

            new Builder(config).Build(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(TCPaths.GameScene);
            Debug.Log("[Town Council] Scene built and set as the first scene in Build Settings. Press Play to try it " +
                      "(the XR Device Simulator works without a headset).");
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------------------------------

        sealed class Builder
        {
            readonly GameConfig m_Config;
            readonly ScenarioDefinition m_Scenario;
            readonly Dictionary<string, Material> m_Materials = new();

            Transform m_Root;

            public Builder(GameConfig config)
            {
                m_Config = config;
                m_Scenario = config.scenario;
            }

            public void Build(Scene scene)
            {
                m_Root = new GameObject("Town Council").transform;
                EnsureSun();

                var systems = new GameObject("Experience Systems");
                systems.transform.SetParent(m_Root, false);
                var rig = systems.AddComponent<PlayerRig>();
                var fader = systems.AddComponent<ScreenFader>();
                var director = systems.AddComponent<ExperienceDirector>();

                BuildCouncilSession();

                var welcome = BuildWelcome(out var welcomePanel, out var connectPanel);
                var foyer = BuildFoyer();
                var rooms = new List<ExperienceArea>();
                for (int i = 0; i < m_Scenario.roles.Count; i++) rooms.Add(BuildRoleRoom(i));
                var roleRoomPanel = BuildRoleRoomPanel(systems.transform, rooms.Count > 0 ? rooms[0].panelAnchor : null);
                var chamber = BuildChamber(out var seats, out var gallery);
                BuildEndingPanel(systems.transform, seats);

                Set(director, "m_Config", m_Config);
                Set(director, "m_Fader", fader);
                Set(director, "m_Rig", rig);
                Set(director, "m_WelcomeArea", welcome);
                Set(director, "m_FoyerArea", foyer);
                SetList(director, "m_RoleRooms", rooms.Cast<Object>().ToList());
                Set(director, "m_ChamberArea", chamber);
                Set(director, "m_ChamberGallerySpawn", gallery);
                Set(director, "m_WelcomePanel", welcomePanel);
                Set(director, "m_ConnectPanel", connectPanel);
                Set(director, "m_RoleRoomPanel", roleRoomPanel);
                SetList(director, "m_Seats", seats.Cast<Object>().ToList());

                var origin = Object.FindFirstObjectByType<XROrigin>();
                if (origin != null)
                {
                    origin.transform.SetPositionAndRotation(k_WelcomePos, Quaternion.identity);
                    Set(rig, "m_Origin", origin);
                    PointResetterAtOurAreas(origin.gameObject);
                }
                else Debug.LogWarning("[Town Council] No XR Origin found in the scene. Add the template's XR rig.");
            }

            /// <summary>
            /// The template rig's CharacterResetter teleports the player at start and on disconnect.
            /// Point it at our welcome space (offline) and foyer (online) so it agrees with the director.
            /// </summary>
            static void PointResetterAtOurAreas(GameObject rig)
            {
                var resetter = rig.GetComponentInChildren<XRMultiplayer.CharacterResetter>(true);
                if (resetter == null) return;
                var so = new SerializedObject(resetter);
                var offline = so.FindProperty("offlinePosition");
                var online = so.FindProperty("onlinePosition");
                if (offline != null) offline.vector3Value = k_WelcomePos + new Vector3(0, 0.15f, 0);
                if (online != null) online.vector3Value = k_FoyerPos + new Vector3(0, 0.15f, 0);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Systems ---------------------------------------------------------------------------

            void BuildCouncilSession()
            {
                var go = new GameObject("Council Session");
                go.transform.SetParent(m_Root, false);
                var netObj = go.AddComponent<NetworkObject>();
                var session = go.AddComponent<CouncilSession>();
                Set(session, "m_Config", m_Config);
                go.AddComponent<CouncilGame>();

                // Keep the rules with the session owner in Distributed Authority sessions.
                var so = new SerializedObject(netObj);
                var ownership = so.FindProperty("Ownership");
                if (ownership != null)
                {
                    ownership.intValue = (int)NetworkObject.OwnershipStatus.SessionOwner;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else Debug.LogWarning("[Town Council] Couldn't set Council Session ownership to Session Owner; set it on its NetworkObject.");
            }

            void EnsureSun()
            {
                if (Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l => l.type == LightType.Directional)) return;
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
                sun.transform.SetParent(m_Root, false);
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Welcome ---------------------------------------------------------------------------

            ExperienceArea BuildWelcome(out WelcomePanel welcomePanel, out ConnectPanel connectPanel)
            {
                var area = Area("Area · Welcome", AreaKind.Welcome, k_WelcomePos, -1, out var content);

                Prim(PrimitiveType.Cylinder, "Floor", content, new Vector3(0, -0.05f, 0), new Vector3(9, 0.05f, 9), Mat("Floor Dark", new Color(0.12f, 0.12f, 0.14f)), true);
                Prim(PrimitiveType.Cylinder, "Glow Ring", content, new Vector3(0, 0.005f, 0), new Vector3(2.4f, 0.005f, 2.4f), Mat("Accent Glow", UIFactory.AccentColor, 1.5f));
                Skyline(content, 14f, 40, 7);

                // Welcome pages
                var canvas = UIFactory.Canvas("Welcome Panel", content, new Vector2(1000, 620), 1.15f, new Vector3(0, 1.45f, 1.6f), Quaternion.identity);
                welcomePanel = canvas.gameObject.AddComponent<WelcomePanel>();
                welcomePanel.titleText = UIFactory.Text(canvas, "Title", "Town Council 2045", 56, new Vector2(0, 230), new Vector2(900, 80), TextAlignmentOptions.Center, FontStyles.Bold);
                welcomePanel.bodyText = UIFactory.Text(canvas, "Body", "", 32, new Vector2(0, 10), new Vector2(880, 330), TextAlignmentOptions.Top);
                welcomePanel.pageText = UIFactory.Text(canvas, "Page", "1 / 5", 24, new Vector2(0, -205), new Vector2(200, 40), TextAlignmentOptions.Center, FontStyles.Normal, UIFactory.QuietTextColor);
                welcomePanel.backButton = UIFactory.Button(canvas, "Back", "Back", new Vector2(-300, -250), new Vector2(240, 70));
                welcomePanel.nextButton = UIFactory.Button(canvas, "Next", "Next", new Vector2(250, -250), new Vector2(340, 70), out var nextLabel, UIFactory.AccentColor);
                welcomePanel.nextButtonLabel = nextLabel;

                // Joining
                var connectCanvas = UIFactory.Canvas("Connect Panel", content, new Vector2(1000, 620), 1.15f, new Vector3(0, 1.45f, 1.6f), Quaternion.identity);
                connectPanel = connectCanvas.gameObject.AddComponent<ConnectPanel>();
                connectPanel.config = m_Config;
                UIFactory.Text(connectCanvas, "Title", "Join a council", 52, new Vector2(0, 245), new Vector2(900, 70), TextAlignmentOptions.Center, FontStyles.Bold);
                connectPanel.createButton = UIFactory.Button(connectCanvas, "Create", "Create a council", new Vector2(0, 140), new Vector2(620, 80), UIFactory.AccentColor, 34);
                UIFactory.Text(connectCanvas, "Create Hint", "Private: share the code with your group.", 22, new Vector2(0, 85), new Vector2(620, 30), TextAlignmentOptions.Center, FontStyles.Normal, UIFactory.QuietTextColor);
                connectPanel.joinWithCodeButton = UIFactory.Button(connectCanvas, "Join With Code", "Join with a code", new Vector2(0, 10), new Vector2(620, 80), null, 34);
                connectPanel.quickJoinButton = UIFactory.Button(connectCanvas, "Quick Join", "Quick join any open council", new Vector2(0, -90), new Vector2(620, 80), null, 30);
                connectPanel.backButton = UIFactory.Button(connectCanvas, "Back", "Back to the intro", new Vector2(-330, -250), new Vector2(260, 60), null, 24);
                connectPanel.statusText = UIFactory.Text(connectCanvas, "Status", "", 24, new Vector2(140, -250), new Vector2(560, 60), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, UIFactory.QuietTextColor);
                BuildKeypad(connectCanvas, connectPanel);
                connectCanvas.gameObject.SetActive(false);

                return area;
            }

            void BuildKeypad(RectTransform parent, ConnectPanel panel)
            {
                var root = UIFactory.Panel(parent, "Keypad", UIFactory.SubPanelColor, Vector2.zero, new Vector2(1000, 620));
                var keypad = root.gameObject.AddComponent<CodeKeypad>();
                keypad.display = UIFactory.Text(root, "Code", "", 44, new Vector2(0, 250), new Vector2(600, 70), TextAlignmentOptions.Center, FontStyles.Bold);

                const string keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                const int columns = 9;
                var size = new Vector2(92, 72);
                for (int i = 0; i < keys.Length; i++)
                {
                    int col = i % columns, row = i / columns;
                    var pos = new Vector2((col - (columns - 1) / 2f) * 102f, 160f - row * 82f);
                    var key = keys[i].ToString();
                    var button = UIFactory.Button(root, $"Key {key}", key, pos, size, null, 32);
                    UnityEventTools.AddStringPersistentListener(button.onClick, keypad.AddCharacter, key);
                }

                var del = UIFactory.Button(root, "Delete", "Delete", new Vector2(-330, -220), new Vector2(200, 70), null, 26);
                UnityEventTools.AddPersistentListener(del.onClick, new UnityAction(keypad.Backspace));
                var clear = UIFactory.Button(root, "Clear", "Clear", new Vector2(-110, -220), new Vector2(200, 70), null, 26);
                UnityEventTools.AddPersistentListener(clear.onClick, new UnityAction(keypad.Clear));
                var join = UIFactory.Button(root, "Join", "Join", new Vector2(110, -220), new Vector2(200, 70), UIFactory.AccentColor, 30);
                UnityEventTools.AddPersistentListener(join.onClick, new UnityAction(keypad.Submit));
                panel.keypadCancelButton = UIFactory.Button(root, "Cancel", "Cancel", new Vector2(330, -220), new Vector2(200, 70), null, 26);

                panel.keypad = keypad;
                panel.keypadRoot = root.gameObject;
                root.gameObject.SetActive(false);
            }

            // Foyer -----------------------------------------------------------------------------

            ExperienceArea BuildFoyer()
            {
                var area = Area("Area · Foyer (Lobby)", AreaKind.Foyer, k_FoyerPos, -1, out var content);
                var floor = Mat("Floor Warm", new Color(0.33f, 0.27f, 0.22f));
                var wall = Mat("Wall Stone", new Color(0.62f, 0.6f, 0.56f));
                var wood = Mat("Wood", new Color(0.36f, 0.22f, 0.13f));

                Prim(PrimitiveType.Cube, "Floor", content, new Vector3(0, -0.05f, 0), new Vector3(10, 0.1f, 10), floor, true);
                Prim(PrimitiveType.Cube, "Back Wall", content, new Vector3(0, 1.75f, 4f), new Vector3(10, 3.5f, 0.2f), wall);
                Prim(PrimitiveType.Cube, "Left Wall", content, new Vector3(-5f, 1.75f, 0), new Vector3(0.2f, 3.5f, 10), wall);
                Prim(PrimitiveType.Cube, "Right Wall", content, new Vector3(5f, 1.75f, 0), new Vector3(0.2f, 3.5f, 10), wall);

                // Card table
                var table = new GameObject("Card Table").transform;
                table.SetParent(content, false);
                table.localPosition = new Vector3(0, 0, 1.0f);
                Prim(PrimitiveType.Cube, "Top", table, new Vector3(0, 0.85f, 0), new Vector3(1.9f, 0.06f, 0.8f), wood, true);
                foreach (var x in new[] { -0.85f, 0.85f })
                foreach (var z in new[] { -0.32f, 0.32f })
                    Prim(PrimitiveType.Cube, "Leg", table, new Vector3(x, 0.41f, z), new Vector3(0.06f, 0.82f, 0.06f), wood);

                int count = m_Scenario.roles.Count;
                float spacing = 0.42f;
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (count - 1) / 2f) * spacing;
                    BuildCard(table, i, new Vector3(x, 0.88f + 0.15f, -0.05f));
                }

                // Lobby board
                var canvas = UIFactory.Canvas("Foyer Panel", content, new Vector2(1200, 760), 1.7f, new Vector3(0, 1.9f, 2.6f), Quaternion.identity);
                var panel = canvas.gameObject.AddComponent<FoyerPanel>();
                UIFactory.Text(canvas, "Title", $"{m_Scenario.townName} Town Hall · Foyer", 50, new Vector2(0, 320), new Vector2(1100, 70), TextAlignmentOptions.Center, FontStyles.Bold);
                panel.codeText = UIFactory.Text(canvas, "Code", "", 34, new Vector2(0, 255), new Vector2(1100, 50), TextAlignmentOptions.Center, FontStyles.Normal, UIFactory.AccentColor);
                panel.seatsText = UIFactory.Text(canvas, "Seats", "", 32, new Vector2(0, 80), new Vector2(1050, 260), TextAlignmentOptions.TopLeft);
                panel.statusText = UIFactory.Text(canvas, "Status", "", 30, new Vector2(0, -95), new Vector2(1050, 80), TextAlignmentOptions.Center);
                panel.hintText = UIFactory.Text(canvas, "Hint", "", 34, new Vector2(0, -175), new Vector2(1050, 60), TextAlignmentOptions.Center, FontStyles.Bold);
                panel.startNowButton = UIFactory.Button(canvas, "Start Now", "Start now with AI", new Vector2(-280, -300), new Vector2(420, 80), UIFactory.AccentColor, 30);
                panel.watchButton = UIFactory.Button(canvas, "Watch", "Watch from the gallery", new Vector2(-280, -300), new Vector2(420, 80), null, 28);
                panel.leaveButton = UIFactory.Button(canvas, "Leave", "Leave council", new Vector2(300, -300), new Vector2(340, 80), null, 28);
                return area;
            }

            void BuildCard(Transform table, int roleIndex, Vector3 localPosition)
            {
                var role = m_Scenario.GetRole(roleIndex);
                var go = new GameObject($"Role Card · {(role != null ? role.displayName : roleIndex.ToString())}");
                go.transform.SetParent(table, false);
                go.transform.localPosition = localPosition;
                go.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

                var collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.22f, 0.32f, 0.014f);

                var body = Prim(PrimitiveType.Cube, "Body", go.transform, Vector3.zero, new Vector3(0.22f, 0.32f, 0.01f), Mat("Card", Color.white));
                var interactable = go.AddComponent<XRSimpleInteractable>();
                var view = go.AddComponent<RoleCardView>();
                view.roleIndex = roleIndex;
                view.config = m_Config;
                view.cardRenderer = body.GetComponent<Renderer>();
                view.interactable = interactable;

                var face = UIFactory.Canvas("Face", go.transform, new Vector2(220, 320), 0.21f, new Vector3(0, 0, -0.0065f), Quaternion.identity, false);
                UIFactory.Panel(face, "Inner", new Color(0.06f, 0.06f, 0.07f, 0.88f), new Vector2(0, 0), new Vector2(204, 304));
                view.titleText = UIFactory.Text(face, "Title", "", 26, new Vector2(0, 110), new Vector2(190, 60), TextAlignmentOptions.Center, FontStyles.Bold);
                view.jobText = UIFactory.Text(face, "Job", "", 18, new Vector2(0, 62), new Vector2(190, 40), TextAlignmentOptions.Center, FontStyles.Normal, UIFactory.QuietTextColor);
                view.stateText = UIFactory.Text(face, "State", "", 17, new Vector2(0, -60), new Vector2(190, 40), TextAlignmentOptions.Center, FontStyles.Italic);
                view.chooseButton = UIFactory.Button(face, "Choose", "Choose", new Vector2(0, -115), new Vector2(150, 44), UIFactory.AccentColor, 20);
            }

            // Role rooms ------------------------------------------------------------------------

            ExperienceArea BuildRoleRoom(int roleIndex)
            {
                var role = m_Scenario.GetRole(roleIndex);
                var name = role != null ? role.displayName : $"Role {roleIndex + 1}";
                var pos = k_RoleRoomsStart + new Vector3(0, 0, roleIndex * k_RoleRoomSpacing);
                var area = Area($"Area · Role Room · {name}", AreaKind.RoleRoom, pos, roleIndex, out var content);

                var anchor = new GameObject("Panel Anchor").transform;
                anchor.SetParent(area.transform, false);
                anchor.localPosition = new Vector3(0, 1.45f, 1.5f);
                area.panelAnchor = anchor;

                if (role != null && role.roomEnvironmentPrefab != null)
                {
                    var env = (GameObject)PrefabUtility.InstantiatePrefab(role.roomEnvironmentPrefab, content);
                    env.name = "Environment";
                    return area;
                }

                var accent = role != null ? role.accentColor : Color.gray;
                var wall = Mat($"Room Wall · {name}", Color.Lerp(accent, new Color(0.15f, 0.15f, 0.17f), 0.65f));
                Prim(PrimitiveType.Cube, "Floor", content, new Vector3(0, -0.05f, 0), new Vector3(6, 0.1f, 6), Mat("Floor Dark", new Color(0.12f, 0.12f, 0.14f)), true);
                Prim(PrimitiveType.Cube, "Back Wall", content, new Vector3(0, 1.5f, 3f), new Vector3(6, 3f, 0.15f), wall);
                Prim(PrimitiveType.Cube, "Left Wall", content, new Vector3(-3f, 1.5f, 0), new Vector3(0.15f, 3f, 6), wall);
                Prim(PrimitiveType.Cube, "Right Wall", content, new Vector3(3f, 1.5f, 0), new Vector3(0.15f, 3f, 6), wall);
                Prim(PrimitiveType.Cube, "Window Glow", content, new Vector3(-1.8f, 1.7f, 2.9f), new Vector3(1.4f, 1f, 0.02f), Mat($"Room Glow · {name}", accent, 1.2f));

                var sign = UIFactory.Canvas("Room Label", content, new Vector2(800, 120), 1.6f, new Vector3(0.9f, 2.55f, 2.9f), Quaternion.identity, false);
                UIFactory.Text(sign, "Label", $"Placeholder: {RoomLabel(role)}", 48, Vector2.zero, new Vector2(800, 120), TextAlignmentOptions.Center, FontStyles.Bold, UIFactory.QuietTextColor);
                return area;
            }

            static string RoomLabel(RoleDefinition role)
            {
                if (role == null) return "role room";
                return role.displayName switch
                {
                    "Amara" => "A&E staff room",
                    "Leo" => "Start-up office",
                    "Edith" => "Care-home room",
                    "Sam" => "Bedroom on Riverside Street",
                    _ => $"{role.job}'s room"
                };
            }

            RoleRoomPanel BuildRoleRoomPanel(Transform parent, Transform anchor)
            {
                var canvas = UIFactory.Canvas("Role Card Panel", parent, new Vector2(900, 760), 1.1f, Vector3.zero, Quaternion.identity);
                if (anchor != null) canvas.SetPositionAndRotation(anchor.position, anchor.rotation);

                var panel = canvas.gameObject.AddComponent<RoleRoomPanel>();
                UIFactory.Text(canvas, "Kicker", "YOUR ROLE", 22, new Vector2(0, 335), new Vector2(820, 30), TextAlignmentOptions.Center, FontStyles.Bold, UIFactory.AccentColor);
                panel.titleText = UIFactory.Text(canvas, "Title", "", 56, new Vector2(0, 285), new Vector2(820, 70), TextAlignmentOptions.Center, FontStyles.Bold);
                panel.jobText = UIFactory.Text(canvas, "Job", "", 30, new Vector2(0, 235), new Vector2(820, 40), TextAlignmentOptions.Center, FontStyles.Normal, UIFactory.QuietTextColor);
                panel.introText = UIFactory.Text(canvas, "Intro", "", 26, new Vector2(0, 165), new Vector2(800, 80), TextAlignmentOptions.Center, FontStyles.Italic);

                UIFactory.Text(canvas, "Goal Label", "YOUR HIDDEN GOAL", 22, new Vector2(0, 95), new Vector2(800, 30), TextAlignmentOptions.Left, FontStyles.Bold, UIFactory.QuietTextColor);
                panel.goalText = UIFactory.Text(canvas, "Goal", "", 32, new Vector2(0, 45), new Vector2(800, 70), TextAlignmentOptions.TopLeft);

                UIFactory.Text(canvas, "Secret Label", "YOUR SECRET · only you can see this", 22, new Vector2(0, -35), new Vector2(800, 30), TextAlignmentOptions.Left, FontStyles.Bold, UIFactory.AccentColor);
                panel.secretText = UIFactory.Text(canvas, "Secret", "", 32, new Vector2(0, -90), new Vector2(800, 80), TextAlignmentOptions.TopLeft);
                UIFactory.Text(canvas, "Secret Hint", "Reveal it when you choose. It could change a vote.", 22, new Vector2(0, -160), new Vector2(800, 30), TextAlignmentOptions.Left, FontStyles.Italic, UIFactory.QuietTextColor);

                panel.continueButton = UIFactory.Button(canvas, "Continue", "Go to the council chamber", new Vector2(0, -290), new Vector2(560, 90), UIFactory.AccentColor, 32);
                return panel;
            }

            // Chamber ---------------------------------------------------------------------------

            ExperienceArea BuildChamber(out List<SeatView> seats, out Transform gallery)
            {
                var area = Area("Area · Council Chamber", AreaKind.Chamber, k_ChamberPos, -1, out var content);
                var floor = Mat("Chamber Floor", new Color(0.18f, 0.17f, 0.2f));
                var stone = Mat("Wall Stone", new Color(0.62f, 0.6f, 0.56f));
                var tableMat = Mat("Table Dark", new Color(0.1f, 0.09f, 0.1f));

                Prim(PrimitiveType.Cylinder, "Floor", content, new Vector3(0, -0.05f, 0), new Vector3(15, 0.05f, 15), floor, true);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16f;
                    Prim(PrimitiveType.Cube, "Pillar", content, new Vector3(Mathf.Sin(a) * 7f, 2f, Mathf.Cos(a) * 7f), new Vector3(0.5f, 4f, 0.5f), stone);
                }

                Prim(PrimitiveType.Cylinder, "Round Table", content, new Vector3(0, 0.4f, 0), new Vector3(2.8f, 0.4f, 2.8f), tableMat, true);
                var city = BuildCityModel(content);
                var orb = CivicOrbBuilder.Build(content, new Vector3(0, 1.95f, 0));
                BuildCivicVoice(content, orb);

                seats = new List<SeatView>();
                int count = m_Scenario.roles.Count;
                for (int i = 0; i < count; i++) seats.Add(BuildSeat(content, i, count));

                BuildBoard(content);

                var presenter = content.gameObject.AddComponent<CityPresenter>();
                presenter.city = city;
                presenter.dimLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Where(l => l.type == LightType.Directional).ToArray();

                gallery = new GameObject("Gallery Spawn").transform;
                gallery.SetParent(area.transform, false);
                gallery.localPosition = new Vector3(0, 0, 5.5f);
                gallery.localRotation = Quaternion.Euler(0, 180f, 0);
                area.spawnPoint = gallery;
                return area;
            }

            CityModelView BuildCityModel(Transform parent)
            {
                var root = new GameObject("City Model").transform;
                root.SetParent(parent, false);
                root.localPosition = new Vector3(0, 0.81f, 0);
                var cityView = root.gameObject.AddComponent<CityModelView>();

                Prim(PrimitiveType.Cylinder, "Ground", root, Vector3.zero, new Vector3(2.3f, 0.01f, 2.3f), Mat("City Ground", new Color(0.2f, 0.32f, 0.22f)));
                Prim(PrimitiveType.Cylinder, "River", root, new Vector3(0.55f, 0.012f, -0.35f), new Vector3(0.7f, 0.002f, 0.18f), Mat("River", new Color(0.2f, 0.4f, 0.7f)));

                var buildingMat = Mat("Building", new Color(0.85f, 0.85f, 0.85f), 0f, true);
                var layout = new Dictionary<string, (Vector3 pos, Vector3 size)>
                {
                    { "TownHall", (new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.16f, 0.22f)) },
                    { "Library", (new Vector3(-0.55f, 0f, 0.2f), new Vector3(0.16f, 0.12f, 0.2f)) },
                    { "Hospital", (new Vector3(0.5f, 0f, 0.35f), new Vector3(0.24f, 0.22f, 0.16f)) },
                    { "RiversideStreet", (new Vector3(0.5f, 0f, -0.55f), new Vector3(0.32f, 0.07f, 0.1f)) },
                    { "CareHome", (new Vector3(-0.45f, 0f, -0.45f), new Vector3(0.2f, 0.1f, 0.16f)) },
                    { "College", (new Vector3(-0.05f, 0f, 0.62f), new Vector3(0.2f, 0.14f, 0.12f)) },
                    { "TechHub", (new Vector3(0.2f, 0f, -0.2f), new Vector3(0.1f, 0.34f, 0.1f)) },
                    { "BusDepot", (new Vector3(-0.72f, 0f, -0.05f), new Vector3(0.12f, 0.06f, 0.22f)) },
                };

                int fallback = 0;
                foreach (var id in m_Scenario.buildings)
                {
                    if (id == null) continue;
                    var key = id.name.Replace("Building_", "");
                    if (!layout.TryGetValue(key, out var spot))
                    {
                        float a = fallback++ * 0.9f;
                        spot = (new Vector3(Mathf.Sin(a) * 0.85f, 0, Mathf.Cos(a) * 0.85f), new Vector3(0.12f, 0.12f, 0.12f));
                    }

                    var go = new GameObject($"Building · {id.displayName}");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = spot.pos;
                    var mesh = Prim(PrimitiveType.Cube, "Mesh", go.transform, new Vector3(0, spot.size.y / 2f + 0.01f, 0), spot.size, buildingMat, true);
                    mesh.isStatic = false; // its height changes with its state

                    var view = go.AddComponent<BuildingView>();
                    view.building = id;
                    view.highlightRenderers = new[] { mesh.GetComponent<Renderer>() };
                    view.placeholderMesh = mesh.transform;

                    // Touch (or point and select) to hear the people it affects.
                    var interactable = go.AddComponent<XRSimpleInteractable>();
                    var audio = go.AddComponent<AudioSource>();
                    audio.playOnAwake = false;
                    audio.spatialBlend = 1f;
                    audio.minDistance = 0.5f;
                    var touch = go.AddComponent<TouchableBuilding>();
                    touch.interactable = interactable;
                    touch.audioSource = audio;

                    var label = UIFactory.Canvas("Label", go.transform, new Vector2(300, 60), 0.22f, new Vector3(0, spot.size.y + 0.07f, 0), Quaternion.identity, false);
                    label.gameObject.AddComponent<FaceCamera>();
                    UIFactory.Text(label, "Text", id.displayName, 36, Vector2.zero, new Vector2(300, 60), TextAlignmentOptions.Center, FontStyles.Bold);
                }

                // Caption for residents' voices, shown above whichever building was touched.
                var player = root.gameObject.AddComponent<PerspectivePlayer>();
                var caption = UIFactory.Canvas("Perspective Caption", root, new Vector2(700, 200), 0.6f, new Vector3(0, 0.4f, 0), Quaternion.identity);
                caption.gameObject.AddComponent<FaceCamera>();
                player.captionRoot = caption.gameObject;
                player.captionText = UIFactory.Text(caption, "Text", "", 30, Vector2.zero, new Vector2(660, 180), TextAlignmentOptions.Center);
                return cityView;
            }

            void BuildCivicVoice(Transform parent, CivicOrbView orb)
            {
                var go = new GameObject("CIVIC Voice");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0, 1.95f, 0);
                var audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0.6f;

                var caption = UIFactory.Canvas("CIVIC Caption", go.transform, new Vector2(900, 200), 0.95f, new Vector3(0, 0.38f, 0), Quaternion.identity);
                caption.gameObject.AddComponent<FaceCamera>();

                var voice = go.AddComponent<CivicVoice>();
                voice.orb = orb;
                voice.voice = audio;
                voice.captionRoot = caption.gameObject;
                voice.captionText = UIFactory.Text(caption, "Text", "", 32, Vector2.zero, new Vector2(860, 180), TextAlignmentOptions.Center);
            }

            void BuildBoard(Transform parent)
            {
                var board = UIFactory.Canvas("Council Board", parent, new Vector2(1100, 720), 1.5f, new Vector3(0, 2.95f, 0), Quaternion.identity);
                board.gameObject.AddComponent<FaceCamera>();
                var panel = board.gameObject.AddComponent<ChamberPanel>();

                var pre = UIFactory.Panel(board, "Before The Game", new Color(0, 0, 0, 0), Vector2.zero, new Vector2(1100, 720));
                panel.preGameRoot = pre.gameObject;
                panel.statusText = UIFactory.Text(pre, "Status", "", 36, new Vector2(0, 230), new Vector2(1000, 160), TextAlignmentOptions.Center, FontStyles.Bold);
                panel.seatsText = UIFactory.Text(pre, "Seats", "", 30, new Vector2(0, -60), new Vector2(980, 380), TextAlignmentOptions.TopLeft);

                var game = UIFactory.Panel(board, "During The Game", new Color(0, 0, 0, 0), Vector2.zero, new Vector2(1100, 720));
                panel.gameRoot = game.gameObject;
                panel.headerText = UIFactory.Text(game, "Header", "", 28, new Vector2(-80, 320), new Vector2(880, 44), TextAlignmentOptions.Left, FontStyles.Bold, UIFactory.QuietTextColor);
                panel.timerText = UIFactory.Text(game, "Timer", "", 64, new Vector2(430, 300), new Vector2(200, 90), TextAlignmentOptions.Right, FontStyles.Bold, UIFactory.AccentColor);
                panel.titleText = UIFactory.Text(game, "Title", "", 48, new Vector2(0, 230), new Vector2(1000, 110), TextAlignmentOptions.Left, FontStyles.Bold);
                panel.bodyText = UIFactory.Text(game, "Body", "", 30, new Vector2(0, 20), new Vector2(1000, 300), TextAlignmentOptions.TopLeft);
                panel.footerText = UIFactory.Text(game, "Footer", "", 24, new Vector2(0, -300), new Vector2(1000, 90), TextAlignmentOptions.Left, FontStyles.Normal, UIFactory.QuietTextColor);
                game.gameObject.SetActive(false);

                var epilogue = UIFactory.Panel(game, "Epilogue", new Color(0, 0, 0, 0), new Vector2(0, -110), new Vector2(280, 280));
                panel.epilogueRoot = epilogue.gameObject;
                var radarGo = new GameObject("Values Fingerprint", typeof(RectTransform), typeof(CanvasRenderer), typeof(RadarChartGraphic));
                radarGo.transform.SetParent(epilogue, false);
                UIFactory.Place((RectTransform)radarGo.transform, Vector2.zero, new Vector2(280, 280));
                panel.radar = radarGo.GetComponent<RadarChartGraphic>();
                panel.radar.raycastTarget = false;
                panel.labelDistance = 1.18f;
                epilogue.gameObject.SetActive(false);
            }

            void BuildEndingPanel(Transform parent, List<SeatView> seats)
            {
                var canvas = UIFactory.Canvas("Your Ending", parent, new Vector2(800, 520), 0.8f, Vector3.zero, Quaternion.identity);
                var panel = canvas.gameObject.AddComponent<EndingPanel>();
                var content = UIFactory.Panel(canvas, "Content", new Color(0, 0, 0, 0), Vector2.zero, new Vector2(800, 520));
                panel.root = content.gameObject;
                UIFactory.Text(content, "Kicker", "YOUR ENDING", 22, new Vector2(0, 225), new Vector2(740, 30), TextAlignmentOptions.Center, FontStyles.Bold, UIFactory.AccentColor);
                panel.titleText = UIFactory.Text(content, "Title", "", 40, new Vector2(0, 180), new Vector2(740, 56), TextAlignmentOptions.Center, FontStyles.Bold);
                panel.endingText = UIFactory.Text(content, "Ending", "", 30, new Vector2(0, 20), new Vector2(720, 240), TextAlignmentOptions.Top);
                panel.footerText = UIFactory.Text(content, "Footer", "", 24, new Vector2(0, -150), new Vector2(720, 60), TextAlignmentOptions.Center, FontStyles.Italic, UIFactory.QuietTextColor);
                panel.leaveButton = UIFactory.Button(content, "Leave", "Leave the council", new Vector2(0, -215), new Vector2(360, 64), null, 26);
                panel.seatSpawns = seats.Select(s => s.PlayerSpawn).ToArray();
                // The panel hides its content until the epilogue; the canvas background is the content's.
                Object.DestroyImmediate(canvas.Find("Background").gameObject);
                var bg = UIFactory.Image(content, "Background", UIFactory.PanelColor, Vector2.zero, new Vector2(800, 520));
                bg.transform.SetAsFirstSibling();
            }

            SeatView BuildSeat(Transform parent, int index, int count)
            {
                float angle = index * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));

                var go = new GameObject($"Seat {index + 1}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = dir * 1.85f;
                go.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up);

                var chair = Prim(PrimitiveType.Cube, "Chair", go.transform, new Vector3(0, 0.5f, -0.45f), new Vector3(0.55f, 1f, 0.12f), Mat("Chair", new Color(0.35f, 0.35f, 0.38f)));

                var ai = new GameObject("AI Councillor (placeholder)");
                ai.transform.SetParent(go.transform, false);
                var holo = Mat("AI Hologram", new Color(0.35f, 0.75f, 0.95f), 0.8f);
                Prim(PrimitiveType.Capsule, "Body", ai.transform, new Vector3(0, 0.75f, 0), new Vector3(0.38f, 0.5f, 0.3f), holo);
                Prim(PrimitiveType.Sphere, "Head", ai.transform, new Vector3(0, 1.45f, 0), Vector3.one * 0.24f, holo);
                ai.SetActive(false);

                var spawn = new GameObject("Player Spawn").transform;
                spawn.SetParent(go.transform, false);

                var plate = UIFactory.Canvas("Nameplate", go.transform, new Vector2(500, 140), 0.7f, new Vector3(0, 2.0f, -0.1f), Quaternion.identity, false);
                plate.gameObject.AddComponent<FaceCamera>();
                var text = UIFactory.Text(plate, "Name", "", 46, Vector2.zero, new Vector2(500, 140), TextAlignmentOptions.Center, FontStyles.Bold);

                var view = go.AddComponent<SeatView>();
                view.seatIndex = index;
                view.playerSpawn = spawn;
                view.nameplate = text;
                view.aiAvatar = ai;
                view.chairRenderer = chair.GetComponent<Renderer>();

                // On the table in front of the seat: ballot rows, the role card and its reveal spot.
                var surface = Mat("Ballot Surface", new Color(0.08f, 0.08f, 0.1f));
                var glow = Mat("Table Glow", Color.white, 0f, true);

                var trayAnchor = Anchor("Ballot Tray", go.transform, new Vector3(-0.05f, 0.805f, 0.62f));
                var tray = go.AddComponent<BallotTrayView>();
                tray.seatIndex = index;
                tray.trayAnchor = trayAnchor;
                tray.surfaceMaterial = surface;
                tray.glowMaterial = glow;

                var secretCanvas = UIFactory.Canvas("Secret Display", go.transform, new Vector2(700, 200), 0.9f, new Vector3(0, 2.5f, -0.1f), Quaternion.identity);
                secretCanvas.gameObject.AddComponent<FaceCamera>();
                var secretText = UIFactory.Text(secretCanvas, "Secret", "", 32, Vector2.zero, new Vector2(660, 180), TextAlignmentOptions.Center);
                secretCanvas.gameObject.SetActive(false);

                var reveal = go.AddComponent<SecretRevealView>();
                reveal.seatIndex = index;
                reveal.cardAnchor = Anchor("Card Home", go.transform, new Vector3(0.36f, 0.805f, 0.6f));
                reveal.revealAnchor = Anchor("Reveal Spot", go.transform, new Vector3(-0.4f, 0.805f, 0.64f));
                reveal.secretDisplay = secretText;
                reveal.secretDisplayRoot = secretCanvas.gameObject;
                reveal.cardMaterial = Mat("Role Card", Color.white, 0f, true);
                reveal.glowMaterial = glow;
                return view;
            }

            // Helpers ---------------------------------------------------------------------------

            static Transform Anchor(string name, Transform parent, Vector3 localPosition)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.localPosition = localPosition;
                return t;
            }

            ExperienceArea Area(string name, AreaKind kind, Vector3 position, int roleIndex, out Transform content)
            {
                var go = new GameObject(name);
                go.transform.SetParent(m_Root, false);
                go.transform.position = position;

                var area = go.AddComponent<ExperienceArea>();
                area.kind = kind;
                area.roleIndex = roleIndex;

                var spawn = new GameObject("Spawn Point").transform;
                spawn.SetParent(go.transform, false);
                area.spawnPoint = spawn;

                content = new GameObject("Content").transform;
                content.SetParent(go.transform, false);
                area.contentRoot = content.gameObject;
                return area;
            }

            void Skyline(Transform parent, float radius, int count, int seed)
            {
                var rng = new System.Random(seed);
                var mat = Mat("Skyline", new Color(0.09f, 0.09f, 0.12f));
                var windows = Mat("Skyline Windows", new Color(1f, 0.75f, 0.45f), 0.6f);
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count + (float)rng.NextDouble() * 0.1f;
                    float h = 2f + (float)rng.NextDouble() * 7f;
                    var pos = new Vector3(Mathf.Sin(a) * radius, h / 2f, Mathf.Cos(a) * radius);
                    var tower = Prim(PrimitiveType.Cube, "Tower", parent, pos, new Vector3(1.2f + (float)rng.NextDouble(), h, 1.2f), mat);
                    tower.transform.LookAt(new Vector3(0, h / 2f, 0) + parent.position);
                    if (rng.NextDouble() < 0.5)
                        Prim(PrimitiveType.Cube, "Lit Window", tower.transform, new Vector3(0, 0.2f, 0.51f), new Vector3(0.3f, 0.05f, 0.01f), windows);
                }
            }

            static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider = false)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPosition;
                go.transform.localScale = localScale;
                if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
                if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
                go.isStatic = keepCollider;
                return go;
            }

            Material Mat(string name, Color color, float emission = 0f, bool emissionKeyword = false)
            {
                if (m_Materials.TryGetValue(name, out var cached)) return cached;

                TCPaths.EnsureFolder(TCPaths.Materials);
                var path = $"{TCPaths.Materials}/{TCPaths.SafeFileName(name)}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    mat.color = color;
                    if (emission > 0f || emissionKeyword)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", color * emission);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    AssetDatabase.CreateAsset(mat, path);
                }
                m_Materials[name] = mat;
                return mat;
            }

            static void Set(Object target, string field, Object value)
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogWarning($"[Town Council] {target.GetType().Name} has no field {field}.");
                    return;
                }
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            static void SetList(Object target, string field, IList<Object> values)
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty(field);
                if (prop == null || !prop.isArray)
                {
                    Debug.LogWarning($"[Town Council] {target.GetType().Name} has no list {field}.");
                    return;
                }
                prop.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
