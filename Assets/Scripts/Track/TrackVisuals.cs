using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swarmblastrr.Track
{
    /// <summary>
    /// Builds track scenery from built-in primitive meshes and runtime materials so the project
    /// needs no prefabs or imported assets. Materials are cached per colour and labels are
    /// <see cref="TextMesh"/> objects facing -Z, toward the camera that sits behind the crowd.
    /// </summary>
    public static class TrackVisuals
    {
        private const string SolidShaderName = "Swarmblastrr/Solid";
        private const string TransparentShaderName = "Swarmblastrr/SolidTransparent";
        private const string StandardShaderName = "Standard";
        private const string LegacyDiffuseShaderName = "Legacy Shaders/Diffuse";

        /// <summary>How far behind Z = 0 the floor and rails extend so the starting crowd stands on something.</summary>
        private const float StartMargin = 12f;
        private const float RailThickness = 0.3f;
        private const float RailHeight = 0.5f;
        private const int LabelFontSize = 64;

        /// <summary>
        /// Pitch applied to labels. A TextMesh with identity rotation faces -Z; a positive X
        /// rotation in Unity turns that -Z normal upward, toward the camera that looks down at the
        /// lane from above and behind, so the glyphs are less foreshortened.
        /// </summary>
        private const float LabelTiltDegrees = 15f;

        private static readonly Dictionary<Color, Material> _materialCache = new Dictionary<Color, Material>();
        private static Font _labelFont;
        private static bool _labelFontSearched;

        /// <summary>
        /// A scaled cube whose top surface is Y = 0 (centre at Y = -0.5). It spans
        /// Z = -12 .. <paramref name="length"/> and is slightly wider than the lane so the rails
        /// have something to stand on.
        /// </summary>
        public static GameObject CreateFloor(Transform parent, float length, float laneHalfWidth, Color color)
        {
            float forward = Mathf.Max(1f, length);
            float total = forward + StartMargin;
            float centreZ = (forward - StartMargin) * 0.5f;
            float width = Mathf.Max(0.1f, laneHalfWidth) * 2f + RailThickness * 2f;

            return CreatePrimitive(
                "Floor",
                parent,
                CubeMesh(),
                SolidMaterial(color),
                new Vector3(0f, -0.5f, centreZ),
                new Vector3(width, 1f, total));
        }

        /// <summary>
        /// Two thin cubes just outside the lane edges (X = ±laneHalfWidth), spanning the same Z
        /// range as <see cref="CreateFloor"/>. Returns their common "Rails" parent.
        /// </summary>
        public static GameObject CreateRails(Transform parent, float length, float laneHalfWidth, Color color)
        {
            float forward = Mathf.Max(1f, length);
            float total = forward + StartMargin;
            float centreZ = (forward - StartMargin) * 0.5f;
            float railX = Mathf.Max(0.1f, laneHalfWidth) + RailThickness * 0.5f;

            GameObject rails = new GameObject("Rails");
            rails.transform.SetParent(parent, false);
            rails.transform.localPosition = Vector3.zero;

            Material material = SolidMaterial(color);
            Vector3 scale = new Vector3(RailThickness, RailHeight, total);
            CreatePrimitive("RailLeft", rails.transform, CubeMesh(), material, new Vector3(-railX, RailHeight * 0.5f, centreZ), scale);
            CreatePrimitive("RailRight", rails.transform, CubeMesh(), material, new Vector3(railX, RailHeight * 0.5f, centreZ), scale);
            return rails;
        }

        /// <summary>
        /// World-space text label built from a <see cref="TextMesh"/>: anchored middle-centre,
        /// facing -Z (toward the camera behind the crowd) with a slight upward tilt. The glyph
        /// height in world units is roughly <paramref name="characterSize"/> * 6.4.
        /// </summary>
        public static TextMesh CreateLabel(Transform parent, string text, Color color, float characterSize, Vector3 localPos)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(LabelTiltDegrees, 0f, 0f);
            go.transform.localScale = Vector3.one;

            TextMesh textMesh = go.AddComponent<TextMesh>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<MeshRenderer>();
            }

            Font font = LabelFont();
            if (font != null)
            {
                textMesh.font = font;
                renderer.sharedMaterial = font.material;
            }

            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = LabelFontSize;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.characterSize = characterSize;
            textMesh.color = color;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return textMesh;
        }

        /// <summary>
        /// A material tinted <paramref name="color"/>, cached per colour. Opaque colours
        /// (alpha ≥ 0.999) use "Swarmblastrr/Solid", others "Swarmblastrr/SolidTransparent"; when
        /// <see cref="Shader.Find"/> cannot locate those, the Standard shader is used instead.
        /// </summary>
        public static Material SolidMaterial(Color color)
        {
            Material cached;
            if (_materialCache.TryGetValue(color, out cached) && cached != null)
            {
                return cached;
            }

            bool opaque = color.a >= 0.999f;
            Shader shader = Shader.Find(opaque ? SolidShaderName : TransparentShaderName);
            Material material;
            if (shader != null)
            {
                material = new Material(shader);
                material.color = color;
            }
            else
            {
                material = CreateStandardFallback(color, opaque);
            }

            material.name = (opaque ? "TrackSolid " : "TrackTransparent ") + ColorUtility.ToHtmlStringRGBA(color);
            material.enableInstancing = true;
            _materialCache[color] = material;
            return material;
        }

        /// <summary>
        /// Child GameObject with a MeshFilter/MeshRenderer pair showing <paramref name="mesh"/> with
        /// <paramref name="material"/>. No collider, no shadows.
        /// </summary>
        internal static GameObject CreatePrimitive(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>Built-in 1x1x1 cube centred on the origin.</summary>
        internal static Mesh CubeMesh()
        {
            return BuiltinMesh("Cube.fbx");
        }

        /// <summary>Built-in capsule: radius 0.5, 2 units tall, centred on the origin.</summary>
        internal static Mesh CapsuleMesh()
        {
            return BuiltinMesh("Capsule.fbx");
        }

        /// <summary>Built-in cylinder: radius 0.5, 2 units tall, centred on the origin.</summary>
        internal static Mesh CylinderMesh()
        {
            return BuiltinMesh("Cylinder.fbx");
        }

        private static Mesh BuiltinMesh(string resourceName)
        {
            return Resources.GetBuiltinResource<Mesh>(resourceName);
        }

        private static Font LabelFont()
        {
            if (_labelFont != null)
            {
                return _labelFont;
            }

            if (_labelFontSearched)
            {
                return null;
            }

            _labelFontSearched = true;
            _labelFont = TryLoadFont("LegacyRuntime.ttf");
            if (_labelFont == null)
            {
                _labelFont = TryLoadFont("Arial.ttf");
            }

            if (_labelFont == null)
            {
                Debug.LogWarning("[Swarmblastrr] No built-in font found; track labels will be invisible.");
            }

            return _labelFont;
        }

        private static Font TryLoadFont(string resourceName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(resourceName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Material CreateStandardFallback(Color color, bool opaque)
        {
            Shader shader = Shader.Find(StandardShaderName);
            if (shader == null)
            {
                shader = Shader.Find(LegacyDiffuseShaderName);
            }

            Material material = new Material(shader);
            material.color = color;

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.2f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (!opaque && material.HasProperty("_Mode"))
            {
                // Standard shader "Fade" rendering mode, set up the way the editor's material inspector does it.
                material.SetFloat("_Mode", 2f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }
    }
}
