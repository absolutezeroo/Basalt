using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Scans mod directories for PNG textures, loads them, and builds a
    /// <see cref="Texture2DArray"/> with per-slice mipmaps for artifact-free voxel rendering.
    /// </summary>
    /// <remarks>
    /// Called once at startup from <see cref="RenderingBootstrapper"/>.
    /// Uses <c>Texture2D.LoadImage()</c> per ADR-011 for runtime mod asset loading.
    ///
    /// Anti-bleeding guarantee: each texture array slice has an independent mipmap chain,
    /// so downsampled mip levels never blend pixels from adjacent tiles (unlike texture atlases).
    ///
    /// Pixel data is copied via <c>SetPixels32</c> (CPU path) rather than
    /// <c>Graphics.CopyTexture</c> (GPU blit) so that <c>Apply(updateMipmaps: true)</c>
    /// can generate correct mipmaps from the CPU-side buffer.
    /// </remarks>
    public static class TextureArrayBuilder
    {
        private const int CHECKERBOARD_CELL = 4;

        /// <summary>
        /// Scans mod root directories for PNG textures, registers them in the
        /// <paramref name="registry"/>, and builds a <see cref="Texture2DArray"/>.
        /// </summary>
        /// <param name="modRootPaths">Absolute paths to mod root directories (e.g. mods/default).</param>
        /// <param name="registry">Texture registry to assign slice indices.</param>
        /// <param name="textureSize">Expected pixel width and height (default 16).</param>
        /// <returns>The built <see cref="Texture2DArray"/> with mipmaps.</returns>
        public static Texture2DArray Build(
            string[] modRootPaths,
            TextureRegistry registry,
            int textureSize = 16)
        {
            var texturePaths = new List<(string path, string filename)>();

            // Phase 1: scan mod directories for PNG files and register filenames
            foreach (string modRoot in modRootPaths)
            {
                string texturesDir = Path.Combine(modRoot, "textures");

                if (!Directory.Exists(texturesDir))
                {
                    continue;
                }

                foreach (string file in Directory.GetFiles(texturesDir, "*.png"))
                {
                    string filename = Path.GetFileName(file);
                    registry.Register(filename);
                    texturePaths.Add((file, filename));
                }
            }

            int sliceCount = registry.Count;

            // Phase 2: load each PNG into a temporary Texture2D
            Texture2D fallback = CreateFallbackTexture(textureSize);
            var loadedTextures = new Texture2D[sliceCount];
            loadedTextures[0] = fallback;

            foreach ((string path, string filename) in texturePaths)
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);

                if (!tex.LoadImage(bytes))
                {
                    Debug.LogWarning(
                        $"[Basalt] Failed to load texture '{filename}' at '{path}'. Using fallback.");
                    Object.Destroy(tex);
                    continue;
                }

                if (tex.width != textureSize || tex.height != textureSize)
                {
                    Debug.LogWarning(
                        $"[Basalt] Texture '{filename}' is {tex.width}x{tex.height}, " +
                        $"expected {textureSize}x{textureSize}. Using fallback.");
                    Object.Destroy(tex);
                    continue;
                }

                ushort index = registry.Resolve(filename);
                loadedTextures[index] = tex;
            }

            // Fill any unassigned slots with the fallback
            for (int i = 0; i < sliceCount; i++)
            {
                if (loadedTextures[i] == null)
                {
                    loadedTextures[i] = fallback;
                }
            }

            // Phase 3: build the Texture2DArray via CPU pixel copy
            // Using SetPixels32 so Apply(updateMipmaps: true) generates correct mipmaps
            // from CPU-side data (Graphics.CopyTexture is GPU-only and Apply would overwrite it)
            var array = new Texture2DArray(
                textureSize, textureSize, sliceCount,
                TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };

            for (int i = 0; i < sliceCount; i++)
            {
                array.SetPixels32(loadedTextures[i].GetPixels32(), i);
            }

            array.Apply(true, true);

            // Cleanup temporary textures
            foreach (Texture2D tex in loadedTextures)
            {
                if (tex != fallback)
                {
                    Object.Destroy(tex);
                }
            }

            Object.Destroy(fallback);

            registry.TextureArray = array;

            return array;
        }

        /// <summary>
        /// Creates a magenta/black checkerboard texture used as the missing-texture fallback.
        /// Matches the Luanti convention for unresolved textures.
        /// </summary>
        private static Texture2D CreateFallbackTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool checker = ((x / CHECKERBOARD_CELL) + (y / CHECKERBOARD_CELL)) % 2 == 0;
                    pixels[y * size + x] = checker
                        ? new Color32(255, 0, 255, 255)
                        : new Color32(0, 0, 0, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return tex;
        }
    }
}
