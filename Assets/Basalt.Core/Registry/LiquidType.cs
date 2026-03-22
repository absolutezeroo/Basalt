namespace Basalt.Core
{
    /// <summary>
    /// Defines whether a node behaves as a liquid and what kind.
    /// Mirrors Luanti's <c>LiquidType</c> in <c>luanti/src/nodedef.h</c>.
    /// </summary>
    public enum LiquidType : byte
    {
        /// <summary>Not a liquid.</summary>
        None = 0,

        /// <summary>Flowing liquid that spreads and has varying height.</summary>
        Flowing = 1,

        /// <summary>Liquid source block that generates flowing liquid.</summary>
        Source = 2,
    }
}