namespace Basalt.Meshing
{
    /// <summary>
    /// Enumerates the six cardinal face directions for a voxel cube.
    /// Values 0-5 are stable and used as array indices in meshing tables.
    /// </summary>
    public enum FaceDirection : byte
    {
        /// <summary>Positive Y face (top).</summary>
        PosY = 0,

        /// <summary>Negative Y face (bottom).</summary>
        NegY = 1,

        /// <summary>Positive X face (right).</summary>
        PosX = 2,

        /// <summary>Negative X face (left).</summary>
        NegX = 3,

        /// <summary>Positive Z face (front).</summary>
        PosZ = 4,

        /// <summary>Negative Z face (back).</summary>
        NegZ = 5,
    }
}