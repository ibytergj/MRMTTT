using UnityEngine;
using MRTabletopAssets;

/// <summary>
/// Updates the VirtualSurfaceColorShader properties based on the PlayerColorManager.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class VirtualSurfaceColorShaderUpdater : MonoBehaviour
{
    [Header("Shader Properties")]
    [SerializeField] private string m_ShaderName = "VirtualSurfaceColorShader";
    [SerializeField] private string[] m_PlayerColorPropertyNames = new string[8]
    {
        "_PlayerColor1", "_PlayerColor2", "_PlayerColor3", "_PlayerColor4",
        "_PlayerColor5", "_PlayerColor6", "_PlayerColor7", "_PlayerColor8"
    };
    [SerializeField] private string m_ActivePlayerIndexProperty = "_ActivePlayerIndex";
    [SerializeField] private string m_ActivePlayerGlowColorProperty = "_ActivePlayerGlowColor";
    [SerializeField] private string m_ActivePlayerGlowIntensityProperty = "_ActivePlayerGlowIntensity";
    [SerializeField] private string m_EffectAnimationParameterProperty = "_EffectAnimationParameter";
    [SerializeField] private string m_ShapeSidesProperty = "_ShapeSides";
    [SerializeField] private string m_HighlightModeProperty = "_HighlightMode";
    [SerializeField] private string m_PatternIntensityProperty = "_PatternIntensity";
    [SerializeField] private string m_AnimationSpeedProperty = "_AnimationSpeed";
    [SerializeField] private string m_BrightnessPulseIntensityProperty = "_BrightnessPulseIntensity";

    [Header("Active Player Highlight")]
    [SerializeField] private bool m_UsePlayerColorManagerSettings = true;
    [SerializeField] private Color m_GlowColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float m_GlowIntensity = 0.3f;
    [SerializeField] private bool m_EnablePulse = true;
    [SerializeField, Range(0.1f, 2f)] private float m_PulseSpeed = 1f;

    private Renderer m_Renderer;
    private MaterialPropertyBlock m_PropertyBlock;
    private int m_CurrentPlayerCount = 4;
    private static readonly int s_AnimParam = Shader.PropertyToID("_EffectAnimationParameter");
    private static readonly int s_HighlightModeProperty = Shader.PropertyToID("_HighlightMode");
    private static readonly int s_PatternIntensityProperty = Shader.PropertyToID("_PatternIntensity");
    private static readonly int s_AnimationSpeedProperty = Shader.PropertyToID("_AnimationSpeed");
    private static readonly int s_BrightnessPulseIntensityProperty = Shader.PropertyToID("_BrightnessPulseIntensity");

    private void Awake()
    {
        m_Renderer = GetComponent<Renderer>();
        m_PropertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        // Subscribe to PlayerColorManager events
        if (PlayerColorManager.Instance != null)
        {
            PlayerColorManager.Instance.OnPlayerColorChanged += HandlePlayerColorChanged;
            PlayerColorManager.Instance.OnSeatColorChanged += HandleSeatColorChanged;
            PlayerColorManager.Instance.OnActivePlayerChanged += HandleActivePlayerChanged;
            PlayerColorManager.Instance.OnColorPaletteChanged += HandleColorPaletteChanged;
            PlayerColorManager.Instance.OnHighlightSettingsChanged += HandleHighlightSettingsChanged;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from PlayerColorManager events
        if (PlayerColorManager.Instance != null)
        {
            PlayerColorManager.Instance.OnPlayerColorChanged -= HandlePlayerColorChanged;
            PlayerColorManager.Instance.OnSeatColorChanged -= HandleSeatColorChanged;
            PlayerColorManager.Instance.OnActivePlayerChanged -= HandleActivePlayerChanged;
            PlayerColorManager.Instance.OnColorPaletteChanged -= HandleColorPaletteChanged;
            PlayerColorManager.Instance.OnHighlightSettingsChanged -= HandleHighlightSettingsChanged;
        }
    }

    private void Start()
    {
        // Initial update of shader properties
        UpdateShaderProperties();

        // If using PlayerColorManager settings, apply them
        if (m_UsePlayerColorManagerSettings && PlayerColorManager.Instance != null)
        {
            ApplyPlayerColorManagerHighlightSettings();
        }
    }

    private void Update()
    {
        bool shouldPulse = m_EnablePulse;
        float pulseSpeed = m_PulseSpeed;

        // If using PlayerColorManager settings, get values from there
        if (m_UsePlayerColorManagerSettings && PlayerColorManager.Instance != null)
        {
            shouldPulse = PlayerColorManager.Instance.EnablePulseAnimation;
            pulseSpeed = PlayerColorManager.Instance.ActivePlayerPulseSpeed;
        }

        if (shouldPulse && PlayerColorManager.Instance != null && PlayerColorManager.Instance.ActivePlayerIndex >= 0)
        {
            // Update animation parameter for pulsing effect
            // Note: We're using Unity's built-in _Time variable in the shader now,
            // so we only need to update the animation parameter for backward compatibility
            m_Renderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetFloat(s_AnimParam, Time.time * pulseSpeed);
            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }
    }

    /// <summary>
    /// Updates all shader properties based on the current PlayerColorManager state.
    /// </summary>
    public void UpdateShaderProperties()
    {
        if (PlayerColorManager.Instance == null)
        {
            Debug.LogWarning("PlayerColorManager instance not found!");
            return;
        }

        m_Renderer.GetPropertyBlock(m_PropertyBlock);

        // Set player colors
        Color[] playerColors = PlayerColorManager.Instance.GetAllPlayerColors();
        for (int i = 0; i < playerColors.Length && i < m_PlayerColorPropertyNames.Length; i++)
        {
            m_PropertyBlock.SetColor(m_PlayerColorPropertyNames[i], playerColors[i]);
        }

        // Set active player index
        int activePlayerIndex = PlayerColorManager.Instance.ActivePlayerIndex;
        m_PropertyBlock.SetFloat(m_ActivePlayerIndexProperty, activePlayerIndex);

        // Set active player glow properties
        // If using PlayerColorManager settings, get values from there
        if (m_UsePlayerColorManagerSettings && PlayerColorManager.Instance != null)
        {
            m_PropertyBlock.SetColor(m_ActivePlayerGlowColorProperty, PlayerColorManager.Instance.ActivePlayerGlowColor);
            m_PropertyBlock.SetFloat(m_ActivePlayerGlowIntensityProperty, PlayerColorManager.Instance.ActivePlayerGlowIntensity);

            // Set accessibility highlight properties
            m_PropertyBlock.SetFloat(s_HighlightModeProperty, (float)PlayerColorManager.Instance.HighlightMode);
            m_PropertyBlock.SetFloat(s_PatternIntensityProperty, PlayerColorManager.Instance.PatternIntensity);
            m_PropertyBlock.SetFloat(s_AnimationSpeedProperty, PlayerColorManager.Instance.AnimationSpeed);
            m_PropertyBlock.SetFloat(s_BrightnessPulseIntensityProperty, PlayerColorManager.Instance.BrightnessPulseIntensity);

            // Log the values for debugging
            Debug.Log($"VirtualSurfaceColorShaderUpdater: Setting accessibility highlight properties - " +
                      $"Mode: {PlayerColorManager.Instance.HighlightMode}, " +
                      $"PatternIntensity: {PlayerColorManager.Instance.PatternIntensity}, " +
                      $"AnimationSpeed: {PlayerColorManager.Instance.AnimationSpeed}, " +
                      $"BrightnessPulseIntensity: {PlayerColorManager.Instance.BrightnessPulseIntensity}");
        }
        else
        {
            m_PropertyBlock.SetColor(m_ActivePlayerGlowColorProperty, m_GlowColor);
            m_PropertyBlock.SetFloat(m_ActivePlayerGlowIntensityProperty, m_GlowIntensity);

            // Set default values for accessibility highlight properties
            m_PropertyBlock.SetFloat(s_HighlightModeProperty, 3f); // Combined mode
            m_PropertyBlock.SetFloat(s_PatternIntensityProperty, 0.5f);
            m_PropertyBlock.SetFloat(s_AnimationSpeedProperty, 1.0f);
            m_PropertyBlock.SetFloat(s_BrightnessPulseIntensityProperty, 0.3f);
        }

        // Set shape sides based on player count
        m_PropertyBlock.SetFloat(m_ShapeSidesProperty, m_CurrentPlayerCount);

        m_Renderer.SetPropertyBlock(m_PropertyBlock);
    }

    /// <summary>
    /// Updates the player count and refreshes shader properties.
    /// </summary>
    public void UpdatePlayerCount(int playerCount)
    {
        m_CurrentPlayerCount = Mathf.Clamp(playerCount, 2, 8);
        UpdateShaderProperties();
    }

    #region Event Handlers

    private void HandlePlayerColorChanged(ulong playerID, Color newColor)
    {
        UpdateShaderProperties();
    }

    private void HandleSeatColorChanged(int seatIndex, Color newColor)
    {
        UpdateShaderProperties();
    }

    private void HandleActivePlayerChanged(int newActivePlayerIndex)
    {
        UpdateActivePlayer(newActivePlayerIndex);
    }

    /// <summary>
    /// Explicitly updates the active player index in the shader.
    /// This can be called directly when needed to force an update.
    /// </summary>
    public void UpdateActivePlayer(int activePlayerIndex)
    {
        Debug.Log($"VirtualSurfaceColorShaderUpdater: Updating active player index to {activePlayerIndex}");
        m_Renderer.GetPropertyBlock(m_PropertyBlock);
        m_PropertyBlock.SetFloat(m_ActivePlayerIndexProperty, activePlayerIndex);
        m_Renderer.SetPropertyBlock(m_PropertyBlock);
    }

    private void HandleColorPaletteChanged()
    {
        UpdateShaderProperties();
    }

    private void HandleHighlightSettingsChanged()
    {
        // If using PlayerColorManager settings, apply the updated settings
        if (m_UsePlayerColorManagerSettings)
        {
            ApplyPlayerColorManagerHighlightSettings();
        }
    }

    private void ApplyPlayerColorManagerHighlightSettings()
    {
        if (PlayerColorManager.Instance == null)
            return;

        m_GlowColor = PlayerColorManager.Instance.ActivePlayerGlowColor;
        m_GlowIntensity = PlayerColorManager.Instance.ActivePlayerGlowIntensity;
        m_EnablePulse = PlayerColorManager.Instance.EnablePulseAnimation;
        m_PulseSpeed = PlayerColorManager.Instance.ActivePlayerPulseSpeed;

        // Update shader properties with new settings
        UpdateShaderProperties();

        // Log that we've applied the settings
        Debug.Log("VirtualSurfaceColorShaderUpdater: Applied PlayerColorManager highlight settings");
    }

    #endregion
}
