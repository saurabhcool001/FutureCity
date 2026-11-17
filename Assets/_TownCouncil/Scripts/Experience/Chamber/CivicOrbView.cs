using System.Collections;
using System.Collections.Generic;
using TownCouncil.Networking;
using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>
    /// CIVIC's presence: a floating sci-fi orb that breathes in and out.
    /// - The core glows brighter and swells on each breath; the outer shells breathe out of phase.
    /// - Holographic rings turn slowly on different axes; motes of light orbit the core.
    /// - On creation (and each time the chamber is entered) it grows from a spark with a burst of pulses.
    /// - Pulse() gives a one-off throb (e.g. when CIVIC speaks); IsSpeaking makes it breathe faster.
    /// - When the council is complete, it wakes up with stronger pulses.
    /// Every part is optional: designers can replace any piece and the rest still animates.
    /// </summary>
    public class CivicOrbView : MonoBehaviour
    {
        [Header("Parts")]
        public Renderer core;
        public Renderer[] shells = new Renderer[0];
        public Transform[] rings = new Transform[0];
        public ParticleSystem motes;
        public Light glow;

        [Header("Colour")]
        public Color color = new(1f, 0.42f, 0.16f);
        [Min(0f)] public float coreGlowMin = 1.2f;
        [Min(0f)] public float coreGlowMax = 4f;
        [Range(0f, 1f)] public float shellAlphaMin = 0.05f;
        [Range(0f, 1f)] public float shellAlphaMax = 0.22f;
        [Range(0f, 1f)] public float ringAlphaMin = 0.25f;
        [Range(0f, 1f)] public float ringAlphaMax = 0.8f;

        [Header("Breathing")]
        [Tooltip("Calm breathing rate. Speaking multiplies it.")]
        [Min(1f)] public float breathsPerMinute = 12f;
        [Range(0f, 0.5f)] public float coreSwell = 0.08f;
        [Range(0f, 0.5f)] public float shellSwell = 0.14f;
        [Min(1f)] public float speakingRateMultiplier = 2.5f;

        [Header("Floating")]
        public float bobHeight = 0.05f;
        public float bobSpeed = 0.45f;
        public float swayDegrees = 6f;

        [Header("Rings (degrees per second, per ring)")]
        public Vector3[] ringSpin = { new(0f, 18f, 0f), new(0f, -11f, 4f), new(6f, 7f, 0f) };

        [Header("Light")]
        [Min(0f)] public float lightMin = 0.6f;
        [Min(0f)] public float lightMax = 1.8f;

        [Header("Motes")]
        [Min(0f)] public float motesRateMin = 10f;
        [Min(0f)] public float motesRateMax = 35f;

        [Header("Awakening")]
        public bool playAwakenOnEnable = true;
        [Min(0.1f)] public float awakenSeconds = 2.4f;
        [Tooltip("Wake up with stronger pulses when every councillor is seated.")]
        public bool wakeWhenCouncilComplete = true;

        public bool IsSpeaking { get; set; }
        public float Breath { get; private set; }

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        readonly List<Renderer> m_RingRenderers = new();
        readonly List<Vector3> m_ShellScales = new();
        MaterialPropertyBlock m_Block;
        Vector3 m_LocalOrigin;
        Vector3 m_CoreScale;
        float m_Phase;
        float m_Pulse;
        float m_Presence = 1f;
        Coroutine m_Awakening;
        CouncilSession m_Session;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_LocalOrigin = transform.localPosition;
            if (core != null) m_CoreScale = core.transform.localScale;
            foreach (var s in shells) m_ShellScales.Add(s != null ? s.transform.localScale : Vector3.one);
            foreach (var ring in rings)
                if (ring != null) m_RingRenderers.AddRange(ring.GetComponentsInChildren<Renderer>(true));
        }

        void OnEnable()
        {
            if (playAwakenOnEnable) Awaken();
            CouncilSession.Spawned += Bind;
            if (CouncilSession.Instance != null) Bind(CouncilSession.Instance);
        }

        void OnDisable()
        {
            CouncilSession.Spawned -= Bind;
            Bind(null);
        }

        // Public API ----------------------------------------------------------------------------

        /// <summary>Grow from a spark into the full orb, with a burst of pulses.</summary>
        public void Awaken()
        {
            if (m_Awakening != null) StopCoroutine(m_Awakening);
            m_Awakening = StartCoroutine(AwakenRoutine());
        }

        /// <summary>A single throb of light and size. 1 = normal, 2 = strong.</summary>
        public void Pulse(float strength = 1f)
        {
            m_Pulse = Mathf.Max(m_Pulse, strength);
            if (motes != null) motes.Emit(Mathf.RoundToInt(25 * strength));
        }

        /// <summary>The "CIVIC is waking up" moment: stronger, quicker pulses for a few seconds.</summary>
        public void WakeUp() => StartCoroutine(WakeUpRoutine());

        // Animation -----------------------------------------------------------------------------

        void Update()
        {
            float dt = Time.deltaTime;
            float rate = breathsPerMinute / 60f * (IsSpeaking ? speakingRateMultiplier : 1f);
            m_Phase += dt * rate * Mathf.PI * 2f;

            // Smooth in-out breath: 0 = fully out, 1 = fully in.
            Breath = (1f - Mathf.Cos(m_Phase)) * 0.5f;
            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, dt * 1.8f);
            float pulse = m_Pulse * m_Pulse;

            AnimateFloat();
            AnimateCore(pulse);
            AnimateShells(pulse);
            AnimateRings(dt, pulse);

            if (glow != null)
                glow.intensity = (Mathf.Lerp(lightMin, lightMax, Breath) + pulse * 2.5f) * m_Presence;

            if (motes != null)
            {
                var emission = motes.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(motesRateMin, motesRateMax, Breath) * m_Presence;
            }
        }

        void AnimateFloat()
        {
            float t = Time.time;
            transform.localPosition = m_LocalOrigin + Vector3.up * (Mathf.Sin(t * bobSpeed * Mathf.PI * 2f) * bobHeight);
            transform.localRotation = Quaternion.Euler(
                Mathf.Sin(t * 0.37f) * swayDegrees, t * 4f, Mathf.Cos(t * 0.29f) * swayDegrees);
        }

        void AnimateCore(float pulse)
        {
            if (core == null) return;
            float swell = 1f + Breath * coreSwell + pulse * 0.25f;
            core.transform.localScale = m_CoreScale * (swell * m_Presence);

            core.GetPropertyBlock(m_Block);
            float glowAmount = (Mathf.Lerp(coreGlowMin, coreGlowMax, Breath) + pulse * 5f) * Mathf.Max(0.2f, m_Presence);
            m_Block.SetColor(k_EmissionColor, color * glowAmount);
            m_Block.SetColor(k_BaseColor, color);
            core.SetPropertyBlock(m_Block);
        }

        void AnimateShells(float pulse)
        {
            for (int i = 0; i < shells.Length; i++)
            {
                var shell = shells[i];
                if (shell == null) continue;

                // Shells breathe out of phase with the core, each a little later than the last.
                float b = (1f - Mathf.Cos(m_Phase + Mathf.PI + i * 0.6f)) * 0.5f;
                float swell = 1f + b * shellSwell * (1f + i * 0.5f) + pulse * 0.45f;
                shell.transform.localScale = m_ShellScales[i] * (swell * m_Presence);

                var c = color;
                c.a = (Mathf.Lerp(shellAlphaMin, shellAlphaMax, b) + pulse * 0.25f) / (1f + i) * m_Presence;
                shell.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_BaseColor, c);
                shell.SetPropertyBlock(m_Block);
            }
        }

        void AnimateRings(float dt, float pulse)
        {
            float speed = IsSpeaking ? 2f : 1f;
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null) continue;
                var spin = ringSpin.Length > 0 ? ringSpin[i % ringSpin.Length] : Vector3.up * 15f;
                rings[i].Rotate(spin * (dt * speed * (1f + pulse)), Space.Self);
            }

            var c = color;
            c.a = (Mathf.Lerp(ringAlphaMin, ringAlphaMax, Breath) + pulse * 0.3f) * m_Presence;
            foreach (var r in m_RingRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_BaseColor, c);
                r.SetPropertyBlock(m_Block);
            }
        }

        IEnumerator AwakenRoutine()
        {
            m_Presence = 0f;
            float t = 0f;
            int pulsesFired = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / awakenSeconds;
                m_Presence = EaseOutBack(Mathf.Clamp01(t));

                // Three pulses while it grows: a spark, a swell, a settle.
                if (pulsesFired < 3 && t >= pulsesFired * 0.3f)
                {
                    Pulse(pulsesFired == 0 ? 2f : 1.2f);
                    pulsesFired++;
                }
                yield return null;
            }
            m_Presence = 1f;
            m_Awakening = null;
        }

        IEnumerator WakeUpRoutine()
        {
            bool wasSpeaking = IsSpeaking;
            IsSpeaking = true;
            for (int i = 0; i < 4; i++)
            {
                Pulse(2f);
                yield return new WaitForSeconds(0.6f);
            }
            yield return new WaitForSeconds(1.5f);
            IsSpeaking = wasSpeaking;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // Council hook --------------------------------------------------------------------------

        void Bind(CouncilSession session)
        {
            if (m_Session != null) m_Session.PhaseChanged -= OnPhaseChanged;
            m_Session = session;
            if (m_Session != null) m_Session.PhaseChanged += OnPhaseChanged;
        }

        void OnPhaseChanged(CouncilPhase phase)
        {
            if (wakeWhenCouncilComplete && phase == CouncilPhase.Complete && isActiveAndEnabled) WakeUp();
        }
    }
}
