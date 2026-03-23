using System;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Persistent noise buffers for Mapgen Flat's single terrain noise channel.
    /// Allocated once with <c>Allocator.Persistent</c> and reused every mapchunk generation.
    /// </summary>
    /// <remarks>
    /// Owns one 2D noise buffer and its lattice metadata.
    /// Follows the same lifetime pattern as <see cref="MapgenV7NoiseChannels"/>.
    /// The terrain channel is only needed when lakes or hills are enabled.
    /// </remarks>
    public sealed class MapgenFlatNoiseChannels : IDisposable
    {
        /// <summary>Terrain noise buffer (80x80).</summary>
        public NoiseBuffer2D Terrain;

        /// <summary>Lattice metadata for the terrain channel.</summary>
        public NoiseLatticeMetadata2D MetaTerrain;

        /// <summary>Gets whether the buffers have been allocated.</summary>
        public bool IsCreated => Terrain.IsCreated;

        /// <summary>
        /// Allocates the persistent terrain noise buffer for the 80x80 mapchunk footprint.
        /// </summary>
        public MapgenFlatNoiseChannels()
        {
            const int sx = MapgenV7Constants.MAPCHUNK_SIZE;
            const int sy = MapgenV7Constants.MAPCHUNK_SIZE;

            // Lattice capacity: (n + 2) per dimension handles any spread >= 1 node/sample
            int cap = (sx + 2) * (sy + 2);

            Terrain = new NoiseBuffer2D(sx, sy, cap, Allocator.Persistent);
            MetaTerrain = new NoiseLatticeMetadata2D(Allocator.Persistent);
        }

        /// <summary>
        /// Disposes all owned buffers and metadata.
        /// </summary>
        public void Dispose()
        {
            Terrain.Dispose();
            MetaTerrain.Dispose();
        }
    }
}
