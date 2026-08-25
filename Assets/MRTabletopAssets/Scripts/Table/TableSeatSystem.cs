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
        [Tooltip("Roots scaled with the table layout: TableTop, Hover Visuals, PassthroughVolume.")]
        Transform[] m_TableScaledRoots;

        /// <summary>The uniform scale last applied to the table roots.</summary>
        public float tableScale { get; private set; } = 1f;

        XROrigin m_XROrigin;

        void Awake()
        {
            FindReferences();
        }

        void FindReferences()
        {
            m_XROrigin = FindAnyObjectByType<XROrigin>();
        }

        public void TeleportToSeat(int seatNum)
        {
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

            // The table system can be inactive at scene load (MR placement
            // flow), so Awake may not have run when the first seat assignment
            // arrives.
            if (m_XROrigin == null)
            {
                FindReferences();
                if (m_XROrigin == null)
                    return;
            }

            m_XROrigin.transform.RotateAround(transform.position, transform.up, rotationAmount);
            m_OnSeatChanged.Invoke(seatNum);

            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Applies a uniform scale to the table roots, keeping the local
        /// player's offset relative to their seat across the change, then
        /// re-raises the seat-changed event so billboards re-place.
        /// Absolute and idempotent.
        /// </summary>
        public void SetTableScale(float scale)
        {
            if (m_TableScaledRoots == null || Mathf.Approximately(tableScale, scale))
                return;

            if (m_XROrigin == null)
                FindReferences();

            var seat = m_TableTop.GetSeat(TableTop.k_CurrentSeat);
            var seatBefore = seat.position;

            // Multiplicative so roots with authored or runtime-animated scales
            // (the PassthroughVolume animates 0 <-> 0.4 for show/hide) keep
            // their own base value.
            float delta = scale / tableScale;
            foreach (var root in m_TableScaledRoots)
            {
                if (root != null)
                    root.localScale *= delta;
            }

            tableScale = scale;

            if (m_XROrigin != null)
                m_XROrigin.transform.position += seat.position - seatBefore;

            m_OnSeatChanged.Invoke(TableTop.k_CurrentSeat);
        }

        float GetRotationAngleBasedOnSeatNum(int seatNum)
        {
            // Spectator seat (-1) faces the same way as seat 0.
            if (seatNum < 0 || seatNum >= m_TableTop.seats.Length)
                return 0f;

            var seatTransform = m_TableTop.seats[seatNum].seatTransform;
            return seatTransform != null ? seatTransform.localRotation.eulerAngles.y : 0f;
        }

        public void ResetSeatRotation()
        {
            Vector3 headForward = new Vector3(m_XROrigin.transform.forward.x, 0, m_XROrigin.transform.forward.z);
            Vector3 seatForward = new Vector3(m_TableTop.GetSeat(TableTop.k_CurrentSeat).forward.x, 0, m_TableTop.GetSeat(TableTop.k_CurrentSeat).forward.z);
            float angle = Vector3.SignedAngle(headForward, seatForward, Vector3.up);

            m_XROrigin.transform.RotateAround(transform.position, transform.up, angle);
        }

        public void ResetToSeatDefault()
        {
            var seat = m_TableTop.GetSeat(TableTop.k_CurrentSeat);

            var seatPosition = seat.position;

            seatPosition.y -= m_DefaultSeatHeight;

            if (m_XROrigin == null)
                FindReferences();

            var targetPosition = seatPosition - seat.forward * m_TableTop.seatOffset;
            var targetRotation = seat.rotation;
            m_XROrigin.transform.SetPositionAndRotation(targetPosition, targetRotation);

            m_TableTop.seatOffset = 0;
        }
    }
}
