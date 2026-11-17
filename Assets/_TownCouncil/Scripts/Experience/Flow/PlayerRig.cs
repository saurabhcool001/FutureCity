using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace TownCouncil.Experience
{
    /// <summary>Moves the local XR rig to a spot in the world, facing that spot's forward.</summary>
    public class PlayerRig : MonoBehaviour
    {
        [SerializeField] XROrigin m_Origin;
        [SerializeField] TeleportationProvider m_Teleporter;

        public XROrigin Origin
        {
            get
            {
                if (m_Origin == null) m_Origin = FindFirstObjectByType<XROrigin>();
                return m_Origin;
            }
        }

        public IEnumerator MoveTo(Transform target)
        {
            if (target == null || Origin == null) yield break;
            if (m_Teleporter == null) m_Teleporter = Origin.GetComponentInChildren<TeleportationProvider>();

            if (m_Teleporter != null && m_Teleporter.isActiveAndEnabled)
            {
                var request = new TeleportRequest
                {
                    destinationPosition = target.position,
                    destinationRotation = target.rotation,
                    matchOrientation = MatchOrientation.TargetUpAndForward,
                    requestTime = Time.time
                };
                if (m_Teleporter.QueueTeleportRequest(request))
                {
                    // The provider applies the request over the next frames.
                    yield return null;
                    yield return null;
                    yield return null;
                    yield break;
                }
            }

            // Fallback when the rig has no teleportation provider.
            var controller = Origin.GetComponent<CharacterController>();
            bool hadController = controller != null && controller.enabled;
            if (hadController) controller.enabled = false;

            Origin.MatchOriginUpCameraForward(target.up, target.forward);
            Origin.MoveCameraToWorldLocation(target.position + target.up * Origin.CameraInOriginSpaceHeight);

            if (hadController) controller.enabled = true;
            yield return null;
        }
    }
}
