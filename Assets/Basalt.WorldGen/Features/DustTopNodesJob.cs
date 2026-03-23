using System.Runtime.CompilerServices;
using Basalt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Burst-compiled job that places biome dust nodes on the first exposed solid surface
    /// of each column. Runs after biome surface replacement, ores, and decorations.
    /// </summary>
    /// <remarks>
    /// Port of Luanti's <c>MapgenBasic::dustTopNodes()</c>.
    /// Source: <c>luanti/src/mapgen/mapgen.cpp</c>.
    ///
    /// Per column: if the biome has a dust node, scan down from the top to find the
    /// first non-air node. If it's a normal/walkable solid node and not the dust node
    /// itself, place dust one node above it.
    /// </remarks>
    [BurstCompile]
    public struct DustTopNodesJob : IJob
    {
        public NativeArray<uint> Nodes;

        [ReadOnly] public NativeArray<ushort> Biomemap;
        [ReadOnly] public NativeArray<BiomeDefRuntime> Biomes;
        [ReadOnly] public NativeArray<NodeDefinition> NodeDefs;

        public int BiomeCount;
        public int3 VmMinPos;
        public int3 VmMaxPos;
        public int WaterLevel;

        public void Execute()
        {
            const int sx = VoxelManipulator.Sx;
            const int sy = VoxelManipulator.Sy;
            const int sz = VoxelManipulator.Sz;

            // Skip if the entire chunk is below water level
            if (VmMaxPos.y < WaterLevel)
            {
                return;
            }

            int index = 0;

            for (int iz = 0; iz < sz; iz++)
            {
                for (int ix = 0; ix < sx; ix++, index++)
                {
                    ushort biomeIdx = Biomemap[index];
                    if (biomeIdx >= BiomeCount)
                    {
                        continue;
                    }

                    BiomeDefRuntime biome = Biomes[biomeIdx];
                    if (biome.ContentDust == BasaltConstants.CONTENT_IGNORE)
                    {
                        continue;
                    }

                    // Start scanning from the top of the VM
                    int yStart = VmMaxPos.y;

                    // Check node at very top
                    int viTop = ix + (yStart - VmMinPos.y) * sx + iz * sx * sy;
                    ushort cTop = GetContent(viTop);

                    if (cTop == BasaltConstants.CONTENT_AIR)
                    {
                        yStart = VmMaxPos.y - 1;
                    }
                    else if (cTop == BasaltConstants.CONTENT_IGNORE)
                    {
                        // Check one below the top of real mapchunk
                        int yCheck = VmMaxPos.y - MapgenV7Constants.OVERGEN_SIZE;
                        int viCheck = ix + (yCheck + 1 - VmMinPos.y) * sx + iz * sx * sy;
                        if (viCheck >= 0 && viCheck < Nodes.Length &&
                            GetContent(viCheck) == BasaltConstants.CONTENT_AIR)
                        {
                            yStart = yCheck;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    // Scan down through air to find top surface
                    int vi = ix + (yStart - VmMinPos.y) * sx + iz * sx * sy;
                    int y = yStart;

                    for (; y >= VmMinPos.y; y--)
                    {
                        if (GetContent(vi) != BasaltConstants.CONTENT_AIR)
                        {
                            break;
                        }
                        vi -= sx; // Move down one Y level
                    }

                    if (y < VmMinPos.y)
                    {
                        continue;
                    }

                    // Check if this node is suitable for dust placement
                    ushort c = GetContent(vi);
                    if (c >= NodeDefs.Length)
                    {
                        continue;
                    }

                    NodeDefinition ndef = NodeDefs[c];

                    // Only place dust on normal/solid walkable nodes that aren't already dust
                    bool isSolidTop = (ndef.Drawtype == DrawType.Normal ||
                                       ndef.Drawtype == DrawType.Allfaces ||
                                       ndef.Drawtype == DrawType.GlasslikeFramed) &&
                                      ndef.Walkable != 0 &&
                                      c != biome.ContentDust;

                    if (isSolidTop)
                    {
                        // Place dust one node above
                        int viAbove = vi + sx;
                        if (viAbove >= 0 && viAbove < Nodes.Length)
                        {
                            Nodes[viAbove] = MapNode.Pack(biome.ContentDust, 0, 0);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetContent(int vi)
        {
            if (vi < 0 || vi >= Nodes.Length)
            {
                return BasaltConstants.CONTENT_IGNORE;
            }
            return (ushort)(Nodes[vi] >> 16);
        }
    }
}
