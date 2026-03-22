using System.Collections.Generic;
using Basalt.Core;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Maps texture filenames to <see cref="Texture2DArray"/> slice indices.
    /// Built during startup by <see cref="TextureArrayBuilder"/>; consumed by
    /// <see cref="NodeRegistry"/> during node registration.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="ITextureResolver"/> so that Core and Scripting assemblies
    /// can resolve texture names without depending on Basalt.Client.
    ///
    /// Slot 0 is always reserved for the fallback (missing texture) checkerboard.
    /// User textures start at index 1.
    /// </remarks>
    public sealed class TextureRegistry : ITextureResolver
    {
        private const string FALLBACK_NAME = "__fallback__";

        private readonly Dictionary<string, ushort> _nameToIndex = new();
        private ushort _nextIndex;

        /// <summary>Gets the built texture array. Set by <see cref="TextureArrayBuilder"/>.</summary>
        public Texture2DArray TextureArray { get; set; }

        /// <summary>Gets the number of registered texture slots (including the fallback).</summary>
        public int Count => _nextIndex;

        /// <summary>
        /// Creates a new texture registry with slot 0 reserved for the fallback texture.
        /// </summary>
        public TextureRegistry()
        {
            _nameToIndex[FALLBACK_NAME] = 0;
            _nextIndex = 1;
        }

        /// <summary>
        /// Registers a texture filename and assigns it the next available slice index.
        /// If the name is already registered, returns the existing index (idempotent).
        /// </summary>
        /// <param name="textureName">Texture filename (e.g. "default_stone.png").</param>
        /// <returns>The assigned array slice index.</returns>
        public ushort Register(string textureName)
        {
            if (_nameToIndex.TryGetValue(textureName, out ushort existing))
            {
                return existing;
            }

            ushort index = _nextIndex++;
            _nameToIndex[textureName] = index;

            return index;
        }

        /// <summary>
        /// Resolves a texture filename to its array slice index.
        /// Returns 0 (fallback) if the texture is not registered.
        /// </summary>
        /// <param name="textureName">Texture filename (e.g. "default_stone.png").</param>
        /// <returns>The array slice index, or 0 if not found.</returns>
        public ushort Resolve(string textureName)
        {
            return _nameToIndex.TryGetValue(textureName, out ushort index)
                ? index
                : (ushort)0;
        }
    }
}
