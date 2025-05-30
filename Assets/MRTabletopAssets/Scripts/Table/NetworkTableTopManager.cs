using System;
using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;
using MRTabletopAssets;

public class NetworkTableTopManager : NetworkBehaviour
{
    // Debug prefix for logging
    private const string DEBUG_TAG = "[NetworkTableTopManager] ";

    public NetworkList<NetworkedSeat> networkedSeats;

    // NetworkVariable to track and synchronize the active player count / table shape
    private NetworkVariable<int> m_ActivePlayerCount = new NetworkVariable<int>(4, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField]
    TableSeatSystem m_SeatSystem;

    [SerializeField]
    TableTop m_TableTop;

    public TableTop tableTop => m_TableTop;

    [SerializeField]
    TableTopSeatButton[] m_SeatButtons;

    void Awake()
    {
        networkedSeats = new NetworkList<NetworkedSeat>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"{DEBUG_TAG}OnNetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, NetworkManager.IsListening: {NetworkManager.Singleton.IsListening}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        // Subscribe to the active player count changes
        m_ActivePlayerCount.OnValueChanged += OnActivePlayerCountChanged;

        if (IsServer)
        {
            Debug.Log($"{DEBUG_TAG}Server initializing networked seats. Seat button count: {m_SeatButtons.Length}");
            networkedSeats.Clear();
            for (int i = 0; i < m_SeatButtons.Length; i++)
            {
                networkedSeats.Add(new NetworkedSeat { isOccupied = false, playerID = 0 });
                Debug.Log($"{DEBUG_TAG}Added empty seat at index {i}");
            }

            // Initialize seat positions based on active player count
            // Start with 4 seats by default
            int seatsToShow = 4;

            // For now, we'll just use the default 4-player layout
            // In a real implementation, you would determine the actual player count

            Debug.Log($"{DEBUG_TAG}Server initializing {seatsToShow} seats");
            m_ActivePlayerCount.Value = seatsToShow; // Set the NetworkVariable
            m_TableTop.UpdateSeatPositions(seatsToShow);
        }
        else
        {
            Debug.Log($"{DEBUG_TAG}Client received active player count: {m_ActivePlayerCount.Value}");
        }

        Debug.Log($"{DEBUG_TAG}Updating networked seats visuals");
        UpdateNetworkedSeatsVisuals();
        networkedSeats.OnListChanged += OnOccupiedSeatsChanged;

        Debug.Log($"{DEBUG_TAG}Requesting any seat from host. Local client ID: {NetworkManager.Singleton.LocalClientId}, Current seat: {TableTop.k_CurrentSeat}");
        RequestAnySeatFromHost();

        if (IsServer)
        {
            Debug.Log($"{DEBUG_TAG}Server subscribing to player state changes");
            XRINetworkGameManager.Instance.playerStateChanged += OnPlayerStateChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Unsubscribe from the active player count changes
        m_ActivePlayerCount.OnValueChanged -= OnActivePlayerCountChanged;

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
        Debug.Log($"{DEBUG_TAG}OnOccupiedSeatsChanged - Event type: {changeEvent.Type}, Index: {changeEvent.Index}");
        UpdateNetworkedSeatsVisuals();
    }

    // Handle changes to the active player count NetworkVariable
    private void OnActivePlayerCountChanged(int previousValue, int newValue)
    {
        Debug.Log($"{DEBUG_TAG}OnActivePlayerCountChanged - Previous: {previousValue}, New: {newValue}, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        if (!IsServer)
        {
            Debug.Log($"{DEBUG_TAG}Client received updated player count: {newValue}, updating seat positions and shader components");
            m_TableTop.UpdateSeatPositions(newValue);

            // Update shader components with new player count
            UpdateShaderComponents(newValue);
        }
        else
        {
            Debug.Log($"{DEBUG_TAG}Server received active player count change notification (this is unexpected)");
        }
    }

    void OnPlayerStateChanged(ulong playerID, bool connected)
    {
        Debug.Log($"{DEBUG_TAG}OnPlayerStateChanged - PlayerID: {playerID}, Connected: {connected}, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        if (!connected)
        {
            Debug.Log($"{DEBUG_TAG}OnPlayerStateChanged - Player {playerID} disconnected, checking seats");
            for (int i = 0; i < networkedSeats.Count; i++)
            {
                Debug.Log($"{DEBUG_TAG}OnPlayerStateChanged - Checking seat {i}: isOccupied={networkedSeats[i].isOccupied}, playerID={networkedSeats[i].playerID}");
                if (networkedSeats[i].playerID == playerID)
                {
                    Debug.Log($"{DEBUG_TAG}OnPlayerStateChanged - Found player {playerID} in seat {i}, removing");
                    ServerRemoveSeat(i);
                }
            }

            Debug.Log($"{DEBUG_TAG}OnPlayerStateChanged - Updating networked seats visuals after player disconnect");
            UpdateNetworkedSeatsVisuals();
        }
        else
        {
            Debug.Log($"{DEBUG_TAG}OnPlayerStateChanged - Player {playerID} connected");
        }

        // Update seat positions based on new player count
        Debug.Log($"{DEBUG_TAG}OnPlayerStateChanged - Updating seat positions based on player count");
        UpdateSeatPositionsBasedOnPlayerCount();
    }

    /// <summary>
    /// Updates the seat positions based on the current number of active players.
    /// </summary>
    private void UpdateSeatPositionsBasedOnPlayerCount()
    {
        Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        if (IsServer && m_TableTop != null)
        {
            int occupiedSeatCount = CountOccupiedSeats();
            int previousPlayerCount = m_ActivePlayerCount.Value;
            Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Occupied seat count: {occupiedSeatCount}, Previous player count: {previousPlayerCount}");

            // Determine how many seats to show based on occupied seats
            int seatsToShow;

            if (occupiedSeatCount <= 4)
            {
                // For 1-4 players, use the standard 4-player layout
                seatsToShow = 4;
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Using standard 4-player layout (fixed positions)");
            }
            else
            {
                // For 5-8 players, use the exact number of players
                // This creates a pentagon for 5, hexagon for 6, etc.
                seatsToShow = occupiedSeatCount;
                seatsToShow = Mathf.Min(8, seatsToShow); // Cap at 8 players
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Using {seatsToShow}-player layout (regular polygon)");
            }

            // Only update if the player count has changed
            if (seatsToShow != previousPlayerCount)
            {
                Debug.Log($"{DEBUG_TAG}Updating seat positions for {seatsToShow} seats (occupied seats: {occupiedSeatCount})");

                // Update the NetworkVariable to synchronize to clients
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Setting m_ActivePlayerCount.Value from {previousPlayerCount} to {seatsToShow}");
                m_ActivePlayerCount.Value = seatsToShow;

                // Update local table
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Calling m_TableTop.UpdateSeatPositions({seatsToShow})");
                m_TableTop.UpdateSeatPositions(seatsToShow);

                // Update VirtualSurfaceColorShaderUpdater components with new player count
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Calling UpdateShaderComponents({seatsToShow})");
                UpdateShaderComponents(seatsToShow);

                // If transitioning between 4-player and 5+ player modes, reposition all players
                if ((previousPlayerCount <= 4 && seatsToShow > 4) ||
                    (previousPlayerCount > 4 && seatsToShow <= 4))
                {
                    Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Transitioning between 4-player and 5+ player modes, repositioning all players");
                    RepositionAllPlayers();
                }

                // Log the current NetworkVariable value to verify it was updated
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - After update: m_ActivePlayerCount.Value = {m_ActivePlayerCount.Value}");
            }
            else
            {
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Player count unchanged ({seatsToShow}), skipping update");
            }

            // Update active player index in PlayerColorManager if available
            if (MRTabletopAssets.PlayerColorManager.Instance != null)
            {
                // Set active player to -1 (no active player) or to the first occupied seat
                int activePlayerIndex = -1;
                for (int i = 0; i < networkedSeats.Count; i++)
                {
                    if (networkedSeats[i].isOccupied)
                    {
                        activePlayerIndex = i;
                        Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Found first occupied seat at index {i}");
                        break;
                    }
                }

                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Setting PlayerColorManager.ActivePlayerIndex to {activePlayerIndex}");
                MRTabletopAssets.PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;

                // Trigger color palette changed event to update all UI elements
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Calling PlayerColorManager.NotifyColorPaletteChanged()");
                MRTabletopAssets.PlayerColorManager.Instance.NotifyColorPaletteChanged();

                // Ensure all clients update their highlighting
                UpdateActivePlayerRpc(activePlayerIndex);
            }
            else
            {
                Debug.LogWarning($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - PlayerColorManager.Instance is null!");
            }
        }
        else
        {
            // Log why we're not updating seat positions
            if (!IsServer)
            {
                Debug.Log($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Not updating seat positions because this is not the server");
            }
            if (m_TableTop == null)
            {
                Debug.LogWarning($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Not updating seat positions because m_TableTop is null");
            }

            Debug.LogWarning($"{DEBUG_TAG}UpdateSeatPositionsBasedOnPlayerCount - Not server or m_TableTop is null. IsServer: {IsServer}, m_TableTop: {(m_TableTop != null ? "not null" : "null")}");
        }
    }

    /// <summary>
    /// Updates all VirtualSurfaceColorShaderUpdater components with the new player count.
    /// </summary>
    private void UpdateShaderComponents(int playerCount)
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
    private void RepositionAllPlayers()
    {
        Debug.Log($"{DEBUG_TAG}RepositionAllPlayers - Repositioning all players based on their current seats");

        // Reposition all players based on their current seats
        for (int i = 0; i < networkedSeats.Count; i++)
        {
            if (networkedSeats[i].isOccupied)
            {
                Debug.Log($"{DEBUG_TAG}RepositionAllPlayers - Reassigning seat {i} to player {networkedSeats[i].playerID}");
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
        Debug.Log($"{DEBUG_TAG}UpdateActivePlayerRpc - ActivePlayerIndex: {activePlayerIndex}, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");

        // Update local highlighting
        if (MRTabletopAssets.PlayerColorManager.Instance != null)
        {
            Debug.Log($"{DEBUG_TAG}UpdateActivePlayerRpc - Setting active player index to {activePlayerIndex} in PlayerColorManager");
            MRTabletopAssets.PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;
            MRTabletopAssets.PlayerColorManager.Instance.NotifyColorPaletteChanged();

            // Force shader updates
            var shaderUpdaters = FindObjectsByType<VirtualSurfaceColorShaderUpdater>(FindObjectsSortMode.None);
            foreach (var updater in shaderUpdaters)
            {
                Debug.Log($"{DEBUG_TAG}UpdateActivePlayerRpc - Updating shader for {updater.gameObject.name}");
                updater.UpdateShaderProperties();
            }
        }
        else
        {
            Debug.LogWarning($"{DEBUG_TAG}UpdateActivePlayerRpc - PlayerColorManager instance not found!");
        }
    }

    /// <summary>
    /// Counts how many seats are currently occupied.
    /// </summary>
    private int CountOccupiedSeats()
    {
        Debug.Log($"{DEBUG_TAG}CountOccupiedSeats - Checking {networkedSeats.Count} seats");
        int count = 0;
        for (int i = 0; i < networkedSeats.Count; i++)
        {
            var seat = networkedSeats[i];
            Debug.Log($"{DEBUG_TAG}CountOccupiedSeats - Seat {i}: isOccupied={seat.isOccupied}, playerID={seat.playerID}");
            if (seat.isOccupied)
                count++;
        }
        Debug.Log($"{DEBUG_TAG}CountOccupiedSeats - Found {count} occupied seats");
        return count;
    }

    void UpdateNetworkedSeatsVisuals()
    {
        Debug.Log($"{DEBUG_TAG}UpdateNetworkedSeatsVisuals - Updating {networkedSeats.Count} seats, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}");

        for (int i = 0; i < networkedSeats.Count; i++)
        {
            Debug.Log($"{DEBUG_TAG}UpdateNetworkedSeatsVisuals - Seat {i}: isOccupied={networkedSeats[i].isOccupied}, playerID={networkedSeats[i].playerID}");

            if (!networkedSeats[i].isOccupied)
            {
                Debug.Log($"{DEBUG_TAG}UpdateNetworkedSeatsVisuals - Setting seat {i} as unoccupied");
                m_SeatButtons[i].SetOccupied(false);
            }
            else
            {
                Debug.Log($"{DEBUG_TAG}UpdateNetworkedSeatsVisuals - Trying to find player with ID {networkedSeats[i].playerID} for seat {i}");
                if (XRINetworkGameManager.Instance.TryGetPlayerByID(networkedSeats[i].playerID, out var player))
                {
                    Debug.Log($"{DEBUG_TAG}UpdateNetworkedSeatsVisuals - Found player {player.playerName} (ID: {networkedSeats[i].playerID}), assigning to seat {i}");
                    m_SeatButtons[i].AssignPlayerToSeat(player);
                }
                else
                {
                    Debug.LogError($"{DEBUG_TAG}UpdateNetworkedSeatsVisuals - Player with id {networkedSeats[i].playerID} not found for seat {i}!");
                }
            }
        }
        Debug.Log($"{DEBUG_TAG}UpdateNetworkedSeatsVisuals - Finished updating seat visuals");
    }

    public void RequestAnySeatFromHost()
    {
        Debug.Log($"{DEBUG_TAG}RequestAnySeatFromHost - LocalClientId: {NetworkManager.Singleton.LocalClientId}, CurrentSeat: {TableTop.k_CurrentSeat}");
        RequestSeatServerRpc(NetworkManager.Singleton.LocalClientId, TableTop.k_CurrentSeat);
    }

    public void RequestSeat(int newSeatChoice)
    {
        int activePlayers = GetActivePlayerCount();
        // Convert logical seat choice to physical if needed
        int physicalSeatChoice = m_TableTop.GetPhysicalSeatIndex(newSeatChoice, activePlayers);
        Debug.Log($"{DEBUG_TAG}RequestSeat - Logical seat: {newSeatChoice}, Physical seat: {physicalSeatChoice}, ActivePlayers: {activePlayers}");
        RequestSeatServerRpc(NetworkManager.Singleton.LocalClientId, TableTop.k_CurrentSeat, physicalSeatChoice);
    }

    /// <summary>
    /// Gets the number of active players based on occupied seats.
    /// </summary>
    private int GetActivePlayerCount()
    {
        int occupiedSeats = CountOccupiedSeats();
        int result = Mathf.Min(8, occupiedSeats);
        Debug.Log($"{DEBUG_TAG}GetActivePlayerCount - Occupied seats: {occupiedSeats}, Result: {result}");
        return result;
    }

    /// <summary>
    /// Gets the network-synchronized player count for use by other components.
    /// This is the authoritative player count that determines seat positioning.
    /// </summary>
    /// <returns>The network-synchronized player count</returns>
    public int GetNetworkSynchronizedPlayerCount()
    {
        int result = m_ActivePlayerCount.Value;
        Debug.Log($"{DEBUG_TAG}GetNetworkSynchronizedPlayerCount - Returning {result}, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}");
        return result;
    }

    [Rpc(SendTo.Server)]
    void RequestSeatServerRpc(ulong localPlayerID, int currentSeatID, int newSeatID = -2)
    {
        Debug.Log($"{DEBUG_TAG}RequestSeatServerRpc - PlayerID: {localPlayerID}, CurrentSeatID: {currentSeatID}, RequestedSeatID: {newSeatID}, IsServer: {IsServer}, IsHost: {IsHost}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        if (newSeatID <= -2)    // Request any available seat
        {
            Debug.Log($"{DEBUG_TAG}RequestSeatServerRpc - Requesting any available seat");
            newSeatID = GetAnyAvailableSeats();
            Debug.Log($"{DEBUG_TAG}RequestSeatServerRpc - Got available seat: {newSeatID}");
        }

        if (!IsSeatOccupied(newSeatID))
        {
            Debug.Log($"{DEBUG_TAG}RequestSeatServerRpc - Seat {newSeatID} is not occupied, assigning to player {localPlayerID}");
            ServerAssignSeat(currentSeatID, newSeatID, localPlayerID);
        }
        else
        {
            Debug.LogWarning($"{DEBUG_TAG}RequestSeatServerRpc - User {localPlayerID} tried to join occupied seat {newSeatID}");
        }
    }

    int GetAnyAvailableSeats()
    {
        int availableSeat = -1;
        Debug.Log($"{DEBUG_TAG}GetAnyAvailableSeats - Checking {networkedSeats.Count} seats");

        // Get the current active player count
        int occupiedSeats = CountOccupiedSeats();
        int activePlayers = GetActivePlayerCount();
        Debug.Log($"{DEBUG_TAG}GetAnyAvailableSeats - Occupied seats: {occupiedSeats}, Active players: {activePlayers}");

        // Preferred seating order for 4 or fewer players
        // This maintains the original seat assignment preference
        if (occupiedSeats < 4)
        {
            int[] preferredOrder = { 0, 1, 2, 3 };
            Debug.Log($"{DEBUG_TAG}GetAnyAvailableSeats - Using preferred order for 4 or fewer players");

            // Try to assign seats in the preferred order
            foreach (int seatIndex in preferredOrder)
            {
                if (seatIndex < networkedSeats.Count && !networkedSeats[seatIndex].isOccupied)
                {
                    availableSeat = seatIndex;
                    Debug.Log($"{DEBUG_TAG}GetAnyAvailableSeats - Found preferred available seat: {availableSeat}");
                    return availableSeat;
                }
            }
        }

        // Fall back to first available seat for 5+ players or if preferred seats are taken
        for (int i = 0; i < networkedSeats.Count; i++)
        {
            Debug.Log($"{DEBUG_TAG}GetAnyAvailableSeats - Seat {i} occupied: {networkedSeats[i].isOccupied}");
            if (!networkedSeats[i].isOccupied)
            {
                availableSeat = i;
                Debug.Log($"{DEBUG_TAG}GetAnyAvailableSeats - Found available seat: {availableSeat}");
                return availableSeat;
            }
        }

        Debug.LogWarning($"{DEBUG_TAG}GetAnyAvailableSeats - No available seats found!");
        return availableSeat;
    }

    bool IsSeatOccupied(int seatID)
    {
        bool result = seatID >= 0 && networkedSeats[seatID].isOccupied;
        Debug.Log($"{DEBUG_TAG}IsSeatOccupied - SeatID: {seatID}, Result: {result}");
        return result;
    }

    void ServerAssignSeat(int currentSeatID, int newSeatID, ulong localPlayerID)
    {
        Debug.Log($"{DEBUG_TAG}ServerAssignSeat - CurrentSeatID: {currentSeatID}, NewSeatID: {newSeatID}, PlayerID: {localPlayerID}");

        if (currentSeatID >= 0)
        {
            Debug.Log($"{DEBUG_TAG}ServerAssignSeat - Removing player from current seat {currentSeatID}");
            ServerRemoveSeat(currentSeatID);
        }

        if (newSeatID >= 0)
        {
            Debug.Log($"{DEBUG_TAG}ServerAssignSeat - Assigning player {localPlayerID} to seat {newSeatID}");
            networkedSeats[newSeatID] = new NetworkedSeat { isOccupied = true, playerID = localPlayerID };
        }

        Debug.Log($"{DEBUG_TAG}ServerAssignSeat - Updating networked seats visuals");
        UpdateNetworkedSeatsVisuals();

        // Update seat positions when a player joins
        Debug.Log($"{DEBUG_TAG}ServerAssignSeat - Updating seat positions based on player count");
        UpdateSeatPositionsBasedOnPlayerCount();

        Debug.Log($"{DEBUG_TAG}ServerAssignSeat - Calling AssignSeatRpc for seat {newSeatID}, player {localPlayerID}");
        AssignSeatRpc(newSeatID, localPlayerID);
    }

    void ServerRemoveSeat(int seatID)
    {
        Debug.Log($"{DEBUG_TAG}ServerRemoveSeat - SeatID: {seatID}");
        networkedSeats[seatID] = new NetworkedSeat { isOccupied = false, playerID = 0 };
        UpdateNetworkedSeatsVisuals();
        Debug.Log($"{DEBUG_TAG}ServerRemoveSeat - Calling RemovePlayerFromSeatRpc for seat {seatID}");
        RemovePlayerFromSeatRpc(seatID);
    }

    [Rpc(SendTo.Everyone)]
    void RemovePlayerFromSeatRpc(int seatID)
    {
        Debug.Log($"{DEBUG_TAG}RemovePlayerFromSeatRpc - SeatID: {seatID}, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");
        m_SeatButtons[seatID].RemovePlayerFromSeat();
    }

    [Rpc(SendTo.Everyone)]
    void AssignSeatRpc(int seatID, ulong playerID)
    {
        Debug.Log($"{DEBUG_TAG}AssignSeatRpc - SeatID: {seatID}, PlayerID: {playerID}, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}, LocalClientId: {NetworkManager.Singleton.LocalClientId}, BuildType: {(Application.isEditor ? "Editor" : "Build")}");

        if (XRINetworkGameManager.Instance.TryGetPlayerByID(playerID, out var player))
        {
            Debug.Log($"{DEBUG_TAG}AssignSeatRpc - Found player {player.playerName} (ID: {playerID}), assigning to seat {seatID}");
            m_SeatButtons[seatID].AssignPlayerToSeat(player);

            if (playerID == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"{DEBUG_TAG}AssignSeatRpc - This is the local player, teleporting to seat {seatID}");

                // Ensure TableSeatSystem is valid
                if (m_SeatSystem != null)
                {
                    // Get the active player count for proper positioning
                    int activePlayers = GetActivePlayerCount();
                    Debug.Log($"{DEBUG_TAG}AssignSeatRpc - Active player count: {activePlayers}, teleporting to seat {seatID}");

                    // Teleport to the seat
                    m_SeatSystem.TeleportToSeat(seatID);
                    Debug.Log($"{DEBUG_TAG}AssignSeatRpc - Teleported to seat {seatID}, current seat: {TableTop.k_CurrentSeat}");

                    // Verify the teleportation was successful
                    if (TableTop.k_CurrentSeat == seatID)
                    {
                        Debug.Log($"{DEBUG_TAG}AssignSeatRpc - Teleportation successful, seat {seatID} confirmed");
                    }
                    else
                    {
                        Debug.LogWarning($"{DEBUG_TAG}AssignSeatRpc - Teleportation may have failed, expected seat {seatID} but got {TableTop.k_CurrentSeat}");
                    }
                }
                else
                {
                    Debug.LogError($"{DEBUG_TAG}AssignSeatRpc - m_SeatSystem is null! Cannot teleport to seat {seatID}");
                }
            }
            else
            {
                Debug.Log($"{DEBUG_TAG}AssignSeatRpc - This is not the local player (local: {NetworkManager.Singleton.LocalClientId}, player: {playerID}), skipping teleportation");
            }
        }
        else
        {
            Debug.LogError($"{DEBUG_TAG}AssignSeatRpc - Player with id {playerID} not found!");
        }
    }

    public void TeleportToSpectatorSeat()
    {
        RequestSeat(-1);
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
