using System.Runtime.CompilerServices;

namespace Basalt.Meshing
{
    /// <summary>
    /// Maps a face direction to its three working axes for greedy meshing.
    /// </summary>
    /// <remarks>
    /// The greedy meshing algorithm iterates slices perpendicular to the face normal.
    /// Within each slice, it operates on a 2D grid: rows along RowAxis, columns along ColAxis.
    ///
    /// Axis encoding: 0 = X, 1 = Y, 2 = Z.
    ///
    /// Face direction to axis mapping:
    ///   PosY / NegY  ->  Normal=Y(1), Row=Z(2), Col=X(0)
    ///   PosX / NegX  ->  Normal=X(0), Row=Y(1), Col=Z(2)
    ///   PosZ / NegZ  ->  Normal=Z(2), Row=Y(1), Col=X(0)
    ///
    /// Within each bitmask word, bit N corresponds to row N. Words are indexed by column.
    /// Greedy spans extend along RowAxis (consecutive bits). Quads extend across ColAxis (words).
    /// </remarks>
    public readonly struct SliceAxes
    {
        /// <summary>Axis perpendicular to the face (0=X, 1=Y, 2=Z).</summary>
        public readonly int NormalAxis;

        /// <summary>First tangent axis within the slice. Greedy spans extend along this axis.</summary>
        public readonly int RowAxis;

        /// <summary>Second tangent axis within the slice. Quads extend across this axis.</summary>
        public readonly int ColAxis;

        /// <summary>
        /// Vertex offset along the normal: 1 for positive faces, 0 for negative faces.
        /// The face plane sits at sliceDepth + NormalOffset.
        /// </summary>
        public readonly int NormalOffset;

        /// <summary>Initializes the axis mapping with explicit normal, row, column, and offset values.</summary>
        public SliceAxes(int normalAxis, int rowAxis, int colAxis, int normalOffset)
        {
            NormalAxis = normalAxis;
            RowAxis = rowAxis;
            ColAxis = colAxis;
            NormalOffset = normalOffset;
        }

        /// <summary>Returns the axis mapping for the given face direction index (0-5).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SliceAxes ForFace(int faceIndex)
        {
            return faceIndex switch
            {
                0 => new SliceAxes(1, 2, 0, 1), // PosY: normal=Y, row=Z, col=X, offset=+1
                1 => new SliceAxes(1, 2, 0, 0), // NegY: normal=Y, row=Z, col=X, offset= 0
                2 => new SliceAxes(0, 1, 2, 1), // PosX: normal=X, row=Y, col=Z, offset=+1
                3 => new SliceAxes(0, 1, 2, 0), // NegX: normal=X, row=Y, col=Z, offset= 0
                4 => new SliceAxes(2, 1, 0, 1), // PosZ: normal=Z, row=Y, col=X, offset=+1
                _ => new SliceAxes(2, 1, 0, 0), // NegZ: normal=Z, row=Y, col=X, offset= 0
            };
        }
    }
}
