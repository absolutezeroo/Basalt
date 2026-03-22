namespace Basalt.Core
{
    /// <summary>
    /// Defines how param1 is interpreted for a node.
    /// Mirrors Luanti's <c>ContentParamType</c> in <c>luanti/src/nodedef.h</c>.
    /// </summary>
    public enum ParamType : byte
    {
        /// <summary>param1 is not used.</summary>
        None = 0,

        /// <summary>param1 encodes light (bits 0-3 night, bits 4-7 day).</summary>
        Light = 1,
    }
}