using System;
using System.Runtime.CompilerServices;

namespace Basalt.Core
{
    /// <summary>
    /// Lightweight handle referencing a chunk slot in a <see cref="ChunkPool"/>.
    /// Uses a generation counter to detect stale handles after a slot is recycled.
    /// </summary>
    public readonly struct ChunkHandle : IEquatable<ChunkHandle>
    {
        /// <summary>Slot index in the pool's flat array.</summary>
        public readonly int Index;

        /// <summary>Generation counter at the time this handle was issued.</summary>
        public readonly int Generation;

        public ChunkHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        /// <summary>Sentinel value representing an invalid or unassigned handle.</summary>
        public static readonly ChunkHandle Invalid = new(-1, 0);

        /// <summary>Gets whether this handle points to a valid slot (index >= 0).</summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Index >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ChunkHandle other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Generation);
        }

        public static bool operator ==(ChunkHandle left, ChunkHandle right) => left.Equals(right);

        public static bool operator !=(ChunkHandle left, ChunkHandle right) => !left.Equals(right);
    }
}