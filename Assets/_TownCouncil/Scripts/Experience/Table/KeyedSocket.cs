using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TownCouncil.Experience
{
    /// <summary>Marks an object (a token, a card) with the key of the sockets it may go into.</summary>
    public class SocketKey : MonoBehaviour
    {
        public string key;
    }

    /// <summary>
    /// A socket that only accepts objects carrying the same <see cref="SocketKey"/>, so a player's
    /// token can't land in another row or another player's slot.
    /// </summary>
    public class KeyedSocket : XRSocketInteractor
    {
        public string key;

        public event Action<KeyedSocket, GameObject> Placed;
        public event Action<KeyedSocket, GameObject> Removed;

        public override bool CanHover(IXRHoverInteractable interactable) => base.CanHover(interactable) && Matches(interactable);
        public override bool CanSelect(IXRSelectInteractable interactable) => base.CanSelect(interactable) && Matches(interactable);

        bool Matches(IXRInteractable interactable) =>
            interactable is Component c && c.TryGetComponent<SocketKey>(out var k) && k.key == key;

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            if (args.interactableObject is Component c) Placed?.Invoke(this, c.gameObject);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (args.interactableObject is Component c) Removed?.Invoke(this, c.gameObject);
        }
    }
}
