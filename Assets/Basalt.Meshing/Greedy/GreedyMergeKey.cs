using System;
using System.Runtime.CompilerServices;

namespace Basalt.Meshing
{
    /// <summary>
    /// Composite merge key for greedy quad expansion, combining content identity and AO pattern.
    /// Two adjacent faces can be merged into one quad only if both fields are equal.
    /// </summary>
    /// <remarks>
    /// Story 2.2 used a bare <c>ushort</c> (content_id only).
    /// Story 2.3 extends with packed AO so that faces with different occlusion gradients
    /// are never incorrectly merged, preventing flat-shaded artifacts on corner occlusions.
    ///
    /// PackedAO layout: bits [2k+1 : 2k] hold ao_k for vertex k (k in 0..3),
    /// matching the vertex ordering in <see cref="GreedyMeshJob.GetQuadVertex"/>.
    /// </remarks>
    public readonly struct GreedyMergeKey : IEquatable<GreedyMergeKey>
    {
        /// <summary>Content type identifier for the node face.</summary>
        public readonly ushort ContentId;

        /// <summary>Four packed 2-bit AO values; see <see cref="AmbientOcclusionUtils.PackAO4"/>.</summary>
        public readonly byte PackedAO;

        /// <summary>Initializes a merge key with content and AO data.</summary>
        public GreedyMergeKey(ushort contentId, byte packedAO)
        {
            ContentId = contentId;
            PackedAO = packedAO;
        }

        /// <summary>Returns true when both content and AO pattern are identical.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(GreedyMergeKey other)
        {
            return ContentId == other.ContentId && PackedAO == other.PackedAO;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is GreedyMergeKey other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (ContentId * 397) ^ PackedAO;
        }
    }
}
