using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TownCouncil.Experience
{
    /// <summary>
    /// UI radar ("fingerprint") chart: a web of rings and spokes with the values drawn as a filled
    /// shape. Values are 0-1, one per axis. Draws itself; no textures needed.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RadarChartGraphic : MaskableGraphic
    {
        [SerializeField] List<float> m_Values = new() { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
        public Color webColor = new(1f, 1f, 1f, 0.18f);
        public Color fillColor = new(1f, 0.42f, 0.16f, 0.45f);
        public Color outlineColor = new(1f, 0.55f, 0.3f, 1f);
        [Min(1)] public int rings = 4;
        public float lineWidth = 3f;

        public void SetValues(IList<float> values)
        {
            m_Values.Clear();
            foreach (var v in values) m_Values.Add(Mathf.Clamp01(v));
            SetVerticesDirty();
        }

        /// <summary>Position of an axis tip, for placing labels (local space of this graphic).</summary>
        public Vector2 AxisPoint(int axis, float radiusScale = 1f)
        {
            int n = Mathf.Max(3, m_Values.Count);
            float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f * radiusScale;
            float a = Mathf.PI * 0.5f - axis * Mathf.PI * 2f / n;
            return rectTransform.rect.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = m_Values.Count;
            if (n < 3) return;

            // Web: rings and spokes
            for (int ring = 1; ring <= rings; ring++)
            {
                float s = (float)ring / rings;
                for (int i = 0; i < n; i++) Line(vh, AxisPoint(i, s), AxisPoint((i + 1) % n, s), webColor, lineWidth * 0.6f);
            }
            for (int i = 0; i < n; i++) Line(vh, rectTransform.rect.center, AxisPoint(i), webColor, lineWidth * 0.6f);

            // Value shape: a fan from the centre
            int centre = vh.currentVertCount;
            vh.AddVert(rectTransform.rect.center, fillColor, Vector2.zero);
            for (int i = 0; i < n; i++) vh.AddVert(AxisPoint(i, m_Values[i]), fillColor, Vector2.zero);
            for (int i = 0; i < n; i++) vh.AddTriangle(centre, centre + 1 + i, centre + 1 + (i + 1) % n);

            for (int i = 0; i < n; i++)
                Line(vh, AxisPoint(i, m_Values[i]), AxisPoint((i + 1) % n, m_Values[(i + 1) % n]), outlineColor, lineWidth);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
        {
            var dir = (b - a).normalized;
            var normal = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
