namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Places a world-space UI root (Navigation Menu, table manipulation
    /// handles) at the local player's seat. The authored transform is treated
    /// as the seat-0 pose: on every seat change it is rotated around the
    /// table center to the seat's yaw and its planar offset is scaled with
    /// the table, so the UI keeps its authored relationship to the table edge
    /// at any seat and any table size. Child offsets are scaled the same way
    /// for roots that sit at the center and carry their radius in children
    /// (the manipulation handles).
    /// </summary>
    public class SeatBillboard : MonoBehaviour
    {
        TableTop m_TableTop;
        TableSeatSystem m_SeatSystem;
        Vector3 m_BaseLocalPosition;
        Vector3[] m_BaseChildLocalPositions;
        bool m_BaseCached;
        bool m_ReferencesSearched;

        void Awake()
        {
            CacheBasePose();
        }

        // Awake may not have run yet when the first seat-changed event
        // arrives (this object can start inactive), so callers lazy-init.
        void CacheBasePose()
        {
            if (m_BaseCached)
                return;

            m_BaseCached = true;
            m_BaseLocalPosition = transform.localPosition;
            m_BaseChildLocalPositions = new Vector3[transform.childCount];
            for (int i = 0; i < m_BaseChildLocalPositions.Length; i++)
                m_BaseChildLocalPositions[i] = transform.GetChild(i).localPosition;
        }

        public void RotateBillboard(int seatID)
        {
            CacheBasePose();
            FindReferences();

            float yaw = SeatIDToAngle(seatID);
            float scale = m_SeatSystem != null ? m_SeatSystem.tableScale : 1f;
            var seatRotation = Quaternion.Euler(0f, yaw, 0f);

            transform.localRotation = seatRotation;
            transform.localPosition = seatRotation * new Vector3(
                m_BaseLocalPosition.x * scale,
                m_BaseLocalPosition.y,
                m_BaseLocalPosition.z * scale);

            int count = Mathf.Min(transform.childCount, m_BaseChildLocalPositions.Length);
            for (int i = 0; i < count; i++)
            {
                var basePosition = m_BaseChildLocalPositions[i];
                transform.GetChild(i).localPosition = new Vector3(basePosition.x * scale, basePosition.y, basePosition.z * scale);
            }
        }

        float SeatIDToAngle(int seatID)
        {
            if (m_TableTop != null && seatID >= 0 && seatID < m_TableTop.seats.Length)
            {
                var seatTransform = m_TableTop.seats[seatID].seatTransform;
                if (seatTransform != null)
                    return seatTransform.localRotation.eulerAngles.y;
            }

            // No table available: the template's fixed 4-seat angles.
            switch (seatID)
            {
                case 0:
                    return 0;
                case 1:
                    return 180;
                case 2:
                    return 270;
                case 3:
                    return 90;
                default:
                    return 0;
            }
        }

        void FindReferences()
        {
            if (m_ReferencesSearched)
                return;

            m_ReferencesSearched = true;
            m_TableTop = FindAnyObjectByType<TableTop>();
            m_SeatSystem = FindAnyObjectByType<TableSeatSystem>();
        }
    }
}
