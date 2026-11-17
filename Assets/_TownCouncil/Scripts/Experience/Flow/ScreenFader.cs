using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TownCouncil.Experience
{
    /// <summary>
    /// Fades the headset view to black and back. Builds its own overlay canvas in front of the
    /// XR camera, so it works with any rig.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] Color m_FadeColor = Color.black;
        [Tooltip("Start fully black and fade in once the first area is ready.")]
        [SerializeField] bool m_StartBlack = true;

        CanvasGroup m_Group;
        Camera m_Camera;
        Coroutine m_Running;

        public float Alpha => m_Group != null ? m_Group.alpha : 0f;
        public bool IsBlack => Alpha >= 0.999f;

        void Awake() => EnsureOverlay();

        void LateUpdate()
        {
            // The rig's camera can be swapped or enabled late; keep the overlay attached to it.
            if (m_Camera == null || !m_Camera.isActiveAndEnabled) EnsureOverlay();
        }

        public IEnumerator FadeOut(float seconds) => FadeTo(1f, seconds);
        public IEnumerator FadeIn(float seconds) => FadeTo(0f, seconds);

        public void SetInstant(float alpha)
        {
            EnsureOverlay();
            if (m_Group != null) m_Group.alpha = alpha;
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            EnsureOverlay();
            if (m_Group == null) yield break;
            if (m_Running != null) StopCoroutine(m_Running);

            float start = m_Group.alpha;
            float t = 0f;
            seconds = Mathf.Max(0.01f, seconds);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                m_Group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            m_Group.alpha = target;
            m_Group.blocksRaycasts = target > 0.5f;
        }

        void EnsureOverlay()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (m_Group != null && m_Camera == cam) return;

            float alpha = m_Group != null ? m_Group.alpha : (m_StartBlack ? 1f : 0f);
            if (m_Group != null) Destroy(m_Group.gameObject);

            m_Camera = cam;
            var go = new GameObject("Screen Fader Overlay", typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(cam.transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.02f;
            canvas.sortingOrder = short.MaxValue;

            var image = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(go.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var img = image.GetComponent<Image>();
            img.color = m_FadeColor;
            img.raycastTarget = false;

            m_Group = go.GetComponent<CanvasGroup>();
            m_Group.alpha = alpha;
            m_Group.interactable = false;
            m_Group.blocksRaycasts = false;
        }
    }
}
