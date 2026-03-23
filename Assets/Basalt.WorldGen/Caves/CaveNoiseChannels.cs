using System;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Persistent noise buffers for cave generation: two noise intersection fields
    /// (cave1, cave2) and one cavern field. All 3D at 80x82x80.
    /// </summary>
    /// <remarks>
    /// Allocated once with <c>Allocator.Persistent</c> and reused every mapchunk generation.
    /// Owned by <see cref="CaveGenerator"/>.
    ///
    /// Source: <c>luanti/src/mapgen/cavegen.cpp</c> — CavesNoiseIntersection uses cave1 + cave2,
    /// CavernsNoise uses cavern.
    /// </remarks>
    public sealed class CaveNoiseChannels : IDisposable
    {
        /// <summary>First cave noise intersection field (80x82x80).</summary>
        public NoiseBuffer3D Cave1;

        /// <summary>Second cave noise intersection field (80x82x80).</summary>
        public NoiseBuffer3D Cave2;

        /// <summary>Cavern noise field (80x82x80).</summary>
        public NoiseBuffer3D Cavern;

        /// <summary>Lattice metadata for Cave1.</summary>
        public NoiseLatticeMetadata3D MetaCave1;

        /// <summary>Lattice metadata for Cave2.</summary>
        public NoiseLatticeMetadata3D MetaCave2;

        /// <summary>Lattice metadata for Cavern.</summary>
        public NoiseLatticeMetadata3D MetaCavern;

        /// <summary>Gets whether the buffers have been allocated.</summary>
        public bool IsCreated => Cave1.IsCreated;

        /// <summary>
        /// Allocates all persistent noise buffers for the 80x82x80 mapchunk volume.
        /// </summary>
        public CaveNoiseChannels()
        {
            const int sx = MapgenV7Constants.MAPCHUNK_SIZE;   // 80
            const int sy = MapgenV7Constants.VM_HEIGHT;       // 82
            const int sz = MapgenV7Constants.MAPCHUNK_SIZE;   // 80

            // Lattice capacity: (n + 2) per dimension handles any spread >= 1 node/sample
            int cap = (sx + 2) * (sy + 2) * (sz + 2);

            Cave1 = new NoiseBuffer3D(sx, sy, sz, cap, Allocator.Persistent);
            Cave2 = new NoiseBuffer3D(sx, sy, sz, cap, Allocator.Persistent);
            Cavern = new NoiseBuffer3D(sx, sy, sz, cap, Allocator.Persistent);

            MetaCave1 = new NoiseLatticeMetadata3D(Allocator.Persistent);
            MetaCave2 = new NoiseLatticeMetadata3D(Allocator.Persistent);
            MetaCavern = new NoiseLatticeMetadata3D(Allocator.Persistent);
        }

        /// <summary>
        /// Disposes all owned buffers and metadata.
        /// </summary>
        public void Dispose()
        {
            Cave1.Dispose();
            Cave2.Dispose();
            Cavern.Dispose();

            MetaCave1.Dispose();
            MetaCave2.Dispose();
            MetaCavern.Dispose();
        }
    }
}
