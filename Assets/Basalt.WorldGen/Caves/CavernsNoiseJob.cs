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
    /// Burst-compiled job that carves large caverns using a single 3D noise field
    /// with a depth-based amplitude taper.
    /// </summary>
    /// <remarks>
    /// Port of Luanti's <c>CavernsNoise::generateCaverns()</c>.
    /// Source: <c>luanti/src/mapgen/cavegen.cpp</c> lines 213-267.
    ///
    /// Caverns only generate below <see cref="CavernLimit"/>. Above that, the amplitude
    /// tapers linearly over <see cref="CavernTaper"/> nodes until it reaches zero.
    /// Carves where <c>|noise| * taperMultiplier > CavernThreshold</c>.
    ///
    /// Outputs a <c>near_cavern</c> flag (byte 0/1) that tells downstream
    /// <see cref="CavesRandomWalkJob"/> whether to disable large random walk caves
    /// to avoid excessive liquid spreading.
    /// </remarks>
    [BurstCompile]
    public struct CavernsNoiseJob : IJob
    {
        /// <summary>VoxelManipulator node buffer. Read and written.</summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> Nodes;

        /// <summary>Cavern 3D noise result buffer (80x82x80).</summary>
        [ReadOnly] public NativeArray<float> CavernResult;

        /// <summary>Node definitions for ground content check.</summary>
        [ReadOnly] public NativeArray<NodeDefinition> NodeDefs;

        /// <summary>
        /// Output: 1 if any cavern was near this mapchunk, 0 otherwise.
        /// Length 1. Read by <see cref="CavesRandomWalkJob"/>.
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<byte> NearCavernOut;

        /// <summary>World-space minimum corner of the VoxelManipulator.</summary>
        public int3 VmMinPos;

        /// <summary>World-space maximum corner of the VoxelManipulator.</summary>
        public int3 VmMaxPos;

        /// <summary>World-space minimum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmin;

        /// <summary>World-space maximum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmax;

        /// <summary>Y coordinate above which caverns are disabled. Default: -256.</summary>
        public int CavernLimit;

        /// <summary>Taper range above CavernLimit. Default: 256.</summary>
        public int CavernTaper;

        /// <summary>Noise threshold for cavern carving. Default: 0.7.</summary>
        public float CavernThreshold;

        /// <summary>Content ID for air nodes.</summary>
        public ushort ContentAir;

        /// <summary>Maximum Y of stone surface (length 1). Chunks above this are skipped.</summary>
        [ReadOnly] public NativeArray<int> StoneSurfaceMaxY;

        public void Execute()
        {
            int maxStoneY = StoneSurfaceMaxY[0];

            // Skip if chunk is above stone surface or above cavern influence range
            if (Nmin.y > maxStoneY || Nmin.y > CavernLimit)
            {
                NearCavernOut[0] = 0;
                return;
            }

            uint packedAir = MapNode.Pack(ContentAir, 0, 0);
            byte nearCavern = 0;

            // Pre-compute cavern amplitude taper values per Y level
            // Luanti: cavern_amp[y_index] = MYMIN((cavern_limit - y) / (float)cavern_taper, 1.0f)
            // We iterate top-down, same as Luanti
            for (int z = Nmin.z; z <= Nmax.z; z++)
            {
                for (int x = Nmin.x; x <= Nmax.x; x++)
                {
                    // Top-down: from Nmax.y to Nmin.y - 1 (1 overgen at bottom)
                    for (int y = Nmax.y; y >= Nmin.y - 1; y--)
                    {
                        int vi = WorldToVmIndex(x, y, z);
                        if (vi < 0)
                        {
                            continue;
                        }

                        ushort content = GetContent(vi);

                        if (content == ContentAir)
                        {
                            continue;
                        }

                        if (content >= NodeDefs.Length || NodeDefs[content].IsGroundContent == 0)
                        {
                            continue;
                        }

                        // Amplitude taper: 1.0 at and below CavernLimit, linearly
                        // decreasing to 0.0 at CavernLimit + CavernTaper
                        float cavernAmp = math.min(
                            (float)(CavernLimit - y) / CavernTaper, 1.0f);

                        if (cavernAmp <= 0f)
                        {
                            continue;
                        }

                        float nAbsAmpCavern = math.abs(CavernResult[vi]) * cavernAmp;

                        // Disable CavesRandomWalk at a safe distance from caverns
                        if (nAbsAmpCavern > CavernThreshold - 0.1f)
                        {
                            nearCavern = 1;

                            if (nAbsAmpCavern > CavernThreshold)
                            {
                                Nodes[vi] = packedAir;
                            }
                        }
                    }
                }
            }

            NearCavernOut[0] = nearCavern;
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
    }
}
