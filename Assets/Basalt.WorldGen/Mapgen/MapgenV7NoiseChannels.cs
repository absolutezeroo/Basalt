using System;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Persistent noise buffers for all Mapgen V7 noise channels.
    /// Allocated once with <c>Allocator.Persistent</c> and reused every mapchunk generation.
    /// </summary>
    /// <remarks>
    /// Owns:
    ///   2D buffers — TerrainBase, TerrainAlt, TerrainPersist, HeightSelect, MountHeight, RidgeUwater.
    ///   3D buffers — Mountain, Ridge.
    ///   Persistence map arrays — PersistBase, PersistAlt (80x80 floats wired into TerrainBase/TerrainAlt).
    ///   Lattice metadata — one set of single-element NativeArrays per buffer.
    ///
    /// PersistenceMap is owned here and referenced (not owned) by TerrainBase and TerrainAlt buffers,
    /// matching the NoiseBuffer2D contract: buffers do NOT dispose their PersistenceMap.
    /// </remarks>
    public sealed class MapgenV7NoiseChannels : IDisposable
    {
        /// <summary>terrain_base noise buffer (80x80, with persistence map).</summary>
        public NoiseBuffer2D TerrainBase;

        /// <summary>terrain_alt noise buffer (80x80, with persistence map).</summary>
        public NoiseBuffer2D TerrainAlt;

        /// <summary>terrain_persist noise buffer (80x80, provides the persistence map data).</summary>
        public NoiseBuffer2D TerrainPersist;

        /// <summary>height_select noise buffer (80x80).</summary>
        public NoiseBuffer2D HeightSelect;

        /// <summary>mount_height noise buffer (80x80).</summary>
        public NoiseBuffer2D MountHeight;

        /// <summary>ridge_uwater noise buffer (80x80).</summary>
        public NoiseBuffer2D RidgeUwater;

        /// <summary>mountain 3D noise buffer (80x82x80).</summary>
        public NoiseBuffer3D Mountain;

        /// <summary>ridge 3D noise buffer (80x82x80).</summary>
        public NoiseBuffer3D Ridge;

        /// <summary>
        /// Persistence map for TerrainBase, filled from TerrainPersist.ResultBuf.
        /// Size: 80x80. Owned here, referenced by TerrainBase.PersistenceMap.
        /// </summary>
        public NativeArray<float> PersistBase;

        /// <summary>
        /// Persistence map for TerrainAlt, filled from TerrainPersist.ResultBuf.
        /// Size: 80x80. Owned here, referenced by TerrainAlt.PersistenceMap.
        /// </summary>
        public NativeArray<float> PersistAlt;

        /// <summary>Lattice metadata for TerrainBase.</summary>
        public NoiseLatticeMetadata2D MetaTerrainBase;

        /// <summary>Lattice metadata for TerrainAlt.</summary>
        public NoiseLatticeMetadata2D MetaTerrainAlt;

        /// <summary>Lattice metadata for TerrainPersist.</summary>
        public NoiseLatticeMetadata2D MetaTerrainPersist;

        /// <summary>Lattice metadata for HeightSelect.</summary>
        public NoiseLatticeMetadata2D MetaHeightSelect;

        /// <summary>Lattice metadata for MountHeight.</summary>
        public NoiseLatticeMetadata2D MetaMountHeight;

        /// <summary>Lattice metadata for RidgeUwater.</summary>
        public NoiseLatticeMetadata2D MetaRidgeUwater;

        /// <summary>Lattice metadata for Mountain (3D).</summary>
        public NoiseLatticeMetadata3D MetaMountain;

        /// <summary>Lattice metadata for Ridge (3D).</summary>
        public NoiseLatticeMetadata3D MetaRidge;

        /// <summary>Gets whether the buffers have been allocated.</summary>
        public bool IsCreated => TerrainBase.IsCreated;

        /// <summary>
        /// Allocates all persistent noise buffers for the 80x82x80 mapchunk volume.
        /// </summary>
        public MapgenV7NoiseChannels()
        {
            const int sx2d = MapgenV7Constants.MAPCHUNK_SIZE;
            const int sy2d = MapgenV7Constants.MAPCHUNK_SIZE;
            const int sx3d = MapgenV7Constants.MAPCHUNK_SIZE;
            const int sy3d = MapgenV7Constants.VM_HEIGHT;
            const int sz3d = MapgenV7Constants.MAPCHUNK_SIZE;

            // Lattice capacity: (n + 2) per dimension handles any spread >= 1 node/sample
            int cap2d = (sx2d + 2) * (sy2d + 2);
            int cap3d = (sx3d + 2) * (sy3d + 2) * (sz3d + 2);

            TerrainBase = new NoiseBuffer2D(sx2d, sy2d, cap2d, Allocator.Persistent);
            TerrainAlt = new NoiseBuffer2D(sx2d, sy2d, cap2d, Allocator.Persistent);
            TerrainPersist = new NoiseBuffer2D(sx2d, sy2d, cap2d, Allocator.Persistent);
            HeightSelect = new NoiseBuffer2D(sx2d, sy2d, cap2d, Allocator.Persistent);
            MountHeight = new NoiseBuffer2D(sx2d, sy2d, cap2d, Allocator.Persistent);
            RidgeUwater = new NoiseBuffer2D(sx2d, sy2d, cap2d, Allocator.Persistent);
            Mountain = new NoiseBuffer3D(sx3d, sy3d, sz3d, cap3d, Allocator.Persistent);
            Ridge = new NoiseBuffer3D(sx3d, sy3d, sz3d, cap3d, Allocator.Persistent);

            PersistBase = new NativeArray<float>(sx2d * sy2d, Allocator.Persistent);
            PersistAlt = new NativeArray<float>(sx2d * sy2d, Allocator.Persistent);

            // Wire the persistence maps into the buffers (externally owned reference)
            TerrainBase.PersistenceMap = PersistBase;
            TerrainAlt.PersistenceMap = PersistAlt;

            MetaTerrainBase = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaTerrainAlt = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaTerrainPersist = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaHeightSelect = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaMountHeight = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaRidgeUwater = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaMountain = new NoiseLatticeMetadata3D(Allocator.Persistent);
            MetaRidge = new NoiseLatticeMetadata3D(Allocator.Persistent);
        }

        /// <summary>
        /// Disposes all owned buffers and metadata.
        /// </summary>
        public void Dispose()
        {
            TerrainBase.Dispose();
            TerrainAlt.Dispose();
            TerrainPersist.Dispose();
            HeightSelect.Dispose();
            MountHeight.Dispose();
            RidgeUwater.Dispose();
            Mountain.Dispose();
            Ridge.Dispose();

            if (PersistBase.IsCreated)
            {
                PersistBase.Dispose();
            }

            if (PersistAlt.IsCreated)
            {
                PersistAlt.Dispose();
            }

            MetaTerrainBase.Dispose();
            MetaTerrainAlt.Dispose();
            MetaTerrainPersist.Dispose();
            MetaHeightSelect.Dispose();
            MetaMountHeight.Dispose();
            MetaRidgeUwater.Dispose();
            MetaMountain.Dispose();
            MetaRidge.Dispose();
        }
    }
}
