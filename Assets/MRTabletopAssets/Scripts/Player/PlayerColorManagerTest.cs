using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace MRTabletopAssets
{
    /// <summary>
    /// Test script for verifying PlayerColorManager functionality.
    /// </summary>
    public class PlayerColorManagerTest : MonoBehaviour
    {
        [SerializeField] private Button m_RegisterPlayerButton;
        [SerializeField] private Button m_UnregisterPlayerButton;
        [SerializeField] private Button m_UpdateSeatButton;
        [SerializeField] private Button m_SetActivePlayerButton;
        [SerializeField] private TMP_InputField m_PlayerIDInput;
        [SerializeField] private TMP_InputField m_SeatIndexInput;
        [SerializeField] private TMP_Dropdown m_ColorDropdown;
        [SerializeField] private TextMeshProUGUI m_StatusText;
        [SerializeField] private Image[] m_ColorDisplayImages;

        private Dictionary<string, Color> m_AvailableColors = new Dictionary<string, Color>()
        {
            { "Blue", Color.blue },
            { "Orange", new Color(1.0f, 0.5f, 0.0f, 1.0f) },
            { "Yellow", Color.yellow },
            { "Purple", new Color(0.5f, 0.0f, 0.5f, 1.0f) },
            { "Red", Color.red },
            { "Green", Color.green },
            { "Black", Color.black },
            { "White", Color.white }
        };

        private List<string> m_ColorNames = new List<string>();

        private void Start()
        {
            // Initialize color dropdown
            m_ColorDropdown.ClearOptions();
            foreach (var colorPair in m_AvailableColors)
            {
                m_ColorNames.Add(colorPair.Key);
            }
            m_ColorDropdown.AddOptions(m_ColorNames);

            // Set up button listeners
            m_RegisterPlayerButton.onClick.AddListener(RegisterPlayer);
            m_UnregisterPlayerButton.onClick.AddListener(UnregisterPlayer);
            m_UpdateSeatButton.onClick.AddListener(UpdateSeat);
            m_SetActivePlayerButton.onClick.AddListener(SetActivePlayer);

            // Subscribe to PlayerColorManager events
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnPlayerColorChanged += HandlePlayerColorChanged;
                PlayerColorManager.Instance.OnPlayerColorConflictResolved += HandlePlayerColorConflictResolved;
                PlayerColorManager.Instance.OnPlayerSeatChanged += HandlePlayerSeatChanged;
                PlayerColorManager.Instance.OnActivePlayerChanged += HandleActivePlayerChanged;
                PlayerColorManager.Instance.OnColorPaletteChanged += HandleColorPaletteChanged;
            }
            else
            {
                m_StatusText.text = "PlayerColorManager instance not found!";
            }

            // Update color display
            UpdateColorDisplay();
        }

        private void OnDestroy()
        {
            // Unsubscribe from PlayerColorManager events
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnPlayerColorChanged -= HandlePlayerColorChanged;
                PlayerColorManager.Instance.OnPlayerColorConflictResolved -= HandlePlayerColorConflictResolved;
                PlayerColorManager.Instance.OnPlayerSeatChanged -= HandlePlayerSeatChanged;
                PlayerColorManager.Instance.OnActivePlayerChanged -= HandleActivePlayerChanged;
                PlayerColorManager.Instance.OnColorPaletteChanged -= HandleColorPaletteChanged;
            }
        }

        private void RegisterPlayer()
        {
            if (PlayerColorManager.Instance == null)
            {
                m_StatusText.text = "PlayerColorManager instance not found!";
                return;
            }

            if (string.IsNullOrEmpty(m_PlayerIDInput.text))
            {
                m_StatusText.text = "Please enter a player ID";
                return;
            }

            if (ulong.TryParse(m_PlayerIDInput.text, out ulong playerID))
            {
                int seatIndex = -1;
                if (!string.IsNullOrEmpty(m_SeatIndexInput.text) && int.TryParse(m_SeatIndexInput.text, out int seat))
                {
                    seatIndex = seat;
                }

                string colorName = m_ColorNames[m_ColorDropdown.value];
                Color preferredColor = m_AvailableColors[colorName];

                Color assignedColor = PlayerColorManager.Instance.RegisterPlayerColor(playerID, preferredColor, seatIndex);
                m_StatusText.text = $"Registered player {playerID} with color {colorName}. Assigned color: {ColorToHex(assignedColor)}";
                UpdateColorDisplay();
            }
            else
            {
                m_StatusText.text = "Invalid player ID format";
            }
        }

        private void UnregisterPlayer()
        {
            if (PlayerColorManager.Instance == null)
            {
                m_StatusText.text = "PlayerColorManager instance not found!";
                return;
            }

            if (string.IsNullOrEmpty(m_PlayerIDInput.text))
            {
                m_StatusText.text = "Please enter a player ID";
                return;
            }

            if (ulong.TryParse(m_PlayerIDInput.text, out ulong playerID))
            {
                PlayerColorManager.Instance.UnregisterPlayerColor(playerID);
                m_StatusText.text = $"Unregistered player {playerID}";
                UpdateColorDisplay();
            }
            else
            {
                m_StatusText.text = "Invalid player ID format";
            }
        }

        private void UpdateSeat()
        {
            if (PlayerColorManager.Instance == null)
            {
                m_StatusText.text = "PlayerColorManager instance not found!";
                return;
            }

            if (string.IsNullOrEmpty(m_PlayerIDInput.text) || string.IsNullOrEmpty(m_SeatIndexInput.text))
            {
                m_StatusText.text = "Please enter both player ID and seat index";
                return;
            }

            if (ulong.TryParse(m_PlayerIDInput.text, out ulong playerID) && int.TryParse(m_SeatIndexInput.text, out int seatIndex))
            {
                PlayerColorManager.Instance.UpdatePlayerSeat(playerID, seatIndex);
                m_StatusText.text = $"Updated player {playerID} to seat {seatIndex}";
                UpdateColorDisplay();
            }
            else
            {
                m_StatusText.text = "Invalid player ID or seat index format";
            }
        }

        private void SetActivePlayer()
        {
            if (PlayerColorManager.Instance == null)
            {
                m_StatusText.text = "PlayerColorManager instance not found!";
                return;
            }

            if (string.IsNullOrEmpty(m_SeatIndexInput.text))
            {
                m_StatusText.text = "Please enter a seat index";
                return;
            }

            if (int.TryParse(m_SeatIndexInput.text, out int activePlayerIndex))
            {
                PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;
                m_StatusText.text = $"Set active player to seat {activePlayerIndex}";
                UpdateColorDisplay();
            }
            else
            {
                m_StatusText.text = "Invalid seat index format";
            }
        }

        private void UpdateColorDisplay()
        {
            if (PlayerColorManager.Instance == null || m_ColorDisplayImages == null)
                return;

            Color[] colors = PlayerColorManager.Instance.GetAllPlayerColors();
            for (int i = 0; i < m_ColorDisplayImages.Length && i < colors.Length; i++)
            {
                m_ColorDisplayImages[i].color = colors[i];
            }
        }

        #region Event Handlers

        private void HandlePlayerColorChanged(ulong playerID, Color newColor)
        {
            if (newColor == Color.clear)
            {
                m_StatusText.text = $"Player {playerID} left";
            }
            else
            {
                m_StatusText.text = $"Player {playerID} color changed to {ColorToHex(newColor)}";
            }
            UpdateColorDisplay();
        }

        private void HandlePlayerColorConflictResolved(ulong playerID, Color requestedColor, Color assignedColor)
        {
            m_StatusText.text = $"Player {playerID} color conflict: requested {ColorToHex(requestedColor)}, assigned {ColorToHex(assignedColor)}";
            UpdateColorDisplay();
        }

        private void HandlePlayerSeatChanged(ulong playerID, int oldSeatIndex, int newSeatIndex)
        {
            m_StatusText.text = $"Player {playerID} moved from seat {oldSeatIndex} to seat {newSeatIndex}";
            UpdateColorDisplay();
        }

        private void HandleActivePlayerChanged(int activePlayerIndex)
        {
            m_StatusText.text = $"Active player changed to seat {activePlayerIndex}";
            UpdateColorDisplay();
        }

        private void HandleColorPaletteChanged()
        {
            m_StatusText.text = "Color palette changed";
            UpdateColorDisplay();
        }

        #endregion

        private string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }
    }
}
