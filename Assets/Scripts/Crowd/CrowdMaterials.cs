using UnityEngine;

namespace Swarmblastrr.Crowd
{
    /// <summary>
    /// Factory for the instanced crowd material and the default unit mesh. Everything is created
    /// at runtime from built-in resources so no asset references are needed.
    /// </summary>
    public static class CrowdMaterials
    {
        private const string CrowdShaderName = "Swarmblastrr/CrowdInstanced";

        private static Mesh _defaultMesh;

        /// <summary>Returns a new instanced material tinted <paramref name="color"/>. Uses "Swarmblastrr/CrowdInstanced" if present, else "Standard".</summary>
        public static Material Create(Color color, Material overrideTemplate = null)
        {
            Material material;
            if (overrideTemplate != null)
            {
                material = new Material(overrideTemplate);
            }
            else
            {
                Shader shader = Shader.Find(CrowdShaderName);
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Legacy Shaders/Diffuse");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Hidden/InternalErrorShader");
                }

                material = new Material(shader);
            }

            material.name = "Crowd " + ColorUtility.ToHtmlStringRGB(color);
            material.color = color;
            material.enableInstancing = true;
            return material;
        }

        /// <summary>The built-in capsule mesh (Capsule.fbx), cached. Falls back to a primitive's mesh if the resource is missing.</summary>
        public static Mesh DefaultUnitMesh()
        {
            if (_defaultMesh != null)
            {
                return _defaultMesh;
            }

            _defaultMesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
            if (_defaultMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                MeshFilter filter = temp.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    _defaultMesh = filter.sharedMesh;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(temp);
                }
                else
                {
                    Object.DestroyImmediate(temp);
                }
            }

            return _defaultMesh;
        }
    }
}
