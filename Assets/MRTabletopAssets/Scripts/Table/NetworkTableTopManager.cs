using System;
using Unity.Netcode;
using XRMultiplayer;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    public class NetworkTableTopManager : NetworkBehaviour
    {
        public NetworkList<NetworkedSeat> networkedSeats;

        /// <summary>
        /// NetworkVariable to track and synchronize the seat count (4 or 8 seats to show).
        /// This represents the table configuration/shape, NOT the actual number of active players.
        /// Use CountOccupiedSeats() to get the actual player count (1-8).
        /// </summary>
        NetworkVariable<int> m_SeatCount = new NetworkVariable<int>(4, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // NetworkVariable to track if table is locked to 8-player mode (set when 5th player joins)
        NetworkVariable<bool> m_Is8PlayerModeLocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // NetworkVariable to track table scale factor (1.0 for 4-player, 2.0 for 8-player)
        NetworkVariable<float> m_TableScaleFactor = new NetworkVariable<float>(1.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField]
        TableSeatSystem m_SeatSystem;

        [SerializeField]
        TableTop m_TableTop;

        public TableTop tableTop => m_TableTop;

        [SerializeField]
        PlayerRepositionManager m_PlayerRepositionManager;

        [SerializeField]
        TableTopSeatButton[] m_SeatButtons;

        void Awake()
        {
            networkedSeats = new NetworkList<NetworkedSeat>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Subscribe to seat count changes
            m_SeatCount.OnValueChanged += OnSeatCountChanged;

            if (IsServer)
            {
                networkedSeats.Clear();
                for (int i = 0; i < m_SeatButtons.Length; i++)
                {
                    networkedSeats.Add(new NetworkedSeat { isOccupied = false, playerID = 0 });
                }

                // Initialize seat positions - start with 4-seat configuration by default
                int seatsToShow = 4;
                m_SeatCount.Value = seatsToShow;
                m_TableTop.UpdateSeatPositions(seatsToShow);
            }
            else
            {
                // Client initialization - handle initial seat count synchronization
                int initialSeatCount = m_SeatCount.Value;

                // Update seat positions based on initial synchronized value
                m_TableTop.UpdateSeatPositions(initialSeatCount, m_Is8PlayerModeLocked.Value);

                // Update shader components with initial seat count
                UpdateShaderComponents(initialSeatCount);

                // If 8-player mode is locked, scale the table locally for late-joining clients
                if (m_Is8PlayerModeLocked.Value && m_PlayerRepositionManager != null)
                {
                    m_PlayerRepositionManager.ScaleTableObjectsOnly();
                }
            }

            UpdateNetworkedSeatsVisuals();
            networkedSeats.OnListChanged += OnOccupiedSeatsChanged;
            RequestAnySeatFromHost();

            if (IsServer)
            {
                XRINetworkGameManager.Instance.playerStateChanged += OnPlayerStateChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // Unsubscribe from seat count changes
            m_SeatCount.OnValueChanged -= OnSeatCountChanged;

            foreach (var seatButton in m_SeatButtons)
            {
                seatButton.RemovePlayerFromSeat();
            }
            networkedSeats.OnListChanged -= OnOccupiedSeatsChanged;
            XRINetworkGameManager.Instance.playerStateChanged -= OnPlayerStateChanged;
            m_SeatSystem.TeleportToSeat(0);
            TableTop.k_CurrentSeat = -2;
        }

        private void OnOccupiedSeatsChanged(NetworkListEvent<NetworkedSeat> changeEvent)
        {
            UpdateNetworkedSeatsVisuals();
        }

        /// <summary>
        /// Handle changes to the seat count NetworkVariable.
        /// This callback fires on clients when the server changes the seat count (4 or 8).
        /// </summary>
        void OnSeatCountChanged(int previousValue, int newValue)
        {
            if (!IsServer)
            {
                m_TableTop.UpdateSeatPositions(newValue, m_Is8PlayerModeLocked.Value);

                // Update shader components with new seat count
                UpdateShaderComponents(newValue);

                // If 8-player mode is locked, scale the table locally for late-joining clients
                if (m_Is8PlayerModeLocked.Value && m_PlayerRepositionManager != null)
                {
                    m_PlayerRepositionManager.ScaleTableObjectsOnly();
                }
            }
        }

        void OnPlayerStateChanged(ulong playerID, bool connected)
        {
            if (!connected)
            {
                for (int i = 0; i < networkedSeats.Count; i++)
                {
                    if (networkedSeats[i].playerID == playerID)
                    {
                        ServerRemoveSeat(i);
                    }
                }

                UpdateNetworkedSeatsVisuals();
            }

            // Update seat positions based on new player count
            UpdateSeatPositionsBasedOnPlayerCount();
        }

        /// <summary>
        /// Updates the seat positions based on the current number of active players.
        /// </summary>
        void UpdateSeatPositionsBasedOnPlayerCount()
        {
            if (IsServer && m_TableTop != null)
            {
                int occupiedSeatCount = CountOccupiedSeats();
                int previousSeatCount = m_SeatCount.Value;

                // Determine how many seats to show based on occupied seats
                int seatsToShow;

                // Check if 8-player mode is locked (happens when 5th player joins)
                if (m_Is8PlayerModeLocked.Value)
                {
                    // Once locked to 8-player mode, always use 8 seats
                    seatsToShow = 8;
                }
                else if (occupiedSeatCount <= 4)
                {
                    // For 1-4 players, use the standard 4-seat layout
                    seatsToShow = 4;
                }
                else
                {
                    // When 5th player joins, lock to 8-player mode permanently
                    seatsToShow = 8;

                    // Check if this is the first time locking (5th player just joined)
                    bool wasJustLocked = !m_Is8PlayerModeLocked.Value;

                    m_Is8PlayerModeLocked.Value = true;
                    m_TableScaleFactor.Value = 2.0f;

                    // Trigger table expansion RPC to reposition existing players
                    if (wasJustLocked)
                    {
                        TriggerTableExpansionRpc();
                    }
                }

                // Only update if the seat count has changed
                if (seatsToShow != previousSeatCount)
                {
                    // Update the NetworkVariable to synchronize to clients
                    m_SeatCount.Value = seatsToShow;

                    // Update local table
                    m_TableTop.UpdateSeatPositions(seatsToShow, m_Is8PlayerModeLocked.Value);

                    // Update VirtualSurfaceColorShaderUpdater components with new seat count
                    UpdateShaderComponents(seatsToShow);

                    // If transitioning between 4-seat and 8-seat modes, reposition all players
                    if ((previousSeatCount <= 4 && seatsToShow > 4) ||
                        (previousSeatCount > 4 && seatsToShow <= 4))
                    {
                        RepositionAllPlayers();
                    }
                }

                // Update active player index in PlayerColorManager if available
                if (PlayerColorManager.Instance != null)
                {
                    // Set active player to -1 (no active player) or to the first occupied seat
                    int activePlayerIndex = -1;
                    for (int i = 0; i < networkedSeats.Count; i++)
                    {
                        if (networkedSeats[i].isOccupied)
                        {
                            activePlayerIndex = i;
                            break;
                        }
                    }

                    PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;

                    // Trigger color palette changed event to update all UI elements
                    PlayerColorManager.Instance.NotifyColorPaletteChanged();

                    // Ensure all clients update their highlighting
                    UpdateActivePlayerRpc(activePlayerIndex);
                }
                else
                {
                    Debug.LogWarning("NetworkTableTopManager: UpdateSeatPositionsBasedOnPlayerCount - PlayerColorManager.Instance is null!");
                }
            }
            else if (IsServer && m_TableTop == null)
            {
                Debug.LogWarning("NetworkTableTopManager: UpdateSeatPositionsBasedOnPlayerCount - m_TableTop is null, cannot update seat positions.");
            }
        }

        /// <summary>
        /// Updates all VirtualSurfaceColorShaderUpdater components with the new seat count.
        /// Note: The parameter is named 'playerCount' for compatibility with the shader updater API,
        /// but it actually represents the seat count (4 or 8), not the actual number of players.
        /// </summary>
        void UpdateShaderComponents(int playerCount)
        {
            // Find all VirtualSurfaceColorShaderUpdater components in the scene
            VirtualSurfaceColorShaderUpdater[] shaderUpdaters = FindObjectsByType<VirtualSurfaceColorShaderUpdater>(FindObjectsSortMode.None);

            foreach (var updater in shaderUpdaters)
            {
                updater.UpdatePlayerCount(playerCount);
            }
        }

        /// <summary>
        /// Repositions all players based on their current seats.
        /// Used when transitioning between 4-player and 5+ player modes.
        /// </summary>
        void RepositionAllPlayers()
        {
            // Reposition all players based on their current seats
            for (int i = 0; i < networkedSeats.Count; i++)
            {
                if (networkedSeats[i].isOccupied)
                {
                    // Reassign the seat to trigger repositioning
                    AssignSeatRpc(i, networkedSeats[i].playerID);
                }
            }
        }

        /// <summary>
        /// Updates the active player highlighting on all clients.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        void UpdateActivePlayerRpc(int activePlayerIndex)
        {
            // Update local highlighting
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;
                PlayerColorManager.Instance.NotifyColorPaletteChanged();

                // Force shader updates
                var shaderUpdaters = FindObjectsByType<VirtualSurfaceColorShaderUpdater>(FindObjectsSortMode.None);
                foreach (var updater in shaderUpdaters)
                {
                    updater.UpdateShaderProperties();
                }
            }
            else
            {
                Debug.LogWarning("NetworkTableTopManager: UpdateActivePlayerRpc - PlayerColorManager instance not found!");
            }
        }

        /// <summary>
        /// Counts how many seats are currently occupied.
        /// </summary>
        int CountOccupiedSeats()
        {
            int count = 0;
            for (int i = 0; i < networkedSeats.Count; i++)
            {
                if (networkedSeats[i].isOccupied)
                    count++;
            }
            return count;
        }

        void UpdateNetworkedSeatsVisuals()
        {
            for (int i = 0; i < networkedSeats.Count; i++)
            {
                if (!networkedSeats[i].isOccupied)
                {
                    m_SeatButtons[i].SetOccupied(false);
                }
                else
                {
                    if (XRINetworkGameManager.Instance.TryGetPlayerByID(networkedSeats[i].playerID, out var player))
                    {
                        m_SeatButtons[i].AssignPlayerToSeat(player);
                    }
                    else
                    {
                        Debug.LogError($"Player with id {networkedSeats[i].playerID} not found");
                    }
                }
            }
        }

        public void RequestAnySeatFromHost()
        {
            RequestSeatServerRpc(NetworkManager.Singleton.LocalClientId, TableTop.k_CurrentSeat);
        }

        public void RequestSeat(int newSeatChoice)
        {
            int activePlayers = GetActivePlayerCount();
            // Convert logical seat choice to physical if needed
            int physicalSeatChoice = m_TableTop.GetPhysicalSeatIndex(newSeatChoice, activePlayers);
            RequestSeatServerRpc(NetworkManager.Singleton.LocalClientId, TableTop.k_CurrentSeat, physicalSeatChoice);
        }

        /// <summary>
        /// Gets the number of active players based on occupied seats.
        /// </summary>
        int GetActivePlayerCount()
        {
            return Mathf.Min(8, CountOccupiedSeats());
        }

        /// <summary>
        /// Gets the network-synchronized seat count for use by other components.
        /// This returns the table configuration (4 or 8 seats), NOT the actual number of active players.
        /// Use CountOccupiedSeats() to get the actual player count (1-8).
        /// </summary>
        /// <returns>The network-synchronized seat count (4 or 8)</returns>
        public int GetNetworkSynchronizedSeatCount()
        {
            return m_SeatCount.Value;
        }

        [Rpc(SendTo.Server)]
        void RequestSeatServerRpc(ulong localPlayerID, int currentSeatID, int newSeatID = -2)
        {
            if (newSeatID <= -2)    // Request any available seat
                newSeatID = GetAnyAvailableSeats();

            if (!IsSeatOccupied(newSeatID))
                ServerAssignSeat(currentSeatID, newSeatID, localPlayerID);
            else
                Debug.LogWarning($"User {localPlayerID} tried to join occupied seat {newSeatID}");
        }

        int GetAnyAvailableSeats()
        {
            int availableSeat = -1;

            // Get the current occupied seat count
            int occupiedSeats = CountOccupiedSeats();

            // Preferred seating order for 4 or fewer players
            // This maintains the original seat assignment preference
            if (occupiedSeats < 4)
            {
                int[] preferredOrder = { 0, 1, 2, 3 };

                // Try to assign seats in the preferred order
                foreach (int seatIndex in preferredOrder)
                {
                    if (seatIndex < networkedSeats.Count && !networkedSeats[seatIndex].isOccupied)
                    {
                        availableSeat = seatIndex;
                        return availableSeat;
                    }
                }
            }

            // Fall back to first available seat for 5+ players or if preferred seats are taken
            for (int i = 0; i < networkedSeats.Count; i++)
            {
                if (!networkedSeats[i].isOccupied)
                {
                    availableSeat = i;
                    return availableSeat;
                }
            }

            Debug.LogWarning("NetworkTableTopManager: GetAnyAvailableSeats - No available seats found!");
            return availableSeat;
        }

        bool IsSeatOccupied(int seatID)
        {
            return seatID >= 0 && networkedSeats[seatID].isOccupied;
        }

        void ServerAssignSeat(int currentSeatID, int newSeatID, ulong localPlayerID)
        {
            if (currentSeatID >= 0)
            {
                ServerRemoveSeat(currentSeatID);
            }
            if (newSeatID >= 0)
            {
                networkedSeats[newSeatID] = new NetworkedSeat { isOccupied = true, playerID = localPlayerID };
            }

            UpdateNetworkedSeatsVisuals();

            // Update seat positions when a player joins
            UpdateSeatPositionsBasedOnPlayerCount();

            AssignSeatRpc(newSeatID, localPlayerID);
        }

        void ServerRemoveSeat(int seatID)
        {
            networkedSeats[seatID] = new NetworkedSeat { isOccupied = false, playerID = 0 };
            UpdateNetworkedSeatsVisuals();
            RemovePlayerFromSeatRpc(seatID);
        }

        [Rpc(SendTo.Everyone)]
        void RemovePlayerFromSeatRpc(int seatID)
        {
            m_SeatButtons[seatID].RemovePlayerFromSeat();
        }

        [Rpc(SendTo.Everyone)]
        void AssignSeatRpc(int seatID, ulong playerID)
        {
            if (XRINetworkGameManager.Instance.TryGetPlayerByID(playerID, out var player))
            {
                m_SeatButtons[seatID].AssignPlayerToSeat(player);
                if (playerID == NetworkManager.Singleton.LocalClientId)
                {
                    if (m_SeatSystem != null)
                    {
                        m_SeatSystem.TeleportToSeat(seatID);
                    }
                    else
                    {
                        Debug.LogError($"NetworkTableTopManager: AssignSeatRpc - m_SeatSystem is null! Cannot teleport to seat {seatID}");
                    }
                }
            }
            else
            {
                Debug.LogError($"Player with id {playerID} not found");
            }
        }

        public void TeleportToSpectatorSeat()
        {
            RequestSeat(-1);
        }

        /// <summary>
        /// Gets whether the table is locked to 8-player mode.
        /// </summary>
        public bool Is8PlayerModeLocked()
        {
            return m_Is8PlayerModeLocked.Value;
        }

        /// <summary>
        /// Gets the current table scale factor (1.0 for 4-player, 2.0 for 8-player).
        /// </summary>
        public float GetTableScaleFactor()
        {
            return m_TableScaleFactor.Value;
        }

        /// <summary>
        /// Triggers table expansion to 8-player mode on all clients.
        /// Called when 5th player joins to reposition existing players 1-4.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        void TriggerTableExpansionRpc()
        {
            // Only reposition players who are already seated (players 1-4)
            // New players (5-8) will spawn directly at correct positions
            if (m_PlayerRepositionManager != null)
            {
                m_PlayerRepositionManager.RepositionPlayerWithFade();
            }
            else
            {
                Debug.LogError("NetworkTableTopManager: TriggerTableExpansionRpc - PlayerRepositionManager is null!");
            }
        }
    }

    [Serializable]
    public struct NetworkedSeat : INetworkSerializable, IEquatable<NetworkedSeat>
    {
        public bool isOccupied;
        public ulong playerID;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref isOccupied);
            serializer.SerializeValue(ref playerID);
        }

        public readonly bool Equals(NetworkedSeat other)
        {
            return isOccupied == other.isOccupied && playerID == other.playerID;
        }
    }
}
