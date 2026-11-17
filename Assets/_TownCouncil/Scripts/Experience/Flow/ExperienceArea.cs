using UnityEngine;
using UnityEngine.Events;

namespace TownCouncil.Experience
{
    public enum AreaKind
    {
        Welcome,
        Foyer,
        RoleRoom,
        Chamber
    }

    /// <summary>
    /// A place the player can be in: the welcome space, the foyer (lobby), a role's personal room
    /// or the council chamber. Only the area the local player is in has its content switched on.
    /// Designers build an area's look freely under its Content root.
    /// </summary>
    public class ExperienceArea : MonoBehaviour
    {
        public AreaKind kind;
        [Tooltip("Role rooms only: index of the role in the scenario's role list.")]
        public int roleIndex = -1;
        [Tooltip("Where the player stands when they arrive. Forward = the way they face.")]
        public Transform spawnPoint;
        [Tooltip("Switched on only while the local player is here. Leave empty to keep it always on.")]
        public GameObject contentRoot;
        [Tooltip("Optional: where shared panels (like the role card panel) are placed in this area.")]
        public Transform panelAnchor;

        [Header("Designer hooks")]
        public UnityEvent onEntered = new();
        public UnityEvent onExited = new();

        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

        public void SetActive(bool active)
        {
            if (contentRoot != null) contentRoot.SetActive(active);
            if (active) onEntered?.Invoke();
            else onExited?.Invoke();
        }

        void OnDrawGizmos()
        {
            var p = SpawnPoint;
            Gizmos.color = new Color(1f, 0.55f, 0.2f);
            Gizmos.DrawWireSphere(p.position + Vector3.up * 0.05f, 0.3f);
            Gizmos.DrawLine(p.position + Vector3.up * 1.6f, p.position + Vector3.up * 1.6f + p.forward * 0.6f);
        }
    }
}
