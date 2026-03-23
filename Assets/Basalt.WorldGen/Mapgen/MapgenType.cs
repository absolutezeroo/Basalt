namespace Basalt.WorldGen
{
    /// <summary>
    /// Identifies the terrain generation algorithm for a world.
    /// </summary>
    /// <remarks>
    /// Matches Luanti's mapgen name registry.
    /// Source: <c>luanti/src/mapgen/mapgen.cpp</c> — <c>MapgenParams::readParams</c>.
    /// </remarks>
    public enum MapgenType
    {
        /// <summary>Mapgen V7 — multi-layer fractal terrain with mountains and rivers.</summary>
        V7 = 0,

        /// <summary>Mapgen Flat — configurable flat plane with optional lakes and hills.</summary>
        Flat = 1,
    }
}
