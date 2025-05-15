using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class TableSeatSystem : MonoBehaviour
{
    public TableTop tableTop => m_TableTop;

    [SerializeField]
    protected TableTop m_TableTop;

    [SerializeField]
    float m_DefaultSeatHeight = -.5f;

    [SerializeField]
    UnityEvent<int> m_OnSeatChanged;

    XROrigin m_XROrigin;

    void Awake()
    {
        FindReferences();
    }

    void FindReferences()
    {
        m_XROrigin = FindFirstObjectByType<XROrigin>();
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
        m_XROrigin.transform.RotateAround(transform.position, transform.up, rotationAmount);
        m_OnSeatChanged.Invoke(seatNum);

        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    float GetRotationAngleBasedOnSeatNum(int seatNum)
    {
        int totalSeats = m_TableTop.seats.Length;
        int activePlayers = GetActivePlayerCount();

        if (activePlayers <= 4)
        {
            // Original 4-player layout with non-sequential numbering
            switch (seatNum)
            {
                case 0: return 0f;      // First position
                case 1: return 180f;    // Opposite position
                case 2: return 270f;    // Left position
                case 3: return 90f;     // Right position
                default: return 0f;
            }
        }
        else
        {
            // Sequential clockwise numbering for 5-8 players
            float anglePerSeat = 360f / activePlayers;
            return seatNum * anglePerSeat;
        }
    }

    /// <summary>
    /// Gets the number of active players based on active seat transforms.
    /// </summary>
    private int GetActivePlayerCount()
    {
        // Count active seats or get from configuration
        int count = 0;
        foreach (var seat in m_TableTop.seats)
        {
            if (seat.seatTransform.gameObject.activeSelf)
                count++;
        }
        return Mathf.Max(2, count); // Ensure at least 2 players
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
