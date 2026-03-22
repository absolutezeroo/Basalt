using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Zeroes <see cref="ResultBuf"/> and initializes <see cref="GmapBuf"/> before
    /// each octave accumulation pass. Required because noise buffers are persistent
    /// and reused across mapchunk generation calls.
    /// </summary>
    /// <remarks>
    /// When a persistence map is active, GmapBuf must be initialized to 1.0
    /// (each element starts with full gain, then gets multiplied by per-element persistence).
    /// When no persistence map is used, GmapBuf is irrelevant and left unchanged.
    /// </remarks>
    [BurstCompile]
    public struct ClearNoiseBufferJob : IJob
    {
        /// <summary>Accumulated result buffer to zero.</summary>
        public NativeArray<float> ResultBuf;

        /// <summary>Running gain buffer. Set to 1.0 when a persistence map is active.</summary>
        public NativeArray<float> GmapBuf;

        /// <summary>Number of elements to clear (Sx*Sy or Sx*Sy*Sz).</summary>
        public int BufSize;

        /// <summary>Whether a persistence map is active (0=false, 1=true).</summary>
        public byte HasPersistenceMap;

        public void Execute()
        {
            if (HasPersistenceMap != 0)
            {
                for (int i = 0; i < BufSize; i++)
                {
                    ResultBuf[i] = 0.0f;
                    GmapBuf[i] = 1.0f;
                }
            }
            else
            {
                for (int i = 0; i < BufSize; i++)
                {
                    ResultBuf[i] = 0.0f;
                }
            }
        }
    }
}
