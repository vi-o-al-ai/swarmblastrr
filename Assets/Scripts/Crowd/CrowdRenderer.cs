using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swarmblastrr.Crowd
{
    /// <summary>
    /// Draws a crowd with <see cref="Graphics.RenderMeshInstanced"/> in chunks of 1023 instances.
    /// Holds no per-unit GameObjects; the owner calls <see cref="Render"/> every LateUpdate with
    /// the completed simulation positions. Units are placed so the mesh's bounds rest on the origin's Y.
    /// </summary>
    public sealed class CrowdRenderer : MonoBehaviour
    {
        /// <summary>Hard limit of Graphics.RenderMeshInstanced per call.</summary>
        private const int ChunkSize = 1023;

        /// <summary>Edge length of the culling bounds centred on the crowd origin.</summary>
        private const float BoundsSize = 200f;

        /// <summary>Mesh drawn for every unit.</summary>
        public Mesh Mesh;

        /// <summary>Instanced material; <c>enableInstancing</c> is forced on.</summary>
        public Material Material;

        /// <summary>Uniform scale applied to every unit.</summary>
        public float UnitScale = 0.45f;

        private readonly List<Matrix4x4[]> _chunks = new List<Matrix4x4[]>();
        private Mesh _boundsMesh;
        private float _groundOffset;

        /// <summary>Assigns mesh, material and scale, and turns on GPU instancing on the material.</summary>
        public void Configure(Mesh mesh, Material material, float unitScale)
        {
            Mesh = mesh;
            Material = material;
            UnitScale = unitScale;
            if (Material != null)
            {
                Material.enableInstancing = true;
            }
        }

        /// <summary>Render <paramref name="count"/> instances at origin + (p.x, 0, p.y) for p in positions. Chunks of 1023 via Graphics.RenderMeshInstanced.</summary>
        public void Render(NativeArray<float2> positions, int count, Vector3 origin)
        {
            if (Mesh == null || Material == null || !positions.IsCreated)
            {
                return;
            }

            count = Mathf.Min(count, positions.Length);
            if (count <= 0)
            {
                return;
            }

            if (!Material.enableInstancing)
            {
                Material.enableInstancing = true;
            }

            float scale = UnitScale;
            float baseY = origin.y + GroundOffset() * scale;
            RenderParams renderParams = new RenderParams(Material)
            {
                worldBounds = new Bounds(origin, new Vector3(BoundsSize, BoundsSize, BoundsSize)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            int chunkIndex = 0;
            for (int start = 0; start < count; start += ChunkSize)
            {
                int n = Mathf.Min(ChunkSize, count - start);
                Matrix4x4[] chunk = GetChunk(chunkIndex);
                chunkIndex++;
                for (int k = 0; k < n; k++)
                {
                    float2 p = positions[start + k];
                    chunk[k] = ScaledTranslation(origin.x + p.x, baseY, origin.z + p.y, scale);
                }

                Graphics.RenderMeshInstanced(renderParams, Mesh, 0, chunk, n, 0);
            }
        }

        /// <summary>Distance from the mesh origin down to its lowest vertex (in mesh units), cached per mesh.</summary>
        private float GroundOffset()
        {
            if (_boundsMesh != Mesh)
            {
                _boundsMesh = Mesh;
                _groundOffset = Mesh != null ? -Mesh.bounds.min.y : 0f;
            }

            return _groundOffset;
        }

        private Matrix4x4[] GetChunk(int index)
        {
            while (_chunks.Count <= index)
            {
                _chunks.Add(new Matrix4x4[ChunkSize]);
            }

            return _chunks[index];
        }

        private static Matrix4x4 ScaledTranslation(float x, float y, float z, float scale)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = scale;
            m.m11 = scale;
            m.m22 = scale;
            m.m03 = x;
            m.m13 = y;
            m.m23 = z;
            return m;
        }
    }
}
