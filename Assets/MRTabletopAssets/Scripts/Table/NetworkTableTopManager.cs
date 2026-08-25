using System;
using Unity.Netcode;
using XRMultiplayer;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    public class NetworkTableTopManager : NetworkBehaviour
    {
        public NetworkList<NetworkedSeat> networkedSeats;

        /// <summary>
        /// The replicated table layout: how many seats the table currently
        /// shows (the table configuration, NOT the number of players). Written
        /// only by the server; every peer (host included) applies it through
        /// <see cref="ApplySeatLayout"/>.
        /// </summary>
        NetworkVariable<int> m_SeatCount = new NetworkVariable<int>(4, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField]
        TableSeatSystem m_SeatSystem;

        [SerializeField]
        TableTop m_TableTop;

        public TableTop tableTop => m_TableTop;

        [SerializeField]
        TableLayoutConfig m_Config;

        [SerializeField]
        PlayerRepositionManager m_PlayerRepositionManager;

        [SerializeField]
        TableTopSeatButton[] m_SeatButtons;

        bool m_ServerIsCollapsing;

        void Awake()
        {
            networkedSeats = new NetworkList<NetworkedSeat>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            m_SeatCount.OnValueChanged += OnSeatCountChanged;

            if (IsServer)
            {
                networkedSeats.Clear();
                for (int i = 0; i < m_SeatButtons.Length; i++)
                {
                    networkedSeats.Add(new NetworkedSeat { isOccupied = false, playerID = 0 });
                }

                m_SeatCount.Value = MinimumSeatCount();
            }

            // Apply the replicated layout before requesting a seat so late
            // joiners spawn onto the correct table.
            ApplySeatLayout(m_SeatCount.Value, animate: false);

            UpdateNetworkedSeatsVisuals();
            networkedSeats.OnListChanged += OnOccupiedSeatsChanged;
            RequestAnySeatFromHost();

            // Every peer listens: on the server this drives occupancy cleanup;
            // on all peers a newly spawned player triggers a visuals pass for
            // seats whose player object had not spawned yet.
            XRINetworkGameManager.Instance.playerStateChanged += OnPlayerStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            m_SeatCount.OnValueChanged -= OnSeatCountChanged;

            // Reset the layout so the next session starts from the base table.
            m_SeatSystem.SetTableScale(1f);
            m_TableTop.SetSeatLayout(MinimumSeatCount());

            foreach (var seatButton in m_SeatButtons)
            {
                seatButton.RemovePlayerFromSeat();
            }
            networkedSeats.OnListChanged -= OnOccupiedSeatsChanged;
            XRINetworkGameManager.Instance.playerStateChanged -= OnPlayerStateChanged;
            m_SeatSystem.TeleportToSeat(0);
            TableTop.k_CurrentSeat = -2;
        }

        int MinimumSeatCount()
        {
            return m_Config != null ? m_Config.minimumSeatCount : 4;
        }

        void OnOccupiedSeatsChanged(NetworkListEvent<NetworkedSeat> changeEvent)
        {
            UpdateNetworkedSeatsVisuals();
        }

        void OnSeatCountChanged(int previousValue, int newValue)
        {
            ApplySeatLayout(newValue, animate: true);
        }

        /// <summary>
        /// Applies a seat layout locally: seat transforms and rim shape via
        /// the TableTop, table scale via the seat system. Runs on every peer,
        /// host included. Absolute and idempotent; the vignette fade is used
        /// only when the layout actually changes.
        /// </summary>
        void ApplySeatLayout(int seatCount, bool animate)
        {
            float scale = m_Config != null ? m_Config.ScaleFor(seatCount) : 1f;
            bool changed = seatCount != m_TableTop.currentSeatCount || !Mathf.Approximately(scale, m_SeatSystem.tableScale);

            void Apply()
            {
                if (m_Config != null)
                    m_TableTop.SetLayoutConfig(m_Config);
                m_TableTop.SetSeatLayout(seatCount);
                m_SeatSystem.SetTableScale(scale);
            }

            if (animate && changed && m_PlayerRepositionManager != null)
                m_PlayerRepositionManager.RunWithVignette(Apply);
            else
                Apply();
        }

        void OnPlayerStateChanged(ulong playerID, bool connected)
        {
            if (connected)
            {
                // A player object finished spawning; reconcile seat visuals
                // that may have been skipped while it was missing.
                UpdateNetworkedSeatsVisuals();
                return;
            }

            if (!IsServer)
                return;

            for (int i = 0; i < networkedSeats.Count; i++)
            {
                if (networkedSeats[i].isOccupied && networkedSeats[i].playerID == playerID)
                {
                    ServerRemoveSeat(i);
                }
            }

            ServerUpdateSeatLayout();
        }

        /// <summary>
        /// Server-side: recomputes the target layout after any occupancy
        /// change. On collapse, players seated beyond the new count are moved
        /// to the lowest free seats first.
        /// </summary>
        void ServerUpdateSeatLayout()
        {
            if (!IsServer || m_Config == null || m_ServerIsCollapsing)
                return;

            int target = m_Config.TargetSeatCount(OccupiedSeatMask(), m_SeatCount.Value);
            if (target == m_SeatCount.Value)
                return;

            if (target < m_SeatCount.Value)
            {
                m_ServerIsCollapsing = true;
                for (int i = target; i < networkedSeats.Count; i++)
                {
                    if (!networkedSeats[i].isOccupied)
                        continue;

                    int freeSeat = FirstFreeSeat(target);
                    if (freeSeat < 0)
                        break;

                    ServerAssignSeat(i, freeSeat, networkedSeats[i].playerID);
                }
                m_ServerIsCollapsing = false;
            }

            m_SeatCount.Value = target;
        }

        int OccupiedSeatMask()
        {
            int mask = 0;
            for (int i = 0; i < networkedSeats.Count; i++)
            {
                if (networkedSeats[i].isOccupied)
                    mask |= 1 << i;
            }
            return mask;
        }

        int FirstFreeSeat(int seatCount)
        {
            for (int i = 0; i < seatCount && i < networkedSeats.Count; i++)
            {
                if (!networkedSeats[i].isOccupied)
                    return i;
            }
            return -1;
        }

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
            // Silent lookup: TryGetPlayerByID logs errors for players whose
            // objects have not spawned on this peer yet, which is a normal
            // transient state during joins.
            var spawnedPlayers = FindObjectsByType<XRINetworkPlayer>();

            for (int i = 0; i < networkedSeats.Count; i++)
            {
                if (!networkedSeats[i].isOccupied)
                {
                    m_SeatButtons[i].SetOccupied(false);
                    continue;
                }

                var player = FindByOwner(spawnedPlayers, networkedSeats[i].playerID);
                if (player != null)
                {
                    m_SeatButtons[i].AssignPlayerToSeat(player);
                }
                // else: not spawned on this peer yet; playerStateChanged
                // re-runs this pass when it does.
            }
        }

        static XRINetworkPlayer FindByOwner(XRINetworkPlayer[] players, ulong playerID)
        {
            foreach (var player in players)
            {
                if (player.OwnerClientId == playerID)
                    return player;
            }
            return null;
        }

        public void RequestAnySeatFromHost()
        {
            RequestSeatServerRpc(NetworkManager.Singleton.LocalClientId, TableTop.k_CurrentSeat);
        }

        public void RequestSeat(int newSeatChoice)
        {
            RequestSeatServerRpc(NetworkManager.Singleton.LocalClientId, TableTop.k_CurrentSeat, newSeatChoice);
        }

        /// <summary>
        /// The replicated seat count (table configuration, not player count).
        /// </summary>
        public int GetNetworkSynchronizedSeatCount()
        {
            return m_SeatCount.Value;
        }

        [Rpc(SendTo.Server)]
        void RequestSeatServerRpc(ulong localPlayerID, int currentSeatID, int newSeatID = -2)
        {
            if (newSeatID <= -2)    // Request any available seat
            {
                newSeatID = GetAnyAvailableSeat();

                if (newSeatID < 0 && m_Config != null)
                {
                    // Table full: grow to the smallest supported count with room.
                    int grownCount = m_Config.CountForOccupancy(CountOccupiedSeats() + 1);
                    if (grownCount > m_SeatCount.Value)
                    {
                        m_SeatCount.Value = grownCount;
                        newSeatID = GetAnyAvailableSeat();
                    }
                }

                if (newSeatID < 0)
                {
                    Debug.LogWarning($"User {localPlayerID} requested a seat but no supported layout has room; they remain a spectator");
                    return;
                }
            }

            if (newSeatID >= m_SeatCount.Value)
            {
                Debug.LogWarning($"User {localPlayerID} requested seat {newSeatID}, beyond the current {m_SeatCount.Value}-seat layout");
                return;
            }

            if (!IsSeatOccupied(newSeatID))
                ServerAssignSeat(currentSeatID, newSeatID, localPlayerID);
            else
                Debug.LogWarning($"User {localPlayerID} tried to join occupied seat {newSeatID}");
        }

        int GetAnyAvailableSeat()
        {
            return FirstFreeSeat(m_SeatCount.Value);
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
            ServerUpdateSeatLayout();

            // The active (highlighted) seat starts as the host's seat; games
            // move it later via PlayerColorManager.SetActiveSeat.
            if (newSeatID >= 0 && localPlayerID == NetworkManager.ServerClientId
                && PlayerColorManager.Instance != null && PlayerColorManager.Instance.ActiveSeat < 0)
            {
                PlayerColorManager.Instance.SetActiveSeat(newSeatID);
            }

            AssignSeatRpc(newSeatID, localPlayerID);
        }

        void ServerRemoveSeat(int seatID)
        {
            networkedSeats[seatID] = new NetworkedSeat { isOccupied = false, playerID = 0 };
            if (PlayerColorManager.Instance != null)
                PlayerColorManager.Instance.CancelRequestsForSeat(seatID);
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
            if (seatID >= 0)
            {
                var player = FindByOwner(FindObjectsByType<XRINetworkPlayer>(), playerID);
                if (player != null)
                    m_SeatButtons[seatID].AssignPlayerToSeat(player);
                // else: not spawned on this peer yet; visuals reconcile via
                // playerStateChanged. The local teleport below never hits
                // this race (the local player always exists locally).
            }

            if (playerID == NetworkManager.Singleton.LocalClientId)
            {
                m_SeatSystem.TeleportToSeat(seatID);
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
}
