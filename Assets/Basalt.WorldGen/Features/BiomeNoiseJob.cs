using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Burst job that combines heat + heat_blend and humidity + humidity_blend
    /// into the final heatmap and humidmap arrays.
    /// </summary>
    /// <remarks>
    /// Matches Luanti's <c>BiomeGenOriginal::calcBiomeNoise()</c>.
    /// Source: <c>luanti/src/mapgen/mg_biome.cpp</c>.
    /// </remarks>
    [BurstCompile]
    public struct BiomeNoiseJob : IJob
    {
        [ReadOnly] public NativeArray<float> HeatResult;
        [ReadOnly] public NativeArray<float> HumidityResult;
        [ReadOnly] public NativeArray<float> HeatBlendResult;
        [ReadOnly] public NativeArray<float> HumidityBlendResult;

        public NativeArray<float> Heatmap;
        public NativeArray<float> Humidmap;

        public int BufSize;

        public void Execute()
        {
            for (int i = 0; i < BufSize; i++)
            {
                Heatmap[i] = HeatResult[i] + HeatBlendResult[i];
                Humidmap[i] = HumidityResult[i] + HumidityBlendResult[i];
            }
        }
    }
}
