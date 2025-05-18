using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class TableSeatSystem : MonoBehaviour
{
    // Debug prefix for logging
    private const string DEBUG_TAG = "[TableSeatSystem] ";

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
        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Seat number: {seatNum}, Current seat: {TableTop.k_CurrentSeat}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        // Check for spectator seat or initial seat
        if (TableTop.k_CurrentSeat < 0)
        {
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - Current seat is negative ({TableTop.k_CurrentSeat}), setting to 0");
            TableTop.k_CurrentSeat = 0;
        }

        int prevSeat = TableTop.k_CurrentSeat;
        TableTop.k_CurrentSeat = seatNum;
        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Previous seat: {prevSeat}, New seat: {seatNum}");

        float currentAngle = GetRotationAngleBasedOnSeatNum(prevSeat);
        float newAngle = GetRotationAngleBasedOnSeatNum(seatNum);
        float rotationAmount = newAngle - currentAngle;

        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Current angle: {currentAngle}, New angle: {newAngle}, Rotation amount: {rotationAmount}");

        if (m_XROrigin != null)
        {
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - XROrigin before rotation: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");
            m_XROrigin.transform.RotateAround(transform.position, transform.up, rotationAmount);
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - XROrigin after rotation: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");
        }
        else
        {
            Debug.LogError($"{DEBUG_TAG}TeleportToSeat - XROrigin is null!");
        }

        m_OnSeatChanged.Invoke(seatNum);
        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Invoked OnSeatChanged event with seat number: {seatNum}");

        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Setting transform position and rotation to zero/identity");
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    float GetRotationAngleBasedOnSeatNum(int seatNum)
    {
        Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Seat number: {seatNum}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        int totalSeats = m_TableTop.seats.Length;
        int activePlayers = GetActivePlayerCount();
        Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Total seats: {totalSeats}, Active players: {activePlayers}");

        float result;
        if (activePlayers <= 4)
        {
            // Original 4-player layout with non-sequential numbering
            switch (seatNum)
            {
                case 0: result = 0f; Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 0 -> 0°"); break;
                case 1: result = 180f; Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 1 -> 180°"); break;
                case 2: result = 270f; Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 2 -> 270°"); break;
                case 3: result = 90f; Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 3 -> 90°"); break;
                default: result = 0f; Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, unknown seat {seatNum} -> 0°"); break;
            }
        }
        else
        {
            // Sequential clockwise numbering for 5-8 players
            float anglePerSeat = 360f / activePlayers;
            result = seatNum * anglePerSeat;
            Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using {activePlayers}-player layout, seat {seatNum} -> {result}° (anglePerSeat: {anglePerSeat}°)");
        }

        return result;
    }

    /// <summary>
    /// Gets the number of active players based on active seat transforms.
    /// </summary>
    private int GetActivePlayerCount()
    {
        Debug.Log($"{DEBUG_TAG}GetActivePlayerCount - Checking active seats, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        // Count active seats or get from configuration
        int count = 0;
        for (int i = 0; i < m_TableTop.seats.Length; i++)
        {
            var seat = m_TableTop.seats[i];
            if (seat.seatTransform != null && seat.seatTransform.gameObject != null)
            {
                bool isActive = seat.seatTransform.gameObject.activeSelf;
                Debug.Log($"{DEBUG_TAG}GetActivePlayerCount - Seat {i} active: {isActive}");
                if (isActive)
                    count++;
            }
            else
            {
                Debug.LogWarning($"{DEBUG_TAG}GetActivePlayerCount - Seat {i} has null transform or gameObject!");
            }
        }

        int result = Mathf.Max(2, count); // Ensure at least 2 players
        Debug.Log($"{DEBUG_TAG}GetActivePlayerCount - Found {count} active seats, returning {result} (minimum 2)");
        return result;
    }

    public void ResetSeatRotation()
    {
        Debug.Log($"{DEBUG_TAG}ResetSeatRotation - Current seat: {TableTop.k_CurrentSeat}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        if (m_XROrigin == null)
        {
            Debug.LogError($"{DEBUG_TAG}ResetSeatRotation - XROrigin is null!");
            FindReferences();
            if (m_XROrigin == null)
            {
                Debug.LogError($"{DEBUG_TAG}ResetSeatRotation - Failed to find XROrigin!");
                return;
            }
        }

        if (TableTop.k_CurrentSeat < 0 || TableTop.k_CurrentSeat >= m_TableTop.seats.Length)
        {
            Debug.LogError($"{DEBUG_TAG}ResetSeatRotation - Invalid current seat: {TableTop.k_CurrentSeat}");
            return;
        }

        Vector3 headForward = new Vector3(m_XROrigin.transform.forward.x, 0, m_XROrigin.transform.forward.z);
        Vector3 seatForward = new Vector3(m_TableTop.GetSeat(TableTop.k_CurrentSeat).forward.x, 0, m_TableTop.GetSeat(TableTop.k_CurrentSeat).forward.z);
        float angle = Vector3.SignedAngle(headForward, seatForward, Vector3.up);

        Debug.Log($"{DEBUG_TAG}ResetSeatRotation - Head forward: {headForward}, Seat forward: {seatForward}, Angle: {angle}");
        Debug.Log($"{DEBUG_TAG}ResetSeatRotation - XROrigin before rotation: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");

        m_XROrigin.transform.RotateAround(transform.position, transform.up, angle);

        Debug.Log($"{DEBUG_TAG}ResetSeatRotation - XROrigin after rotation: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");
    }

    public void ResetToSeatDefault()
    {
        Debug.Log($"{DEBUG_TAG}ResetToSeatDefault - Current seat: {TableTop.k_CurrentSeat}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        if (TableTop.k_CurrentSeat < 0 || TableTop.k_CurrentSeat >= m_TableTop.seats.Length)
        {
            Debug.LogError($"{DEBUG_TAG}ResetToSeatDefault - Invalid current seat: {TableTop.k_CurrentSeat}");
            return;
        }

        var seat = m_TableTop.GetSeat(TableTop.k_CurrentSeat);
        Debug.Log($"{DEBUG_TAG}ResetToSeatDefault - Seat position: {seat.position}, rotation: {seat.rotation.eulerAngles}");

        var seatPosition = seat.position;
        seatPosition.y -= m_DefaultSeatHeight;
        Debug.Log($"{DEBUG_TAG}ResetToSeatDefault - Adjusted seat position with height offset ({m_DefaultSeatHeight}): {seatPosition}");

        if (m_XROrigin == null)
        {
            Debug.LogWarning($"{DEBUG_TAG}ResetToSeatDefault - XROrigin is null, finding references");
            FindReferences();
            if (m_XROrigin == null)
            {
                Debug.LogError($"{DEBUG_TAG}ResetToSeatDefault - Failed to find XROrigin!");
                return;
            }
        }

        var targetPosition = seatPosition - seat.forward * m_TableTop.seatOffset;
        var targetRotation = seat.rotation;
        Debug.Log($"{DEBUG_TAG}ResetToSeatDefault - Target position: {targetPosition}, Target rotation: {targetRotation.eulerAngles}, Seat offset: {m_TableTop.seatOffset}");

        Debug.Log($"{DEBUG_TAG}ResetToSeatDefault - XROrigin before repositioning: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");
        m_XROrigin.transform.SetPositionAndRotation(targetPosition, targetRotation);
        Debug.Log($"{DEBUG_TAG}ResetToSeatDefault - XROrigin after repositioning: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");

        Debug.Log($"{DEBUG_TAG}ResetToSeatDefault - Setting table seat offset to 0 (was {m_TableTop.seatOffset})");
        m_TableTop.seatOffset = 0;
    }
}
