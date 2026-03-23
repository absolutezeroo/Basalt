using System.Collections.Generic;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Managed decoration definition used during registration (before Bake).
    /// String names are resolved during <see cref="DecorationRegistry.Bake"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors Luanti's <c>Decoration</c>, <c>DecoSimple</c>, and <c>DecoSchematic</c> classes.
    /// Source: <c>luanti/src/mapgen/mg_decoration.h</c>.
    /// </remarks>
    public class DecorationDef
    {
        /// <summary>Decoration type (Simple or Schematic).</summary>
        public DecorationType Type = DecorationType.Simple;

        /// <summary>Decoration flags (see <see cref="DecorationFlags"/>).</summary>
        public uint Flags = 0;

        /// <summary>Nodes this decoration can be placed on top of.</summary>
        public List<string> PlaceOn = new();

        /// <summary>Biome names this decoration is restricted to. Empty = all biomes.</summary>
        public List<string> Biomes = new();

        /// <summary>Side length of placement subdivision tiles.</summary>
        public short Sidelen = 1;

        /// <summary>Minimum Y coordinate.</summary>
        public int YMin = -31007;

        /// <summary>Maximum Y coordinate.</summary>
        public int YMax = 31007;

        /// <summary>Fill ratio (density) when not using noise. Range [0, 1] typically.</summary>
        public float FillRatio = 0f;

        /// <summary>Noise params for density when DECO_USE_NOISE is set.</summary>
        public NoiseParams NoiseParams = NoiseParams.Default;

        /// <summary>Nodes that must be nearby for placement. Empty = no constraint.</summary>
        public List<string> SpawnBy = new();

        /// <summary>Minimum number of spawnby neighbors required. -1 = no constraint.</summary>
        public short NSpawnBy = -1;

        /// <summary>Vertical offset from surface for placement start.</summary>
        public short PlaceOffsetY = 0;

        // ---- Simple-specific ----

        /// <summary>Simple: node names to choose from for the decoration column.</summary>
        public List<string> Decorations = new();

        /// <summary>Simple: base height of the decoration column.</summary>
        public short DecoHeight = 1;

        /// <summary>Simple: maximum height (0 = use DecoHeight, >0 = random range).</summary>
        public short DecoHeightMax = 0;

        /// <summary>Simple: base param2 value.</summary>
        public byte DecoParam2 = 0;

        /// <summary>Simple: maximum param2 value (0 = use DecoParam2).</summary>
        public byte DecoParam2Max = 0;

        // ---- Schematic-specific ----

        /// <summary>Schematic: index into the flat schematic data array. -1 = none.</summary>
        public int SchematicIndex = -1;

        /// <summary>Schematic: rotation mode (0=0, 1=90, 2=180, 3=270, 4=random).</summary>
        public byte Rotation = 4;
    }
}
