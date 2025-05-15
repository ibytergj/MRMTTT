using System;
using UnityEngine;
using MRTabletopAssets;

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



    public Transform GetSeat(int seatIdx)
    {
        if (seatIdx <= -1)
            return m_Seats[0].seatTransform;

        return m_Seats[seatIdx].seatTransform;
    }

    void OnValidate()
    {
        foreach (TableSeat seat in m_Seats)
        {
            seat.seatTransform.localPosition = -seat.seatTransform.forward * m_SeatDistance;
        }
    }

    // Editor-only methods
    #if UNITY_EDITOR
    [ContextMenu("Test 8 Player Positioning")]
    public void TestEightPlayerPositioning()
    {
        UpdateSeatPositions(8);
        Debug.Log("Positioned seats for 8 players");
    }

    [ContextMenu("Test 4 Player Positioning")]
    public void TestFourPlayerPositioning()
    {
        UpdateSeatPositions(4);
        Debug.Log("Positioned seats for 4 players");
    }


    #endif

    /// <summary>
    /// Updates the seat positions based on the number of active players.
    /// For 4 or fewer players, uses the original layout.
    /// For 5-8 players, uses a sequential layout with evenly distributed angles.
    /// </summary>
    /// <param name="playerCount">Number of active players (2-8)</param>
    public void UpdateSeatPositions(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 2, 8);

        Debug.Log($"Updating seat positions for {playerCount} players");

        if (playerCount <= 4)
        {
            // Use existing layout for 4 or fewer players
            PositionSeatsForFourOrLess(playerCount);
        }
        else
        {
            // Use sequential layout for 5-8 players
            PositionSeatsSequentially(playerCount);
        }

        // Update the player count in shader components
        UpdatePlayerCount(playerCount);
    }

    /// <summary>
    /// Positions seats using the original layout for 4 or fewer players.
    /// </summary>
    private void PositionSeatsForFourOrLess(int playerCount)
    {
        // Activate/deactivate seats as needed
        for (int i = 0; i < m_Seats.Length; i++)
        {
            bool isActive = i < playerCount;
            if (m_Seats[i].seatTransform.gameObject != null)
                m_Seats[i].seatTransform.gameObject.SetActive(isActive);
        }

        // Position seats using existing layout
        if (playerCount >= 1)
        {
            // Seat 0: 0 degrees
            m_Seats[0].seatTransform.localRotation = Quaternion.Euler(0, 0, 0);
            Vector3 direction = m_Seats[0].seatTransform.forward;
            m_Seats[0].seatTransform.localPosition = direction * m_SeatDistance;
        }

        if (playerCount >= 2)
        {
            // Seat 1: 180 degrees
            m_Seats[1].seatTransform.localRotation = Quaternion.Euler(0, 180, 0);
            Vector3 direction = m_Seats[1].seatTransform.forward;
            m_Seats[1].seatTransform.localPosition = direction * m_SeatDistance;
        }

        if (playerCount >= 3)
        {
            // Seat 2: 270 degrees
            m_Seats[2].seatTransform.localRotation = Quaternion.Euler(0, 270, 0);
            Vector3 direction = m_Seats[2].seatTransform.forward;
            m_Seats[2].seatTransform.localPosition = direction * m_SeatDistance;
        }

        if (playerCount >= 4)
        {
            // Seat 3: 90 degrees
            m_Seats[3].seatTransform.localRotation = Quaternion.Euler(0, 90, 0);
            Vector3 direction = m_Seats[3].seatTransform.forward;
            m_Seats[3].seatTransform.localPosition = direction * m_SeatDistance;
        }

        // Log the arrangement
        Debug.Log($"Positioned {playerCount} seats in the standard layout (0°, 180°, 270°, 90°)");
    }

    /// <summary>
    /// Positions seats sequentially around the table for 5-8 players in a regular polygon.
    /// </summary>
    private void PositionSeatsSequentially(int playerCount)
    {
        float angleStep = 360f / playerCount;

        // Activate/deactivate and position seats
        for (int i = 0; i < m_Seats.Length; i++)
        {
            bool isActive = i < playerCount;
            if (m_Seats[i].seatTransform.gameObject != null)
                m_Seats[i].seatTransform.gameObject.SetActive(isActive);

            if (isActive)
            {
                // Calculate angle for this seat (clockwise starting from 0)
                float angle = i * angleStep;

                // Set rotation to face the center
                m_Seats[i].seatTransform.localRotation = Quaternion.Euler(0, angle, 0);

                // Position the seat at the calculated angle
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                m_Seats[i].seatTransform.localPosition = direction * m_SeatDistance;
            }
        }

        // Log the arrangement
        Debug.Log($"Positioned {playerCount} seats in a {GetPolygonName(playerCount)} arrangement");
    }

    /// <summary>
    /// Gets the name of the polygon based on the number of sides.
    /// </summary>
    private string GetPolygonName(int sides)
    {
        switch (sides)
        {
            case 3: return "triangle";
            case 4: return "square";
            case 5: return "pentagon";
            case 6: return "hexagon";
            case 7: return "heptagon";
            case 8: return "octagon";
            default: return $"{sides}-sided polygon";
        }
    }

    /// <summary>
    /// Maps physical seat index to logical player index.
    /// For 4 or fewer players, maintains the current mapping.
    /// For 5+ players, physical and logical indices are the same.
    /// </summary>
    public int GetLogicalPlayerIndex(int physicalSeatIndex, int totalActivePlayers)
    {
        if (totalActivePlayers <= 4)
        {
            // For 4 or fewer players, maintain the current mapping
            // Current physical layout: 0->0°, 1->180°, 2->270°, 3->90°
            // Logical clockwise order: 0, 3, 1, 2
            switch (physicalSeatIndex)
            {
                case 0: return 0;
                case 1: return 2;
                case 2: return 3;
                case 3: return 1;
                default: return physicalSeatIndex;
            }
        }
        else
        {
            // For 5+ players, physical and logical indices are the same
            return physicalSeatIndex;
        }
    }

    /// <summary>
    /// Maps logical player index to physical seat index.
    /// For 4 or fewer players, maintains the current mapping.
    /// For 5+ players, physical and logical indices are the same.
    /// </summary>
    public int GetPhysicalSeatIndex(int logicalPlayerIndex, int totalActivePlayers)
    {
        if (totalActivePlayers <= 4)
        {
            // For 4 or fewer players, maintain the current mapping
            // Logical clockwise order: 0, 3, 1, 2
            // Current physical layout: 0->0°, 1->180°, 2->270°, 3->90°
            switch (logicalPlayerIndex)
            {
                case 0: return 0;
                case 1: return 3;
                case 2: return 1;
                case 3: return 2;
                default: return logicalPlayerIndex;
            }
        }
        else
        {
            // For 5+ players, physical and logical indices are the same
            return logicalPlayerIndex;
        }
    }

    /// <summary>
    /// Updates the player count in materials using the VirtualSurfaceColorShader.
    /// </summary>
    private void UpdatePlayerCount(int playerCount)
    {
        // Find VirtualSurfaceColorShaderUpdater components and update them
        VirtualSurfaceColorShaderUpdater[] updaters = GetComponentsInChildren<VirtualSurfaceColorShaderUpdater>();
        foreach (var updater in updaters)
        {
            // Update the player count (which updates shape sides)
            updater.UpdatePlayerCount(playerCount);
        }

        // For backward compatibility, also update materials directly
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.materials)
            {
                if (material != null && material.shader != null &&
                    (material.shader.name.Contains("VirtualSurfaceShader") ||
                     material.shader.name.Contains("VirtualSurfaceColorShader")))
                {
                    // Update the shape sides parameter
                    material.SetInt("_ShapeSides", playerCount);
                }
            }
        }

        Debug.Log($"Updated table shape to {playerCount} sides");
    }




}

[Serializable]
public struct TableSeat
{
    public Transform seatTransform;
    public int seatID;
}
