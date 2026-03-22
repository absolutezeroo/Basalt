namespace Basalt.Core
{
    /// <summary>
    /// Resolves texture filenames to texture array slice indices.
    /// Implemented by the client rendering layer; consumed by node registration and Lua scripting.
    /// </summary>
    /// <remarks>
    /// Lives in Basalt.Core so that Basalt.Scripting can resolve texture names
    /// without depending on Basalt.Client (avoids circular assembly dependency).
    /// </remarks>
    public interface ITextureResolver
    {
        /// <summary>
        /// Returns the texture array slice index for the given filename.
        /// </summary>
        /// <param name="textureName">Texture filename (e.g. "default_stone.png").</param>
        /// <returns>The array slice index, or 0 (fallback) if the texture is not registered.</returns>
        ushort Resolve(string textureName);
    }
}
