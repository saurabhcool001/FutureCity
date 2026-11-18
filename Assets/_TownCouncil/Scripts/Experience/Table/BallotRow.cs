using TMPro;
using TownCouncil.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TownCouncil.Experience
{
    /// <summary>
    /// One policy's ballot at one seat: three slots (Agree, Disagree, Amend) with a lamp each.
    /// At the local player's seat there is also a token in a home socket: drop it in a slot to vote,
    /// take it out to withdraw. Other seats only show lamps.
    /// </summary>
    public class BallotRow : MonoBehaviour
    {
        public static readonly Color AgreeColor = new(0.3f, 0.78f, 0.45f);
        public static readonly Color DisagreeColor = new(0.9f, 0.3f, 0.28f);
        public static readonly Color AmendColor = new(0.98f, 0.72f, 0.2f);
        static readonly Color k_LampOff = new(0.12f, 0.12f, 0.14f);
        static readonly Color k_LampVoted = new(0.85f, 0.85f, 0.9f);

        const float k_SlotSpacing = 0.1f;

        public int Seat { get; private set; }
        public int PolicyIndex { get; private set; }
        public bool IsLocal { get; private set; }

        /// <summary>Raised at the local seat when the token is placed (choice) or taken out (None).</summary>
        public event System.Action<BallotRow, VoteChoice> TokenMoved;

        readonly KeyedSocket[] m_Slots = new KeyedSocket[3];
        readonly Renderer[] m_Lamps = new Renderer[3];
        KeyedSocket m_Home;
        XRGrabInteractable m_Token;
        TextMeshPro m_Label;

        static VoteChoice ChoiceAt(int i) => i == 0 ? VoteChoice.Agree : i == 1 ? VoteChoice.Disagree : VoteChoice.Amend;
        static Color ColorAt(int i) => i == 0 ? AgreeColor : i == 1 ? DisagreeColor : AmendColor;

        public void Build(int seat, int policyIndex, bool isLocal, Color tokenColor, Material surface, Material glow)
        {
            Seat = seat;
            PolicyIndex = policyIndex;
            IsLocal = isLocal;
            string key = $"ballot-{seat}-{policyIndex}";

            TableKit.Shape(PrimitiveType.Cube, "Plate", transform, new Vector3(0.05f, -0.004f, 0f), new Vector3(0.44f, 0.008f, 0.11f), surface);
            m_Label = TableKit.FlatLabel("Policy Label", transform, new Vector3(0.05f, 0.002f, 0.072f), "", 0.22f, Color.white);

            string[] names = { "AGREE", "DISAGREE", "AMEND" };
            for (int i = 0; i < 3; i++)
            {
                var x = -0.05f + i * k_SlotSpacing + 0.05f;
                var well = TableKit.Shape(PrimitiveType.Cylinder, $"Slot {names[i]}", transform, new Vector3(x, 0.001f, 0f), new Vector3(0.07f, 0.003f, 0.07f), glow);
                TableKit.Tint(well.GetComponent<Renderer>(), ColorAt(i), 0.6f);
                TableKit.FlatLabel("Label", transform, new Vector3(x, 0.002f, -0.048f), names[i], 0.14f, ColorAt(i));

                var lamp = TableKit.Shape(PrimitiveType.Sphere, "Lamp", transform, new Vector3(x, 0.01f, 0.035f), Vector3.one * 0.014f, glow);
                m_Lamps[i] = lamp.GetComponent<Renderer>();

                if (!isLocal) continue;
                m_Slots[i] = TableKit.Socket($"Socket {names[i]}", transform, new Vector3(x, 0.012f, 0f), key, 0.04f);
                m_Slots[i].Placed += OnPlaced;
                m_Slots[i].Removed += OnRemoved;
            }

            if (isLocal)
            {
                var homePos = new Vector3(-0.15f, 0.012f, 0f);
                TableKit.Shape(PrimitiveType.Cylinder, "Token Rest", transform, homePos - new Vector3(0, 0.011f, 0), new Vector3(0.06f, 0.002f, 0.06f), surface);
                m_Home = TableKit.Socket("Token Home", transform, homePos, key, 0.04f);

                var token = TableKit.Shape(PrimitiveType.Cylinder, "Vote Token", transform, homePos, new Vector3(0.045f, 0.006f, 0.045f), glow, true);
                TableKit.Tint(token.GetComponent<Renderer>(), tokenColor, 0.4f);
                m_Token = TableKit.Grabbable(token, key);
                m_Home.startingSelectedInteractable = m_Token;
            }

            SetOpen(false);
            ShowLamps(VoteChoice.None, false, false);
        }

        public void SetLabel(string text)
        {
            if (m_Label != null) m_Label.text = text;
        }

        /// <summary>Open = the slots accept the token (decision window).</summary>
        public void SetOpen(bool open)
        {
            foreach (var slot in m_Slots)
                if (slot != null) slot.socketActive = open;
        }

        public void ReturnToken()
        {
            if (m_Token != null) TableKit.SendHome(m_Token, m_Home);
        }

        /// <summary>
        /// Lamps: the chosen slot lights in its colour when the vote is visible; otherwise all three glow
        /// softly to show "has voted" without saying how.
        /// </summary>
        public void ShowLamps(VoteChoice choice, bool showChoice, bool highlightWinner)
        {
            for (int i = 0; i < 3; i++)
            {
                bool hasVoted = choice != VoteChoice.None;
                if (showChoice && ChoiceAt(i) == choice)
                    TableKit.Tint(m_Lamps[i], ColorAt(i), highlightWinner ? 4f : 2.5f);
                else if (!showChoice && hasVoted)
                    TableKit.Tint(m_Lamps[i], k_LampVoted, 0.8f);
                else
                    TableKit.Tint(m_Lamps[i], k_LampOff, 0f);
            }
        }

        void OnPlaced(KeyedSocket socket, GameObject _)
        {
            for (int i = 0; i < 3; i++)
                if (m_Slots[i] == socket) TokenMoved?.Invoke(this, ChoiceAt(i));
        }

        void OnRemoved(KeyedSocket socket, GameObject _) => TokenMoved?.Invoke(this, VoteChoice.None);

        void OnDestroy()
        {
            foreach (var slot in m_Slots)
            {
                if (slot == null) continue;
                slot.Placed -= OnPlaced;
                slot.Removed -= OnRemoved;
            }
        }
    }
}
