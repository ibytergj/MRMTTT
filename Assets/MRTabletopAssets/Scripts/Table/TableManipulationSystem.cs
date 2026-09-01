using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class TableManipulator : MonoBehaviour
    {
        [SerializeField]
        protected GameObject m_TableVisualsObject;

        [SerializeField]
        protected TableTop m_TableTop;
        protected XROrigin m_XROrigin;
        protected TeleportationProvider m_TeleportationProvider;
        protected Transform m_Head;

        protected XRGrabInteractable m_GrabInteractable;

        protected TableSeatSystem m_TableSeatSystem;

        Rigidbody m_Rigidbody;

        Vector3 m_InitialTableLocalPos;
        Vector3[] m_BaseChildLocalPositions;
        Vector3 m_BaseVisualScale;

        void Awake()
        {
            m_InitialTableLocalPos = transform.localPosition;
            m_BaseChildLocalPositions = new Vector3[transform.childCount];
            for (int i = 0; i < m_BaseChildLocalPositions.Length; i++)
                m_BaseChildLocalPositions[i] = transform.GetChild(i).localPosition;
            m_BaseVisualScale = m_TableVisualsObject.transform.localScale;
        }

        protected virtual void Start()
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_TableSeatSystem = GetComponentInParent<TableSeatSystem>();
            m_Rigidbody = GetComponent<Rigidbody>();

            m_XROrigin = FindAnyObjectByType<XROrigin>();
            m_Head = m_XROrigin.Camera.transform;

            m_TeleportationProvider = m_XROrigin.GetComponentInChildren<TeleportationProvider>();
            m_TableVisualsObject.SetActive(false);

            m_GrabInteractable.firstSelectEntered.AddListener(StartSelection);
            m_GrabInteractable.lastSelectExited.AddListener(EndSelection);

            if (m_TableSeatSystem != null)
            {
                m_TableSeatSystem.tableScaleChanged += ApplyTableScale;
                ApplyTableScale(m_TableSeatSystem.tableScale);
            }
        }

        void OnDestroy()
        {
            if (m_TableSeatSystem != null)
                m_TableSeatSystem.tableScaleChanged -= ApplyTableScale;

            if (m_GrabInteractable == null)
                return;
            m_GrabInteractable.firstSelectEntered.RemoveListener(StartSelection);
            m_GrabInteractable.lastSelectExited.RemoveListener(EndSelection);
        }

        /// <summary>
        /// Keeps the manipulator matched to the table size. Child offsets
        /// carry the handle radius (Handles on the rotation manipulator, the
        /// move visual's offset back to the table center on the free-move
        /// one): their radial (z) offset scales with the table, like
        /// SeatBillboard children. The move visual also shows the table
        /// footprint, so its scale follows the table's uniform scale.
        /// </summary>
        void ApplyTableScale(float scale)
        {
            int count = Mathf.Min(transform.childCount, m_BaseChildLocalPositions.Length);
            for (int i = 0; i < count; i++)
            {
                var basePosition = m_BaseChildLocalPositions[i];
                transform.GetChild(i).localPosition = new Vector3(basePosition.x, basePosition.y, basePosition.z * scale);
            }

            m_TableVisualsObject.transform.localScale = m_BaseVisualScale * scale;
        }

        Matrix4x4 m_InitialTableTransform;
        Matrix4x4 m_InitialPlayerTransform;

        public void StartSelection(SelectEnterEventArgs args)
        {
            m_InitialTableTransform = transform.localToWorldMatrix;
            m_InitialPlayerTransform = m_XROrigin.transform.localToWorldMatrix;
            m_TableVisualsObject.SetActive(true);
        }

        public void EndSelection(SelectExitEventArgs args)
        {
            if (m_GrabInteractable.isSelected)
                return;

            m_TableVisualsObject.SetActive(false);

            _ = MovePlayer();
        }

        public async Awaitable MovePlayer()
        {
            // Wait for the next fixed update for the table to be updated
            await Awaitable.FixedUpdateAsync();

            // Compute the final table transform
            Matrix4x4 finalTableTransform = transform.localToWorldMatrix;

            // Compute the table's transform delta
            Matrix4x4 tableTransformDelta = finalTableTransform * m_InitialTableTransform.inverse;

            // Compute the inverse of the table's transform delta
            Matrix4x4 inverseTableTransformDelta = tableTransformDelta.inverse;

            // Apply the inverse of the table's transform delta to the player's transform
            Matrix4x4 newPlayerTransform = inverseTableTransformDelta * m_InitialPlayerTransform;

            // Update seat offset if needed
            UpdateSeatOffset();

            // Reset the table's position and rotation. The authored local
            // position is a table-edge radius, so it scales with the table
            // (the free-move manipulator sits at the edge; identity for the
            // rotation manipulator, which sits at the center).
            var resetPosition = m_InitialTableLocalPos;
            resetPosition.z *= m_TableSeatSystem != null ? m_TableSeatSystem.tableScale : 1f;
            transform.localPosition = resetPosition;
            transform.localRotation = Quaternion.identity;

            // Move rigitbody to match the transform
            m_Rigidbody.MovePosition(transform.position);
            m_Rigidbody.MoveRotation(transform.rotation);

            // Update the player's position and rotation
            m_XROrigin.transform.position = newPlayerTransform.GetColumn(3);
            m_XROrigin.transform.rotation = Quaternion.LookRotation(
                newPlayerTransform.GetColumn(2),
                newPlayerTransform.GetColumn(1)
            );
        }

        void UpdateSeatOffset()
        {
            // Get the current seat's forward direction
            Vector3 seatForward = m_TableTop.GetSeat(TableTop.k_CurrentSeat).forward;

            // Calculate the vector from the table center to the head
            Vector3 tableToHead = m_Head.position - transform.position;

            // Calculate the new seat offset
            float newSeatOffset = Vector3.Project(-tableToHead, seatForward).magnitude - m_TableTop.seatDistance;

            // Update the table top's seat offset
            m_TableTop.seatOffset = newSeatOffset;
        }
    }
}
