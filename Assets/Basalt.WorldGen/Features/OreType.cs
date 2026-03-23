namespace Basalt.WorldGen
{
    /// <summary>
    /// Ore placement algorithm types matching Luanti's <c>OreType</c> enum.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mg_ore.h</c>.
    /// </remarks>
    public enum OreType : byte
    {
        Scatter = 0,
        Sheet = 1,
        Puff = 2,
        Blob = 3,
        Vein = 4,
        Stratum = 5,
    }
}
