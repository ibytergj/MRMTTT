using UnityEngine;
using TMPro;
using UnityEngine.UI;
using XRMultiplayer;
using MRTabletopAssets;

public class TableTopSeatButton : MonoBehaviour
{
    public Color[] seatColors => m_SeatColors;
    [SerializeField]
    Color[] m_SeatColors;

    [SerializeField]
    Image[] m_SeatImages;

    [SerializeField]
    TMP_Text m_SeatNumberText;

    [SerializeField]
    TMP_Text m_SeatNameText;

    [SerializeField]
    TMP_Text m_PlayerInSeatText;

    [SerializeField]
    GameObject m_SeatUnoccupiedObject;

    [SerializeField]
    GameObject m_SeatOccupiedObject;

    [SerializeField]
    GameObject m_OwnedSetUI;

    [SerializeField]
    GameObject m_TakenSeatUI;

    [SerializeField]
    GameObject m_HoveredObject;

    [SerializeField]
    GameObject[] m_WorldSpaceSeatHoverObjects;

    [Header("Editor Variables")]
    [SerializeField]
    bool m_IsSpectator = false;

    [SerializeField, Range(0, 7)]
    int m_SeatID;

    [SerializeField]
    bool m_IsHovered = false;

    [SerializeField]
    bool m_IsOccupied = false;

    [SerializeField]
    bool m_IsLocalPlayer = false;

    [SerializeField]
    string m_AvailableSeatText = "<color=#7B7B7B><i>Available</i></color>";

    string m_PlayerNameInSeat = "Player Name";

    XRINetworkPlayer m_PlayerInSeat;
    [SerializeField] Button m_MuteButton;
    [SerializeField] Toggle m_HideAvatarToggle;
    [SerializeField] Image m_VoiceChatFillImage;
    [SerializeField] Image m_MicIcon;
    [SerializeField] Image m_SquelchedIcon;
    [SerializeField] Sprite m_MutedSprite;
    [SerializeField] Sprite m_UnmutedSprite;

    void OnValidate()
    {
        if (!m_IsSpectator)
        {
            m_SeatID = Mathf.Clamp(m_SeatID, 0, m_SeatColors.Length - 1);

            // Use PlayerColorManager if available, otherwise use local colors
            if (Application.isPlaying && PlayerColorManager.Instance != null)
            {
                UpdateSeatButtonColors();
            }
            else
            {
                foreach (var icon in m_SeatImages)
                    icon.color = m_SeatColors[m_SeatID];
            }

            m_SeatNumberText.text = (m_SeatID + 1).ToString();
            m_SeatNameText.text = "Seat " + (m_SeatID + 1);
        }
        else
        {
            // Spectator is always true because we remove the player from the list if nt
            m_IsOccupied = true;
        }

        SetOccupied(m_IsOccupied);
    }

    /// <summary>
    /// Updates the seat button colors based on the PlayerColorManager.
    /// </summary>
    private void UpdateSeatButtonColors()
    {
        if (PlayerColorManager.Instance != null)
        {
            Color seatColor = PlayerColorManager.Instance.GetPlayerColor(m_SeatID);
            foreach (var icon in m_SeatImages)
                icon.color = seatColor;
        }
    }

    private void Start()
    {
        // Initialize seat button colors
        UpdateSeatButtonColors();
    }

    private void OnEnable()
    {
        // Subscribe to PlayerColorManager events
        if (PlayerColorManager.Instance != null)
        {
            PlayerColorManager.Instance.OnSeatColorChanged += HandleSeatColorChanged;
            PlayerColorManager.Instance.OnColorPaletteChanged += HandleColorPaletteChanged;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from PlayerColorManager events
        if (PlayerColorManager.Instance != null)
        {
            PlayerColorManager.Instance.OnSeatColorChanged -= HandleSeatColorChanged;
            PlayerColorManager.Instance.OnColorPaletteChanged -= HandleColorPaletteChanged;
        }
    }

    void Update()
    {
        if (m_PlayerInSeat != null)
            m_VoiceChatFillImage.fillAmount = m_PlayerInSeat.playerVoiceAmp;
    }

    // Event handlers for PlayerColorManager events
    private void HandleSeatColorChanged(int seatIndex, Color newColor)
    {
        if (seatIndex == m_SeatID)
        {
            UpdateSeatButtonColors();
        }
    }

    private void HandleColorPaletteChanged()
    {
        UpdateSeatButtonColors();
    }

    public void SetPlayerName(string name)
    {
        m_PlayerNameInSeat = name;
        m_PlayerInSeatText.text = m_IsOccupied ? (m_IsLocalPlayer ? "You" : m_PlayerNameInSeat) : m_AvailableSeatText;
    }

    public void SetLocalPlayer(bool local, bool updateOccupied = true)
    {
        m_IsLocalPlayer = local;
        if (updateOccupied)
            SetOccupied(m_IsOccupied);
    }

    public void AssignPlayerToSeat(XRINetworkPlayer player)
    {
        if (m_PlayerInSeat != null)
            RemovePlayerFromSeat();

        m_PlayerInSeat = player;

        SetPlayerName(m_PlayerInSeat.playerName);

        m_PlayerInSeat.onNameUpdated += SetPlayerName;

        m_PlayerInSeat.selfMuted.OnValueChanged += UpdateSelfMutedState;
        m_PlayerInSeat.squelched.Subscribe(UpdateSquelchedState);

        m_MuteButton.onClick.AddListener(SquelchPressed);
        m_SquelchedIcon.enabled = m_PlayerInSeat.squelched.Value;

        m_HideAvatarToggle.onValueChanged.AddListener(SetPlayerAvatarHidden);
        if (m_PlayerInSeat.TryGetComponent(out PlayerColocation playerColocation))
        {
            m_HideAvatarToggle.SetIsOnWithoutNotify(!playerColocation.isShowingAvatar);
        }

        // Handle player color assignment with PlayerColorManager
        if (PlayerColorManager.Instance != null)
        {
            // Check if this is a seat swap
            bool isSeatSwap = PlayerColorManager.Instance.HasRegisteredColor(player.OwnerClientId);

            if (isSeatSwap)
            {
                // For seat swaps, maintain the player's existing color
                PlayerColorManager.Instance.UpdatePlayerSeat(player.OwnerClientId, m_SeatID);
            }
            else
            {
                // For new players, try to use their preferred color
                Color preferredColor = player.playerColor;
                Color assignedColor = PlayerColorManager.Instance.RegisterPlayerColor(
                    player.OwnerClientId, preferredColor, m_SeatID);

                // Update the local player color
                if (player.IsLocalPlayer)
                {
                    XRINetworkGameManager.LocalPlayerColor.Value = assignedColor;
                }
            }
        }
        else
        {
            // Fallback to old behavior if PlayerColorManager is not available
            if (m_PlayerInSeat.IsLocalPlayer)
            {
                XRINetworkGameManager.LocalPlayerColor.Value = m_SeatColors[m_SeatID];
            }
        }

        SetLocalPlayer(m_PlayerInSeat.IsLocalPlayer, false);
        SetOccupied(true);
    }

    public void RemovePlayerFromSeat()
    {
        if (m_PlayerInSeat == null)
        {
            Debug.LogWarning("Trying to remove player from seat but no player is assigned to this seat.");
            return;
        }

        // Unregister player color from PlayerColorManager
        if (PlayerColorManager.Instance != null)
        {
            PlayerColorManager.Instance.UnregisterPlayerColor(m_PlayerInSeat.OwnerClientId);
        }

        m_PlayerInSeat.onNameUpdated -= SetPlayerName;
        m_PlayerInSeat.selfMuted.OnValueChanged -= UpdateSelfMutedState;
        m_PlayerInSeat.squelched.Unsubscribe(UpdateSquelchedState);

        m_HideAvatarToggle.onValueChanged.RemoveListener(SetPlayerAvatarHidden);
        m_HideAvatarToggle.SetIsOnWithoutNotify(false);

        m_MuteButton.onClick.RemoveListener(SquelchPressed);

        m_VoiceChatFillImage.fillAmount = 0;
        m_SquelchedIcon.enabled = false;
        m_PlayerInSeat = null;
        SetLocalPlayer(false);
        SetOccupied(false);
    }

    void SetPlayerAvatarHidden(bool hidden)
    {
        if (m_PlayerInSeat != null && m_PlayerInSeat.TryGetComponent(out PlayerColocation playerColocation))
            playerColocation.SetAvatarActive(!hidden);
    }
    void SquelchPressed()
    {
        m_PlayerInSeat.ToggleSquelch();
    }

    public void UpdateSelfMutedState(bool old, bool current)
    {
        m_MicIcon.sprite = current ? m_MutedSprite : m_UnmutedSprite;
    }

    void UpdateSquelchedState(bool squelched)
    {
        m_SquelchedIcon.enabled = squelched;
    }

    public void SetOccupied(bool occupied)
    {
        m_IsOccupied = occupied;
        if (!m_IsSpectator)
            m_SeatImages[1].enabled = m_IsOccupied;
        m_PlayerInSeatText.text = m_IsOccupied ? (m_IsLocalPlayer ? "You" : m_PlayerNameInSeat) : m_AvailableSeatText;

        SetHover(m_IsHovered);
    }

    public void SetHover(bool hover)
    {
        m_IsHovered = hover;
        if (!m_IsHovered)
        {
            m_HoveredObject.SetActive(false);
            ShowWorldSpaceHover(false);

            if (!m_IsSpectator)
                m_PlayerInSeatText.gameObject.SetActive(true);
            return;
        }

        m_HoveredObject.SetActive(true);

        ShowWorldSpaceHover(!m_IsOccupied);

        if (!m_IsSpectator)
            m_PlayerInSeatText.gameObject.SetActive(false);

        if (m_IsOccupied)
        {
            m_SeatUnoccupiedObject.SetActive(false);
            m_SeatOccupiedObject.SetActive(true);
            m_OwnedSetUI.SetActive(m_IsLocalPlayer);
            m_TakenSeatUI.SetActive(!m_IsLocalPlayer);
        }
        else
        {
            m_SeatUnoccupiedObject.SetActive(true);
            m_SeatOccupiedObject.SetActive(false);
        }
    }

    void ShowWorldSpaceHover(bool show)
    {
        foreach (var obj in m_WorldSpaceSeatHoverObjects)
        {
            if (obj != null)
                obj.SetActive(show);
        }
    }
}
