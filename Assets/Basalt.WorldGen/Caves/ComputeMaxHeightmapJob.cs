using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Reduces the heightmap (80x80 shorts) to a single maximum Y value.
    /// Used to provide a tight <c>stoneSurfaceMaxY</c> bound for cave generation,
    /// avoiding unnecessary cave carving above the actual stone surface.
    /// </summary>
    [BurstCompile]
    public struct ComputeMaxHeightmapJob : IJob
    {
        /// <summary>Surface heightmap from terrain generation (80x80).</summary>
        [ReadOnly] public NativeArray<short> Heightmap;

        /// <summary>Output: single-element array receiving the maximum heightmap value.</summary>
        [WriteOnly] public NativeArray<int> MaxY;

        public void Execute()
        {
            int max = int.MinValue;

            for (int i = 0; i < Heightmap.Length; i++)
            {
                int h = Heightmap[i];
                if (h > max)
                {
                    max = h;
                }
            }

            MaxY[0] = max;
        }
    }
}
