using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Burst-compiled job that places all registered decorations into the VoxelManipulator.
    /// Implements DecoSimple (column of nodes) and DecoSchematic (3D voxel template).
    /// </summary>
    /// <remarks>
    /// Port of Luanti's <c>Decoration::placeDeco()</c>, <c>DecoSimple::generate()</c>,
    /// and <c>DecoSchematic::generate()</c>.
    /// Source: <c>luanti/src/mapgen/mg_decoration.cpp</c>.
    ///
    /// The chunk XZ area is subdivided into sidelen x sidelen tiles. Per tile,
    /// density comes from fill_ratio or 2D noise. Random positions are chosen
    /// within each tile.
    /// </remarks>
    [BurstCompile]
    public struct PlaceAllDecorationsJob : IJob
    {
        /// <summary>VoxelManipulator node buffer (read/write). Decorations are placed into this array.</summary>
        public NativeArray<uint> Nodes;

        /// <summary>Heightmap array mapping XZ columns to surface Y. Used to find placement height.</summary>
        [ReadOnly] public NativeArray<short> Heightmap;

        /// <summary>Biome index per XZ column in the chunk area. Used for biome-restricted decorations.</summary>
        [ReadOnly] public NativeArray<ushort> Biomemap;

        /// <summary>Flat array of all registered decoration definitions.</summary>
        [ReadOnly] public NativeArray<DecorationDefRuntime> Decos;

        /// <summary>Flat array of content IDs for place_on lists (indexed by each deco's PlaceOnOffset).</summary>
        [ReadOnly] public NativeArray<ushort> PlaceOn;

        /// <summary>Flat array of biome indices per decoration (indexed by each deco's BiomesOffset).</summary>
        [ReadOnly] public NativeArray<ushort> DecoBiomes;

        /// <summary>Flat array of content IDs for spawn_by lists (indexed by each deco's SpawnByOffset).</summary>
        [ReadOnly] public NativeArray<ushort> SpawnBy;

        /// <summary>Flat array of content IDs for DecoSimple (indexed by each deco's DecosOffset).</summary>
        [ReadOnly] public NativeArray<ushort> DecoContents;

        /// <summary>Array of schematic metadata (size, offsets into SchematicNodes/Probs arrays).</summary>
        [ReadOnly] public NativeArray<SchematicData> Schematics;

        /// <summary>Flat array of packed node data for all schematics (indexed by SchematicData.NodesOffset).</summary>
        [ReadOnly] public NativeArray<uint> SchematicNodes;

        /// <summary>Per-node placement probability for schematics (255 = always, indexed by SchematicData.ProbsOffset).</summary>
        [ReadOnly] public NativeArray<byte> SchematicProbs;

        /// <summary>Per-Y-slice placement probability for schematics (255 = always, indexed by SchematicData.SliceProbsOffset).</summary>
        [ReadOnly] public NativeArray<byte> SchematicSliceProbs;

        /// <summary>Number of decoration definitions to process.</summary>
        public int DecoCount;

        /// <summary>Minimum corner of the VoxelManipulator volume in world coordinates.</summary>
        public int3 VmMinPos;

        /// <summary>Maximum corner of the VoxelManipulator volume in world coordinates.</summary>
        public int3 VmMaxPos;

        /// <summary>Minimum corner of the mapchunk content area in world coordinates.</summary>
        public int3 Nmin;

        /// <summary>Maximum corner of the mapchunk content area in world coordinates.</summary>
        public int3 Nmax;

        /// <summary>World seed used for noise-based decoration density.</summary>
        public int MapSeed;

        /// <summary>Per-mapchunk block seed for deterministic pseudo-random placement.</summary>
        public uint Blockseed;

        public void Execute()
        {
            for (int d = 0; d < DecoCount; d++)
            {
                DecorationDefRuntime deco = Decos[d];

                if (Nmax.y < deco.YMin || deco.YMax < Nmin.y)
                {
                    continue;
                }

                PlaceDeco(in deco, d);
            }
        }

        private void PlaceDeco(in DecorationDefRuntime deco, int decoIndex)
        {
            var ps = new PcgRandom(Blockseed + 53);
            int careaSize = Nmax.x - Nmin.x + 1;

            int sidelen = deco.Sidelen;
            if (sidelen <= 0 || careaSize % sidelen != 0)
            {
                sidelen = careaSize;
            }

            int area = sidelen * sidelen;

            for (int z0 = 0; z0 < careaSize; z0 += sidelen)
            {
                for (int x0 = 0; x0 < careaSize; x0 += sidelen)
                {
                    int p2dMinX = Nmin.x + x0;
                    int p2dMinZ = Nmin.z + z0;
                    int p2dMaxX = p2dMinX + sidelen - 1;
                    int p2dMaxZ = p2dMinZ + sidelen - 1;

                    // Compute density
                    float nval;
                    if ((deco.Flags & DecorationFlags.DECO_USE_NOISE) != 0)
                    {
                        nval = NoiseKernel.Fractal2D(
                            in deco.NoiseParams,
                            p2dMinX + sidelen / 2f,
                            p2dMinZ + sidelen / 2f,
                            MapSeed);
                    }
                    else
                    {
                        nval = deco.FillRatio;
                    }

                    uint decoCount;
                    bool cover = false;

                    if (nval >= 10.0f)
                    {
                        cover = true;
                        decoCount = (uint)area;
                    }
                    else
                    {
                        float decoCountF = area * nval;
                        if (decoCountF >= 1.0f)
                        {
                            decoCount = (uint)decoCountF;
                        }
                        else if (decoCountF > 0.0f)
                        {
                            // Low density: probabilistic single placement
                            if (ps.Next() <= (uint)(decoCountF * (float)uint.MaxValue))
                            {
                                decoCount = 1;
                            }
                            else
                            {
                                decoCount = 0;
                            }
                        }
                        else
                        {
                            decoCount = 0;
                        }
                    }

                    int cx = p2dMinX - 1;
                    int cz = p2dMinZ;

                    for (uint i = 0; i < decoCount; i++)
                    {
                        int x, z;
                        if (!cover)
                        {
                            x = ps.Range(p2dMinX, p2dMaxX);
                            z = ps.Range(p2dMinZ, p2dMaxZ);
                        }
                        else
                        {
                            cx++;
                            if (cx == p2dMaxX + 1)
                            {
                                cz++;
                                cx = p2dMinX;
                            }
                            x = cx;
                            z = cz;
                        }

                        int mapindex = careaSize * (z - Nmin.z) + (x - Nmin.x);

                        // Get surface Y from heightmap
                        int hmIdx = VoxelManipulator.HeightmapIndex(x - VmMinPos.x, z - VmMinPos.z);
                        if (hmIdx < 0 || hmIdx >= Heightmap.Length)
                        {
                            continue;
                        }

                        int surfaceY = Heightmap[hmIdx];

                        if (surfaceY < deco.YMin || surfaceY > deco.YMax ||
                            surfaceY < Nmin.y || surfaceY > Nmax.y)
                        {
                            continue;
                        }

                        // Biome check
                        if (deco.BiomesCount > 0 && mapindex >= 0 && mapindex < Biomemap.Length)
                        {
                            ushort biomeIdx = Biomemap[mapindex];
                            if (!ContainsBiome(in deco, biomeIdx))
                            {
                                continue;
                            }
                        }

                        int3 pos = new int3(x, surfaceY, z);

                        switch (deco.Type)
                        {
                            case DecorationType.Simple:
                                GenerateSimple(in deco, ref ps, pos);
                                break;
                            case DecorationType.Schematic:
                                GenerateSchematic(in deco, ref ps, pos);
                                break;
                        }
                    }
                }
            }
        }

        private void GenerateSimple(in DecorationDefRuntime deco, ref PcgRandom pr, int3 pos)
        {
            if (deco.DecosCount == 0)
            {
                return;
            }

            // Check place_on
            if (!CanPlaceDecoration(in deco, pos))
            {
                return;
            }

            // Bounds check
            int maxHeight = math.max(deco.DecoHeight, deco.DecoHeightMax);
            if (pos.y + deco.PlaceOffsetY + maxHeight > VmMaxPos.y)
            {
                return;
            }

            if (pos.y + 1 + deco.PlaceOffsetY < VmMinPos.y)
            {
                return;
            }

            // Pick random decoration content
            ushort cPlace = DecoContents[deco.DecosOffset + pr.Range(0, deco.DecosCount - 1)];
            short height = (deco.DecoHeightMax > 0)
                ? (short)pr.Range(deco.DecoHeight, deco.DecoHeightMax)
                : deco.DecoHeight;
            byte param2 = (deco.DecoParam2Max > 0)
                ? (byte)pr.Range(deco.DecoParam2, deco.DecoParam2Max)
                : deco.DecoParam2;

            bool forcePlace = (deco.Flags & DecorationFlags.DECO_FORCE_PLACEMENT) != 0;
            uint packedDeco = MapNode.Pack(cPlace, 0, param2);

            // Place column upward from surface
            int startY = pos.y + deco.PlaceOffsetY + 1;
            for (int i = 0; i < height; i++)
            {
                int y = startY + i;
                int vi = WorldToVmIndex(pos.x, y, pos.z);
                if (vi < 0)
                {
                    break;
                }

                ushort c = GetContent(vi);
                if (c != BasaltConstants.CONTENT_AIR && c != BasaltConstants.CONTENT_IGNORE && !forcePlace)
                {
                    break;
                }

                Nodes[vi] = packedDeco;
            }
        }

        private void GenerateSchematic(in DecorationDefRuntime deco, ref PcgRandom pr, int3 pos)
        {
            if (deco.SchematicIndex < 0 || deco.SchematicIndex >= Schematics.Length)
            {
                return;
            }

            if (!CanPlaceDecoration(in deco, pos))
            {
                return;
            }

            SchematicData schem = Schematics[deco.SchematicIndex];

            // Position adjustment
            int3 p = pos;

            if ((deco.Flags & DecorationFlags.DECO_PLACE_CENTER_Y) != 0)
            {
                p.y -= (schem.Size.y - 1) / 2;
            }
            else
            {
                p.y += deco.PlaceOffsetY;
            }

            // Bounds check
            if (p.y + schem.Size.y - 1 > VmMaxPos.y)
            {
                return;
            }

            if (p.y < VmMinPos.y)
            {
                return;
            }

            // Rotation (0-3 fixed, 4 = random)
            int rot = deco.Rotation >= 4 ? pr.Range(0, 3) : deco.Rotation;

            // XZ centering
            if ((deco.Flags & DecorationFlags.DECO_PLACE_CENTER_X) != 0)
            {
                if (rot == 0 || rot == 2)
                {
                    p.x -= (schem.Size.x - 1) / 2;
                }
                else
                {
                    p.z -= (schem.Size.x - 1) / 2;
                }
            }

            if ((deco.Flags & DecorationFlags.DECO_PLACE_CENTER_Z) != 0)
            {
                if (rot == 0 || rot == 2)
                {
                    p.z -= (schem.Size.z - 1) / 2;
                }
                else
                {
                    p.x -= (schem.Size.z - 1) / 2;
                }
            }

            bool forcePlace = (deco.Flags & DecorationFlags.DECO_FORCE_PLACEMENT) != 0;

            BlitSchematic(in schem, p, rot, forcePlace, ref pr);
        }

        private void BlitSchematic(in SchematicData schem, int3 pos, int rot, bool forcePlace, ref PcgRandom pr)
        {
            int3 size = schem.Size;
            int nodeIdx = 0;

            for (int sy = 0; sy < size.y; sy++)
            {
                // Per-slice probability
                if (schem.SliceProbsCount > sy)
                {
                    byte sliceProb = SchematicSliceProbs[schem.SliceProbsOffset + sy];
                    if (sliceProb != 255 && pr.Range(0, 254) >= sliceProb)
                    {
                        nodeIdx += size.x * size.z;
                        continue;
                    }
                }

                for (int sz = 0; sz < size.z; sz++)
                {
                    for (int sx = 0; sx < size.x; sx++, nodeIdx++)
                    {
                        // Per-node probability
                        if (schem.ProbsOffset + nodeIdx < SchematicProbs.Length)
                        {
                            byte nodeProb = SchematicProbs[schem.ProbsOffset + nodeIdx];
                            if (nodeProb != 255 && pr.Range(0, 254) >= nodeProb)
                            {
                                continue;
                            }
                        }

                        // Rotate coordinates
                        int rx, rz;
                        RotateXZ(sx, sz, size.x, size.z, rot, out rx, out rz);

                        int wx = pos.x + rx;
                        int wy = pos.y + sy;
                        int wz = pos.z + rz;

                        int vi = WorldToVmIndex(wx, wy, wz);
                        if (vi < 0)
                        {
                            continue;
                        }

                        uint packedNode = SchematicNodes[schem.NodesOffset + nodeIdx];
                        ushort content = (ushort)(packedNode >> 16);

                        // Skip CONTENT_IGNORE nodes in schematic (means "don't modify")
                        if (content == BasaltConstants.CONTENT_IGNORE)
                        {
                            continue;
                        }

                        if (!forcePlace)
                        {
                            ushort existing = GetContent(vi);
                            if (existing != BasaltConstants.CONTENT_AIR &&
                                existing != BasaltConstants.CONTENT_IGNORE)
                            {
                                continue;
                            }
                        }

                        Nodes[vi] = packedNode;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RotateXZ(int x, int z, int sizeX, int sizeZ, int rot, out int rx, out int rz)
        {
            switch (rot)
            {
                case 1: // 90 degrees
                    rx = sizeZ - 1 - z;
                    rz = x;
                    break;
                case 2: // 180 degrees
                    rx = sizeX - 1 - x;
                    rz = sizeZ - 1 - z;
                    break;
                case 3: // 270 degrees
                    rx = z;
                    rz = sizeX - 1 - x;
                    break;
                default: // 0 degrees
                    rx = x;
                    rz = z;
                    break;
            }
        }

        private bool CanPlaceDecoration(in DecorationDefRuntime deco, int3 pos)
        {
            // Check place_on
            if (deco.PlaceOnCount > 0)
            {
                int vi = WorldToVmIndex(pos.x, pos.y, pos.z);
                if (vi < 0)
                {
                    return false;
                }

                ushort surfaceContent = GetContent(vi);
                bool found = false;
                for (int i = 0; i < deco.PlaceOnCount; i++)
                {
                    if (PlaceOn[deco.PlaceOnOffset + i] == surfaceContent)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            // Check spawnby neighbors
            if (deco.NSpawnBy >= 0 && deco.SpawnByCount > 0)
            {
                int nneighs = 0;

                // 8 neighbors at y+1 from surface (horizontal ring)
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                        {
                            continue;
                        }

                        int vi = WorldToVmIndex(pos.x + dx, pos.y + 1, pos.z + dz);
                        if (vi < 0)
                        {
                            continue;
                        }

                        ushort c = GetContent(vi);
                        for (int s = 0; s < deco.SpawnByCount; s++)
                        {
                            if (SpawnBy[deco.SpawnByOffset + s] == c)
                            {
                                nneighs++;
                                break;
                            }
                        }
                    }
                }

                if (nneighs < deco.NSpawnBy)
                {
                    return false;
                }
            }

            return true;
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
        private bool ContainsBiome(in DecorationDefRuntime deco, ushort biomeIdx)
        {
            for (int i = 0; i < deco.BiomesCount; i++)
            {
                if (DecoBiomes[deco.BiomesOffset + i] == biomeIdx)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
