namespace Basalt.Client
{
    /// <summary>
    /// Pipeline phases for a chunk meshing request.
    /// </summary>
    internal enum MeshRequestPhase : byte
    {
        /// <summary>No meshing in progress.</summary>
        Idle = 0,

        /// <summary>NeighborhoodBuildJob is scheduled and running.</summary>
        Neighborhood = 1,

        /// <summary>GreedyCountJob is scheduled and running.</summary>
        Counting = 2,

        /// <summary>Count result is ready; waiting to be batched into a MeshDataArray.</summary>
        CountComplete = 3,

        /// <summary>GreedyWriteJob is scheduled, writing into MeshData buffers.</summary>
        Writing = 4,

        /// <summary>Write complete; ready for ApplyAndDisposeWritableMeshData.</summary>
        ReadyToApply = 5,
    }
}
