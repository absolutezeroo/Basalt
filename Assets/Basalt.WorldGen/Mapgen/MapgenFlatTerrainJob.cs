using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Burst-compiled terrain fill job for Mapgen Flat.
    /// Writes stone/water/air nodes into the VoxelManipulator and produces the surface heightmap.
    /// </summary>
    /// <remarks>
    /// Implements Luanti's flat terrain logic with optional lakes and hills.
    ///
    /// Source: <c>luanti/src/mapgen/mapgen_flat.cpp</c> — <c>MapgenFlat::generateTerrain()</c> lines 274-319.
    ///
    /// Per-column logic:
    ///   1. Start with <c>stone_level = ground_level</c>.
    ///   2. If lakes enabled and <c>noise &lt; lake_threshold</c>:
    ///      <c>stone_level -= (lake_threshold - noise) * lake_steepness</c>.
    ///   3. Else if hills enabled and <c>noise &gt; hill_threshold</c>:
    ///      <c>stone_level += (noise - hill_threshold) * hill_steepness</c>.
    ///   4. For each Y: if <c>y &lt;= stone_level</c> → stone, elif <c>y &lt;= water_level</c> → water, else → air.
    ///
    /// When neither lakes nor hills are enabled, <see cref="TerrainResult"/> is not read
    /// (the buffer may contain zeroes). All columns produce the same flat plane.
    ///
    /// 2D noise indexing: ix + iz * Sx  (ix=0..79, iz=0..79)
    /// VoxelManipulator indexing: dx + dy * Sx + dz * Sx * Sy — <see cref="VoxelManipulator.LocalToIndex"/>
    /// </remarks>
    [BurstCompile]
    public struct MapgenFlatTerrainJob : IJob
    {
        /// <summary>Terrain noise ResultBuf, size Sx*Sz (80x80). Only read when lakes or hills are enabled.</summary>
        [ReadOnly]
        public NativeArray<float> TerrainResult;

        /// <summary>Node buffer to fill (80x82x80 packed uints).</summary>
        public NativeArray<uint> Nodes;

        /// <summary>Surface heightmap output (80x80 shorts).</summary>
        public NativeArray<short> Heightmap;

        /// <summary>Generation parameters including ground level, water level, and content IDs.</summary>
        public MapgenFlatParams Params;

        /// <summary>World Y coordinate of the buffer's minimum corner (includes overgeneration).</summary>
        public int MinY;

        /// <summary>World Y coordinate of the buffer's maximum corner (includes overgeneration).</summary>
        public int MaxY;

        public void Execute()
        {
            const int sx = VoxelManipulator.Sx;
            const int sy = VoxelManipulator.Sy;
            const int sz = VoxelManipulator.Sz;

            uint packedStone = Basalt.Core.MapNode.Pack(Params.ContentStone, 0, 0);
            uint packedWater = Basalt.Core.MapNode.Pack(Params.ContentWater, 0, 0);
            uint packedAir = Basalt.Core.MapNode.Pack(Params.ContentAir, 0, 0);

            bool useLakes = Params.EnableLakes != 0;
            bool useHills = Params.EnableHills != 0;

            int groundLevel = Params.GroundLevel;
            int waterLevel = Params.WaterLevel;

            for (int iz = 0; iz < sz; iz++)
            {
                for (int ix = 0; ix < sx; ix++)
                {
                    int index2d = ix + iz * sx;

                    // Start with flat ground
                    int stoneLevel = groundLevel;

                    // Luanti: mapgen_flat.cpp lines 293-299
                    if (useLakes || useHills)
                    {
                        float nTerrain = TerrainResult[index2d];

                        if (useLakes && nTerrain < Params.LakeThreshold)
                        {
                            // Depress stone level to form a lake basin
                            int depress = (int)((Params.LakeThreshold - nTerrain) * Params.LakeSteepness);
                            stoneLevel = groundLevel - depress;
                        }
                        else if (useHills && nTerrain > Params.HillThreshold)
                        {
                            // Raise stone level to form a hill
                            int rise = (int)((nTerrain - Params.HillThreshold) * Params.HillSteepness);
                            stoneLevel = groundLevel + rise;
                        }
                    }

                    // Track highest stone Y for heightmap.
                    // Initialize from stoneLevel so that even if the entire column
                    // is below the chunk floor, the heightmap receives the true
                    // surface height, matching Luanti's pre-loop behaviour.
                    int heightmapY = stoneLevel;

                    int columnBase = ix + iz * sx * sy;

                    for (int iy = 0; iy < sy; iy++)
                    {
                        int worldY = MinY + iy;
                        int nodeIndex = columnBase + iy * sx;

                        uint nodePacked;

                        if (worldY <= stoneLevel)
                        {
                            nodePacked = packedStone;

                            if (worldY > heightmapY)
                            {
                                heightmapY = worldY;
                            }
                        }
                        else if (worldY <= waterLevel)
                        {
                            nodePacked = packedWater;
                        }
                        else
                        {
                            nodePacked = packedAir;
                        }

                        Nodes[nodeIndex] = nodePacked;
                    }

                    Heightmap[VoxelManipulator.HeightmapIndex(ix, iz)] = (short)heightmapY;
                }
            }
        }
    }
}
