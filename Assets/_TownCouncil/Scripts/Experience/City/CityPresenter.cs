using System.Collections;
using System.Collections.Generic;
using TownCouncil.Core;
using TownCouncil.Networking;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// Keeps the table's city model in step with the game:
    /// buildings a policy affects glow while it's discussed; in the future view the lights dim
    /// and the round's decisions change the buildings one by one; late joiners see the town as it is.
    /// </summary>
    public class CityPresenter : MonoBehaviour
    {
        public CityModelView city;
        [Tooltip("Lights dimmed during the future view (the sun, chamber lights...).")]
        public Light[] dimLights = new Light[0];
        [Tooltip("Light level while dimmed, as a fraction of normal.")]
        [Range(0f, 1f)] public float dimTo = 0.25f;
        public float dimSeconds = 1.2f;
        [Tooltip("Pause before the first change, and between changes, in the future view.")]
        public float changeInterval = 1.4f;

        CouncilGame m_Game;
        float[] m_LightIntensity;
        Coroutine m_Future;
        Coroutine m_Dim;
        float m_DimAmount;

        void Start()
        {
            m_LightIntensity = new float[dimLights.Length];
            for (int i = 0; i < dimLights.Length; i++) m_LightIntensity[i] = dimLights[i] != null ? dimLights[i].intensity : 0f;
        }

        void Update()
        {
            if (m_Game != null || CouncilGame.Instance == null || !CouncilGame.Instance.IsSpawned) return;
            m_Game = CouncilGame.Instance;
            m_Game.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(m_Game.Phase);
        }

        void OnDisable() => SetDimInstant(0f);

        void OnDestroy()
        {
            if (m_Game != null) m_Game.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (city == null) return;

            city.ClearHighlights();
            if (phase == GamePhase.Proposal || phase == GamePhase.Deciding)
                foreach (var p in m_Game.ActivePolicies)
                {
                    var policy = m_Game.GetPolicy(p);
                    if (policy != null) city.Highlight(policy.affectedBuildings, true);
                }

            if (phase == GamePhase.FutureView)
            {
                DimTo(1f);
                if (m_Future != null) StopCoroutine(m_Future);
                m_Future = StartCoroutine(PlayFuture());
                return;
            }

            DimTo(0f);
            if (m_Future != null) { StopCoroutine(m_Future); m_Future = null; }

            // Outside the future view, show the town as decided before this round (or all of it at the end).
            city.ShowCity(phase == GamePhase.Epilogue ? m_Game.Record.City : CityBefore(m_Game.RoundIndex));
        }

        IReadOnlyDictionary<BuildingId, BuildingStateId> CityBefore(int roundIndex)
        {
            var earlier = new List<PolicyResult>();
            foreach (var r in m_Game.Record.Results)
                if (r.RoundIndex < roundIndex) earlier.Add(r);
            return new CouncilRecord(m_Game.Scenario, earlier).City;
        }

        IEnumerator PlayFuture()
        {
            city.ShowCity(CityBefore(m_Game.RoundIndex));
            yield return new WaitForSeconds(changeInterval);

            foreach (var r in m_Game.Record.Results)
            {
                if (r.RoundIndex != m_Game.RoundIndex) continue;
                var outcome = m_Game.Record.GetPolicy(r.RoundIndex, r.PolicyIndex)?.OutcomeFor(r.Choice);
                if (outcome == null || outcome.cityChanges.Count == 0) continue;
                city.Apply(outcome.cityChanges, true);
                yield return new WaitForSeconds(changeInterval);
            }
            m_Future = null;
        }

        void DimTo(float amount)
        {
            if (Mathf.Approximately(amount, m_DimAmount) && m_Dim == null) return;
            if (m_Dim != null) StopCoroutine(m_Dim);
            m_Dim = StartCoroutine(DimRoutine(amount));
        }

        IEnumerator DimRoutine(float target)
        {
            float start = m_DimAmount;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, dimSeconds);
                SetDimInstant(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetDimInstant(target);
            m_Dim = null;
        }

        /// <summary>0 = normal lighting, 1 = fully dimmed to the dimTo level.</summary>
        void SetDimInstant(float amount)
        {
            m_DimAmount = amount;
            if (m_LightIntensity == null) return;
            for (int i = 0; i < dimLights.Length; i++)
                if (dimLights[i] != null)
                    dimLights[i].intensity = m_LightIntensity[i] * Mathf.Lerp(1f, dimTo, amount);
        }
    }
}
