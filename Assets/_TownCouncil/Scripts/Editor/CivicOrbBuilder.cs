using System.Collections.Generic;
using TownCouncil.Experience;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TownCouncil.EditorTools
{
    /// <summary>
    /// Builds CIVIC's sci-fi orb: emissive core, translucent breathing shells, holographic rings
    /// and arcs, orbiting motes and a pulsing light. All animation lives in <see cref="CivicOrbView"/>.
    /// </summary>
    public static class CivicOrbBuilder
    {
        const string k_UrpParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        static readonly Color k_Orange = new(1f, 0.42f, 0.16f);

        [MenuItem(TCPaths.MenuRoot + "Rebuild CIVIC in Open Scene", priority = 3)]
        public static void RebuildInOpenScene()
        {
            Transform parent = null;
            var position = new Vector3(0, 1.95f, 0);

            var existing = Object.FindFirstObjectByType<CivicOrbView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                parent = existing.transform.parent;
                position = existing.transform.localPosition;
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
            else
            {
                foreach (var area in Object.FindObjectsByType<ExperienceArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (area.kind == AreaKind.Chamber) parent = area.contentRoot != null ? area.contentRoot.transform : area.transform;
            }

            var orb = Build(parent, position);
            Undo.RegisterCreatedObjectUndo(orb.gameObject, "Rebuild CIVIC");
            Selection.activeObject = orb.gameObject;
            EditorSceneManager.MarkSceneDirty(orb.gameObject.scene);
            Debug.Log("[Town Council] CIVIC rebuilt. Save the scene to keep it.");
        }

        public static CivicOrbView Build(Transform parent, Vector3 localPosition)
        {
            var root = new GameObject("CIVIC");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var coreMat = LitEmissive("CIVIC Core", k_Orange);
            var shellMat = Additive("CIVIC Shell", new Color(k_Orange.r, k_Orange.g, k_Orange.b, 0.15f), false);
            var ringMat = Additive("CIVIC Ring", k_Orange, false);
            var moteMat = Additive("CIVIC Motes", k_Orange, true);

            // Core and shells
            var core = Sphere("Core", root.transform, 0.15f, coreMat);
            var shells = new List<Renderer>
            {
                Sphere("Shell Inner", root.transform, 0.24f, shellMat),
                Sphere("Shell Outer", root.transform, 0.34f, shellMat),
                Sphere("Aura", root.transform, 0.48f, shellMat)
            };

            // Rings: a full ring, a ring of three arcs, and a faint wide ring
            var rings = new List<Transform>
            {
                Ring("Ring A", root.transform, Quaternion.Euler(72f, 0f, 0f), 0.27f, 0.006f, ringMat, new[] { (0f, 360f) }),
                Ring("Ring B · Arcs", root.transform, Quaternion.Euler(18f, 0f, 58f), 0.31f, 0.009f, ringMat,
                    new[] { (0f, 80f), (120f, 200f), (240f, 320f) }),
                Ring("Ring C", root.transform, Quaternion.Euler(-38f, 30f, 0f), 0.37f, 0.003f, ringMat, new[] { (0f, 360f) })
            };

            var motes = Motes(root.transform, moteMat);

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = k_Orange;
            light.range = 4.5f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;

            var view = root.AddComponent<CivicOrbView>();
            view.core = core;
            view.shells = shells.ToArray();
            view.rings = rings.ToArray();
            view.motes = motes;
            view.glow = light;
            view.color = k_Orange;
            return view;
        }

        // Parts ---------------------------------------------------------------------------------

        static Renderer Sphere(string name, Transform parent, float diameter, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static Transform Ring(string name, Transform parent, Quaternion tilt, float radius, float width,
            Material material, (float from, float to)[] arcs)
        {
            var ring = new GameObject(name).transform;
            ring.SetParent(parent, false);
            ring.localRotation = tilt;

            for (int a = 0; a < arcs.Length; a++)
            {
                var (from, to) = arcs[a];
                bool full = Mathf.Approximately(to - from, 360f);
                var go = new GameObject(full ? "Line" : $"Arc {a + 1}");
                go.transform.SetParent(ring, false);

                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = full;
                line.widthMultiplier = width;
                line.numCapVertices = full ? 0 : 2;
                line.alignment = LineAlignment.View;
                line.sharedMaterial = material;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;

                int points = Mathf.Max(8, Mathf.CeilToInt((to - from) / 4f));
                line.positionCount = full ? points : points + 1;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float deg = Mathf.Lerp(from, to, (float)i / (full ? points : points));
                    float rad = deg * Mathf.Deg2Rad;
                    line.SetPosition(i, new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius);
                }
            }
            return ring;
        }

        static ParticleSystem Motes(Transform parent, Material material)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.024f);
            main.startColor = k_Orange;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.maxParticles = 300;

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.22f;
            shape.radiusThickness = 0.35f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.02f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(k_Orange, 0.4f), new GradientColorKey(k_Orange, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // Materials -----------------------------------------------------------------------------

        static Material LitEmissive(string name, Color color)
        {
            var path = MaterialPath(name);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 3f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.9f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>URP Particles/Unlit set to transparent additive: glows without lighting.</summary>
        static Material Additive(string name, Color color, bool softParticleTexture)
        {
            var path = MaterialPath(name);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var source = softParticleTexture ? AssetDatabase.LoadAssetAtPath<Material>(k_UrpParticleMaterial) : null;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (source != null) mat = new Material(source);
            else if (shader != null) mat = new Material(shader);
            else mat = new Material(Shader.Find("Sprites/Default"));

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);                         // Transparent
                mat.SetFloat("_Blend", 2f);                           // Additive
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.One);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static string MaterialPath(string name)
        {
            TCPaths.EnsureFolder(TCPaths.Materials);
            return $"{TCPaths.Materials}/{TCPaths.SafeFileName(name)}.mat";
        }
    }
}
