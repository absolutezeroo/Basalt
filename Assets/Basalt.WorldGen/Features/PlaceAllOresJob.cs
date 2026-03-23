using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Burst-compiled job that places all registered ores into the VoxelManipulator.
    /// Implements all 6 Luanti ore types: Scatter, Sheet, Puff, Blob, Vein, Stratum.
    /// </summary>
    /// <remarks>
    /// Port of Luanti's ore generate() methods.
    /// Source: <c>luanti/src/mapgen/mg_ore.cpp</c>.
    ///
    /// Each ore type is a private method called from the main loop.
    /// Uses PcgRandom for deterministic placement and NoiseKernel.Fractal2D/3D
    /// for per-point noise evaluation (no bulk noise maps allocated).
    /// </remarks>
    [BurstCompile]
    public struct PlaceAllOresJob : IJob
    {
        /// <summary>VoxelManipulator node buffer. Read and written.</summary>
        public NativeArray<uint> Nodes;

        /// <summary>Biome index per column, used for biome-filtered ore placement.</summary>
        [ReadOnly] public NativeArray<ushort> Biomemap;

        /// <summary>Baked ore definitions indexed by ore registration order.</summary>
        [ReadOnly] public NativeArray<OreDefRuntime> Ores;

        /// <summary>Flat-packed content IDs for all ores' wherein node lists.</summary>
        [ReadOnly] public NativeArray<ushort> Wherein;

        /// <summary>Flat-packed biome indices for all ores' biome filter lists.</summary>
        [ReadOnly] public NativeArray<ushort> OreBiomes;

        /// <summary>Number of registered ores to process.</summary>
        public int OreCount;

        /// <summary>World-space minimum corner of the VoxelManipulator.</summary>
        public int3 VmMinPos;

        /// <summary>World-space maximum corner of the VoxelManipulator.</summary>
        public int3 VmMaxPos;

        /// <summary>World-space minimum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmin;

        /// <summary>World-space maximum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmax;

        /// <summary>World seed for noise evaluation.</summary>
        public int MapSeed;

        /// <summary>Block seed for deterministic per-mapchunk random placement.</summary>
        public uint Blockseed;

        public void Execute()
        {
            for (int i = 0; i < OreCount; i++)
            {
                OreDefRuntime ore = Ores[i];

                if (Nmin.y > ore.YMax || Nmax.y < ore.YMin)
                {
                    continue;
                }

                int actualYmin = math.max(Nmin.y, ore.YMin);
                int actualYmax = math.min(Nmax.y, ore.YMax);

                if (ore.ClustSize >= actualYmax - actualYmin + 1)
                {
                    continue;
                }

                int3 nmin = new int3(Nmin.x, actualYmin, Nmin.z);
                int3 nmax = new int3(Nmax.x, actualYmax, Nmax.z);

                switch (ore.Type)
                {
                    case OreType.Scatter:
                        PlaceScatter(in ore, nmin, nmax);
                        break;
                    case OreType.Sheet:
                        PlaceSheet(in ore, nmin, nmax);
                        break;
                    case OreType.Puff:
                        PlacePuff(in ore, nmin, nmax);
                        break;
                    case OreType.Blob:
                        PlaceBlob(in ore, nmin, nmax);
                        break;
                    case OreType.Vein:
                        PlaceVein(in ore, nmin, nmax);
                        break;
                    case OreType.Stratum:
                        PlaceStratum(in ore, nmin, nmax);
                        break;
                }
            }
        }

        private void PlaceScatter(in OreDefRuntime ore, int3 nmin, int3 nmax)
        {
            var pr = new PcgRandom(Blockseed);
            uint packedOre = MapNode.Pack(ore.ContentOre, 0, ore.OreParam2);

            int sizex = nmax.x - nmin.x + 1;
            uint volume = (uint)(nmax.x - nmin.x + 1) *
                          (uint)(nmax.y - nmin.y + 1) *
                          (uint)(nmax.z - nmin.z + 1);
            uint csize = (uint)ore.ClustSize;
            uint cvolume = csize * csize * csize;
            uint nclusters = volume / (uint)ore.ClustScarcity;

            for (uint i = 0; i < nclusters; i++)
            {
                int x0 = pr.Range(nmin.x, nmax.x - (int)csize + 1);
                int y0 = pr.Range(nmin.y, nmax.y - (int)csize + 1);
                int z0 = pr.Range(nmin.z, nmax.z - (int)csize + 1);

                if ((ore.Flags & OreFlags.OREFLAG_USE_NOISE) != 0 &&
                    NoiseKernel.Fractal3D(in ore.NoiseParams, x0, y0, z0, MapSeed) < ore.NThresh)
                {
                    continue;
                }

                if (!CheckBiome(in ore, sizex, x0 - nmin.x, z0 - nmin.z))
                {
                    continue;
                }

                for (int z1 = 0; z1 < (int)csize; z1++)
                {
                    for (int y1 = 0; y1 < (int)csize; y1++)
                    {
                        for (int x1 = 0; x1 < (int)csize; x1++)
                        {
                            if (pr.Range(1, (int)cvolume) > ore.ClustNumOres)
                            {
                                continue;
                            }

                            int vi = WorldToVmIndex(x0 + x1, y0 + y1, z0 + z1);

                            if (vi < 0)
                            {
                                continue;
                            }

                            if (!ContainsWherein(in ore, GetContent(vi)))
                            {
                                continue;
                            }

                            Nodes[vi] = packedOre;
                        }
                    }
                }
            }
        }

        private void PlaceSheet(in OreDefRuntime ore, int3 nmin, int3 nmax)
        {
            var pr = new PcgRandom(Blockseed + 4234);
            uint packedOre = MapNode.Pack(ore.ContentOre, 0, ore.OreParam2);

            int sizex = nmax.x - nmin.x + 1;
            ushort maxHeight = ore.ColumnHeightMax > 0 ? ore.ColumnHeightMax : ore.ColumnHeightMin;
            int yStartMin = nmin.y + maxHeight;
            int yStartMax = nmax.y - maxHeight;

            int yStart = yStartMin < yStartMax
                ? pr.Range(yStartMin, yStartMax)
                : (yStartMin + yStartMax) / 2;

            int index = 0;

            for (int z = nmin.z; z <= nmax.z; z++)
            {
                for (int x = nmin.x; x <= nmax.x; x++, index++)
                {
                    float noiseval = NoiseKernel.Fractal2D(in ore.NoiseParams, x, z, MapSeed + yStart);

                    if (noiseval < ore.NThresh)
                    {
                        continue;
                    }

                    if (!CheckBiome(in ore, sizex, x - nmin.x, z - nmin.z))
                    {
                        continue;
                    }

                    ushort height = ore.ColumnHeightMax > 0
                        ? (ushort)pr.Range(ore.ColumnHeightMin, ore.ColumnHeightMax)
                        : ore.ColumnHeightMin;

                    int ymidpoint = yStart + (int)noiseval;
                    int y0 = math.max(nmin.y, ymidpoint - (int)(height * (1f - ore.ColumnMidpointFactor)));
                    int y1 = math.min(nmax.y, y0 + height - 1);

                    for (int y = y0; y <= y1; y++)
                    {
                        int vi = WorldToVmIndex(x, y, z);

                        if (vi < 0)
                        {
                            continue;
                        }

                        if (!ContainsWherein(in ore, GetContent(vi)))
                        {
                            continue;
                        }

                        Nodes[vi] = packedOre;
                    }
                }
            }
        }

        private void PlacePuff(in OreDefRuntime ore, int3 nmin, int3 nmax)
        {
            var pr = new PcgRandom(Blockseed + 4234);
            uint packedOre = MapNode.Pack(ore.ContentOre, 0, ore.OreParam2);

            int sizex = nmax.x - nmin.x + 1;
            int yStart = pr.Range(nmin.y, nmax.y);

            int index = 0;

            for (int z = nmin.z; z <= nmax.z; z++)
            {
                for (int x = nmin.x; x <= nmax.x; x++, index++)
                {
                    float noiseval = NoiseKernel.Fractal2D(in ore.NoiseParams, x, z, MapSeed + yStart);

                    if (noiseval < ore.NThresh)
                    {
                        continue;
                    }

                    if (!CheckBiome(in ore, sizex, x - nmin.x, z - nmin.z))
                    {
                        continue;
                    }

                    float ntop = NoiseKernel.Fractal2D(in ore.NoisePuffTop, x, z, MapSeed);
                    float nbottom = NoiseKernel.Fractal2D(in ore.NoisePuffBottom, x, z, MapSeed);

                    if ((ore.Flags & OreFlags.OREFLAG_PUFF_CLIFFS) == 0)
                    {
                        float ndiff = noiseval - ore.NThresh;

                        if (ndiff < 1.0f)
                        {
                            ntop *= ndiff;
                            nbottom *= ndiff;
                        }
                    }

                    int ymid = yStart;
                    int y0 = ymid - (int)nbottom;
                    int y1 = ymid + (int)ntop;

                    if ((ore.Flags & OreFlags.OREFLAG_PUFF_ADDITIVE) != 0 && y0 > y1)
                    {
                        int tmp = y0;
                        y0 = y1;
                        y1 = tmp;
                    }

                    y0 = math.max(nmin.y, y0);
                    y1 = math.min(nmax.y, y1);

                    for (int y = y0; y <= y1; y++)
                    {
                        int vi = WorldToVmIndex(x, y, z);

                        if (vi < 0)
                        {
                            continue;
                        }

                        if (!ContainsWherein(in ore, GetContent(vi)))
                        {
                            continue;
                        }

                        Nodes[vi] = packedOre;
                    }
                }
            }
        }

        private void PlaceBlob(in OreDefRuntime ore, int3 nmin, int3 nmax)
        {
            var pr = new PcgRandom(Blockseed + 2404);
            uint packedOre = MapNode.Pack(ore.ContentOre, 0, ore.OreParam2);

            int sizex = nmax.x - nmin.x + 1;
            uint volume = (uint)(nmax.x - nmin.x + 1) *
                          (uint)(nmax.y - nmin.y + 1) *
                          (uint)(nmax.z - nmin.z + 1);
            uint csize = (uint)ore.ClustSize;
            uint nblobs = volume / (uint)ore.ClustScarcity;

            for (uint i = 0; i < nblobs; i++)
            {
                int x0 = pr.Range(nmin.x, nmax.x - (int)csize + 1);
                int y0 = pr.Range(nmin.y, nmax.y - (int)csize + 1);
                int z0 = pr.Range(nmin.z, nmax.z - (int)csize + 1);

                if (!CheckBiome(in ore, sizex, x0 - nmin.x, z0 - nmin.z))
                {
                    continue;
                }

                int blobSeed = (int)(Blockseed + i);

                for (int z1 = 0; z1 < (int)csize; z1++)
                {
                    for (int y1 = 0; y1 < (int)csize; y1++)
                    {
                        for (int x1 = 0; x1 < (int)csize; x1++)
                        {
                            int wx = x0 + x1;
                            int wy = y0 + y1;
                            int wz = z0 + z1;

                            int vi = WorldToVmIndex(wx, wy, wz);

                            if (vi < 0)
                            {
                                continue;
                            }

                            if (!ContainsWherein(in ore, GetContent(vi)))
                            {
                                continue;
                            }

                            float noiseval = NoiseKernel.Fractal3D(in ore.NoiseParams, wx, wy, wz, blobSeed);

                            float xdist = x1 - (int)csize / 2f;
                            float ydist = y1 - (int)csize / 2f;
                            float zdist = z1 - (int)csize / 2f;
                            noiseval -= math.sqrt(xdist * xdist + ydist * ydist + zdist * zdist) / csize;

                            if (noiseval < ore.NThresh)
                            {
                                continue;
                            }

                            Nodes[vi] = packedOre;
                        }
                    }
                }
            }
        }

        private void PlaceVein(in OreDefRuntime ore, int3 nmin, int3 nmax)
        {
            var pr = new PcgRandom(Blockseed + 520);
            uint packedOre = MapNode.Pack(ore.ContentOre, 0, ore.OreParam2);

            int sizex = nmax.x - nmin.x + 1;

            for (int z = nmin.z; z <= nmax.z; z++)
            {
                for (int y = nmin.y; y <= nmax.y; y++)
                {
                    for (int x = nmin.x; x <= nmax.x; x++)
                    {
                        int vi = WorldToVmIndex(x, y, z);

                        if (vi < 0)
                        {
                            continue;
                        }

                        if (!ContainsWherein(in ore, GetContent(vi)))
                        {
                            continue;
                        }

                        if (!CheckBiome(in ore, sizex, x - nmin.x, z - nmin.z))
                        {
                            continue;
                        }

                        float randval = (float)pr.Next() / (uint.MaxValue / 2f) - 1f;

                        float noiseval1 = NoiseKernel.Fractal3D(in ore.NoiseParams, x, y, z, MapSeed);
                        float noiseval2 = NoiseKernel.Fractal3D(in ore.NoiseParams, x, y, z, MapSeed + 436);

                        float contour1 = Contour(noiseval1);
                        float contour2 = Contour(noiseval2);

                        if (contour1 * contour2 + randval * ore.RandomFactor < ore.NThresh)
                        {
                            continue;
                        }

                        Nodes[vi] = packedOre;
                    }
                }
            }
        }

        private void PlaceStratum(in OreDefRuntime ore, int3 nmin, int3 nmax)
        {
            var pr = new PcgRandom(Blockseed + 4234);
            uint packedOre = MapNode.Pack(ore.ContentOre, 0, ore.OreParam2);

            bool useNoise = (ore.Flags & OreFlags.OREFLAG_USE_NOISE) != 0;
            bool useNoise2 = (ore.Flags & OreFlags.OREFLAG_USE_NOISE2) != 0;
            int sizex = nmax.x - nmin.x + 1;

            int index = 0;

            for (int z = nmin.z; z <= nmax.z; z++)
            {
                for (int x = nmin.x; x <= nmax.x; x++, index++)
                {
                    if (!CheckBiome(in ore, sizex, x - nmin.x, z - nmin.z))
                    {
                        continue;
                    }

                    int y0, y1;

                    if (useNoise)
                    {
                        float nhalfthick = (useNoise2
                            ? NoiseKernel.Fractal2D(in ore.NoiseStratumThickness, x, z, MapSeed)
                            : ore.StratumThickness) / 2f;

                        float nmid = NoiseKernel.Fractal2D(in ore.NoiseParams, x, z, MapSeed);
                        y0 = math.max(nmin.y, (int)math.ceil(nmid - nhalfthick));
                        y1 = math.min(nmax.y, (int)(nmid + nhalfthick));
                    }
                    else
                    {
                        y0 = nmin.y;
                        y1 = nmax.y;
                    }

                    for (int y = y0; y <= y1; y++)
                    {
                        if (pr.Range(1, ore.ClustScarcity) != 1)
                        {
                            continue;
                        }

                        int vi = WorldToVmIndex(x, y, z);

                        if (vi < 0)
                        {
                            continue;
                        }

                        if (!ContainsWherein(in ore, GetContent(vi)))
                        {
                            continue;
                        }

                        Nodes[vi] = packedOre;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Contour(float v)
        {
            v = math.abs(v);
            return v >= 1f ? 0f : 1f - v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WorldToVmIndex(int x, int y, int z)
        {
            int dx = x - VmMinPos.x;
            int dy = y - VmMinPos.y;
            int dz = z - VmMinPos.z;

            if (dx < 0 || dx >= VoxelManipulator.Sx ||
                dy < 0 || dy >= VoxelManipulator.Sy ||
                dz < 0 || dz >= VoxelManipulator.Sz)
            {
                return -1;
            }

            return dx + dy * VoxelManipulator.Sx + dz * VoxelManipulator.Sx * VoxelManipulator.Sy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetContent(int vi)
        {
            return (ushort)(Nodes[vi] >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ContainsWherein(in OreDefRuntime ore, ushort content)
        {
            for (int i = 0; i < ore.WhereinCount; i++)
            {
                if (Wherein[ore.WhereinOffset + i] == content)
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckBiome(in OreDefRuntime ore, int sizex, int localX, int localZ)
        {
            if (ore.BiomesCount == 0)
            {
                return true;
            }

            int mapIndex = sizex * localZ + localX;

            if (mapIndex < 0 || mapIndex >= Biomemap.Length)
            {
                return true;
            }

            ushort biomeIdx = Biomemap[mapIndex];

            for (int i = 0; i < ore.BiomesCount; i++)
            {
                if (OreBiomes[ore.BiomesOffset + i] == biomeIdx)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
