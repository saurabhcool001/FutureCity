using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TownCouncil.Experience
{
    /// <summary>Small runtime builders shared by the table views (tokens, sockets, labels, lamps).</summary>
    public static class TableKit
    {
        static MaterialPropertyBlock s_Block;

        public static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (!keepCollider)
            {
                Object.DestroyImmediate(collider);
            }
            else if (type == PrimitiveType.Cylinder)
            {
                // A cylinder's default capsule collider becomes a ball when the cylinder is flat (a token);
                // use the real shape instead. Convex so it works on a moving rigidbody.
                Object.DestroyImmediate(collider);
                var mesh = go.AddComponent<MeshCollider>();
                mesh.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mesh.convex = true;
            }
            return go;
        }

        public static void Tint(Renderer renderer, Color color, float glow = 0f)
        {
            if (renderer == null) return;
            s_Block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(s_Block);
            s_Block.SetColor("_BaseColor", color);
            s_Block.SetColor("_Color", color);
            s_Block.SetColor("_EmissionColor", color * glow);
            renderer.SetPropertyBlock(s_Block);
        }

        /// <summary>A grabbable object with a key (token, card). The collider comes from the primitive.</summary>
        public static XRGrabInteractable Grabbable(GameObject go, string key)
        {
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // If it ever overlaps something, ease it out instead of firing it across the room.
            body.maxDepenetrationVelocity = 0.3f;
            go.AddComponent<SocketKey>().key = key;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            return grab;
        }

        public static KeyedSocket Socket(string name, Transform parent, Vector3 localPosition, string key, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = radius;
            var socket = go.AddComponent<KeyedSocket>();
            socket.key = key;
            socket.showInteractableHoverMeshes = true;
            socket.recycleDelayTime = 0.2f;
            return socket;
        }

        /// <summary>World-space text lying flat on the table, readable from the seat (top edge toward the table centre).</summary>
        public static TextMeshPro FlatLabel(string name, Transform parent, Vector3 localPosition, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(0.4f, 0.06f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = size;
            tmp.fontSizeMin = size * 0.4f;
            return tmp;
        }

        /// <summary>Moves a grabbable back into its home socket, wherever it is.</summary>
        public static void SendHome(XRGrabInteractable grab, KeyedSocket home)
        {
            if (grab == null || home == null) return;
            var manager = grab.interactionManager;
            if (grab.isSelected && manager != null) manager.CancelInteractableSelection((IXRSelectInteractable)grab);

            var target = home.attachTransform != null ? home.attachTransform : home.transform;
            grab.transform.SetPositionAndRotation(target.position, target.rotation);
            if (grab.TryGetComponent<Rigidbody>(out var body))
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            if (manager != null && home.isActiveAndEnabled && home.CanSelect((IXRSelectInteractable)grab))
                manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)home, (IXRSelectInteractable)grab);
        }
    }
}
