using System;
using TMPro;
using TownCouncil.Core;
using UnityEngine;
using UnityEngine.UI;
using XRMultiplayer;

namespace TownCouncil.Experience
{
    /// <summary>
    /// Joining a council: create a private council (others join with its code), join with a code,
    /// or quick-join any open council. Uses the VR Multiplayer template's session code underneath.
    /// </summary>
    public class ConnectPanel : MonoBehaviour
    {
        [Header("References")]
        public Button createButton;
        public Button quickJoinButton;
        public Button joinWithCodeButton;
        public Button backButton;
        public GameObject keypadRoot;
        public CodeKeypad keypad;
        public Button keypadCancelButton;
        public TMP_Text statusText;

        [Header("Settings")]
        public GameConfig config;
        [Tooltip("Councils made with Create are private: others join with the code shown in the foyer.")]
        public bool createPrivateCouncils = true;

        public event Action BackRequested;

        bool m_Busy;

        void Awake()
        {
            if (createButton != null) createButton.onClick.AddListener(CreateCouncil);
            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(QuickJoin);
            if (joinWithCodeButton != null) joinWithCodeButton.onClick.AddListener(OpenKeypad);
            if (backButton != null) backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (keypadCancelButton != null) keypadCancelButton.onClick.AddListener(CloseKeypad);
            if (keypad != null) keypad.Submitted += JoinWithCode;
        }

        void OnEnable()
        {
            CloseKeypad();
            m_Busy = false;
            var manager = XRINetworkGameManager.Instance;
            if (manager != null)
            {
                manager.OnConnectionUpdated += SetStatus;
                manager.OnConnectionFailedAction += OnFailed;
            }
        }

        void OnDisable()
        {
            var manager = XRINetworkGameManager.Instance;
            if (manager != null)
            {
                manager.OnConnectionUpdated -= SetStatus;
                manager.OnConnectionFailedAction -= OnFailed;
            }
        }

        void Update()
        {
            bool ready = IsReady();
            SetInteractable(ready && !m_Busy);
            if (!ready && statusText != null && !m_Busy) statusText.text = "Getting ready to connect…";
        }

        public void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        void OnFailed(string reason)
        {
            m_Busy = false;
            SetStatus($"Couldn't connect: {reason}");
        }

        static bool IsReady()
        {
            var manager = XRINetworkGameManager.Instance;
            return manager != null && manager.IsAuthenticated();
        }

        int SeatCount => config != null ? Mathf.Max(1, config.SeatCount) : 4;

        public void CreateCouncil()
        {
            if (!IsReady()) return;
            m_Busy = true;
            SetStatus("Creating a council…");
            XRINetworkGameManager.Instance.CreateNewLobby(null, createPrivateCouncils, SeatCount);
        }

        public void QuickJoin()
        {
            if (!IsReady()) return;
            m_Busy = true;
            SetStatus("Looking for an open council…");
            XRINetworkGameManager.Instance.QuickJoinLobby();
        }

        void JoinWithCode(string code)
        {
            if (!IsReady()) return;
            m_Busy = true;
            CloseKeypad();
            SetStatus($"Joining council {code}…");
            XRINetworkGameManager.Instance.JoinLobbyByCode(code.ToUpperInvariant());
        }

        void OpenKeypad()
        {
            if (keypadRoot == null) return;
            if (keypad != null) keypad.Clear();
            keypadRoot.SetActive(true);
        }

        void CloseKeypad()
        {
            if (keypadRoot != null) keypadRoot.SetActive(false);
        }

        void SetInteractable(bool value)
        {
            if (createButton != null) createButton.interactable = value;
            if (quickJoinButton != null) quickJoinButton.interactable = value;
            if (joinWithCodeButton != null) joinWithCodeButton.interactable = value;
        }
    }
}
