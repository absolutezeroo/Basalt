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
    /// Burst-compiled job that carves caves using the noise intersection method.
    /// Two 3D noise fields are evaluated through a contour function, and their product
    /// determines where tunnels form.
    /// </summary>
    /// <remarks>
    /// Port of Luanti's <c>CavesNoiseIntersection::generateCaves()</c>.
    /// Source: <c>luanti/src/mapgen/cavegen.cpp</c> lines 55-169.
    ///
    /// The contour function maps noise values to a triangular peak at 0:
    /// <c>contour(v) = max(0, 1 - |v|)</c>.
    /// Caves form where <c>contour(cave1) * contour(cave2) > cave_width</c>.
    ///
    /// Simplified vs Luanti: biome surface replacement at tunnel entrances is omitted.
    /// GenerateBiomesJob handles surface nodes after caves, achieving equivalent results.
    /// </remarks>
    [BurstCompile]
    public struct CavesNoiseIntersectionJob : IJob
    {
        /// <summary>VoxelManipulator node buffer. Read and written.</summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> Nodes;

        /// <summary>Cave1 3D noise result buffer (80x82x80).</summary>
        [ReadOnly] public NativeArray<float> Cave1Result;

        /// <summary>Cave2 3D noise result buffer (80x82x80).</summary>
        [ReadOnly] public NativeArray<float> Cave2Result;

        /// <summary>Node definitions for ground content check.</summary>
        [ReadOnly] public NativeArray<NodeDefinition> NodeDefs;

        /// <summary>World-space minimum corner of the VoxelManipulator.</summary>
        public int3 VmMinPos;

        /// <summary>World-space maximum corner of the VoxelManipulator.</summary>
        public int3 VmMaxPos;

        /// <summary>World-space minimum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmin;

        /// <summary>World-space maximum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmax;

        /// <summary>Cave width threshold. Tunnels form where contour product exceeds this.</summary>
        public float CaveWidth;

        /// <summary>Content ID for air nodes.</summary>
        public ushort ContentAir;

        /// <summary>Maximum Y of stone surface across the mapchunk (length 1). Caps cave scan.</summary>
        [ReadOnly] public NativeArray<int> StoneSurfaceMaxY;

        public void Execute()
        {
            // cave_width >= 10 disables generation (Luanti convention)
            if (CaveWidth >= 10f)
            {
                return;
            }

            int maxStoneY = StoneSurfaceMaxY[0];

            // Skip if entire chunk is above stone surface
            if (Nmin.y > maxStoneY)
            {
                return;
            }

            const int sx = VoxelManipulator.Sx;
            const int sy = VoxelManipulator.Sy;

            uint packedAir = MapNode.Pack(ContentAir, 0, 0);

            // Iterate columns (z, x) then top-down (y from nmax to nmin-1)
            // Luanti iterates the mapchunk range nmin..nmax, plus 1 overgen at bottom
            for (int z = Nmin.z; z <= Nmax.z; z++)
            {
                for (int x = Nmin.x; x <= Nmax.x; x++)
                {
                    // Iterate top-down through the column
                    // Luanti: y from nmax.Y down to nmin.Y - 1 (1 overgen at bottom)
                    for (int y = Nmax.y; y >= Nmin.y - 1; y--)
                    {
                        int vi = WorldToVmIndex(x, y, z);
                        if (vi < 0)
                        {
                            continue;
                        }

                        ushort content = GetContent(vi);

                        // Skip air and non-ground-content nodes
                        if (content == ContentAir)
                        {
                            continue;
                        }

                        if (content >= NodeDefs.Length || NodeDefs[content].IsGroundContent == 0)
                        {
                            continue;
                        }

                        // 3D noise index matches VoxelManipulator index
                        // (same layout: dx + dy*Sx + dz*Sx*Sy)
                        int noiseIndex = vi;

                        float d1 = Contour(Cave1Result[noiseIndex]);
                        float d2 = Contour(Cave2Result[noiseIndex]);

                        if (d1 * d2 > CaveWidth)
                        {
                            Nodes[vi] = packedAir;
                        }
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
    }
}
