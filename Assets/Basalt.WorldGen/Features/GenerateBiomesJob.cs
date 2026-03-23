using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Burst-compiled biome surface replacement job.
    /// Selects biome per column via weighted Voronoi in (heat, humidity) space,
    /// then replaces stone surfaces with biome top/filler/stone nodes and water
    /// with biome water nodes. Fills the biomemap.
    /// </summary>
    /// <remarks>
    /// Port of Luanti's <c>MapgenBasic::generateBiomes()</c>.
    /// Source: <c>luanti/src/mapgen/mapgen.cpp</c> lines 623-830.
    ///
    /// Column scan: top-down (node_max.Y to node_min.Y).
    /// Tracks nplaced counter through top → filler → stone layers.
    /// Biome re-evaluated at Y transition boundaries.
    /// </remarks>
    [BurstCompile]
    public struct GenerateBiomesJob : IJob
    {
        /// <summary>Node buffer (80x82x80 packed uints). Read and written.</summary>
        public NativeArray<uint> Nodes;

        /// <summary>Surface heightmap (80x80 shorts). Read-only (from terrain job).</summary>
        [ReadOnly] public NativeArray<short> Heightmap;

        /// <summary>Combined heatmap (80x80). Read-only.</summary>
        [ReadOnly] public NativeArray<float> Heatmap;

        /// <summary>Combined humidmap (80x80). Read-only.</summary>
        [ReadOnly] public NativeArray<float> Humidmap;

        /// <summary>Filler depth noise result (80x80). Read-only.</summary>
        [ReadOnly] public NativeArray<float> FillerDepthResult;

        /// <summary>Biome index per column (80x80). Written by this job.</summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<ushort> Biomemap;

        /// <summary>Baked biome definitions. Index 0 = default biome.</summary>
        [ReadOnly] public NativeArray<BiomeDefRuntime> Biomes;

        /// <summary>Number of registered biomes.</summary>
        public int BiomeCount;

        /// <summary>Content ID for stone (used to detect stone nodes).</summary>
        public ushort ContentStone;

        /// <summary>Content ID for water source.</summary>
        public ushort ContentWater;

        /// <summary>Content ID for river water source.</summary>
        public ushort ContentRiverWater;

        /// <summary>Water level Y coordinate.</summary>
        public int WaterLevel;

        /// <summary>World X of vm MinPos.</summary>
        public int NodeMinX;

        /// <summary>World Y of vm MinPos (includes overgeneration).</summary>
        public int NodeMinY;

        /// <summary>World Z of vm MinPos.</summary>
        public int NodeMinZ;

        /// <summary>World Y of vm MaxPos (includes overgeneration).</summary>
        public int NodeMaxY;

        public void Execute()
        {
            const int sx = VoxelManipulator.Sx; // 80
            const int sy = VoxelManipulator.Sy; // 82
            const int sz = VoxelManipulator.Sz; // 80

            // Use the actual mapchunk range (excluding overgeneration) for biome scanning.
            // Overgeneration is 1 node on each Y side.
            int scanMinY = NodeMinY + MapgenV7Constants.OVERGEN_SIZE;
            int scanMaxY = NodeMaxY - MapgenV7Constants.OVERGEN_SIZE;

            int index = 0;

            for (int iz = 0; iz < sz; iz++)
            {
                for (int ix = 0; ix < sx; ix++, index++)
                {
                    int worldX = NodeMinX + ix;
                    int worldZ = NodeMinZ + iz;

                    float heat = Heatmap[index];
                    float humidity = Humidmap[index];
                    float fillerDepthNoise = FillerDepthResult[index];

                    BiomeDefRuntime biome = default;
                    bool hasBiome = false;
                    ushort waterBiomeIndex = 0;

                    ushort depthTop = 0;
                    int baseFiller = 0;
                    ushort depthWaterTop = 0;
                    ushort depthRiverbed = 0;

                    // Check node above the scan range for air/water detection
                    int viAbove = ix + (scanMaxY + 1 - NodeMinY) * sx + iz * sx * sy;
                    ushort cAbove = GetContent(viAbove);
                    bool airAbove = cAbove == BasaltConstants.CONTENT_AIR;
                    bool riverWaterAbove = cAbove == ContentRiverWater;
                    bool waterAbove = cAbove == ContentWater || riverWaterAbove;

                    Biomemap[index] = 0; // BIOME_NONE

                    // nplaced: 0 = expecting top, increments through top/filler, U16_MAX = stone
                    ushort nplaced = (airAbove || waterAbove) ? (ushort)0 : ushort.MaxValue;

                    for (int y = scanMaxY; y >= scanMinY; y--)
                    {
                        int vi = ix + (y - NodeMinY) * sx + iz * sx * sy;
                        ushort c = GetContent(vi);

                        bool biomeOutdated = !hasBiome;

                        bool isStoneSurface = (c == ContentStone) &&
                            (airAbove || waterAbove || biomeOutdated);

                        bool isWaterSurface = (c == ContentWater || c == ContentRiverWater) &&
                            (airAbove || biomeOutdated);

                        if (isStoneSurface || isWaterSurface)
                        {
                            if (biomeOutdated)
                            {
                                biome = CalcBiomeFromNoise(heat, humidity, worldX, y, worldZ);
                                hasBiome = true;
                            }

                            if (Biomemap[index] == 0 && isStoneSurface)
                                Biomemap[index] = biome.Index;

                            if (waterBiomeIndex == 0 && isWaterSurface)
                                waterBiomeIndex = biome.Index;

                            depthTop = (ushort)biome.DepthTop;
                            baseFiller = (int)math.max(
                                biome.DepthTop + biome.DepthFiller + fillerDepthNoise, 0f);
                            depthWaterTop = (ushort)biome.DepthWaterTop;
                            depthRiverbed = (ushort)biome.DepthRiverbed;
                        }

                        if (c == ContentStone)
                        {
                            // Check node below for structural support
                            int viBelow = ix + (y - 1 - NodeMinY) * sx + iz * sx * sy;
                            if (viBelow >= 0 && viBelow < Nodes.Length)
                            {
                                ushort cBelow = GetContent(viBelow);
                                if (cBelow == BasaltConstants.CONTENT_AIR ||
                                    cBelow == ContentWater ||
                                    cBelow == ContentRiverWater)
                                {
                                    nplaced = ushort.MaxValue;
                                }
                            }

                            if (hasBiome)
                            {
                                if (riverWaterAbove)
                                {
                                    if (nplaced < depthRiverbed)
                                    {
                                        SetContent(vi, biome.ContentRiverbed);
                                        nplaced++;
                                    }
                                    else
                                    {
                                        nplaced = ushort.MaxValue;
                                        riverWaterAbove = false;
                                    }
                                }
                                else if (nplaced < depthTop)
                                {
                                    SetContent(vi, biome.ContentTop);
                                    nplaced++;
                                }
                                else if (nplaced < baseFiller)
                                {
                                    SetContent(vi, biome.ContentFiller);
                                    nplaced++;
                                }
                                else
                                {
                                    if (biome.ContentStone != BasaltConstants.CONTENT_IGNORE)
                                        SetContent(vi, biome.ContentStone);
                                    nplaced = ushort.MaxValue;
                                }
                            }

                            airAbove = false;
                            waterAbove = false;
                        }
                        else if (c == ContentWater)
                        {
                            if (hasBiome)
                            {
                                ushort waterNode = (y > WaterLevel - depthWaterTop)
                                    ? biome.ContentWaterTop : biome.ContentWater;
                                if (waterNode != BasaltConstants.CONTENT_IGNORE)
                                    SetContent(vi, waterNode);
                            }

                            nplaced = 0;
                            airAbove = false;
                            waterAbove = true;
                        }
                        else if (c == ContentRiverWater)
                        {
                            if (hasBiome && biome.ContentRiverWater != BasaltConstants.CONTENT_IGNORE)
                                SetContent(vi, biome.ContentRiverWater);

                            nplaced = 0;
                            airAbove = false;
                            waterAbove = true;
                            riverWaterAbove = true;
                        }
                        else if (c == BasaltConstants.CONTENT_AIR)
                        {
                            nplaced = 0;
                            airAbove = true;
                            waterAbove = false;
                        }
                        else
                        {
                            nplaced = ushort.MaxValue;
                            airAbove = false;
                            waterAbove = false;
                        }
                    }

                    // Fallback: use water biome if no stone surface found
                    if (Biomemap[index] == 0 && waterBiomeIndex != 0)
                        Biomemap[index] = waterBiomeIndex;
                }
            }
        }

        /// <summary>
        /// Weighted Voronoi biome selection in (heat, humidity) space.
        /// Matches Luanti's <c>BiomeGenOriginal::calcBiomeFromNoise()</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BiomeDefRuntime CalcBiomeFromNoise(float heat, float humidity, int x, int y, int z)
        {
            int closestIdx = 0;
            int closestBlendIdx = -1;
            float distMin = float.MaxValue;
            float distMinBlend = float.MaxValue;

            // Start from 1 to skip default biome at index 0
            for (int i = 1; i < BiomeCount; i++)
            {
                BiomeDefRuntime b = Biomes[i];

                // AABB position filter
                if (y < b.YMin || y > b.YMax + b.VerticalBlend ||
                    x < -BasaltConstants.MAX_MAP_GENERATION_LIMIT ||
                    z < -BasaltConstants.MAX_MAP_GENERATION_LIMIT)
                    continue;

                float dHeat = heat - b.HeatPoint;
                float dHumidity = humidity - b.HumidityPoint;
                float dist = dHeat * dHeat + dHumidity * dHumidity;

                if (b.Weight > 0f)
                    dist /= b.Weight;

                if (y <= b.YMax)
                {
                    if (dist < distMin)
                    {
                        distMin = dist;
                        closestIdx = i;
                    }
                }
                else if (dist < distMinBlend)
                {
                    distMinBlend = dist;
                    closestBlendIdx = i;
                }
            }

            // Pseudorandom vertical blend
            if (closestBlendIdx >= 0 && distMinBlend <= distMin)
            {
                BiomeDefRuntime blendBiome = Biomes[closestBlendIdx];
                ulong seed = (ulong)(long)(y + (heat + humidity) * 0.9f);
                var rng = new PcgRandom(seed);
                if (rng.Range(0, blendBiome.VerticalBlend) >= y - blendBiome.YMax)
                    return blendBiome;
            }

            return Biomes[closestIdx];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetContent(int vi)
        {
            if (vi < 0 || vi >= Nodes.Length)
                return BasaltConstants.CONTENT_IGNORE;
            return (ushort)(Nodes[vi] >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetContent(int vi, ushort content)
        {
            if (content == BasaltConstants.CONTENT_IGNORE)
                return;
            uint existing = Nodes[vi];
            Nodes[vi] = ((uint)content << 16) | (existing & 0xFFFF);
        }
    }
}
