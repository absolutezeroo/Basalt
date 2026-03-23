using Unity.Jobs;

namespace Basalt.WorldGen
{
    /// <summary>
    /// Schedules the three-phase fractal noise pipeline for 2D and 3D noise channels.
    /// </summary>
    /// <remarks>
    /// Shared by all mapgen implementations. Extracted from <see cref="MapgenV7"/> to avoid duplication.
    ///
    /// Phase 1: <see cref="NoiseFillLatticeJob"/> / <see cref="NoiseFillLatticeJob3D"/>
    ///          — fills integer-corner hash lattice.
    /// Phase 2: <see cref="NoiseInterpolateRowsJob2D"/> / <see cref="NoiseInterpolatePlanesJob3D"/>
    ///          — bilinear/trilinear interpolation (parallel over rows/planes).
    /// Phase 3: <see cref="NoiseAccumulateJob"/> — accumulates octave into ResultBuf.
    /// </remarks>
    internal static class NoiseMapScheduler
    {
        /// <summary>
        /// Schedules the full three-phase 2D noise pipeline (fill lattice, interpolate rows,
        /// accumulate octaves) for one noise channel.
        /// </summary>
        /// <remarks>
        /// For 2D terrain noise, Luanti maps XZ world coordinates to the 2D grid:
        /// NoiseFillLatticeJob.X = originX / spread.x, NoiseFillLatticeJob.Y = originZ / spread.z.
        /// </remarks>
        internal static JobHandle ScheduleMap2D(
            NoiseBuffer2D buffer,
            NoiseLatticeMetadata2D meta,
            in NoiseParams np,
            float originX, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            bool hasPersistMap = buffer.PersistenceMap.IsCreated && buffer.PersistenceMap.Length > 0;

            var clearJob = new ClearNoiseBufferJob
            {
                ResultBuf = buffer.ResultBuf,
                GmapBuf = buffer.GmapBuf,
                BufSize = buffer.Sx * buffer.Sy,
                HasPersistenceMap = (byte)(hasPersistMap ? 1 : 0),
            };
            JobHandle clearHandle = clearJob.Schedule(dependency);

            float f = 1.0f;
            float g = 1.0f;
            JobHandle octaveHandle = clearHandle;

            for (int octave = 0; octave < np.Octaves; octave++)
            {
                int octaveSeed = mapSeed + np.Seed + octave;
                bool isLast = octave == np.Octaves - 1;

                // Phase 1: Fill integer-corner hash lattice
                var fillJob = new NoiseFillLatticeJob
                {
                    X = originX / np.Spread.x,
                    Y = originZ / np.Spread.z,
                    F = f,
                    SpreadX = np.Spread.x,
                    SpreadY = np.Spread.z,
                    Seed = octaveSeed,
                    Sx = buffer.Sx,
                    Sy = buffer.Sy,
                    NoiseBuf = buffer.NoiseBuf,
                    OutNlx = meta.Nlx,
                    OutNly = meta.Nly,
                    OutOrigU = meta.OrigU,
                    OutOrigV = meta.OrigV,
                    OutStepX = meta.StepX,
                    OutStepY = meta.StepY,
                };
                JobHandle fillHandle = fillJob.Schedule(octaveHandle);

                // Phase 2: Interpolate rows in parallel
                var interpJob = new NoiseInterpolateRowsJob2D
                {
                    NoiseBuf = buffer.NoiseBuf,
                    ValueBuf = buffer.ValueBuf,
                    Nlx = meta.Nlx,
                    OrigU = meta.OrigU,
                    OrigV = meta.OrigV,
                    StepX = meta.StepX,
                    StepY = meta.StepY,
                    Sx = buffer.Sx,
                    Eased = (byte)(np.IsEased2D ? 1 : 0),
                };
                JobHandle interpHandle = interpJob.Schedule(buffer.Sy, 1, fillHandle);

                // Phase 3: Accumulate octave into result buffer
                var accumulateJob = new NoiseAccumulateJob
                {
                    ValueBuf = buffer.ValueBuf,
                    ResultBuf = buffer.ResultBuf,
                    GmapBuf = buffer.GmapBuf,
                    PersistenceMap = hasPersistMap ? buffer.PersistenceMap : default,
                    G = g,
                    BufSize = buffer.Sx * buffer.Sy,
                    AbsValue = (byte)(np.IsAbsValue ? 1 : 0),
                    HasPersistence = (byte)(hasPersistMap ? 1 : 0),
                    IsLastOctave = (byte)(isLast ? 1 : 0),
                    Offset = np.Offset,
                    Scale = np.Scale,
                };
                octaveHandle = accumulateJob.Schedule(interpHandle);

                f *= np.Lacunarity;
                g *= np.Persist;
            }

            return octaveHandle;
        }

        /// <summary>
        /// Schedules the full three-phase 3D noise pipeline for one noise channel.
        /// </summary>
        internal static JobHandle ScheduleMap3D(
            NoiseBuffer3D buffer,
            NoiseLatticeMetadata3D meta,
            in NoiseParams np,
            float originX, float originY, float originZ,
            int mapSeed,
            JobHandle dependency)
        {
            var clearJob = new ClearNoiseBufferJob
            {
                ResultBuf = buffer.ResultBuf,
                GmapBuf = buffer.GmapBuf,
                BufSize = buffer.Sx * buffer.Sy * buffer.Sz,
                HasPersistenceMap = 0,
            };
            JobHandle clearHandle = clearJob.Schedule(dependency);

            float f = 1.0f;
            float g = 1.0f;
            JobHandle octaveHandle = clearHandle;

            for (int octave = 0; octave < np.Octaves; octave++)
            {
                int octaveSeed = mapSeed + np.Seed + octave;
                bool isLast = octave == np.Octaves - 1;

                // Phase 1: Fill 3D integer-corner hash lattice
                var fillJob = new NoiseFillLatticeJob3D
                {
                    X = originX / np.Spread.x,
                    Y = originY / np.Spread.y,
                    Z = originZ / np.Spread.z,
                    F = f,
                    SpreadX = np.Spread.x,
                    SpreadY = np.Spread.y,
                    SpreadZ = np.Spread.z,
                    Seed = octaveSeed,
                    Sx = buffer.Sx,
                    Sy = buffer.Sy,
                    Sz = buffer.Sz,
                    NoiseBuf = buffer.NoiseBuf,
                    OutNlx = meta.Nlx,
                    OutNly = meta.Nly,
                    OutNlz = meta.Nlz,
                    OutOrigU = meta.OrigU,
                    OutOrigV = meta.OrigV,
                    OutOrigW = meta.OrigW,
                    OutStepX = meta.StepX,
                    OutStepY = meta.StepY,
                    OutStepZ = meta.StepZ,
                };
                JobHandle fillHandle = fillJob.Schedule(octaveHandle);

                // Phase 2: Interpolate planes in parallel
                var interpJob = new NoiseInterpolatePlanesJob3D
                {
                    NoiseBuf = buffer.NoiseBuf,
                    ValueBuf = buffer.ValueBuf,
                    Nlx = meta.Nlx,
                    Nly = meta.Nly,
                    OrigU = meta.OrigU,
                    OrigV = meta.OrigV,
                    OrigW = meta.OrigW,
                    StepX = meta.StepX,
                    StepY = meta.StepY,
                    StepZ = meta.StepZ,
                    Sx = buffer.Sx,
                    Sy = buffer.Sy,
                    Eased = (byte)(np.IsEased3D ? 1 : 0),
                };
                JobHandle interpHandle = interpJob.Schedule(buffer.Sz, 1, fillHandle);

                // Phase 3: Accumulate octave
                var accumulateJob = new NoiseAccumulateJob
                {
                    ValueBuf = buffer.ValueBuf,
                    ResultBuf = buffer.ResultBuf,
                    GmapBuf = buffer.GmapBuf,
                    PersistenceMap = default,
                    G = g,
                    BufSize = buffer.Sx * buffer.Sy * buffer.Sz,
                    AbsValue = (byte)(np.IsAbsValue ? 1 : 0),
                    HasPersistence = 0,
                    IsLastOctave = (byte)(isLast ? 1 : 0),
                    Offset = np.Offset,
                    Scale = np.Scale,
                };
                octaveHandle = accumulateJob.Schedule(interpHandle);

                f *= np.Lacunarity;
                g *= np.Persist;
            }

            return octaveHandle;
        }
    }
}
