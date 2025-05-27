using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MRTabletopAssets
{
    /// <summary>
    /// Centralizes player color definitions and provides access to player colors throughout the codebase.
    /// Implemented as a singleton for easy access with host-authoritative color management.
    /// </summary>
    public class PlayerColorManager : NetworkBehaviour
    {
        // Singleton instance
        public static PlayerColorManager Instance { get; private set; }

        // Default player colors (8 distinct colors)
        [SerializeField] private Color[] m_PlayerColors = new Color[8];

        // Active player index for highlighting
        [SerializeField] private int m_ActivePlayerIndex = -1;
        private NetworkVariable<int> m_NetworkActivePlayerIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Active player highlighting settings
        [Header("Active Player Highlighting")]
        [SerializeField] private Color m_ActivePlayerGlowColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float m_ActivePlayerGlowIntensity = 0.3f;
        [SerializeField, Range(0.1f, 2f)] private float m_ActivePlayerPulseSpeed = 1.0f;
        [SerializeField] private bool m_EnablePulseAnimation = true;

        [Header("Accessibility Settings")]
        [Tooltip("The highlighting method to use for active player indication")]
        [SerializeField] private AccessibilityHighlightMode m_HighlightMode = AccessibilityHighlightMode.Combined;
        [Tooltip("Use color contrast optimized for color blindness")]
        [SerializeField] private bool m_UseColorBlindFriendlyContrast = true;
        [Tooltip("Intensity of the pattern overlay")]
        [SerializeField, Range(0f, 1f)] private float m_PatternIntensity = 0.5f;
        [Tooltip("Speed of the pattern animation")]
        [SerializeField, Range(0.1f, 2f)] private float m_AnimationSpeed = 1.0f;
        [Tooltip("Intensity of the brightness pulsing effect")]
        [SerializeField, Range(0f, 1f)] private float m_BrightnessPulseIntensity = 0.3f;

        // Enum for accessibility highlight modes
        public enum AccessibilityHighlightMode
        {
            ColorContrast = 0,
            Pattern = 1,
            Animation = 2,
            Combined = 3
        }

        // Events for color changes
        public event Action<ulong, Color> OnPlayerColorChanged;
        public event Action<ulong, Color, Color> OnPlayerColorConflictResolved;
        public event Action<ulong, int, int> OnPlayerSeatChanged;
        public event Action<int, Color> OnSeatColorChanged;
        public event Action<int> OnActivePlayerChanged;
        public event Action OnColorPaletteChanged;
        public event Action OnHighlightSettingsChanged;

        // Tracking dictionaries
        private Dictionary<Color, ulong> m_ColorToPlayerID = new Dictionary<Color, ulong>();
        private Dictionary<ulong, bool> m_PlayerHasCustomColor = new Dictionary<ulong, bool>();
        private Dictionary<ulong, Color> m_PlayerPreferredColor = new Dictionary<ulong, Color>();
        private Dictionary<ulong, int> m_PlayerToSeatIndex = new Dictionary<ulong, int>();

        // Network synchronization
        private NetworkList<Color> m_HostColorPalette;
        private bool m_InNetworkedSession = false;
        private Color[] m_LocalColorPaletteBackup = new Color[8];

        // Property for active player index with event trigger
        public int ActivePlayerIndex
        {
            get => m_ActivePlayerIndex;
            set => SetActivePlayerIndex(value);
        }

        // Active player highlighting properties
        public Color ActivePlayerGlowColor
        {
            get => m_ActivePlayerGlowColor;
            set => SetActivePlayerGlowColor(value);
        }

        public float ActivePlayerGlowIntensity
        {
            get => m_ActivePlayerGlowIntensity;
            set => SetActivePlayerGlowIntensity(value);
        }

        public float ActivePlayerPulseSpeed
        {
            get => m_ActivePlayerPulseSpeed;
            set => SetActivePlayerPulseSpeed(value);
        }

        public bool EnablePulseAnimation
        {
            get => m_EnablePulseAnimation;
            set => SetEnablePulseAnimation(value);
        }

        // Accessibility properties
        public AccessibilityHighlightMode HighlightMode
        {
            get => m_HighlightMode;
            set => SetHighlightMode(value);
        }

        public bool UseColorBlindFriendlyContrast
        {
            get => m_UseColorBlindFriendlyContrast;
            set => SetUseColorBlindFriendlyContrast(value);
        }

        public float PatternIntensity
        {
            get => m_PatternIntensity;
            set => SetPatternIntensity(value);
        }

        public float AnimationSpeed
        {
            get => m_AnimationSpeed;
            set => SetAnimationSpeed(value);
        }

        public float BrightnessPulseIntensity
        {
            get => m_BrightnessPulseIntensity;
            set => SetBrightnessPulseIntensity(value);
        }

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize network list
            m_HostColorPalette = new NetworkList<Color>();

            // Initialize default colors if not set
            InitializeDefaultColors();
        }

        private void Start()
        {
            // Initialize player color based on saved preferences or default to first color
            InitializePlayerColor();
        }

        /// <summary>
        /// Initializes the player color based on saved preferences or defaults to the first color in the array.
        /// </summary>
        private void InitializePlayerColor()
        {
            // Check if XRINetworkGameManager is available
            if (XRMultiplayer.XRINetworkGameManager.LocalPlayerColor == null)
            {
                Debug.LogWarning("PlayerColorManager: XRINetworkGameManager.LocalPlayerColor is not available.");
                return;
            }

            // Load color preference (will return default if none exists)
            Color preferredColor = LoadColorPreference();

            // Set the local player color
            XRMultiplayer.XRINetworkGameManager.LocalPlayerColor.Value = preferredColor;
            Debug.Log($"PlayerColorManager: Initialized player color to: {ColorUtility.ToHtmlStringRGB(preferredColor)}");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            m_InNetworkedSession = true;

            // Subscribe to NetworkVariable changes
            m_NetworkActivePlayerIndex.OnValueChanged += OnNetworkActivePlayerIndexChanged;

            if (IsServer)
            {
                // Ensure host player's color is properly set
                EnsureHostPlayerColor();

                // Host initializes the network color palette
                InitializeHostColorPalette();

                // Initialize the NetworkVariable with the current active player index
                m_NetworkActivePlayerIndex.Value = m_ActivePlayerIndex;
                Debug.Log($"PlayerColorManager: Initialized network active player index to {m_ActivePlayerIndex}");
            }
            else
            {
                // Clients back up their local palette
                BackupLocalColorPalette();

                // Clients initialize their local active player index from the NetworkVariable
                m_ActivePlayerIndex = m_NetworkActivePlayerIndex.Value;
                Debug.Log($"PlayerColorManager: Client initialized active player index from network: {m_ActivePlayerIndex}");
            }

            // Subscribe to host color palette changes
            m_HostColorPalette.OnListChanged += OnHostColorPaletteChanged;
        }

        /// <summary>
        /// Ensures the host player's color is properly set when they become the host.
        /// </summary>
        private void EnsureHostPlayerColor()
        {
            // Get the current local player color
            Color currentColor = XRMultiplayer.XRINetworkGameManager.LocalPlayerColor.Value;

            // Check if it's white (the default from XRINetworkGameManager)
            if (currentColor == Color.white)
            {
                // If it's still the default white, load preference or use default blue
                Color hostColor = LoadColorPreference();
                Debug.Log($"PlayerColorManager: Host player color was white. Setting to preferred/default color: {ColorUtility.ToHtmlStringRGB(hostColor)}");
                XRMultiplayer.XRINetworkGameManager.LocalPlayerColor.Value = hostColor;

                // Also register this color for the host player
                ulong hostId = NetworkManager.Singleton.LocalClientId;
                RegisterPlayerColor(hostId, hostColor, 0);
            }
            else
            {
                Debug.Log($"PlayerColorManager: Host player using color: {ColorUtility.ToHtmlStringRGB(currentColor)}");

                // Register the current color for the host player
                ulong hostId = NetworkManager.Singleton.LocalClientId;
                RegisterPlayerColor(hostId, currentColor, 0);

                // Save this color as preference
                SaveColorPreference(currentColor);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            m_InNetworkedSession = false;

            // Unsubscribe from host color palette changes
            m_HostColorPalette.OnListChanged -= OnHostColorPaletteChanged;

            // Unsubscribe from NetworkVariable changes
            m_NetworkActivePlayerIndex.OnValueChanged -= OnNetworkActivePlayerIndexChanged;

            if (!IsServer)
            {
                // Restore local color palette
                RestoreLocalColorPalette();
            }

            // Clear player tracking
            ClearPlayerTracking();
        }

        private void InitializeDefaultColors()
        {
            // Only initialize colors that haven't been set in the inspector
            if (m_PlayerColors.Length < 8)
            {
                m_PlayerColors = new Color[8];
            }

            // Set default colors if not already set - matching the Seat Button Variant Prefab
            if (m_PlayerColors[0] == Color.clear) m_PlayerColors[0] = new Color(0.0f, 0.0f, 1.0f, 1.0f); // Blue
            if (m_PlayerColors[1] == Color.clear) m_PlayerColors[1] = new Color(1.0f, 0.5019608f, 0.0f, 1.0f); // Orange
            if (m_PlayerColors[2] == Color.clear) m_PlayerColors[2] = new Color(1.0f, 1.0f, 0.0f, 1.0f); // Yellow
            if (m_PlayerColors[3] == Color.clear) m_PlayerColors[3] = new Color(0.5019608f, 0.0f, 0.5019608f, 1.0f); // Purple
            if (m_PlayerColors[4] == Color.clear) m_PlayerColors[4] = new Color(1.0f, 0.0f, 0.0f, 1.0f); // Red
            if (m_PlayerColors[5] == Color.clear) m_PlayerColors[5] = new Color(0.0f, 0.7176471f, 0.0f, 1.0f); // Green
            if (m_PlayerColors[6] == Color.clear) m_PlayerColors[6] = new Color(0.0f, 0.0f, 0.0f, 1.0f); // Black
            if (m_PlayerColors[7] == Color.clear) m_PlayerColors[7] = new Color(1.0f, 1.0f, 1.0f, 1.0f); // White
        }

        #region Network Synchronization

        private void InitializeHostColorPalette()
        {
            // Clear the network list
            m_HostColorPalette.Clear();

            // Add all colors from the host's palette
            for (int i = 0; i < m_PlayerColors.Length; i++)
            {
                m_HostColorPalette.Add(m_PlayerColors[i]);
            }

            Debug.Log("Host color palette initialized");

            // Notify about color palette change
            OnColorPaletteChanged?.Invoke();
        }

        private void BackupLocalColorPalette()
        {
            // Backup local color palette before using host's
            for (int i = 0; i < m_PlayerColors.Length; i++)
            {
                m_LocalColorPaletteBackup[i] = m_PlayerColors[i];
            }

            Debug.Log("Local color palette backed up");
        }

        private void RestoreLocalColorPalette()
        {
            // Restore local color palette
            for (int i = 0; i < m_PlayerColors.Length; i++)
            {
                m_PlayerColors[i] = m_LocalColorPaletteBackup[i];
            }

            Debug.Log("Local color palette restored");
            OnColorPaletteChanged?.Invoke();
        }

        private void OnHostColorPaletteChanged(NetworkListEvent<Color> changeEvent)
        {
            // Update local palette from host's palette
            if (!IsServer && m_HostColorPalette.Count == 8)
            {
                bool colorsChanged = false;

                for (int i = 0; i < m_HostColorPalette.Count; i++)
                {
                    if (m_PlayerColors[i] != m_HostColorPalette[i])
                    {
                        m_PlayerColors[i] = m_HostColorPalette[i];
                        colorsChanged = true;
                    }
                }

                if (colorsChanged)
                {
                    Debug.Log("Host color palette updated");
                    OnColorPaletteChanged?.Invoke();
                }
            }
        }

        private void ClearPlayerTracking()
        {
            m_ColorToPlayerID.Clear();
            m_PlayerHasCustomColor.Clear();
            m_PlayerPreferredColor.Clear();
            m_PlayerToSeatIndex.Clear();
        }

        #endregion

        #region Color Management

        /// <summary>
        /// Sets the active player index and triggers the OnActivePlayerChanged event.
        /// </summary>
        public void SetActivePlayerIndex(int index)
        {
            if (m_ActivePlayerIndex != index)
            {
                m_ActivePlayerIndex = index;
                Debug.Log($"PlayerColorManager: SetActivePlayerIndex - Setting active player index to {index}, IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}");

                // Update the glow color based on the new active player's color
                if (index >= 0 && index < m_PlayerColors.Length)
                {
                    UpdateActivePlayerGlowColor();
                }

                // Notify local listeners
                OnActivePlayerChanged?.Invoke(m_ActivePlayerIndex);

                // If in a networked session and we're the server, update the NetworkVariable
                // This will automatically sync to all clients
                if (m_InNetworkedSession && IsServer)
                {
                    Debug.Log($"PlayerColorManager: SetActivePlayerIndex - Updating NetworkVariable to {index}");
                    m_NetworkActivePlayerIndex.Value = index;

                    // For backward compatibility, also use the RPC
                    SyncActivePlayerIndexClientRpc(m_ActivePlayerIndex);
                }
            }
        }

        /// <summary>
        /// Handles changes to the NetworkVariable for active player index.
        /// </summary>
        private void OnNetworkActivePlayerIndexChanged(int previousValue, int newValue)
        {
            // Only clients should respond to this (server already updated its local value)
            if (!IsServer)
            {
                Debug.Log($"PlayerColorManager: OnNetworkActivePlayerIndexChanged - Previous: {previousValue}, New: {newValue}");

                // Update local active player index
                if (m_ActivePlayerIndex != newValue)
                {
                    m_ActivePlayerIndex = newValue;

                    // Update the glow color based on the new active player's color
                    if (newValue >= 0 && newValue < m_PlayerColors.Length)
                    {
                        UpdateActivePlayerGlowColor();
                    }

                    // Notify local listeners
                    OnActivePlayerChanged?.Invoke(m_ActivePlayerIndex);
                    Debug.Log($"PlayerColorManager: Client updated active player index to {newValue}");
                }
            }
        }

        /// <summary>
        /// Registers a player's color preference and assigns a color based on availability.
        /// </summary>
        /// <param name="playerID">The network player ID</param>
        /// <param name="preferredColor">The player's preferred color</param>
        /// <param name="seatIndex">Optional seat index for the player</param>
        /// <returns>The assigned color (may differ from preferred if there's a conflict)</returns>
        public Color RegisterPlayerColor(ulong playerID, Color preferredColor, int seatIndex = -1)
        {
            // Store the player's preferred color
            m_PlayerPreferredColor[playerID] = preferredColor;

            // Check if this color is already taken
            if (m_ColorToPlayerID.TryGetValue(preferredColor, out ulong existingPlayerID) && existingPlayerID != playerID)
            {
                // Color conflict - resolve based on priority
                Color assignedColor = ResolveColorConflict(playerID, preferredColor, seatIndex);

                // Mark as not using custom color
                m_PlayerHasCustomColor[playerID] = false;

                // Notify about conflict resolution
                OnPlayerColorConflictResolved?.Invoke(playerID, preferredColor, assignedColor);

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncPlayerColorClientRpc(playerID, assignedColor);
                }

                return assignedColor;
            }
            else
            {
                // Color is available, assign it
                if (m_ColorToPlayerID.ContainsKey(preferredColor))
                {
                    m_ColorToPlayerID.Remove(preferredColor);
                }

                m_ColorToPlayerID[preferredColor] = playerID;
                m_PlayerHasCustomColor[playerID] = true;

                // If seat index is provided, update the seat assignment
                if (seatIndex >= 0)
                {
                    UpdatePlayerSeat(playerID, seatIndex);
                }

                // Notify about color change
                OnPlayerColorChanged?.Invoke(playerID, preferredColor);

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncPlayerColorClientRpc(playerID, preferredColor);
                }

                return preferredColor;
            }
        }

        /// <summary>
        /// Updates a player's seat assignment without changing their color.
        /// </summary>
        /// <param name="playerID">The network player ID</param>
        /// <param name="newSeatIndex">The new seat index</param>
        public void UpdatePlayerSeat(ulong playerID, int newSeatIndex)
        {
            int oldSeatIndex = -1;

            // Check if player already has a seat
            if (m_PlayerToSeatIndex.TryGetValue(playerID, out oldSeatIndex))
            {
                if (oldSeatIndex == newSeatIndex)
                {
                    // No change needed
                    return;
                }
            }

            // Update seat assignment
            m_PlayerToSeatIndex[playerID] = newSeatIndex;

            // Notify about seat change
            OnPlayerSeatChanged?.Invoke(playerID, oldSeatIndex, newSeatIndex);

            // If in a networked session and we're the server, sync to clients
            if (m_InNetworkedSession && IsServer)
            {
                SyncPlayerSeatClientRpc(playerID, oldSeatIndex, newSeatIndex);
            }
        }

        /// <summary>
        /// Unregisters a player's color when they leave.
        /// </summary>
        /// <param name="playerID">The network player ID</param>
        public void UnregisterPlayerColor(ulong playerID)
        {
            // Find and remove the player's color from the mapping
            Color playerColor = Color.clear;
            foreach (var kvp in m_ColorToPlayerID)
            {
                if (kvp.Value == playerID)
                {
                    playerColor = kvp.Key;
                    m_ColorToPlayerID.Remove(kvp.Key);
                    break;
                }
            }

            // Remove from other dictionaries
            m_PlayerHasCustomColor.Remove(playerID);
            m_PlayerPreferredColor.Remove(playerID);

            // Remove seat assignment
            int seatIndex = -1;
            if (m_PlayerToSeatIndex.TryGetValue(playerID, out seatIndex))
            {
                m_PlayerToSeatIndex.Remove(playerID);
                OnPlayerSeatChanged?.Invoke(playerID, seatIndex, -1);
            }

            // Notify about player leaving (color clear indicates player left)
            OnPlayerColorChanged?.Invoke(playerID, Color.clear);

            // If in a networked session and we're the server, sync to clients
            if (m_InNetworkedSession && IsServer)
            {
                SyncPlayerLeftClientRpc(playerID);
            }
        }

        /// <summary>
        /// Resolves color conflicts by finding an alternative color.
        /// </summary>
        private Color ResolveColorConflict(ulong playerID, Color requestedColor, int seatIndex)
        {
            // Try to use seat color if seat is assigned
            if (seatIndex >= 0 && seatIndex < m_PlayerColors.Length)
            {
                Color seatColor = m_PlayerColors[seatIndex];

                // Check if seat color is available
                if (!m_ColorToPlayerID.ContainsKey(seatColor))
                {
                    m_ColorToPlayerID[seatColor] = playerID;
                    return seatColor;
                }
            }

            // Try to find any available color from the palette
            foreach (Color color in m_PlayerColors)
            {
                if (!m_ColorToPlayerID.ContainsKey(color))
                {
                    m_ColorToPlayerID[color] = playerID;
                    return color;
                }
            }

            // If all colors are taken, generate a unique color
            Color uniqueColor = GenerateUniqueColor();
            m_ColorToPlayerID[uniqueColor] = playerID;
            return uniqueColor;
        }

        /// <summary>
        /// Generates a unique color that's not in the current palette.
        /// </summary>
        private Color GenerateUniqueColor()
        {
            // Start with a random color
            Color uniqueColor = new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                1.0f
            );

            // Ensure it's not too close to existing colors
            bool isUnique = false;
            int attempts = 0;

            while (!isUnique && attempts < 10)
            {
                isUnique = true;

                foreach (Color existingColor in m_ColorToPlayerID.Keys)
                {
                    // Check if colors are too similar
                    if (ColorDistance(uniqueColor, existingColor) < 0.3f)
                    {
                        isUnique = false;
                        uniqueColor = new Color(
                            UnityEngine.Random.value,
                            UnityEngine.Random.value,
                            UnityEngine.Random.value,
                            1.0f
                        );
                        break;
                    }
                }

                attempts++;
            }

            return uniqueColor;
        }

        /// <summary>
        /// Calculates the distance between two colors in RGB space.
        /// </summary>
        private float ColorDistance(Color a, Color b)
        {
            return Mathf.Sqrt(
                Mathf.Pow(a.r - b.r, 2) +
                Mathf.Pow(a.g - b.g, 2) +
                Mathf.Pow(a.b - b.b, 2)
            );
        }

        /// <summary>
        /// Checks if a player has a registered color.
        /// </summary>
        public bool HasRegisteredColor(ulong playerID)
        {
            foreach (var kvp in m_ColorToPlayerID)
            {
                if (kvp.Value == playerID)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the color for a specific player by ID.
        /// </summary>
        public Color GetPlayerColorByID(ulong playerID)
        {
            foreach (var kvp in m_ColorToPlayerID)
            {
                if (kvp.Value == playerID)
                {
                    return kvp.Key;
                }
            }

            // Player doesn't have a registered color
            return Color.clear;
        }

        /// <summary>
        /// Gets the seat index for a specific player by ID.
        /// </summary>
        public int GetPlayerSeatByID(ulong playerID)
        {
            if (m_PlayerToSeatIndex.TryGetValue(playerID, out int seatIndex))
            {
                return seatIndex;
            }

            // Player doesn't have a seat
            return -1;
        }

        #endregion

        #region Public Accessor Methods

        /// <summary>
        /// Gets the color for the specified player index.
        /// </summary>
        public Color GetPlayerColor(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < m_PlayerColors.Length)
            {
                return m_PlayerColors[playerIndex];
            }

            // Fallback to first color
            return m_PlayerColors[0];
        }

        /// <summary>
        /// Sets the color for the specified player index.
        /// </summary>
        public void SetPlayerColor(int playerIndex, Color color)
        {
            if (playerIndex >= 0 && playerIndex < m_PlayerColors.Length)
            {
                Color oldColor = m_PlayerColors[playerIndex];
                m_PlayerColors[playerIndex] = color;

                // Notify about seat color change
                OnSeatColorChanged?.Invoke(playerIndex, color);

                // If in a networked session and we're the server, update the host color palette
                if (m_InNetworkedSession && IsServer)
                {
                    // Update the host color palette
                    if (m_HostColorPalette.Count > playerIndex)
                    {
                        m_HostColorPalette[playerIndex] = color;
                    }

                    // Sync to clients
                    SyncSeatColorClientRpc(playerIndex, color);
                }
            }
        }

        /// <summary>
        /// Gets all player colors.
        /// </summary>
        public Color[] GetAllPlayerColors()
        {
            return m_PlayerColors;
        }

        /// <summary>
        /// Gets the active player color.
        /// </summary>
        public Color GetActivePlayerColor()
        {
            return GetPlayerColor(m_ActivePlayerIndex);
        }

        /// <summary>
        /// Gets player colors for a specific player count.
        /// </summary>
        public Color[] GetPlayerColors(int playerCount)
        {
            playerCount = Mathf.Clamp(playerCount, 1, 8);
            Color[] colors = new Color[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                colors[i] = GetPlayerColor(i);
            }

            return colors;
        }

        /// <summary>
        /// Manually triggers the OnColorPaletteChanged event to update all UI elements.
        /// </summary>
        public void NotifyColorPaletteChanged()
        {
            OnColorPaletteChanged?.Invoke();

            // If in a networked session and we're the server, sync to clients
            if (m_InNetworkedSession && IsServer)
            {
                NotifyColorPaletteChangedClientRpc();
            }
        }

        /// <summary>
        /// Saves the player's color preference to PlayerPrefs.
        /// </summary>
        /// <param name="color">The color to save as preference</param>
        public void SaveColorPreference(Color color)
        {
            string colorHex = "#" + ColorUtility.ToHtmlStringRGB(color);
            PlayerPrefs.SetString("PreferredPlayerColor", colorHex);
            PlayerPrefs.Save();
            Debug.Log($"PlayerColorManager: Saved color preference: {colorHex}");
        }

        /// <summary>
        /// Loads the player's color preference from PlayerPrefs.
        /// </summary>
        /// <returns>The saved color preference, or the first color in the array if no preference exists</returns>
        public Color LoadColorPreference()
        {
            string savedColorHex = PlayerPrefs.GetString("PreferredPlayerColor", "");
            if (!string.IsNullOrEmpty(savedColorHex) && ColorUtility.TryParseHtmlString(savedColorHex, out Color savedColor))
            {
                Debug.Log($"PlayerColorManager: Loaded color preference: {savedColorHex}");
                return savedColor;
            }

            // Return first color as default if no preference is saved
            Debug.Log($"PlayerColorManager: No saved preference found, using default color: {ColorUtility.ToHtmlStringRGB(m_PlayerColors[0])}");
            return m_PlayerColors[0];
        }

        #endregion

        #region Network RPCs

        [ClientRpc]
        private void SyncPlayerColorClientRpc(ulong playerID, Color color)
        {
            if (!IsServer)
            {
                // Update local tracking
                foreach (var kvp in m_ColorToPlayerID)
                {
                    if (kvp.Value == playerID)
                    {
                        m_ColorToPlayerID.Remove(kvp.Key);
                        break;
                    }
                }

                m_ColorToPlayerID[color] = playerID;
                m_PlayerHasCustomColor[playerID] = true;

                // Notify about color change
                OnPlayerColorChanged?.Invoke(playerID, color);
            }
        }

        [ClientRpc]
        private void SyncPlayerSeatClientRpc(ulong playerID, int oldSeatIndex, int newSeatIndex)
        {
            if (!IsServer)
            {
                // Update local tracking
                m_PlayerToSeatIndex[playerID] = newSeatIndex;

                // Notify about seat change
                OnPlayerSeatChanged?.Invoke(playerID, oldSeatIndex, newSeatIndex);
            }
        }

        [ClientRpc]
        private void SyncPlayerLeftClientRpc(ulong playerID)
        {
            if (!IsServer)
            {
                // Find and remove the player's color from the mapping
                Color playerColor = Color.clear;
                foreach (var kvp in m_ColorToPlayerID)
                {
                    if (kvp.Value == playerID)
                    {
                        playerColor = kvp.Key;
                        m_ColorToPlayerID.Remove(kvp.Key);
                        break;
                    }
                }

                // Remove from other dictionaries
                m_PlayerHasCustomColor.Remove(playerID);
                m_PlayerPreferredColor.Remove(playerID);

                // Remove seat assignment
                int seatIndex = -1;
                if (m_PlayerToSeatIndex.TryGetValue(playerID, out seatIndex))
                {
                    m_PlayerToSeatIndex.Remove(playerID);
                    OnPlayerSeatChanged?.Invoke(playerID, seatIndex, -1);
                }

                // Notify about player leaving (color clear indicates player left)
                OnPlayerColorChanged?.Invoke(playerID, Color.clear);
            }
        }

        [ClientRpc]
        private void SyncSeatColorClientRpc(int seatIndex, Color color)
        {
            if (!IsServer)
            {
                // Update local palette
                if (seatIndex >= 0 && seatIndex < m_PlayerColors.Length)
                {
                    m_PlayerColors[seatIndex] = color;

                    // Notify about seat color change
                    OnSeatColorChanged?.Invoke(seatIndex, color);
                }
            }
        }

        [ClientRpc]
        private void SyncActivePlayerIndexClientRpc(int activePlayerIndex)
        {
            if (!IsServer)
            {
                // Update local active player index
                if (m_ActivePlayerIndex != activePlayerIndex)
                {
                    m_ActivePlayerIndex = activePlayerIndex;
                    OnActivePlayerChanged?.Invoke(m_ActivePlayerIndex);
                }
            }
        }

        [ClientRpc]
        private void NotifyColorPaletteChangedClientRpc()
        {
            if (!IsServer)
            {
                // Notify about color palette change
                OnColorPaletteChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncActivePlayerGlowColorClientRpc(Color color)
        {
            if (!IsServer)
            {
                m_ActivePlayerGlowColor = color;
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncActivePlayerGlowIntensityClientRpc(float intensity)
        {
            if (!IsServer)
            {
                m_ActivePlayerGlowIntensity = intensity;
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncActivePlayerPulseSpeedClientRpc(float speed)
        {
            if (!IsServer)
            {
                m_ActivePlayerPulseSpeed = speed;
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncEnablePulseAnimationClientRpc(bool enable)
        {
            if (!IsServer)
            {
                m_EnablePulseAnimation = enable;
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncHighlightModeClientRpc(byte mode)
        {
            if (!IsServer)
            {
                m_HighlightMode = (AccessibilityHighlightMode)mode;
                Debug.Log($"PlayerColorManager: Received highlight mode {m_HighlightMode} from server");
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncUseColorBlindFriendlyContrastClientRpc(bool useColorBlindFriendly)
        {
            if (!IsServer)
            {
                m_UseColorBlindFriendlyContrast = useColorBlindFriendly;
                Debug.Log($"PlayerColorManager: Received color-blind friendly contrast {(useColorBlindFriendly ? "enabled" : "disabled")} from server");

                // If color-blind friendly contrast is enabled and we have an active player,
                // update the glow color to use the color-blind friendly contrast
                if (m_UseColorBlindFriendlyContrast && m_ActivePlayerIndex >= 0)
                {
                    UpdateActivePlayerGlowColor();
                }

                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncPatternIntensityClientRpc(float intensity)
        {
            if (!IsServer)
            {
                m_PatternIntensity = intensity;
                Debug.Log($"PlayerColorManager: Received pattern intensity {intensity} from server");
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncAnimationSpeedClientRpc(float speed)
        {
            if (!IsServer)
            {
                m_AnimationSpeed = speed;
                Debug.Log($"PlayerColorManager: Received animation speed {speed} from server");
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        [ClientRpc]
        private void SyncBrightnessPulseIntensityClientRpc(float intensity)
        {
            if (!IsServer)
            {
                m_BrightnessPulseIntensity = intensity;
                Debug.Log($"PlayerColorManager: Received brightness pulse intensity {intensity} from server");
                OnHighlightSettingsChanged?.Invoke();
            }
        }

        #endregion

        #region Active Player Highlighting

        /// <summary>
        /// Sets the active player glow color and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetActivePlayerGlowColor(Color color)
        {
            if (m_ActivePlayerGlowColor != color)
            {
                m_ActivePlayerGlowColor = color;
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncActivePlayerGlowColorClientRpc(color);
                }
            }
        }

        /// <summary>
        /// Sets the active player glow intensity and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetActivePlayerGlowIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (m_ActivePlayerGlowIntensity != intensity)
            {
                m_ActivePlayerGlowIntensity = intensity;
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncActivePlayerGlowIntensityClientRpc(intensity);
                }
            }
        }

        /// <summary>
        /// Sets the active player pulse speed and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetActivePlayerPulseSpeed(float speed)
        {
            speed = Mathf.Clamp(speed, 0.1f, 2f);
            if (m_ActivePlayerPulseSpeed != speed)
            {
                m_ActivePlayerPulseSpeed = speed;
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncActivePlayerPulseSpeedClientRpc(speed);
                }
            }
        }

        /// <summary>
        /// Sets whether the pulse animation is enabled and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetEnablePulseAnimation(bool enable)
        {
            if (m_EnablePulseAnimation != enable)
            {
                m_EnablePulseAnimation = enable;
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncEnablePulseAnimationClientRpc(enable);
                }
            }
        }

        /// <summary>
        /// Sets the highlight mode and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetHighlightMode(AccessibilityHighlightMode mode)
        {
            if (m_HighlightMode != mode)
            {
                m_HighlightMode = mode;
                Debug.Log($"PlayerColorManager: Highlight mode changed to {mode}");
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncHighlightModeClientRpc((byte)mode);
                }
            }
        }

        /// <summary>
        /// Sets whether to use color-blind friendly contrast and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetUseColorBlindFriendlyContrast(bool useColorBlindFriendly)
        {
            if (m_UseColorBlindFriendlyContrast != useColorBlindFriendly)
            {
                m_UseColorBlindFriendlyContrast = useColorBlindFriendly;
                Debug.Log($"PlayerColorManager: Color-blind friendly contrast {(useColorBlindFriendly ? "enabled" : "disabled")}");

                // If color-blind friendly contrast is enabled and we have an active player,
                // update the glow color to use the color-blind friendly contrast
                if (m_UseColorBlindFriendlyContrast && m_ActivePlayerIndex >= 0)
                {
                    UpdateActivePlayerGlowColor();
                }

                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncUseColorBlindFriendlyContrastClientRpc(useColorBlindFriendly);
                }
            }
        }

        /// <summary>
        /// Sets the pattern intensity and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetPatternIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (m_PatternIntensity != intensity)
            {
                m_PatternIntensity = intensity;
                Debug.Log($"PlayerColorManager: Pattern intensity set to {intensity}");
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncPatternIntensityClientRpc(intensity);
                }
            }
        }

        /// <summary>
        /// Sets the animation speed and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetAnimationSpeed(float speed)
        {
            speed = Mathf.Clamp(speed, 0.1f, 2f);
            if (m_AnimationSpeed != speed)
            {
                m_AnimationSpeed = speed;
                Debug.Log($"PlayerColorManager: Animation speed set to {speed}");
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncAnimationSpeedClientRpc(speed);
                }
            }
        }

        /// <summary>
        /// Sets the brightness pulse intensity and triggers the OnHighlightSettingsChanged event.
        /// </summary>
        private void SetBrightnessPulseIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (m_BrightnessPulseIntensity != intensity)
            {
                m_BrightnessPulseIntensity = intensity;
                Debug.Log($"PlayerColorManager: Brightness pulse intensity set to {intensity}");
                OnHighlightSettingsChanged?.Invoke();

                // If in a networked session and we're the server, sync to clients
                if (m_InNetworkedSession && IsServer)
                {
                    SyncBrightnessPulseIntensityClientRpc(intensity);
                }
            }
        }

        /// <summary>
        /// Updates the active player glow color based on the active player's color and accessibility settings.
        /// </summary>
        private void UpdateActivePlayerGlowColor()
        {
            if (m_ActivePlayerIndex < 0 || m_ActivePlayerIndex >= m_PlayerColors.Length)
                return;

            Color playerColor = GetActivePlayerColor();
            Color newGlowColor = Color.white;

            // if (m_UseColorBlindFriendlyContrast)
            // {
            //     newGlowColor = CalculateColorBlindFriendlyContrast(playerColor);
            //     Debug.Log($"PlayerColorManager: Updated glow color to color-blind friendly contrast: {ColorUtility.ToHtmlStringRGB(newGlowColor)} for player color {ColorUtility.ToHtmlStringRGB(playerColor)}");
            // }
            // else
            // {
            //     // Use standard contrast calculation
            //     newGlowColor = CalculateContrastingColor(playerColor);
            //     Debug.Log($"PlayerColorManager: Updated glow color to standard contrast: {ColorUtility.ToHtmlStringRGB(newGlowColor)} for player color {ColorUtility.ToHtmlStringRGB(playerColor)}");
            // }

            // Set the glow color (using existing method to handle network sync)
            SetActivePlayerGlowColor(newGlowColor);
        }

        /// <summary>
        /// Calculates a contrasting color based on the input color.
        /// </summary>
        private Color CalculateContrastingColor(Color baseColor)
        {
            // Calculate luminance (perceived brightness)
            float luminance = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;

            // Create complementary color (opposite on color wheel)
            Color contrastingColor = new Color(
                1.0f - baseColor.r,
                1.0f - baseColor.g,
                1.0f - baseColor.b,
                1.0f
            );

            // Adjust brightness for better contrast
            Color.RGBToHSV(contrastingColor, out float h, out float s, out float v);

            // If original color is dark, make contrasting color brighter
            // If original color is light, make contrasting color darker
            if (luminance < 0.5f)
            {
                v = Mathf.Min(1.0f, v + 0.3f);
            }
            else
            {
                v = Mathf.Max(0.0f, v - 0.3f);
            }

            // Ensure good saturation
            s = Mathf.Max(0.6f, s);

            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>
        /// Calculates a color-blind friendly contrasting color based on the input color.
        /// Optimized for different types of color blindness.
        /// </summary>
        private Color CalculateColorBlindFriendlyContrast(Color baseColor)
        {
            // Convert to HSV for easier manipulation
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);

            // For red-green color blindness (most common), focus on brightness and blue-yellow axis
            // Invert brightness for maximum contrast
            float newV = v > 0.5f ? 0.2f : 0.9f;

            // For protanopia/deuteranopia, blue-yellow contrast works better than red-green
            // Shift hue toward blue if original is yellowish, and toward yellow if original is bluish
            if (h > 0.4f && h < 0.7f) // If in blue-ish range
            {
                h = 0.15f; // Shift toward yellow
            }
            else if (h < 0.2f || h > 0.8f) // If in red/yellow-ish range
            {
                h = 0.6f; // Shift toward blue
            }

            // Ensure good saturation
            s = Mathf.Max(0.7f, s);

            return Color.HSVToRGB(h, s, newV);
        }

        #endregion
    }
}