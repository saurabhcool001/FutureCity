using System;
using System.Collections.Generic;
using TownCouncil.Core;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// A building on the city model. Maps each state (Normal, Closed, Upgraded...) to the objects
    /// that show it, and can glow while a policy that affects it is being discussed.
    /// Designers own the visuals; gameplay only calls SetState and SetHighlighted.
    /// </summary>
    public class BuildingView : MonoBehaviour
    {
        [Serializable]
        public class StateVisual
        {
            public BuildingStateId state;
            [Tooltip("Objects switched on in this state (and off in every other state).")]
            public GameObject[] show = Array.Empty<GameObject>();
        }

        public BuildingId building;
        public List<StateVisual> states = new();
        public Renderer[] highlightRenderers = Array.Empty<Renderer>();
        public Color highlightColor = new(1f, 0.45f, 0.2f);

        public BuildingStateId CurrentState { get; private set; }
        public bool IsHighlighted { get; private set; }

        MaterialPropertyBlock m_Block;

        public void SetState(BuildingStateId state)
        {
            CurrentState = state;
            foreach (var visual in states)
            {
                bool on = visual.state == state;
                foreach (var go in visual.show)
                    if (go != null) go.SetActive(on);
            }
        }

        public void SetHighlighted(bool on)
        {
            IsHighlighted = on;
            m_Block ??= new MaterialPropertyBlock();
            foreach (var r in highlightRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(m_Block);
                m_Block.SetColor("_EmissionColor", on ? highlightColor * 2f : Color.black);
                r.SetPropertyBlock(m_Block);
            }
        }
    }
}
