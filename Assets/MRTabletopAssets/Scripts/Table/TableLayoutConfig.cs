using System;
using System.Collections.Generic;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>How the table behaves when a seat is vacated.</summary>
    public enum CollapsePolicy
    {
        /// <summary>The table never shrinks mid-session; a vacated seat stays open for the next joiner.</summary>
        Never,

        /// <summary>Shrink only when every remaining player already sits below the smaller count (nobody has to move).</summary>
        WhenRemainingFit,

        /// <summary>Shrink on every leave; the server first moves displaced players to the lowest free seats.</summary>
        Immediate,
    }

    /// <summary>Authored seat yaws (and optional scale pin) for one seat count.</summary>
    [Serializable]
    public struct SeatLayoutPreset
    {
        public int seatCount;

        [Tooltip("Seat yaw in degrees per seat index, clockwise viewed from above.")]
        public float[] yaws;

        [Tooltip("Table scale for this count. 0 = derive from the constant-seat-spacing formula.")]
        public float scale;
    }

    /// <summary>
    /// The per-game table layout contract: which seat counts a game supports,
    /// how the table collapses when players leave, and the seat geometry per
    /// count. The template default asset supports {4} only, preserving stock
    /// behavior; games swap in their own asset to change behavior without
    /// touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "TableLayoutConfig", menuName = "MRTTT/Table Layout Config")]
    public class TableLayoutConfig : ScriptableObject
    {
        public const int k_MinSeatCount = 3;
        public const int k_MaxSeatCount = 8;

        [SerializeField]
        [Tooltip("Seat counts the game supports, sorted ascending, each 3-8.")]
        int[] m_SupportedSeatCounts = { 4 };

        [SerializeField]
        CollapsePolicy m_CollapsePolicy = CollapsePolicy.Never;

        [SerializeField]
        List<SeatLayoutPreset> m_Presets = new List<SeatLayoutPreset>();

        [SerializeField]
        float m_BaseRadius = 0.75f;

        public IReadOnlyList<int> supportedSeatCounts => m_SupportedSeatCounts;
        public CollapsePolicy collapsePolicy => m_CollapsePolicy;
        public float baseRadius => m_BaseRadius;

        /// <summary>Smallest supported seat count.</summary>
        public int minimumSeatCount => m_SupportedSeatCounts.Length > 0 ? m_SupportedSeatCounts[0] : k_MinSeatCount;

        /// <summary>
        /// Table scale for <paramref name="seatCount"/> relative to the 4-seat
        /// table: a preset pin wins, otherwise the constant-seat-spacing
        /// formula sin(π/4)/sin(π/n).
        /// </summary>
        public float ScaleFor(int seatCount)
        {
            if (TryGetPreset(seatCount, out var preset) && preset.scale > 0f)
                return preset.scale;

            return Mathf.Sin(Mathf.PI / 4f) / Mathf.Sin(Mathf.PI / seatCount);
        }

        /// <summary>
        /// Seat yaws (degrees, clockwise) for <paramref name="seatCount"/>:
        /// the matching preset if one is authored, else a regular polygon at
        /// i·360/n.
        /// </summary>
        public float[] YawsFor(int seatCount)
        {
            if (TryGetPreset(seatCount, out var preset) && preset.yaws != null && preset.yaws.Length == seatCount)
                return (float[])preset.yaws.Clone();

            var yaws = new float[seatCount];
            for (int i = 0; i < seatCount; i++)
                yaws[i] = i * 360f / seatCount;
            return yaws;
        }

        /// <summary>
        /// Smallest supported seat count with room for
        /// <paramref name="occupancy"/> players, or -1 when no supported count
        /// fits (the joiner is refused a seat / spectates).
        /// </summary>
        public int CountForOccupancy(int occupancy)
        {
            foreach (var count in m_SupportedSeatCounts)
            {
                if (count >= occupancy)
                    return count;
            }

            return -1;
        }

        /// <summary>
        /// The seat count the layout should adopt given which seats are
        /// occupied (<paramref name="occupiedMask"/>, bit i = seat i) and the
        /// <paramref name="current"/> count. Growth to fit the highest
        /// occupied seat is always immediate; shrinking follows the collapse
        /// policy. Pure — no scene or network state.
        /// </summary>
        public int TargetSeatCount(int occupiedMask, int current)
        {
            int occupancy = CountBits(occupiedMask);
            int highestOccupiedPlusOne = HighestBitPlusOne(occupiedMask);

            // Smallest supported count every occupied seat fits in without moving.
            int fitInPlace = CountForOccupancy(highestOccupiedPlusOne);
            // Smallest supported count with enough seats after re-seating.
            int fitByCount = CountForOccupancy(occupancy);

            if (fitInPlace < 0)
                return current; // Misconfigured mask; never propose an unsupported layout.

            // Growth is always immediate.
            if (fitInPlace > current)
                return fitInPlace;

            switch (m_CollapsePolicy)
            {
                case CollapsePolicy.Never:
                    return Mathf.Max(current, fitInPlace);
                case CollapsePolicy.WhenRemainingFit:
                    return fitInPlace;
                case CollapsePolicy.Immediate:
                    return fitByCount > 0 ? fitByCount : fitInPlace;
                default:
                    return current;
            }
        }

        bool TryGetPreset(int seatCount, out SeatLayoutPreset preset)
        {
            foreach (var candidate in m_Presets)
            {
                if (candidate.seatCount == seatCount)
                {
                    preset = candidate;
                    return true;
                }
            }

            preset = default;
            return false;
        }

        static int CountBits(int mask)
        {
            int count = 0;
            while (mask != 0)
            {
                mask &= mask - 1;
                count++;
            }

            return count;
        }

        static int HighestBitPlusOne(int mask)
        {
            int highest = 0;
            for (int i = 0; mask != 0; i++, mask >>= 1)
            {
                if ((mask & 1) != 0)
                    highest = i + 1;
            }

            return highest;
        }

        void OnValidate()
        {
            if (m_SupportedSeatCounts == null || m_SupportedSeatCounts.Length == 0)
                m_SupportedSeatCounts = new[] { 4 };

            for (int i = 0; i < m_SupportedSeatCounts.Length; i++)
                m_SupportedSeatCounts[i] = Mathf.Clamp(m_SupportedSeatCounts[i], k_MinSeatCount, k_MaxSeatCount);
            Array.Sort(m_SupportedSeatCounts);
        }
    }
}
