using System;
using System.Collections;
using System.Collections.Generic;
using TownCouncil.Core;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// A building on the city model. Maps each state (Normal, Closed, Upgraded...) to the objects
    /// that show it, and can glow while a policy that affects it is being discussed.
    /// With no art for a state, the placeholder mesh is tinted and resized from the state's
    /// placeholder settings, so every decision is visible from day one.
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

        [Header("Placeholder (used when a state has no art)")]
        [Tooltip("The mesh tinted and resized for states without art. Usually the placeholder box.")]
        public Transform placeholderMesh;
        public Color baseColor = new(0.85f, 0.85f, 0.85f);

        public BuildingStateId CurrentState { get; private set; }
        public bool IsHighlighted { get; private set; }

        MaterialPropertyBlock m_Block;
        Vector3 m_BaseScale;
        Vector3 m_BasePosition;
        Color m_Tint;
        float m_Glow;
        Coroutine m_Change;

        void Awake()
        {
            if (placeholderMesh != null)
            {
                m_BaseScale = placeholderMesh.localScale;
                m_BasePosition = placeholderMesh.localPosition;
            }
            m_Tint = baseColor;
        }

        /// <summary>Show a state. Animate = a small pop and flash so the change is noticed.</summary>
        public void SetState(BuildingStateId state, bool animate = false)
        {
            if (state == CurrentState && !animate) return;
            CurrentState = state;

            bool hasArt = false;
            foreach (var visual in states)
            {
                bool on = visual.state == state;
                hasArt |= on;
                foreach (var go in visual.show)
                    if (go != null) go.SetActive(on);
            }

            if (!hasArt && placeholderMesh != null)
            {
                m_Tint = state != null ? state.tint : baseColor;
                m_Glow = state != null ? state.glow : 0f;
                float height = state != null ? state.heightMultiplier : 1f;
                if (m_Change != null) StopCoroutine(m_Change);
                if (animate && isActiveAndEnabled) m_Change = StartCoroutine(Change(height));
                else ApplyPlaceholder(height, 0f);
            }
            else if (animate && isActiveAndEnabled)
            {
                if (m_Change != null) StopCoroutine(m_Change);
                m_Change = StartCoroutine(Flash());
            }
        }

        public void SetHighlighted(bool on)
        {
            IsHighlighted = on;
            Paint(0f);
        }

        IEnumerator Change(float targetHeight)
        {
            float startHeight = placeholderMesh.localScale.y / Mathf.Max(0.0001f, m_BaseScale.y);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.9f;
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float pop = Mathf.Sin(t * Mathf.PI) * 0.15f;
                ApplyPlaceholder(Mathf.Lerp(startHeight, targetHeight, eased) + pop, Mathf.Sin(t * Mathf.PI) * 4f);
                yield return null;
            }
            ApplyPlaceholder(targetHeight, 0f);
            m_Change = null;
        }

        IEnumerator Flash()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.9f;
                Paint(Mathf.Sin(t * Mathf.PI) * 4f);
                yield return null;
            }
            Paint(0f);
            m_Change = null;
        }

        void ApplyPlaceholder(float heightMultiplier, float flash)
        {
            var scale = m_BaseScale;
            scale.y *= heightMultiplier;
            placeholderMesh.localScale = scale;
            // Keep the base on the ground while the height changes.
            var pos = m_BasePosition;
            pos.y += (scale.y - m_BaseScale.y) * 0.5f;
            placeholderMesh.localPosition = pos;
            Paint(flash);
        }

        void Paint(float flash)
        {
            m_Block ??= new MaterialPropertyBlock();
            var emission = (IsHighlighted ? highlightColor * 2f : Color.black) + m_Tint * (m_Glow + flash);
            foreach (var r in highlightRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(m_Block);
                m_Block.SetColor("_BaseColor", m_Tint);
                m_Block.SetColor("_Color", m_Tint);
                m_Block.SetColor("_EmissionColor", emission);
                r.SetPropertyBlock(m_Block);
            }
        }
    }
}
