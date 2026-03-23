using Unity.Burst;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable decoration definition for Burst job access.
    /// Created by <see cref="DecorationRegistry.Bake"/> from <see cref="DecorationDef"/>.
    /// </summary>
    [BurstCompile]
    public struct DecorationDefRuntime
    {
        /// <summary>Decoration type.</summary>
        public DecorationType Type;

        /// <summary>Decoration flags.</summary>
        public uint Flags;

        // ---- Flat-packed placeOn array ----

        /// <summary>Start index into the flat placeOn content ID array.</summary>
        public int PlaceOnOffset;

        /// <summary>Number of placeOn content IDs.</summary>
        public int PlaceOnCount;

        // ---- Flat-packed biomes array ----

        /// <summary>Start index into the flat biome index array.</summary>
        public int BiomesOffset;

        /// <summary>Number of biome indices. 0 = no biome filter.</summary>
        public int BiomesCount;

        // ---- Flat-packed spawnBy array ----

        /// <summary>Start index into the flat spawnBy content ID array.</summary>
        public int SpawnByOffset;

        /// <summary>Number of spawnBy content IDs.</summary>
        public int SpawnByCount;

        /// <summary>Side length of placement subdivision tiles.</summary>
        public short Sidelen;

        /// <summary>Minimum Y coordinate.</summary>
        public int YMin;

        /// <summary>Maximum Y coordinate.</summary>
        public int YMax;

        /// <summary>Fill ratio when not using noise.</summary>
        public float FillRatio;

        /// <summary>Noise params for density.</summary>
        public NoiseParams NoiseParams;

        /// <summary>Minimum spawnby neighbors. -1 = no constraint.</summary>
        public short NSpawnBy;

        /// <summary>Vertical offset from surface.</summary>
        public short PlaceOffsetY;

        // ---- Simple-specific ----

        /// <summary>Start index into the flat decoration content ID array.</summary>
        public int DecosOffset;

        /// <summary>Number of decoration content IDs.</summary>
        public int DecosCount;

        /// <summary>Base decoration column height.</summary>
        public short DecoHeight;

        /// <summary>Maximum decoration column height (0 = use DecoHeight).</summary>
        public short DecoHeightMax;

        /// <summary>Base param2 value.</summary>
        public byte DecoParam2;

        /// <summary>Maximum param2 value (0 = use DecoParam2).</summary>
        public byte DecoParam2Max;

        // ---- Schematic-specific ----

        /// <summary>Index into the flat SchematicData array. -1 = none.</summary>
        public int SchematicIndex;

        /// <summary>Rotation mode (0-3 = fixed, 4 = random).</summary>
        public byte Rotation;
    }
}
