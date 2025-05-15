using System;
using Unity.Netcode;
using UnityEngine;
using XRMultiplayer;
using MRTabletopAssets;

public class NetworkTableTopManager : NetworkBehaviour
{
    public NetworkList<NetworkedSeat> networkedSeats;

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

        if (IsServer)
        {
            networkedSeats.Clear();
            for (int i = 0; i < m_SeatButtons.Length; i++)
            {
                networkedSeats.Add(new NetworkedSeat { isOccupied = false, playerID = 0 });
            }

            // Initialize seat positions based on active player count
            // Start with 4 seats by default
            int seatsToShow = 4;

            // For now, we'll just use the default 4-player layout
            // In a real implementation, you would determine the actual player count

            Debug.Log($"NetworkTableTopManager initializing {seatsToShow} seats");
            m_TableTop.UpdateSeatPositions(seatsToShow);
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
    private void UpdateSeatPositionsBasedOnPlayerCount()
    {
        if (IsServer && m_TableTop != null)
        {
            int occupiedSeatCount = CountOccupiedSeats();

            // Determine how many seats to show based on occupied seats
            int seatsToShow;

            if (occupiedSeatCount <= 4)
            {
                // For 1-4 players, use the standard 4-player layout
                seatsToShow = 4;
            }
            else
            {
                // For 5-8 players, use the exact number of players
                // This creates a pentagon for 5, hexagon for 6, etc.
                seatsToShow = occupiedSeatCount;
                seatsToShow = Mathf.Min(8, seatsToShow); // Cap at 8 players
            }

            Debug.Log($"Updating seat positions for {seatsToShow} seats (occupied seats: {occupiedSeatCount})");
            m_TableTop.UpdateSeatPositions(seatsToShow);

            // Update VirtualSurfaceColorShaderUpdater components with new player count
            UpdateShaderComponents(seatsToShow);

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
                        break;
                    }
                }

                MRTabletopAssets.PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;

                // Trigger color palette changed event to update all UI elements
                MRTabletopAssets.PlayerColorManager.Instance.NotifyColorPaletteChanged();
            }
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
    /// Counts how many seats are currently occupied.
    /// </summary>
    private int CountOccupiedSeats()
    {
        int count = 0;
        foreach (var seat in networkedSeats)
        {
            if (seat.isOccupied)
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
    private int GetActivePlayerCount()
    {
        int occupiedSeats = CountOccupiedSeats();
        return Mathf.Min(8, occupiedSeats);
    }

    [Rpc(SendTo.Server)]
    void RequestSeatServerRpc(ulong localPlayerID, int currentSeatID, int newSeatID = -2)
    {
        if (newSeatID <= -2)    // Request any available seat
            newSeatID = GetAnyAvailableSeats();

        if (!IsSeatOccupied(newSeatID))
            ServerAssignSeat(currentSeatID, newSeatID, localPlayerID);
        else
            Debug.Log("User tried to join an occupied seat");
    }

    int GetAnyAvailableSeats()
    {
        int availableSeat = -1;
        for (int i = 0; i < networkedSeats.Count; i++)
        {
            if (!networkedSeats[i].isOccupied)
            {
                availableSeat = i;
                return availableSeat;
            }
        }

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
                m_SeatSystem.TeleportToSeat(seatID);
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
