using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Burst-compiled terrain fill job for Mapgen V7.
    /// Reads precomputed noise result buffers and writes stone/water/air nodes
    /// into the VoxelManipulator, then produces the surface heightmap.
    /// </summary>
    /// <remarks>
    /// Must be scheduled after all noise pipeline jobs for all channels have completed.
    /// Implements Luanti's per-column terrain blending logic exactly.
    ///
    /// Source: <c>luanti/src/mapgen/mapgen_v7.cpp</c> — <c>MapgenV7::generateTerrain()</c> lines 467-574.
    ///
    /// 2D noise indexing: ix + iz * Sx  (ix=0..79, iz=0..79)
    /// 3D noise indexing: ix + iy * Sx + iz * Sx * Sy  (x fastest, then y, then z)
    /// VoxelManipulator indexing: same as 3D noise — <see cref="VoxelManipulator.LocalToIndex"/>
    ///
    /// Height blend formula (lines 410-420):
    ///   hselect = clamp(height_select_noise, 0, 1)
    ///   if height_alt > height_base: surface_y = height_alt
    ///   else: surface_y = height_base * hselect + height_alt * (1 - hselect)
    ///
    /// Mountain formula (lines 434-441):
    ///   mounthn = max(mount_height_noise, 1.0)
    ///   density_gradient = -((y - mount_zero_level) / mounthn)
    ///   is_mountain = (mountain_noise + density_gradient) >= 0
    ///
    /// River channel formula (lines 444-458):
    ///   width = 0.2
    ///   absuwatern = abs(ridge_uwater_noise) * 2.0
    ///   if absuwatern > width: no river
    ///   altitude = y - water_level
    ///   height_mod = (altitude + 17.0) / 2.5
    ///   width_mod = width - absuwatern
    ///   nridge = ridge_noise * max(altitude, 0) / 7.0
    ///   is_river = (nridge + width_mod * height_mod) >= 0.6
    /// </remarks>
    [BurstCompile]
    public struct MapgenV7TerrainJob : IJob
    {
        /// <summary>terrain_base ResultBuf, size Sx*Sz (80x80).</summary>
        [ReadOnly]
        public NativeArray<float> TerrainBaseResult;

        /// <summary>terrain_alt ResultBuf, size Sx*Sz.</summary>
        [ReadOnly]
        public NativeArray<float> TerrainAltResult;

        /// <summary>height_select ResultBuf, size Sx*Sz.</summary>
        [ReadOnly]
        public NativeArray<float> HeightSelectResult;

        /// <summary>mount_height ResultBuf, size Sx*Sz.</summary>
        [ReadOnly]
        public NativeArray<float> MountHeightResult;

        /// <summary>ridge_uwater ResultBuf, size Sx*Sz.</summary>
        [ReadOnly]
        public NativeArray<float> RidgeUwaterResult;

        /// <summary>mountain ResultBuf, size Sx*Sy*Sz (80x82x80).</summary>
        [ReadOnly]
        public NativeArray<float> MountainResult;

        /// <summary>ridge ResultBuf, size Sx*Sy*Sz.</summary>
        [ReadOnly]
        public NativeArray<float> RidgeResult;

        /// <summary>Node buffer to fill (80x82x80 packed uints).</summary>
        public NativeArray<uint> Nodes;

        /// <summary>Surface heightmap output (80x80 shorts).</summary>
        public NativeArray<short> Heightmap;

        /// <summary>Generation parameters including water level and content IDs.</summary>
        public MapgenV7Params Params;

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

            bool hasMountains = Params.EnableMountains != 0;

            // Luanti: gen_rivers = ridges enabled && maxY >= water_level - 16 && !floatlands
            // No floatlands in this implementation.
            bool genRivers = Params.EnableRidges != 0 && MaxY >= Params.WaterLevel - 16;

            int waterLevel = Params.WaterLevel;
            int mountZeroLevel = Params.MountZeroLevel;

            for (int iz = 0; iz < sz; iz++)
            {
                for (int ix = 0; ix < sx; ix++)
                {
                    int index2d = ix + iz * sx;

                    // ---- Base terrain height via height_select blend ----
                    // Source: baseTerrainLevelFromMap(), lines 410-420
                    float heightBase = TerrainBaseResult[index2d];
                    float heightAlt = TerrainAltResult[index2d];
                    float hselect = math.clamp(HeightSelectResult[index2d], 0.0f, 1.0f);

                    float surfaceFloat;
                    if (heightAlt > heightBase)
                    {
                        surfaceFloat = heightAlt;
                    }
                    else
                    {
                        surfaceFloat = heightBase * hselect + heightAlt * (1.0f - hselect);
                    }

                    // Luanti: s16 surface_y = baseTerrainLevelFromMap(index2d);
                    // C++ float→s16 truncates toward zero. C# (int) also truncates.
                    int surfaceY = (int)surfaceFloat;

                    // Mountain height for this column
                    float mounthn = 0.0f;
                    if (hasMountains)
                    {
                        mounthn = math.max(MountHeightResult[index2d], 1.0f);
                    }

                    // Ridge uwater value for this column (used in river channel detection)
                    float ridgeUwater = 0.0f;
                    if (genRivers)
                    {
                        ridgeUwater = RidgeUwaterResult[index2d];
                    }

                    // Track highest stone Y for heightmap.
                    // Initialize from surfaceY so that even if the entire column
                    // is below the chunk floor, the heightmap receives the true
                    // surface height, matching Luanti's pre-loop update.
                    int heightmapY = surfaceY;

                    int columnBase = ix + iz * sx * sy;

                    for (int iy = 0; iy < sy; iy++)
                    {
                        int worldY = MinY + iy;
                        int nodeIndex = columnBase + iy * sx;

                        // Per-voxel river channel check
                        // Source: getRiverChannelFromMap(), lines 444-458
                        bool isRiverChannel = genRivers &&
                            IsRiverChannel(nodeIndex, ridgeUwater, worldY, waterLevel, RidgeResult);

                        uint nodePacked;

                        if (worldY <= surfaceY && !isRiverChannel)
                        {
                            // Base terrain: stone
                            nodePacked = packedStone;
                            if (worldY > heightmapY)
                            {
                                heightmapY = worldY;
                            }
                        }
                        else if (hasMountains &&
                                 IsMountainTerrain(nodeIndex, worldY, mounthn, mountZeroLevel, MountainResult) &&
                                 !isRiverChannel)
                        {
                            // Mountain terrain: stone
                            nodePacked = packedStone;
                            if (worldY > heightmapY)
                            {
                                heightmapY = worldY;
                            }
                        }
                        else if (worldY <= waterLevel)
                        {
                            // Below or at water level, not stone: water
                            nodePacked = packedWater;
                        }
                        else
                        {
                            // Above everything: air
                            nodePacked = packedAir;
                        }

                        Nodes[nodeIndex] = nodePacked;
                    }

                    // Write heightmap for this column
                    Heightmap[VoxelManipulator.HeightmapIndex(ix, iz)] = (short)heightmapY;
                }
            }
        }

        /// <summary>
        /// Evaluates whether a node falls inside the mountain density field.
        /// </summary>
        /// <remarks>
        /// Luanti formula (mapgen_v7.cpp lines 434-441):
        ///   mounthn = max(mount_height_noise, 1.0)
        ///   density_gradient = -((y - mount_zero_level) / mounthn)
        ///   is_mountain = (mountain_noise + density_gradient) >= 0.0
        ///
        /// The negative gradient causes density to decrease with altitude,
        /// so mountains naturally taper off at higher Y values.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMountainTerrain(
            int index3d, int worldY, float mounthn, int mountZeroLevel,
            NativeArray<float> mountainResult)
        {
            float densityGradient = -((float)(worldY - mountZeroLevel) / mounthn);
            float mountainNoise = mountainResult[index3d];

            return mountainNoise + densityGradient >= 0.0f;
        }

        /// <summary>
        /// Evaluates whether a node is inside a river channel carved by ridges.
        /// </summary>
        /// <remarks>
        /// Luanti formula (mapgen_v7.cpp lines 444-458):
        ///   width = 0.2
        ///   absuwatern = abs(ridge_uwater) * 2.0
        ///   if absuwatern > width: not a river
        ///   altitude = y - water_level
        ///   height_mod = (altitude + 17.0) / 2.5
        ///   width_mod = width - absuwatern
        ///   nridge = ridge_noise * max(altitude, 0) / 7.0
        ///   is_river = (nridge + width_mod * height_mod) >= 0.6
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRiverChannel(
            int index3d, float ridgeUwater, int worldY, int waterLevel,
            NativeArray<float> ridgeResult)
        {
            const float WIDTH = 0.2f;

            float absuwatern = math.abs(ridgeUwater) * 2.0f;
            if (absuwatern > WIDTH)
            {
                return false;
            }

            float altitude = worldY - waterLevel;
            float heightMod = (altitude + 17.0f) / 2.5f;
            float widthMod = WIDTH - absuwatern;
            float nridge = ridgeResult[index3d] * math.max(altitude, 0.0f) / 7.0f;

            return nridge + widthMod * heightMod >= 0.6f;
        }
    }
}
