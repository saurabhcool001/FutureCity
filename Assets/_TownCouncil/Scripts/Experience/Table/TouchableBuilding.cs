using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TownCouncil.Experience
{
    /// <summary>
    /// Touch (poke, grab or point and select) a building on the city model to hear a resident's view
    /// for the current round. Falls back to the building's description when no clip is set.
    /// </summary>
    [RequireComponent(typeof(BuildingView))]
    public class TouchableBuilding : MonoBehaviour
    {
        public XRBaseInteractable interactable;
        public AudioSource audioSource;

        BuildingView m_View;

        void Awake()
        {
            m_View = GetComponent<BuildingView>();
            if (interactable == null) interactable = GetComponent<XRBaseInteractable>();
            if (interactable != null) interactable.selectEntered.AddListener(OnSelected);
        }

        void OnDestroy()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnSelected);
        }

        void OnSelected(SelectEnterEventArgs _) => Touch();

        public void Touch()
        {
            var player = PerspectivePlayer.Instance;
            if (player == null || m_View.building == null) return;
            player.Play(m_View.building, FindClip(), audioSource, transform);
        }

        PerspectiveClip FindClip()
        {
            var game = CouncilGame.Instance;
            var round = game != null ? game.CurrentRound : null;
            if (round == null) return null;
            foreach (var clip in round.perspectives)
                if (clip != null && clip.building == m_View.building) return clip;
            return null;
        }
    }
}
