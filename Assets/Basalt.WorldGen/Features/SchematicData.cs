using Unity.Burst;
using Unity.Mathematics;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Blittable schematic descriptor for Burst job access.
    /// References into flat NativeArrays for node data, probabilities, and slice probabilities.
    /// </summary>
    /// <remarks>
    /// Schematics are 3D voxel templates placed by <see cref="DecorationType.Schematic"/> decorations.
    /// Node data is stored as packed uint values in a shared flat array.
    /// Source: <c>luanti/src/mapgen/mg_schematic.h</c>.
    /// </remarks>
    [BurstCompile]
    public struct SchematicData
    {
        /// <summary>Dimensions of the schematic (X, Y, Z).</summary>
        public int3 Size;

        /// <summary>Start index into the flat schematic nodes array.</summary>
        public int NodesOffset;

        /// <summary>Number of nodes (Size.x * Size.y * Size.z).</summary>
        public int NodesCount;

        /// <summary>Start index into the flat per-node probability array.</summary>
        public int ProbsOffset;

        /// <summary>Start index into the flat per-Y-slice probability array.</summary>
        public int SliceProbsOffset;

        /// <summary>Number of Y slices (= Size.y).</summary>
        public int SliceProbsCount;
    }
}
