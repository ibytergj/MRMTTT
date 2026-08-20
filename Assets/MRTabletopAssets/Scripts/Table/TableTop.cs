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

        public Transform GetSeat(int seatIdx)
        {
            if (seatIdx <= -1)
                return m_Seats[0].seatTransform;

            return m_Seats[seatIdx].seatTransform;
        }

        // NOTE: The template's OnValidate() auto seat placement has been intentionally removed.
        // Seat transforms (position/rotation) are authored manually in the scene/Inspector.

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
        /// Updates the seat visibility (active/inactive) based on the number of active players.
        /// Seat transforms (position/rotation) are NOT modified - they should be set manually in the Inspector.
        /// </summary>
        /// <param name="playerCount">Number of active players (2-8)</param>
        /// <param name="force8PlayerMode">Force 8-player mode (show all 8 seats) regardless of player count</param>
        public void UpdateSeatPositions(int playerCount, bool force8PlayerMode = false)
        {
            playerCount = Mathf.Clamp(playerCount, 2, 8);

            // Determine how many seats to activate
            int seatsToActivate = force8PlayerMode ? 8 : playerCount;

            // Activate/deactivate seats based on player count
            // NOTE: Seat positions and rotations are NOT modified - they should be set in the Inspector
            for (int i = 0; i < m_Seats.Length; i++)
            {
                bool isActive = i < seatsToActivate;
                if (m_Seats[i].seatTransform != null && m_Seats[i].seatTransform.gameObject != null)
                {
                    m_Seats[i].seatTransform.gameObject.SetActive(isActive);
                }
                else
                {
                    Debug.LogWarning($"TableTop: UpdateSeatPositions - Seat {i} has null transform or gameObject!");
                }
            }

            // Update the player count in shader components
            UpdatePlayerCount(playerCount);
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
        void UpdatePlayerCount(int playerCount)
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
        }
    }

    [Serializable]
    public struct TableSeat
    {
        public Transform seatTransform;
        public int seatID;
    }
}
