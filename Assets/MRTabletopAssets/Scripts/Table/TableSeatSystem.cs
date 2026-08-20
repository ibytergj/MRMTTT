using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine.Events;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    public class TableSeatSystem : MonoBehaviour
    {
        public TableTop tableTop => m_TableTop;

        [SerializeField]
        protected TableTop m_TableTop;

        [SerializeField]
        float m_DefaultSeatHeight = -.5f;

        [SerializeField]
        UnityEvent<int> m_OnSeatChanged;

        [SerializeField]
        NetworkTableTopManager m_NetworkTableTopManager;

        XROrigin m_XROrigin;

        void Awake()
        {
            FindReferences();
        }

        void FindReferences()
        {
            m_XROrigin = FindAnyObjectByType<XROrigin>();

            if (m_NetworkTableTopManager == null)
            {
                m_NetworkTableTopManager = FindAnyObjectByType<NetworkTableTopManager>();
                if (m_NetworkTableTopManager == null)
                {
                    Debug.LogWarning("TableSeatSystem: FindReferences - Could not find NetworkTableTopManager!");
                }
            }
        }

        public void TeleportToSeat(int seatNum)
        {
            // Validate TableTop reference
            if (m_TableTop == null)
            {
                Debug.LogError($"TableSeatSystem: TeleportToSeat - TableTop reference is null! Cannot teleport to seat {seatNum}");
                return;
            }

            // Validate seat number
            if (seatNum >= 0 && seatNum >= m_TableTop.seats.Length)
            {
                Debug.LogError($"TableSeatSystem: TeleportToSeat - Invalid seat number {seatNum}! Max seat index is {m_TableTop.seats.Length - 1}");
                return;
            }

            // Check for spectator seat or initial seat
            if (TableTop.k_CurrentSeat < 0)
            {
                TableTop.k_CurrentSeat = 0;
            }

            int prevSeat = TableTop.k_CurrentSeat;
            TableTop.k_CurrentSeat = seatNum;

            float currentAngle = GetRotationAngleBasedOnSeatNum(prevSeat);
            float newAngle = GetRotationAngleBasedOnSeatNum(seatNum);
            float rotationAmount = newAngle - currentAngle;

            if (m_XROrigin == null)
            {
                FindReferences();

                if (m_XROrigin == null)
                {
                    Debug.LogError("TableSeatSystem: TeleportToSeat - Failed to find XROrigin! Cannot teleport player");
                    return;
                }
            }

            try
            {
                m_XROrigin.transform.RotateAround(transform.position, transform.up, rotationAmount);
                m_OnSeatChanged.Invoke(seatNum);

                transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"TableSeatSystem: TeleportToSeat - Exception during teleportation: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public float GetRotationAngleBasedOnSeatNum(int seatNum)
        {
            if (m_TableTop == null)
            {
                Debug.LogError("TableSeatSystem: GetRotationAngleBasedOnSeatNum - TableTop reference is null!");
                return 0f;
            }

            // Handle spectator seat (-1) or invalid seat
            if (seatNum < 0)
            {
                return 0f;
            }

            // Validate seat index
            if (seatNum >= m_TableTop.seats.Length)
            {
                Debug.LogError($"TableSeatSystem: GetRotationAngleBasedOnSeatNum - Invalid seat number {seatNum}! Max seat index is {m_TableTop.seats.Length - 1}");
                return 0f;
            }

            // Read the actual rotation from the seat transform set in the Inspector
            Transform seatTransform = m_TableTop.seats[seatNum].seatTransform;
            if (seatTransform == null)
            {
                Debug.LogError($"TableSeatSystem: GetRotationAngleBasedOnSeatNum - Seat {seatNum} has null transform!");
                return 0f;
            }

            return seatTransform.localRotation.eulerAngles.y;
        }

        /// <summary>
        /// Gets the seat count (4 or 8) based on the network-synchronized seat configuration.
        /// This represents the table layout, NOT the actual number of players.
        /// Falls back to counting active seat transforms if network data is unavailable.
        /// </summary>
        int GetActivePlayerCount()
        {
            // First try to get the network-synchronized seat count (4 or 8)
            if (m_NetworkTableTopManager != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                // Access the seat count from NetworkTableTopManager (4 or 8 seats for table layout)
                int networkSeatCount = m_NetworkTableTopManager.GetNetworkSynchronizedSeatCount();

                if (networkSeatCount > 0)
                {
                    return networkSeatCount;
                }

                Debug.LogWarning($"TableSeatSystem: GetActivePlayerCount - Network seat count is invalid ({networkSeatCount}), falling back to local count");
            }

            // Fallback: Count active seats locally
            int count = 0;
            for (int i = 0; i < m_TableTop.seats.Length; i++)
            {
                var seat = m_TableTop.seats[i];
                if (seat.seatTransform != null && seat.seatTransform.gameObject != null)
                {
                    if (seat.seatTransform.gameObject.activeSelf)
                        count++;
                }
                else
                {
                    Debug.LogWarning($"TableSeatSystem: GetActivePlayerCount - Seat {i} has null transform or gameObject!");
                }
            }

            return Mathf.Max(2, count); // Ensure at least 2 players
        }

        public void ResetSeatRotation()
        {
            if (m_XROrigin == null)
            {
                FindReferences();
                if (m_XROrigin == null)
                {
                    Debug.LogError("TableSeatSystem: ResetSeatRotation - Failed to find XROrigin!");
                    return;
                }
            }

            // Validate seat index (GetSeat handles -1 by returning seat 0)
            if (TableTop.k_CurrentSeat >= m_TableTop.seats.Length)
            {
                Debug.LogError($"TableSeatSystem: ResetSeatRotation - Invalid current seat: {TableTop.k_CurrentSeat}");
                return;
            }

            Vector3 headForward = new Vector3(m_XROrigin.transform.forward.x, 0, m_XROrigin.transform.forward.z);
            Vector3 seatForward = new Vector3(m_TableTop.GetSeat(TableTop.k_CurrentSeat).forward.x, 0, m_TableTop.GetSeat(TableTop.k_CurrentSeat).forward.z);
            float angle = Vector3.SignedAngle(headForward, seatForward, Vector3.up);

            m_XROrigin.transform.RotateAround(transform.position, transform.up, angle);
        }

        public void ResetToSeatDefault()
        {
            // Validate seat index (GetSeat handles -1 by returning seat 0)
            if (TableTop.k_CurrentSeat >= m_TableTop.seats.Length)
            {
                Debug.LogError($"TableSeatSystem: ResetToSeatDefault - Invalid current seat: {TableTop.k_CurrentSeat}");
                return;
            }

            var seat = m_TableTop.GetSeat(TableTop.k_CurrentSeat);

            var seatPosition = seat.position;

            seatPosition.y -= m_DefaultSeatHeight;

            if (m_XROrigin == null)
            {
                FindReferences();
                if (m_XROrigin == null)
                {
                    Debug.LogError("TableSeatSystem: ResetToSeatDefault - Failed to find XROrigin!");
                    return;
                }
            }

            var targetPosition = seatPosition - seat.forward * m_TableTop.seatOffset;
            var targetRotation = seat.rotation;
            m_XROrigin.transform.SetPositionAndRotation(targetPosition, targetRotation);

            m_TableTop.seatOffset = 0;
        }
    }
}
