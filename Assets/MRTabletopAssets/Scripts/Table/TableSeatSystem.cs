using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Unity.Netcode;

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

    [SerializeField]
    private NetworkTableTopManager m_NetworkTableTopManager;

    XROrigin m_XROrigin;

    void Awake()
    {
        FindReferences();
    }

    void FindReferences()
    {
        m_XROrigin = FindFirstObjectByType<XROrigin>();

        if (m_NetworkTableTopManager == null)
        {
            m_NetworkTableTopManager = FindFirstObjectByType<NetworkTableTopManager>();
            if (m_NetworkTableTopManager == null)
            {
                Debug.LogWarning($"{DEBUG_TAG}FindReferences - Could not find NetworkTableTopManager!");
            }
        }
    }

    public void TeleportToSeat(int seatNum)
    {
        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Seat number: {seatNum}, Current seat: {TableTop.k_CurrentSeat}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        // Validate TableTop reference
        if (m_TableTop == null)
        {
            Debug.LogError($"{DEBUG_TAG}TeleportToSeat - TableTop reference is null! Cannot teleport to seat {seatNum}");
            return;
        }

        // Validate seat number
        if (seatNum >= 0 && seatNum >= m_TableTop.seats.Length)
        {
            Debug.LogError($"{DEBUG_TAG}TeleportToSeat - Invalid seat number {seatNum}! Max seat index is {m_TableTop.seats.Length - 1}");
            return;
        }

        // Check for spectator seat or initial seat
        if (TableTop.k_CurrentSeat < 0)
        {
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - Current seat is negative ({TableTop.k_CurrentSeat}), setting to 0");
            TableTop.k_CurrentSeat = 0;
        }

        int prevSeat = TableTop.k_CurrentSeat;
        TableTop.k_CurrentSeat = seatNum;
        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Previous seat: {prevSeat}, New seat: {seatNum}");

        // Get rotation angles
        float currentAngle = GetRotationAngleBasedOnSeatNum(prevSeat);
        float newAngle = GetRotationAngleBasedOnSeatNum(seatNum);
        float rotationAmount = newAngle - currentAngle;

        Debug.Log($"{DEBUG_TAG}TeleportToSeat - Current angle: {currentAngle}, New angle: {newAngle}, Rotation amount: {rotationAmount}");

        // Check for XROrigin
        if (m_XROrigin == null)
        {
            Debug.LogWarning($"{DEBUG_TAG}TeleportToSeat - XROrigin is null, attempting to find it");
            FindReferences();

            if (m_XROrigin == null)
            {
                Debug.LogError($"{DEBUG_TAG}TeleportToSeat - Failed to find XROrigin! Cannot teleport player");
                return;
            }
            else
            {
                Debug.Log($"{DEBUG_TAG}TeleportToSeat - Successfully found XROrigin");
            }
        }

        try
        {
            // Perform the rotation
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - XROrigin before rotation: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");
            m_XROrigin.transform.RotateAround(transform.position, transform.up, rotationAmount);
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - XROrigin after rotation: position={m_XROrigin.transform.position}, rotation={m_XROrigin.transform.rotation.eulerAngles}");

            // Invoke the seat changed event
            m_OnSeatChanged.Invoke(seatNum);
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - Invoked OnSeatChanged event with seat number: {seatNum}");

            // Reset transform
            Debug.Log($"{DEBUG_TAG}TeleportToSeat - Setting transform position and rotation to zero/identity");
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Debug.Log($"{DEBUG_TAG}TeleportToSeat - Successfully teleported to seat {seatNum}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"{DEBUG_TAG}TeleportToSeat - Exception during teleportation: {ex.Message}\n{ex.StackTrace}");
        }
    }

    float GetRotationAngleBasedOnSeatNum(int seatNum)
    {
        Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Seat number: {seatNum}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        // Handle invalid seat number
        if (m_TableTop == null)
        {
            Debug.LogError($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - TableTop reference is null!");
            return 0f;
        }

        int totalSeats = m_TableTop.seats.Length;

        // Get the network-synchronized player count
        int activePlayers = GetActivePlayerCount();

        // Log detailed information about the player count source
        if (m_NetworkTableTopManager != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Network active: IsServer={NetworkManager.Singleton.IsServer}, IsClient={NetworkManager.Singleton.IsClient}, IsHost={NetworkManager.Singleton.IsHost}");
            Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using network-synchronized player count: {activePlayers}");
        }
        else
        {
            Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Network not active, using local player count: {activePlayers}");
        }

        Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Total seats: {totalSeats}, Active players: {activePlayers}");

        // Handle spectator seat (-1) or invalid seat
        if (seatNum < 0)
        {
            Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Spectator or invalid seat ({seatNum}), returning 0°");
            return 0f;
        }

        float result;
        if (activePlayers <= 4)
        {
            // Original 4-player layout with non-sequential numbering
            // This maintains exact backward compatibility with the original implementation
            switch (seatNum)
            {
                case 0:
                    result = 0f;
                    Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 0 -> 0° (bottom)");
                    break;
                case 1:
                    result = 180f;
                    Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 1 -> 180° (top)");
                    break;
                case 2:
                    result = 270f;
                    Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 2 -> 270° (left)");
                    break;
                case 3:
                    result = 90f;
                    Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, seat 3 -> 90° (right)");
                    break;
                default:
                    // For any other seat in 4-player mode, use a fallback
                    result = 0f;
                    Debug.LogWarning($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using 4-player layout, unknown seat {seatNum} -> 0° (fallback)");
                    break;
            }
        }
        else
        {
            // Sequential clockwise numbering for 5-8 players
            // For 5+ players, we use evenly distributed angles in a regular polygon
            float anglePerSeat = 360f / activePlayers;
            result = seatNum * anglePerSeat;
            Debug.Log($"{DEBUG_TAG}GetRotationAngleBasedOnSeatNum - Using {activePlayers}-player layout, seat {seatNum} -> {result}° (anglePerSeat: {anglePerSeat}°)");
        }

        return result;
    }

    /// <summary>
    /// Gets the number of active players based on the network-synchronized player count.
    /// Falls back to counting active seat transforms if network data is unavailable.
    /// </summary>
    private int GetActivePlayerCount()
    {
        Debug.Log($"{DEBUG_TAG}GetActivePlayerCount - BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        // First try to get the network-synchronized player count
        if (m_NetworkTableTopManager != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Access the ActivePlayerCount from NetworkTableTopManager
            int networkPlayerCount = m_NetworkTableTopManager.GetNetworkSynchronizedPlayerCount();

            if (networkPlayerCount > 0)
            {
                Debug.Log($"{DEBUG_TAG}GetActivePlayerCount - Using network-synchronized player count: {networkPlayerCount}, IsServer: {NetworkManager.Singleton.IsServer}, IsClient: {NetworkManager.Singleton.IsClient}");
                return networkPlayerCount;
            }
            else
            {
                Debug.LogWarning($"{DEBUG_TAG}GetActivePlayerCount - Network player count is invalid ({networkPlayerCount}), falling back to local count");
            }
        }
        else
        {
            Debug.LogWarning($"{DEBUG_TAG}GetActivePlayerCount - NetworkTableTopManager not available or network not active, falling back to local count");
        }

        // Fallback: Count active seats locally
        Debug.Log($"{DEBUG_TAG}GetActivePlayerCount - Falling back to counting active seats");
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
