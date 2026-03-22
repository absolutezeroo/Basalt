using Unity.Burst;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Noise system constants matching Luanti (ex-Minetest) values exactly.
    /// </summary>
    /// <remarks>
    /// Sources:
    ///   Magic constants — <c>luanti/src/noise.cpp</c> lines 35-39.
    ///   Flag constants  — <c>luanti/src/noise.h</c> lines 111-117.
    ///   PCG constants   — <c>luanti/src/noise.h</c> line 95 and <c>noise.cpp</c> line 72.
    /// </remarks>
    [BurstCompile]
    public static class NoiseConstants
    {
        /// <summary>X-axis magic multiplier for the integer noise hash.</summary>
        public const uint NOISE_MAGIC_X = 1619U;

        /// <summary>Y-axis magic multiplier for the integer noise hash.</summary>
        public const uint NOISE_MAGIC_Y = 31337U;

        /// <summary>Z-axis magic multiplier for the integer noise hash.</summary>
        public const uint NOISE_MAGIC_Z = 52591U;

        /// <summary>
        /// Seed magic multiplier — intentionally unsigned to match C++ unsigned promotion.
        /// Applied as <c>NOISE_MAGIC_SEED * (uint)seed</c> in C# to replicate the overflow.
        /// </summary>
        public const uint NOISE_MAGIC_SEED = 1013U;

        /// <summary>
        /// Default flag set. In 2D, implies eased (bilinear with quintic smoothing).
        /// When this bit is set, 2D noise is eased; 3D noise is NOT eased by default.
        /// </summary>
        public const uint NOISE_FLAG_DEFAULTS = 0x01U;

        /// <summary>Explicit easing flag. Forces quintic s-curve for both 2D and 3D.</summary>
        public const uint NOISE_FLAG_EASED = 0x02U;

        /// <summary>Absolute-value flag. Each octave's value is abs()-ed before accumulation.</summary>
        public const uint NOISE_FLAG_ABSVALUE = 0x04U;

        /// <summary>Point-buffer mode (reserved for future use, not implemented in Luanti core).</summary>
        public const uint NOISE_FLAG_POINTBUFFER = 0x08U;

        /// <summary>Simplex mode flag (not implemented in this port; reserved).</summary>
        public const uint NOISE_FLAG_SIMPLEX = 0x10U;

        /// <summary>PCG multiplier constant (LCG component of PCG-XSH-RR).</summary>
        public const ulong PCG_MULTIPLIER = 6364136223846793005UL;

        /// <summary>Default PCG state initializer.</summary>
        public const ulong PCG_DEFAULT_STATE = 0x853c49e6748fea9bUL;

        /// <summary>Default PCG sequence/stream selector.</summary>
        public const ulong PCG_DEFAULT_SEQ = 0xda3e39cb94b95bdbUL;
    }
}
