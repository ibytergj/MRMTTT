namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Places a world-space UI root (Navigation Menu, table manipulation
    /// handles) at the local player's seat: yaw read from the seat transform,
    /// child offsets scaled with the table so the UI keeps its distance from
    /// the table edge across layout changes.
    /// </summary>
    public class SeatBillboard : MonoBehaviour
    {
        TableTop m_TableTop;
        TableSeatSystem m_SeatSystem;
        Vector3[] m_BaseLocalPositions;
        bool m_ReferencesSearched;

        void Awake()
        {
            CacheBasePositions();
        }

        // Awake may not have run yet when the first seat-changed event
        // arrives (this object can start inactive), so callers lazy-init.
        void CacheBasePositions()
        {
            if (m_BaseLocalPositions != null)
                return;

            m_BaseLocalPositions = new Vector3[transform.childCount];
            for (int i = 0; i < m_BaseLocalPositions.Length; i++)
                m_BaseLocalPositions[i] = transform.GetChild(i).localPosition;
        }

        public void RotateBillboard(int seatID)
        {
            transform.localRotation = Quaternion.Euler(0, SeatIDToAngle(seatID), 0);
            ApplyTableScale();
        }

        float SeatIDToAngle(int seatID)
        {
            FindReferences();

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

        void ApplyTableScale()
        {
            CacheBasePositions();
            float scale = m_SeatSystem != null ? m_SeatSystem.tableScale : 1f;
            int count = Mathf.Min(transform.childCount, m_BaseLocalPositions.Length);
            for (int i = 0; i < count; i++)
            {
                var basePosition = m_BaseLocalPositions[i];
                transform.GetChild(i).localPosition = new Vector3(basePosition.x * scale, basePosition.y, basePosition.z * scale);
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
