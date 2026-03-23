using System;
using Unity.Collections;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Persistent noise buffers for biome feature generation: heat, humidity, blend,
    /// filler depth, plus combined heatmap/humidmap and biomemap.
    /// </summary>
    /// <remarks>
    /// Allocated once with <c>Allocator.Persistent</c> and reused every mapchunk generation.
    /// Owned by <see cref="MapgenFeatures"/>.
    /// </remarks>
    public sealed class MapgenFeaturesNoiseChannels : IDisposable
    {
        /// <summary>Heat noise buffer (80x80).</summary>
        public NoiseBuffer2D Heat;

        /// <summary>Humidity noise buffer (80x80).</summary>
        public NoiseBuffer2D Humidity;

        /// <summary>Heat blend noise buffer (80x80).</summary>
        public NoiseBuffer2D HeatBlend;

        /// <summary>Humidity blend noise buffer (80x80).</summary>
        public NoiseBuffer2D HumidityBlend;

        /// <summary>Filler depth noise buffer (80x80).</summary>
        public NoiseBuffer2D FillerDepth;

        /// <summary>Lattice metadata for Heat.</summary>
        public NoiseLatticeMetadata2D MetaHeat;

        /// <summary>Lattice metadata for Humidity.</summary>
        public NoiseLatticeMetadata2D MetaHumidity;

        /// <summary>Lattice metadata for HeatBlend.</summary>
        public NoiseLatticeMetadata2D MetaHeatBlend;

        /// <summary>Lattice metadata for HumidityBlend.</summary>
        public NoiseLatticeMetadata2D MetaHumidityBlend;

        /// <summary>Lattice metadata for FillerDepth.</summary>
        public NoiseLatticeMetadata2D MetaFillerDepth;

        /// <summary>Combined heatmap: heat + heat_blend (80x80).</summary>
        public NativeArray<float> Heatmap;

        /// <summary>Combined humidmap: humidity + humidity_blend (80x80).</summary>
        public NativeArray<float> Humidmap;

        /// <summary>Biome index per column (80x80). Filled by GenerateBiomesJob.</summary>
        public NativeArray<ushort> Biomemap;

        public bool IsCreated => Heat.IsCreated;

        public MapgenFeaturesNoiseChannels()
        {
            const int sx = MapgenV7Constants.MAPCHUNK_SIZE; // 80
            const int sy = MapgenV7Constants.MAPCHUNK_SIZE; // 80
            int cap = (sx + 2) * (sy + 2);

            Heat = new NoiseBuffer2D(sx, sy, cap, Allocator.Persistent);
            Humidity = new NoiseBuffer2D(sx, sy, cap, Allocator.Persistent);
            HeatBlend = new NoiseBuffer2D(sx, sy, cap, Allocator.Persistent);
            HumidityBlend = new NoiseBuffer2D(sx, sy, cap, Allocator.Persistent);
            FillerDepth = new NoiseBuffer2D(sx, sy, cap, Allocator.Persistent);

            MetaHeat = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaHumidity = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaHeatBlend = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaHumidityBlend = new NoiseLatticeMetadata2D(Allocator.Persistent);
            MetaFillerDepth = new NoiseLatticeMetadata2D(Allocator.Persistent);

            int mapSize = sx * sy;
            Heatmap = new NativeArray<float>(mapSize, Allocator.Persistent);
            Humidmap = new NativeArray<float>(mapSize, Allocator.Persistent);
            Biomemap = new NativeArray<ushort>(mapSize, Allocator.Persistent);
        }

        public void Dispose()
        {
            Heat.Dispose();
            Humidity.Dispose();
            HeatBlend.Dispose();
            HumidityBlend.Dispose();
            FillerDepth.Dispose();

            MetaHeat.Dispose();
            MetaHumidity.Dispose();
            MetaHeatBlend.Dispose();
            MetaHumidityBlend.Dispose();
            MetaFillerDepth.Dispose();

            if (Heatmap.IsCreated) Heatmap.Dispose();
            if (Humidmap.IsCreated) Humidmap.Dispose();
            if (Biomemap.IsCreated) Biomemap.Dispose();
        }
    }
}
