using System;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    public class TableTop : MonoBehaviour
    {
        public static int k_CurrentSeat = -1;

        [SerializeField]
        TableSeat[] m_Seats;
        public TableSeat[] seats => m_Seats;

        [SerializeField]
        float m_SeatDistance = 0.75f;
        public float seatDistance => m_SeatDistance;

        [SerializeField]
        float m_SeatOffset;
        public float seatOffset
        {
            get => m_SeatOffset;
            set => m_SeatOffset = value;
        }

        [SerializeField]
        TableLayoutConfig m_LayoutConfig;
        public TableLayoutConfig layoutConfig => m_LayoutConfig;

        /// <summary>
        /// Runtime injection point: NetworkTableTopManager pushes its config
        /// here so both components always agree (the serialized reference is
        /// for editor preview).
        /// </summary>
        public void SetLayoutConfig(TableLayoutConfig config)
        {
            m_LayoutConfig = config;
        }

        /// <summary>Raised after <see cref="SetSeatLayout"/> applies a layout, with the new seat count.</summary>
        public event Action<int> seatLayoutChanged;

        /// <summary>The seat count of the last applied layout.</summary>
        public int currentSeatCount { get; private set; } = 4;

        public Transform GetSeat(int seatIdx)
        {
            if (seatIdx <= -1)
                return m_Seats[0].seatTransform;

            return m_Seats[seatIdx].seatTransform;
        }

        /// <summary>
        /// Applies the layout for <paramref name="seatCount"/> seats: seat
        /// yaws from the config (preset or regular polygon), positions derived
        /// from yaw and distance, first <paramref name="seatCount"/> seats
        /// active, rim shape sides updated. Absolute and idempotent.
        /// </summary>
        public void SetSeatLayout(int seatCount)
        {
            float[] yaws = m_LayoutConfig != null ? m_LayoutConfig.YawsFor(seatCount) : RegularPolygonYaws(seatCount);

            for (int i = 0; i < m_Seats.Length; i++)
            {
                var seatTransform = m_Seats[i].seatTransform;
                if (seatTransform == null)
                    continue;

                bool isActive = i < seatCount;
                seatTransform.gameObject.SetActive(isActive);
                if (isActive)
                {
                    float yaw = yaws[i];
                    seatTransform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    seatTransform.localPosition = SeatGeometry.SeatLocalPosition(yaw, DistanceFor(m_Seats[i]));
                }
            }

            currentSeatCount = seatCount;

            foreach (var updater in GetComponentsInChildren<SeatColorRimUpdater>(true))
                updater.UpdatePlayerCount(seatCount);

            seatLayoutChanged?.Invoke(seatCount);
        }

        float DistanceFor(TableSeat seat)
        {
            return seat.seatDistanceOverride > 0f ? seat.seatDistanceOverride : m_SeatDistance;
        }

        static float[] RegularPolygonYaws(int seatCount)
        {
            var yaws = new float[seatCount];
            for (int i = 0; i < seatCount; i++)
                yaws[i] = i * 360f / seatCount;
            return yaws;
        }

        void OnValidate()
        {
            foreach (TableSeat seat in m_Seats)
            {
                if (seat.seatTransform != null)
                    seat.seatTransform.localPosition = -seat.seatTransform.forward * DistanceFor(seat);
            }
        }
    }

    [Serializable]
    public struct TableSeat
    {
        public Transform seatTransform;
        public int seatID;

        [Tooltip("Seat distance from the table center. 0 = use the table's seat distance.")]
        public float seatDistanceOverride;
    }
}
