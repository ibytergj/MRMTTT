using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Host-authoritative per-seat color state, replicated to all peers.
    /// Player color == seat color: each seat starts with its default palette
    /// color, and every seat-colored surface (rim, avatars, seat buttons,
    /// hover visuals) reads from here. Requires a NetworkObject on the same
    /// GameObject. Server-arbitrated color changes and the transfer protocol
    /// are documented in Documentation/8Player/Design.md.
    /// </summary>
    public class PlayerColorManager : NetworkBehaviour
    {
        public static PlayerColorManager Instance { get; private set; }

        [SerializeField]
        [Tooltip("Default per-seat colors. Games assign their own palette asset.")]
        PlayerColorPalette m_Palette;

        [Header("Active Player Highlighting (local)")]
        [SerializeField, Range(0f, 1f)] float m_GlowIntensity = 0.3f;
        [SerializeField] bool m_EnablePulse = true;
        [SerializeField] AccessibilityHighlightMode m_HighlightMode = AccessibilityHighlightMode.Combined;
        [SerializeField, Range(0f, 1f)] float m_PatternIntensity = 0.5f;
        [SerializeField, Range(0.1f, 2f)] float m_AnimationSpeed = 1.0f;
        [SerializeField, Range(0f, 1f)] float m_BrightnessPulseIntensity = 0.3f;

        public enum AccessibilityHighlightMode
        {
            ColorContrast = 0,
            Pattern = 1,
            Animation = 2,
            Combined = 3,
        }

        const float k_TransferTimeoutSeconds = 30f;

        NetworkList<Color> m_SeatColors;
        NetworkVariable<int> m_ActiveSeat = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        struct PendingColorRequest
        {
            public int requestId;
            public int requesterSeat;
            public int holderSeat;
            public Color color;
            public ulong requesterClientId;
            public float createdTime;
        }

        readonly List<PendingColorRequest> m_PendingRequests = new List<PendingColorRequest>();
        int m_NextRequestId = 1;

        /// <summary>Raised on every peer when one seat's color changes.</summary>
        public event Action<int, Color> OnSeatColorChanged;

        /// <summary>Raised on every peer when the active (highlighted) seat changes.</summary>
        public event Action<int> OnActiveSeatChanged;

        /// <summary>Raised on every peer after any palette change (bulk or single).</summary>
        public event Action OnColorPaletteChanged;

        public float GlowIntensity => m_GlowIntensity;
        public bool EnablePulse => m_EnablePulse;
        public AccessibilityHighlightMode HighlightMode => m_HighlightMode;
        public float PatternIntensity => m_PatternIntensity;
        public float AnimationSpeed => m_AnimationSpeed;
        public float BrightnessPulseIntensity => m_BrightnessPulseIntensity;

        /// <summary>The highlighted seat, -1 before the host is seated.</summary>
        public int ActiveSeat => m_ActiveSeat.Value;

        /// <summary>Glow color for the active seat: always contrasts with the seat color.</summary>
        public Color ActiveGlowColor => ActiveSeat >= 0 ? GetContrastColor(ActiveSeat) : Color.white;

        void Awake()
        {
            Instance = this;
            m_SeatColors = new NetworkList<Color>();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                m_SeatColors.Clear();
                foreach (var color in DefaultColors())
                    m_SeatColors.Add(color);
            }

            m_SeatColors.OnListChanged += OnSeatColorsChanged;
            m_ActiveSeat.OnValueChanged += OnActiveSeatValueChanged;
            OnColorPaletteChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            m_SeatColors.OnListChanged -= OnSeatColorsChanged;
            m_ActiveSeat.OnValueChanged -= OnActiveSeatValueChanged;
            m_PendingRequests.Clear();
            OnColorPaletteChanged?.Invoke();
        }

        void Update()
        {
            if (!IsServer || m_PendingRequests.Count == 0)
                return;

            for (int i = m_PendingRequests.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime - m_PendingRequests[i].createdTime >= k_TransferTimeoutSeconds)
                    ResolveRequest(m_PendingRequests[i], accepted: false);
            }
        }

        Color[] DefaultColors()
        {
            return m_Palette != null ? m_Palette.NormalizedColors() : PlayerColorPalette.FallbackColors();
        }

        /// <summary>The seat's current color (replicated when in session, palette default otherwise).</summary>
        public Color GetPlayerColor(int seatIndex)
        {
            if (IsSpawned && seatIndex >= 0 && seatIndex < m_SeatColors.Count)
                return m_SeatColors[seatIndex];

            var defaults = DefaultColors();
            return defaults[Mathf.Clamp(seatIndex, 0, defaults.Length - 1)];
        }

        public Color[] GetAllPlayerColors()
        {
            if (!IsSpawned)
                return DefaultColors();

            var colors = new Color[m_SeatColors.Count];
            for (int i = 0; i < m_SeatColors.Count; i++)
                colors[i] = m_SeatColors[i];
            return colors;
        }

        /// <summary>Black or white, whichever contrasts with the seat's color.</summary>
        public Color GetContrastColor(int seatIndex)
        {
            return ContrastColor.For(GetPlayerColor(seatIndex));
        }

        /// <summary>Server API: moves the animated highlight. Games call this to pass the turn.</summary>
        public void SetActiveSeat(int seatIndex)
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerColorManager: SetActiveSeat is server-only");
                return;
            }

            m_ActiveSeat.Value = seatIndex;
        }

        /// <summary>Server API: assigns a color to a seat directly (e.g. host palette editing).</summary>
        public void SetSeatColor(int seatIndex, Color color)
        {
            if (!IsServer)
            {
                Debug.LogWarning("PlayerColorManager: SetSeatColor is server-only");
                return;
            }

            if (seatIndex >= 0 && seatIndex < m_SeatColors.Count)
                m_SeatColors[seatIndex] = color;
        }

        /// <summary>
        /// A seated player asks for a free color. The server rejects colors
        /// held by another occupied seat (use the transfer protocol instead).
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestSeatColorRpc(int seatIndex, Color color, RpcParams rpcParams = default)
        {
            int holder = SeatHoldingColor(color);
            if (holder >= 0 && holder != seatIndex)
                return; // Held by someone else: requester must negotiate a transfer.

            SetSeatColor(seatIndex, color);
        }

        /// <summary>
        /// Step 2 of the transfer protocol: a seated player wants a color
        /// another seat holds. The server records one pending request per
        /// color and relays it to the holder only.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestColorTransferRpc(int requesterSeat, Color color, RpcParams rpcParams = default)
        {
            int holderSeat = SeatHoldingColor(color);
            if (holderSeat < 0 || holderSeat == requesterSeat)
                return;

            // One outstanding request per color and per requester.
            foreach (var pending in m_PendingRequests)
            {
                if (pending.color == color || pending.requesterSeat == requesterSeat)
                    return;
            }

            var request = new PendingColorRequest
            {
                requestId = m_NextRequestId++,
                requesterSeat = requesterSeat,
                holderSeat = holderSeat,
                color = color,
                requesterClientId = rpcParams.Receive.SenderClientId,
                createdTime = Time.unscaledTime,
            };
            m_PendingRequests.Add(request);

            var holderClientId = ClientIdForSeat(holderSeat);
            if (holderClientId.HasValue)
                ColorTransferRequestedRpc(request.requestId, requesterSeat, color, RpcTarget.Single(holderClientId.Value, RpcTargetUse.Temp));
            else
                ResolveRequest(request, accepted: false);
        }

        /// <summary>Sent to the color holder only: someone is asking for their color.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void ColorTransferRequestedRpc(int requestId, int requesterSeat, Color color, RpcParams rpcParams = default)
        {
            // Picker UI hook (follow-on work): show the accept/deny prompt.
            Debug.Log($"PlayerColorManager: seat {requesterSeat} requests this player's color (request {requestId})");
        }

        /// <summary>Step 3: the holder accepts or denies.</summary>
        [Rpc(SendTo.Server)]
        public void RespondColorTransferRpc(int requestId, bool accepted, RpcParams rpcParams = default)
        {
            for (int i = 0; i < m_PendingRequests.Count; i++)
            {
                if (m_PendingRequests[i].requestId == requestId)
                {
                    ResolveRequest(m_PendingRequests[i], accepted);
                    return;
                }
            }
        }

        /// <summary>Sent to the requester only when their request is denied or times out.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void ColorTransferDeniedRpc(int requestId, RpcParams rpcParams = default)
        {
            // Picker UI hook (follow-on work): show the denial notice.
        }

        /// <summary>Server-side: a seat was vacated; cancel its pending requests.</summary>
        public void CancelRequestsForSeat(int seatIndex)
        {
            if (!IsServer)
                return;

            for (int i = m_PendingRequests.Count - 1; i >= 0; i--)
            {
                if (m_PendingRequests[i].requesterSeat == seatIndex || m_PendingRequests[i].holderSeat == seatIndex)
                    ResolveRequest(m_PendingRequests[i], accepted: false);
            }
        }

        void ResolveRequest(PendingColorRequest request, bool accepted)
        {
            m_PendingRequests.Remove(request);

            if (!accepted)
            {
                ColorTransferDeniedRpc(request.requestId, RpcTarget.Single(request.requesterClientId, RpcTargetUse.Temp));
                return;
            }

            // The requester takes the color; the releasing seat gets the next
            // free default palette color.
            SetSeatColor(request.requesterSeat, request.color);
            SetSeatColor(request.holderSeat, NextFreeDefaultColor());
        }

        int SeatHoldingColor(Color color)
        {
            for (int i = 0; i < m_SeatColors.Count; i++)
            {
                if (m_SeatColors[i] == color)
                    return i;
            }
            return -1;
        }

        Color NextFreeDefaultColor()
        {
            foreach (var candidate in DefaultColors())
            {
                if (SeatHoldingColor(candidate) < 0)
                    return candidate;
            }
            return Color.gray;
        }

        ulong? ClientIdForSeat(int seatIndex)
        {
            var manager = FindAnyObjectByType<NetworkTableTopManager>();
            if (manager == null || seatIndex < 0 || seatIndex >= manager.networkedSeats.Count)
                return null;

            var seat = manager.networkedSeats[seatIndex];
            return seat.isOccupied ? seat.playerID : (ulong?)null;
        }

        void OnSeatColorsChanged(NetworkListEvent<Color> changeEvent)
        {
            if (changeEvent.Type == NetworkListEvent<Color>.EventType.Value)
                OnSeatColorChanged?.Invoke(changeEvent.Index, changeEvent.Value);
            OnColorPaletteChanged?.Invoke();
        }

        void OnActiveSeatValueChanged(int previousValue, int newValue)
        {
            OnActiveSeatChanged?.Invoke(newValue);
        }
    }
}
