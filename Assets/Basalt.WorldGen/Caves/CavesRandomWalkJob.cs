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
    /// Burst-compiled job that carves random walk caves (both small tunnels and large caves).
    /// Uses <see cref="PseudoRandom"/> for deterministic tunnel routing.
    /// </summary>
    /// <remarks>
    /// Port of Luanti's <c>MapgenBasic::generateCavesRandomWalk()</c> and
    /// <c>CavesRandomWalk::makeCave()</c> / <c>makeTunnel()</c> / <c>carveRoute()</c>.
    /// Source: <c>luanti/src/mapgen/mapgen.cpp</c> lines 847-875.
    /// Source: <c>luanti/src/mapgen/cavegen.cpp</c> lines 270-610.
    ///
    /// Must be IJob (not parallel) because PseudoRandom state is sequential.
    /// </remarks>
    [BurstCompile]
    public struct CavesRandomWalkJob : IJob
    {
        /// <summary>VoxelManipulator node buffer. Read and written.</summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<uint> Nodes;

        /// <summary>Surface heightmap (80x80). Used to keep caves underground.</summary>
        [ReadOnly] public NativeArray<short> Heightmap;

        /// <summary>Node definitions for ground content check.</summary>
        [ReadOnly] public NativeArray<NodeDefinition> NodeDefs;

        /// <summary>Near-cavern flag from CavernsNoiseJob (length 1).</summary>
        [ReadOnly] public NativeArray<byte> NearCavern;

        /// <summary>World-space minimum corner of the VoxelManipulator.</summary>
        public int3 VmMinPos;

        /// <summary>World-space maximum corner of the VoxelManipulator.</summary>
        public int3 VmMaxPos;

        /// <summary>World-space minimum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmin;

        /// <summary>World-space maximum corner of the mapchunk (excluding overgeneration).</summary>
        public int3 Nmax;

        /// <summary>Y coordinate below which large caves can generate.</summary>
        public int LargeCaveDepth;

        /// <summary>Minimum/maximum counts for small random walk caves.</summary>
        public int SmallCaveNumMin;
        public int SmallCaveNumMax;

        /// <summary>Minimum/maximum counts for large random walk caves.</summary>
        public int LargeCaveNumMin;
        public int LargeCaveNumMax;

        /// <summary>Probability of a large cave being flooded (0.0 - 1.0).</summary>
        public float LargeCaveFlooded;

        /// <summary>Block seed for this mapchunk.</summary>
        public uint Blockseed;

        /// <summary>World seed for noise evaluation.</summary>
        public int MapSeed;

        /// <summary>Content ID for air nodes.</summary>
        public ushort ContentAir;

        /// <summary>Content ID for water source nodes.</summary>
        public ushort ContentWater;

        /// <summary>Water surface Y coordinate.</summary>
        public int WaterLevel;

        /// <summary>Maximum Y of stone surface (length 1). Chunks above this are skipped.</summary>
        [ReadOnly] public NativeArray<int> StoneSurfaceMaxY;

        public void Execute()
        {
            int maxStoneY = StoneSurfaceMaxY[0];

            if (Nmin.y > maxStoneY)
            {
                return;
            }

            // Luanti: PseudoRandom ps(blockseed + 21343)
            var ps = new PseudoRandom((int)(Blockseed + 21343));

            // Small random walk caves
            int numSmallCaves = ps.Range(SmallCaveNumMin, SmallCaveNumMax);
            for (int i = 0; i < numSmallCaves; i++)
            {
                MakeCave(ref ps, false, maxStoneY);
            }

            // Large caves: if near cavern, disable by setting depth to world base
            int effectiveLargeCaveDepth = LargeCaveDepth;
            if (NearCavern[0] != 0)
            {
                effectiveLargeCaveDepth = -31007; // -MAX_MAP_GENERATION_LIMIT
            }

            if (Nmax.y > effectiveLargeCaveDepth)
            {
                return;
            }

            int numLargeCaves = ps.Range(LargeCaveNumMin, LargeCaveNumMax);
            for (int i = 0; i < numLargeCaves; i++)
            {
                MakeCave(ref ps, true, maxStoneY);
            }
        }

        /// <summary>
        /// Generates a single random walk cave (small or large).
        /// </summary>
        /// <remarks>
        /// Port of <c>CavesRandomWalk::makeCave()</c>.
        /// Source: <c>luanti/src/mapgen/cavegen.cpp</c> lines 308-420.
        /// </remarks>
        private void MakeCave(ref PseudoRandom ps, bool isLargeCave, int maxStoneHeight)
        {
            bool flooded = ps.Range(1, 1000) <= (int)(LargeCaveFlooded * 1000f);
            if (!isLargeCave)
            {
                flooded = false;
            }

            // Set initial parameters from randomness
            int dswitchint = ps.Range(1, 14);

            int partMaxLengthRs;
            int tunnelRoutepoints;
            int minTunnelDiameter;
            int maxTunnelDiameter;

            if (isLargeCave)
            {
                partMaxLengthRs = ps.Range(2, 4);
                tunnelRoutepoints = ps.Range(5, ps.Range(15, 30));
                minTunnelDiameter = 5;
                maxTunnelDiameter = ps.Range(7, ps.Range(8, 24));
            }
            else
            {
                partMaxLengthRs = ps.Range(2, 9);
                tunnelRoutepoints = ps.Range(10, ps.Range(15, 30));
                minTunnelDiameter = 2;
                maxTunnelDiameter = ps.Range(2, 6);
            }

            bool largeCaveIsFlat = (ps.Range(0, 1) == 0);

            float3 mainDirection = float3.zero;

            // Allowed route area size in nodes
            int3 ar = Nmax - Nmin + new int3(1, 1, 1);
            // Area starting point in nodes
            int3 of = Nmin;

            // Allow caves to extend beyond the mapchunk edge
            const int insure = 2;
            int more = math.max(BasaltConstants.MAP_BLOCKSIZE - maxTunnelDiameter / 2 - insure, 1);
            ar += new int3(1, 1, 1) * more * 2;
            of -= new int3(1, 1, 1) * more;

            int routeYMin = 0;
            // Allow half a diameter + 7 over stone surface
            int routeYMax = -of.y + maxStoneHeight + maxTunnelDiameter / 2 + 7;
            routeYMax = math.clamp(routeYMax, 0, ar.y - 1);

            if (isLargeCave)
            {
                int minpos = 0;
                if (Nmin.y < WaterLevel && Nmax.y > WaterLevel)
                {
                    minpos = WaterLevel - maxTunnelDiameter / 3 - of.y;
                    routeYMax = WaterLevel + maxTunnelDiameter / 3 - of.y;
                }
                routeYMin = ps.Range(minpos, minpos + maxTunnelDiameter);
                routeYMin = math.clamp(routeYMin, 0, routeYMax);
            }

            int routeStartYMin = math.clamp(routeYMin, 0, ar.y - 1);
            int routeStartYMax = math.clamp(routeYMax, routeStartYMin, ar.y - 1);

            // Randomize starting position
            float3 orp;
            orp.z = (float)((int)ps.Next() % ar.z) + 0.5f;
            orp.y = (float)ps.Range(routeStartYMin, routeStartYMax) + 0.5f;
            orp.x = (float)((int)ps.Next() % ar.x) + 0.5f;

            // Generate tunnel segments
            int rs = minTunnelDiameter; // Current tunnel radius

            for (int j = 0; j < tunnelRoutepoints; j++)
            {
                bool dirswitch = (j % dswitchint == 0);

                MakeTunnel(ref ps, ref orp, ref mainDirection, ref rs,
                    dirswitch, isLargeCave, largeCaveIsFlat, flooded,
                    minTunnelDiameter, maxTunnelDiameter, partMaxLengthRs,
                    ar, of, routeYMin, routeYMax);
            }
        }

        /// <summary>
        /// Generates one tunnel segment of a random walk cave.
        /// </summary>
        /// <remarks>
        /// Port of <c>CavesRandomWalk::makeTunnel()</c>.
        /// Source: <c>luanti/src/mapgen/cavegen.cpp</c> lines 423-505.
        /// </remarks>
        private void MakeTunnel(
            ref PseudoRandom ps,
            ref float3 orp,
            ref float3 mainDirection,
            ref int rs,
            bool dirswitch,
            bool isLargeCave,
            bool largeCaveIsFlat,
            bool flooded,
            int minTunnelDiameter,
            int maxTunnelDiameter,
            int partMaxLengthRs,
            int3 ar, int3 of,
            int routeYMin, int routeYMax)
        {
            if (dirswitch && !isLargeCave)
            {
                mainDirection.z = ((float)((int)ps.Next() % 20) - 10f) / 10f;
                mainDirection.y = ((float)((int)ps.Next() % 20) - 10f) / 30f;
                mainDirection.x = ((float)((int)ps.Next() % 20) - 10f) / 10f;
                mainDirection *= (float)ps.Range(0, 10) / 10f;
            }

            // Randomize size
            rs = ps.Range(minTunnelDiameter, maxTunnelDiameter);
            int rsPartMaxLengthRs = rs * partMaxLengthRs;

            int3 maxlen;
            if (isLargeCave)
            {
                maxlen = new int3(rsPartMaxLengthRs, rsPartMaxLengthRs / 2, rsPartMaxLengthRs);
            }
            else
            {
                maxlen = new int3(rsPartMaxLengthRs, ps.Range(1, rsPartMaxLengthRs), rsPartMaxLengthRs);
            }

            // Ensure maxlen components are >= 1 to avoid division by zero in ps.Next() % maxlen
            maxlen = math.max(maxlen, new int3(1, 1, 1));

            // Luanti: vec is always computed first, then conditionally overridden.
            // Both paths must consume the same PseudoRandom calls for sequence parity.
            // Source: cavegen.cpp lines 455-463.
            float3 vec;
            vec.z = (float)((int)ps.Next() % maxlen.z) - (float)maxlen.z / 2f;
            vec.y = (float)((int)ps.Next() % maxlen.y) - (float)maxlen.y / 2f;
            vec.x = (float)((int)ps.Next() % maxlen.x) - (float)maxlen.x / 2f;

            // Jump downward sometimes (override vec with deeper Y range)
            if (!isLargeCave && ps.Range(0, 12) == 0)
            {
                vec.z = (float)((int)ps.Next() % maxlen.z) - (float)maxlen.z / 2f;
                vec.y = (float)((int)ps.Next() % (maxlen.y * 2)) - (float)maxlen.y;
                vec.x = (float)((int)ps.Next() % maxlen.x) - (float)maxlen.x / 2f;
            }

            // Do not make caves that are above ground
            int3 p1 = new int3((int)orp.x, (int)orp.y, (int)orp.z) + of + rs / 2;
            int3 p2 = new int3((int)vec.x, (int)vec.y, (int)vec.z) + p1;
            if (IsPosAboveSurface(p1) || IsPosAboveSurface(p2))
            {
                return;
            }

            vec += mainDirection;

            float3 rp = orp + vec;
            rp.x = math.clamp(rp.x, 0f, ar.x - 1f);
            rp.y = math.clamp(rp.y, routeYMin, routeYMax - 1f);
            rp.z = math.clamp(rp.z, 0f, ar.z - 1f);

            vec = rp - orp;

            float veclen = math.length(vec);
            if (veclen < 0.05f)
            {
                veclen = 1.0f;
            }

            // Every second section is rough
            bool randomizeXz = (ps.Range(1, 2) == 1);

            // Carve routes
            for (float f = 0f; f < 1.0f; f += 1.0f / veclen)
            {
                CarveRoute(ref ps, orp, vec, f, randomizeXz, rs,
                    isLargeCave, largeCaveIsFlat, flooded, of);
            }

            orp = rp;
        }

        /// <summary>
        /// Carves a spheroid shape at one point along the tunnel route.
        /// </summary>
        /// <remarks>
        /// Port of <c>CavesRandomWalk::carveRoute()</c>.
        /// Source: <c>luanti/src/mapgen/cavegen.cpp</c> lines 508-594.
        /// </remarks>
        private void CarveRoute(
            ref PseudoRandom ps,
            float3 orp, float3 vec, float f,
            bool randomizeXz, int rs,
            bool isLargeCave, bool largeCaveIsFlat, bool flooded,
            int3 of)
        {
            uint packedAir = MapNode.Pack(ContentAir, 0, 0);
            uint packedWater = MapNode.Pack(ContentWater, 0, 0);

            float3 fp = orp + vec * f;
            fp.x += 0.1f * ps.Range(-10, 10);
            fp.z += 0.1f * ps.Range(-10, 10);
            int3 cp = new int3((int)fp.x, (int)fp.y, (int)fp.z);

            int d0 = -rs / 2;
            int d1 = d0 + rs;
            if (randomizeXz)
            {
                d0 += ps.Range(-1, 1);
                d1 += ps.Range(-1, 1);
            }

            bool flatCaveFloor = !isLargeCave && ps.Range(0, 2) == 2;

            for (int z0 = d0; z0 <= d1; z0++)
            {
                int si = rs / 2 - math.max(0, math.abs(z0) - rs / 7 - 1);
                for (int x0 = -si - ps.Range(0, 1); x0 <= si - 1 + ps.Range(0, 1); x0++)
                {
                    int maxabsxz = math.max(math.abs(x0), math.abs(z0));
                    int si2 = rs / 2 - math.max(0, maxabsxz - rs / 7 - 1);

                    for (int y0 = -si2; y0 <= si2; y0++)
                    {
                        // Make better floors in small caves
                        if (flatCaveFloor && y0 <= -rs / 2 && rs <= 7)
                        {
                            continue;
                        }

                        if (largeCaveIsFlat)
                        {
                            // Make large caves not so tall
                            if (rs > 7 && math.abs(y0) >= rs / 3)
                            {
                                continue;
                            }
                        }

                        int wx = cp.x + x0 + of.x;
                        int wy = cp.y + y0 + of.y;
                        int wz = cp.z + z0 + of.z;

                        int vi = WorldToVmIndex(wx, wy, wz);
                        if (vi < 0)
                        {
                            continue;
                        }

                        ushort content = GetContent(vi);
                        if (content >= NodeDefs.Length || NodeDefs[content].IsGroundContent == 0)
                        {
                            continue;
                        }

                        if (isLargeCave)
                        {
                            int fullYmin = Nmin.y - BasaltConstants.MAP_BLOCKSIZE;
                            int fullYmax = Nmax.y + BasaltConstants.MAP_BLOCKSIZE;

                            if (flooded && fullYmin < WaterLevel && fullYmax > WaterLevel)
                            {
                                Nodes[vi] = (wy <= WaterLevel) ? packedWater : packedAir;
                            }
                            else if (flooded && fullYmax < WaterLevel)
                            {
                                // Luanti: lavanode for deep (startp.y - 2), airnode above.
                                // startp = orp + of (segment start in world space).
                                // Simplified: use water for flooded caves below water level.
                                int startY = (int)orp.y + of.y;
                                Nodes[vi] = (wy < startY - 2) ? packedWater : packedAir;
                            }
                            else
                            {
                                Nodes[vi] = packedAir;
                            }
                        }
                        else
                        {
                            Nodes[vi] = packedAir;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a world position is above the terrain surface.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsPosAboveSurface(int3 p)
        {
            int dx = p.x - VmMinPos.x;
            int dz = p.z - VmMinPos.z;

            if (dx >= 0 && dx < VoxelManipulator.Sx &&
                dz >= 0 && dz < VoxelManipulator.Sz)
            {
                int hmIndex = dx + dz * VoxelManipulator.Sx;
                if (hmIndex >= 0 && hmIndex < Heightmap.Length)
                {
                    return p.y > Heightmap[hmIndex];
                }
            }

            // Fallback: above water level = above surface
            return p.y > WaterLevel;
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