using System.Collections.Generic;
using TownCouncil.Core;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>The model of the town on the council table. Finds buildings by their BuildingId.</summary>
    public class CityModelView : MonoBehaviour
    {
        readonly Dictionary<BuildingId, BuildingView> m_Buildings = new();

        void Awake()
        {
            foreach (var b in GetComponentsInChildren<BuildingView>(true))
                if (b.building != null) m_Buildings[b.building] = b;
        }

        public bool TryGet(BuildingId id, out BuildingView view) => m_Buildings.TryGetValue(id, out view);

        public void Highlight(IEnumerable<BuildingId> ids, bool on)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (id != null && m_Buildings.TryGetValue(id, out var view)) view.SetHighlighted(on);
        }

        public void ClearHighlights()
        {
            foreach (var view in m_Buildings.Values) view.SetHighlighted(false);
        }

        public void Apply(IEnumerable<CityChange> changes)
        {
            if (changes == null) return;
            foreach (var change in changes)
                if (change.building != null && m_Buildings.TryGetValue(change.building, out var view))
                    view.SetState(change.state);
        }
    }
}
