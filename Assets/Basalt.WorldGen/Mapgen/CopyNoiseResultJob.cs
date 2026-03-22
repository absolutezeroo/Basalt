using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Copies the terrain_persist noise result into both persistence map arrays
    /// (one for terrain_base, one for terrain_alt) in a single Burst pass.
    /// </summary>
    /// <remarks>
    /// Scheduled after terrain_persist completes and before terrain_base and terrain_alt
    /// begin their octave accumulation (which reads from the persistence map).
    ///
    /// Luanti computes terrain_persist once, then passes the result buffer as the
    /// persistence map to both terrain_base and terrain_alt noise computations.
    /// Source: <c>luanti/src/mapgen/mapgen_v7.cpp</c> lines 474-478.
    /// </remarks>
    [BurstCompile]
    public struct CopyNoiseResultJob : IJob
    {
        /// <summary>Source result buffer (terrain_persist.ResultBuf).</summary>
        [ReadOnly]
        public NativeArray<float> Source;

        /// <summary>Destination A — persistence map for terrain_base.</summary>
        [WriteOnly]
        public NativeArray<float> DestA;

        /// <summary>Destination B — persistence map for terrain_alt.</summary>
        [WriteOnly]
        public NativeArray<float> DestB;

        /// <summary>Number of elements (Sx * Sz = 80 * 80 = 6400).</summary>
        public int BufSize;

        public void Execute()
        {
            for (int i = 0; i < BufSize; i++)
            {
                float value = Source[i];
                DestA[i] = value;
                DestB[i] = value;
            }
        }
    }
}
