using System;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Creates and owns the URP <see cref="Material"/> for voxel chunk rendering.
    /// Binds the <see cref="Texture2DArray"/> to the Basalt/Voxel shader.
    /// </summary>
    /// <remarks>
    /// One material instance is shared across all chunk <see cref="MeshRenderer"/> components
    /// via <c>sharedMaterial</c> to avoid per-chunk material copies.
    /// </remarks>
    public sealed class VoxelMaterial : IDisposable
    {
        private const string SHADER_NAME = "Basalt/Voxel";
        private const string TEXTURE_ARRAY_PROPERTY = "_TextureArray";

        private Material _material;

        /// <summary>Gets the shared voxel material for chunk renderers.</summary>
        public Material Material => _material;

        /// <summary>
        /// Creates the material from the Basalt/Voxel shader and assigns the texture array.
        /// </summary>
        /// <param name="textureArray">The built texture array from <see cref="TextureArrayBuilder"/>.</param>
        public void Initialize(Texture2DArray textureArray)
        {
            Shader shader = Shader.Find(SHADER_NAME);

            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Shader '{SHADER_NAME}' not found. Ensure Voxel.shader is in Assets/Shaders/.");
            }

            _material = new Material(shader);
            _material.SetTexture(TEXTURE_ARRAY_PROPERTY, textureArray);
            _material.enableInstancing = true;
        }

        /// <summary>Destroys the material instance.</summary>
        public void Dispose()
        {
            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
                _material = null;
            }
        }
    }
}
