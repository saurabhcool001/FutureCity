using UnityEngine;

namespace TownCouncil.Experience
{
    /// <summary>Keeps a nameplate or label turned toward the local headset.</summary>
    public class FaceCamera : MonoBehaviour
    {
        public bool yawOnly = true;
        Camera m_Camera;

        void LateUpdate()
        {
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Camera == null) return;
            var dir = transform.position - m_Camera.transform.position;
            if (yawOnly) dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
