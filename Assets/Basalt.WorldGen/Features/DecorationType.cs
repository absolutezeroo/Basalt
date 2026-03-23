namespace Basalt.WorldGen
{
    /// <summary>
    /// Decoration placement algorithm types matching Luanti's <c>DecorationType</c> enum.
    /// </summary>
    /// <remarks>
    /// Source: <c>luanti/src/mapgen/mg_decoration.h</c>.
    /// </remarks>
    public enum DecorationType : byte
    {
        Simple = 0,
        Schematic = 1,
    }
}
