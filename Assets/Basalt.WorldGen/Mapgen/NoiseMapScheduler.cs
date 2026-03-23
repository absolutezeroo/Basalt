using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Schedules monolithic noise map jobs for 2D and 3D noise channels.
    /// Each call schedules a single <see cref="NoiseMap2DJob"/> or <see cref="NoiseMap3DJob"/>
    /// that handles the full fractal pipeline internally (lattice fill, interpolation,
    /// octave accumulation, offset/scale).
    /// </summary>
    /// <remarks>
    /// Shared by all mapgen implementations. Extracted from <see cref="MapgenV7"/> to avoid duplication.
    /// </remarks>
    internal static class NoiseMapScheduler
    {
        /// <summary>
        /// Schedules a monolithic 2D noise map job for one noise channel.
        /// </summary>
        /// <remarks>
        /// For 2D terrain noise, Luanti maps XZ world coordinates to the 2D grid:
        /// originX / spread.x and originZ / spread.z.
        /// </remarks>
        internal static JobHandle ScheduleMap2D(
            NoiseBuffer2D buffer,
            in NoiseParams np,
            float originX, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            var job = new NoiseMap2DJob
            {
                Np = np,
                OriginX = originX / np.Spread.x,
                OriginZ = originZ / np.Spread.z,
                MapSeed = mapSeed,
                Sx = buffer.Sx,
                Sy = buffer.Sy,
                NoiseBuf = buffer.NoiseBuf,
                ValueBuf = buffer.ValueBuf,
                ResultBuf = buffer.ResultBuf,
                GmapBuf = buffer.GmapBuf,
                PersistenceMap = buffer.PersistenceMap,
            };

            return job.Schedule(dependency);
        }

        /// <summary>
        /// Schedules a monolithic 3D noise map job for one noise channel.
        /// </summary>
        internal static JobHandle ScheduleMap3D(
            NoiseBuffer3D buffer,
            in NoiseParams np,
            float originX, float originY, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            var job = new NoiseMap3DJob
            {
                Np = np,
                OriginX = originX / np.Spread.x,
                OriginY = originY / np.Spread.y,
                OriginZ = originZ / np.Spread.z,
                MapSeed = mapSeed,
                Sx = buffer.Sx,
                Sy = buffer.Sy,
                Sz = buffer.Sz,
                NoiseBuf = buffer.NoiseBuf,
                ValueBuf = buffer.ValueBuf,
                ResultBuf = buffer.ResultBuf,
                GmapBuf = buffer.GmapBuf,
                PersistenceMap = buffer.PersistenceMap,
            };

            return job.Schedule(dependency);
        }
    }
}
