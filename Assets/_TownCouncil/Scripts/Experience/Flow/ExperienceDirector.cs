using System;
using System.Collections;
using System.Collections.Generic;
using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;
using XRMultiplayer;

namespace TownCouncil.Experience
{
    /// <summary>
    /// Runs the local player's journey before the game starts:
    /// Welcome (headset on, what the game is) → Foyer (multiplayer lobby, pick a card)
    /// → the role's own room → Council chamber (your seat at the round table).
    /// Every move between areas is a fade out, a teleport and a fade in.
    ///
    /// The director only moves the local player and shows panels. Seat and role rules live in
    /// <see cref="CouncilSession"/>; the director reacts to its replicated state.
    /// </summary>
    public class ExperienceDirector : MonoBehaviour
    {
        public enum Stage
        {
            Welcome,
            Foyer,
            RoleRoom,
            Chamber
        }

        [SerializeField] GameConfig m_Config;
        [SerializeField] ScreenFader m_Fader;
        [SerializeField] PlayerRig m_Rig;

        [Header("Areas")]
        [SerializeField] ExperienceArea m_WelcomeArea;
        [SerializeField] ExperienceArea m_FoyerArea;
        [SerializeField] List<ExperienceArea> m_RoleRooms = new();
        [SerializeField] ExperienceArea m_ChamberArea;
        [Tooltip("Where people who joined after the council locked watch from.")]
        [SerializeField] Transform m_ChamberGallerySpawn;

        [Header("Panels")]
        [SerializeField] WelcomePanel m_WelcomePanel;
        [SerializeField] ConnectPanel m_ConnectPanel;
        [SerializeField] RoleRoomPanel m_RoleRoomPanel;

        [Header("Chamber seats (one per seat, same order as the session's seats)")]
        [SerializeField] List<SeatView> m_Seats = new();

        public static ExperienceDirector Instance { get; private set; }

        public Stage CurrentStage { get; private set; } = Stage.Welcome;
        public bool IsTransitioning { get; private set; }
        public GameConfig Config => m_Config;

        /// <summary>Raised after each arrival, once the fade-in has finished.</summary>
        public event Action<Stage> StageEntered;

        CouncilSession m_Session;
        ExperienceArea m_CurrentArea;
        Coroutine m_CardRevealRoutine;
        int m_LocalRoleShown = -1;

        // Lifecycle ----------------------------------------------------------------------------

        void Awake()
        {
            Instance = this;
            if (m_Rig == null) m_Rig = GetComponent<PlayerRig>();
            if (m_Fader == null) m_Fader = GetComponent<ScreenFader>();
        }

        void OnEnable()
        {
            XRINetworkGameManager.Connected.Subscribe(OnConnectedChanged);
            CouncilSession.Spawned += OnSessionSpawned;
            CouncilSession.Despawned += OnSessionDespawned;
            if (m_WelcomePanel != null) m_WelcomePanel.JoinRequested += ShowConnectPanel;
            if (m_ConnectPanel != null) m_ConnectPanel.BackRequested += ShowWelcomePanel;
            if (m_RoleRoomPanel != null) m_RoleRoomPanel.ContinueRequested += OnRoleRoomContinue;
        }

        void OnDisable()
        {
            XRINetworkGameManager.Connected.Unsubscribe(OnConnectedChanged);
            CouncilSession.Spawned -= OnSessionSpawned;
            CouncilSession.Despawned -= OnSessionDespawned;
            if (m_WelcomePanel != null) m_WelcomePanel.JoinRequested -= ShowConnectPanel;
            if (m_ConnectPanel != null) m_ConnectPanel.BackRequested -= ShowWelcomePanel;
            if (m_RoleRoomPanel != null) m_RoleRoomPanel.ContinueRequested -= OnRoleRoomContinue;
            BindSession(null);
        }

        IEnumerator Start()
        {
            foreach (var area in AllAreas()) area.SetActive(false);
            ShowWelcomePanel();

            // Arrive in the welcome space under black, then fade in.
            m_Fader.SetInstant(1f);
            yield return null; // let the XR rig initialise its camera
            yield return Arrive(m_WelcomeArea, null);
            CurrentStage = Stage.Welcome;
            yield return m_Fader.FadeIn(m_Config != null ? m_Config.fadeInSeconds : 1f);
            StageEntered?.Invoke(Stage.Welcome);

            if (CouncilSession.Instance != null && CouncilSession.Instance.IsSpawned)
                OnSessionSpawned(CouncilSession.Instance);
        }

        // Public API (debug tools, designer hooks) ---------------------------------------------

        /// <summary>Move the local player to a stage now. Mainly for debug tools.</summary>
        public void GoTo(Stage stage)
        {
            switch (stage)
            {
                case Stage.Welcome: StartTransition(m_WelcomeArea, null, Stage.Welcome); break;
                case Stage.Foyer: StartTransition(m_FoyerArea, null, Stage.Foyer); break;
                case Stage.RoleRoom: StartTransition(RoleRoomFor(Mathf.Max(0, LocalRoleIndex())), null, Stage.RoleRoom); break;
                case Stage.Chamber: StartTransition(m_ChamberArea, ChamberSpawnForLocal(), Stage.Chamber); break;
            }
        }

        public void WatchFromGallery() => StartTransition(m_ChamberArea, m_ChamberGallerySpawn, Stage.Chamber);

        // Network events -----------------------------------------------------------------------

        void OnConnectedChanged(bool connected)
        {
            if (!connected && CurrentStage != Stage.Welcome)
            {
                m_LocalRoleShown = -1;
                ShowConnectPanel();
                if (m_ConnectPanel != null) m_ConnectPanel.SetStatus("Disconnected from the council.");
                StartTransition(m_WelcomeArea, null, Stage.Welcome);
            }
        }

        void OnSessionSpawned(CouncilSession session)
        {
            BindSession(session);
            if (CurrentStage == Stage.Welcome)
                StartTransition(m_FoyerArea, null, Stage.Foyer);
        }

        void OnSessionDespawned(CouncilSession session)
        {
            if (m_Session == session) BindSession(null);
        }

        void BindSession(CouncilSession session)
        {
            if (m_Session != null) m_Session.SeatsChanged -= OnSeatsChanged;
            m_Session = session;
            if (m_Session != null) m_Session.SeatsChanged += OnSeatsChanged;
        }

        void OnSeatsChanged()
        {
            // The card the player asked for was accepted: reveal it, then move to their room.
            int role = LocalRoleIndex();
            if (role >= 0 && role != m_LocalRoleShown && CurrentStage == Stage.Foyer)
            {
                m_LocalRoleShown = role;
                if (m_CardRevealRoutine != null) StopCoroutine(m_CardRevealRoutine);
                m_CardRevealRoutine = StartCoroutine(GoToRoleRoomAfterReveal(role));
            }
        }

        IEnumerator GoToRoleRoomAfterReveal(int role)
        {
            yield return new WaitForSeconds(m_Config != null ? m_Config.cardRevealSeconds : 1.5f);
            var room = RoleRoomFor(role);
            if (m_RoleRoomPanel != null)
            {
                m_RoleRoomPanel.Show(m_Session != null ? m_Session.GetRole(role) : null);
                m_RoleRoomPanel.MoveTo(room.panelAnchor);
            }
            StartTransition(room, null, Stage.RoleRoom);
            if (m_Session != null) m_Session.ReportStage(SeatStage.InRoleRoom);
        }

        void OnRoleRoomContinue()
        {
            if (CurrentStage != Stage.RoleRoom) return;
            StartTransition(m_ChamberArea, ChamberSpawnForLocal(), Stage.Chamber);
            if (m_Session != null) m_Session.ReportStage(SeatStage.Seated);
        }

        // Panels -------------------------------------------------------------------------------

        void ShowWelcomePanel()
        {
            if (m_WelcomePanel != null) m_WelcomePanel.Show(m_Config != null ? m_Config.scenario : null);
            if (m_ConnectPanel != null) m_ConnectPanel.gameObject.SetActive(false);
        }

        void ShowConnectPanel()
        {
            if (m_WelcomePanel != null) m_WelcomePanel.gameObject.SetActive(false);
            if (m_ConnectPanel != null) m_ConnectPanel.gameObject.SetActive(true);
        }

        // Transitions ----------------------------------------------------------------------------

        void StartTransition(ExperienceArea area, Transform spawnOverride, Stage stage)
        {
            if (area == null)
            {
                Debug.LogWarning($"[Town Council] No area set up for stage {stage}.", this);
                return;
            }
            StartCoroutine(Transition(area, spawnOverride, stage));
        }

        IEnumerator Transition(ExperienceArea area, Transform spawnOverride, Stage stage)
        {
            while (IsTransitioning) yield return null;
            IsTransitioning = true;

            float fadeOut = m_Config != null ? m_Config.fadeOutSeconds : 0.8f;
            float hold = m_Config != null ? m_Config.holdOnBlackSeconds : 0.4f;
            float fadeIn = m_Config != null ? m_Config.fadeInSeconds : 1f;

            yield return m_Fader.FadeOut(fadeOut);
            yield return Arrive(area, spawnOverride);
            CurrentStage = stage;
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            yield return m_Fader.FadeIn(fadeIn);

            IsTransitioning = false;
            StageEntered?.Invoke(stage);
        }

        IEnumerator Arrive(ExperienceArea area, Transform spawnOverride)
        {
            if (m_CurrentArea != null && m_CurrentArea != area) m_CurrentArea.SetActive(false);
            m_CurrentArea = area;
            area.SetActive(true);
            yield return m_Rig.MoveTo(spawnOverride != null ? spawnOverride : area.SpawnPoint);
        }

        // Helpers ----------------------------------------------------------------------------------

        int LocalRoleIndex() =>
            m_Session != null && m_Session.TryGetLocalSeat(out var seat) ? seat.RoleIndex : -1;

        ExperienceArea RoleRoomFor(int roleIndex)
        {
            foreach (var room in m_RoleRooms)
                if (room != null && room.roleIndex == roleIndex) return room;
            Debug.LogWarning($"[Town Council] No role room for role {roleIndex}; using the chamber.", this);
            return m_ChamberArea;
        }

        Transform ChamberSpawnForLocal()
        {
            int seat = m_Session != null ? m_Session.LocalSeatIndex : -1;
            if (seat >= 0 && seat < m_Seats.Count && m_Seats[seat] != null) return m_Seats[seat].PlayerSpawn;
            return m_ChamberGallerySpawn;
        }

        IEnumerable<ExperienceArea> AllAreas()
        {
            if (m_WelcomeArea != null) yield return m_WelcomeArea;
            if (m_FoyerArea != null) yield return m_FoyerArea;
            foreach (var r in m_RoleRooms) if (r != null) yield return r;
            if (m_ChamberArea != null) yield return m_ChamberArea;
        }
    }
}
