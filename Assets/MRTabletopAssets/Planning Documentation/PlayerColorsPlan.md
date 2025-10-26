# Player Color Management System Plan

## Overview

This document outlines the plan for extending the tabletop system from supporting 4 players to 8 players, with a focus on player color management. The system will provide a consistent way to manage player colors across the game, handle color conflicts, and notify game components of color changes.

## Requirements

### 1. Player Support Extension
- Extend player support from 4 to 8 players
- Update seat positioning for 5-8 players using regular polygon shapes
- Implement logical player mapping for consistent clockwise ordering

### 2. Color Management
- Create a centralized PlayerColorManager to handle player colors
- Support both custom player colors and seat-based fallback colors
- Ensure colors remain unique among players in a session
- Maintain consistent player-color mapping for gameplay purposes

### 3. Shader Integration
- Update VirtualSurfaceColorShader to support 8 players
- Implement active player highlighting using shader properties
- Support multiple colormaps for different player counts

### 4. User Experience
- Allow players to select and persist their preferred colors
- Handle color conflicts gracefully with minimal disruption
- Maintain player color identity during seat swaps

## Current Implementation
- Player colors are currently defined in multiple places:
  - `PlayerAppearanceMenu.cs` - Defines player colors for the player appearance menu
  - `TableTopSeatButton.cs` - Defines seat colors for the seat buttons
  - `VirtualSurfaceColorShader.shadergraph` - Defines player colors for the shader
- There is no centralized player color management system
- The system currently supports only 4 players

## Implementation Details

### PlayerColorManager

The PlayerColorManager will be the central component for managing player colors:

```csharp
public class PlayerColorManager : NetworkBehaviour
{
    // Singleton instance
    public static PlayerColorManager Instance { get; private set; }

    // Default player colors (8 distinct colors)
    [SerializeField] private Color[] m_PlayerColors = new Color[8];

    // Active player index for highlighting
    [SerializeField] private int m_ActivePlayerIndex = -1;

    // Active player highlighting settings
    [Header("Active Player Highlighting")]
    [SerializeField] private Color m_ActivePlayerGlowColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float m_ActivePlayerGlowIntensity = 0.3f;
    [SerializeField, Range(0.1f, 2f)] private float m_ActivePlayerPulseSpeed = 1.0f;
    [SerializeField] private bool m_EnablePulseAnimation = true;

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
    private NetworkList<Color> m_HostColorPalette = new NetworkList<Color>();
    private bool m_InNetworkedSession = false;
    private Color[] m_LocalColorPaletteBackup = new Color[8];

    // Properties
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
}
```

#### Default Colors

The default player colors will match the Seat Button Variant Prefab:
1. Blue: RGB(0, 0, 1)
2. Orange: RGB(1, 0.5, 0)
3. Yellow: RGB(1, 1, 0)
4. Purple: RGB(0.5, 0, 0.5)
5. Red: RGB(1, 0, 0)
6. Green: RGB(0, 0.72, 0)
7. Black: RGB(0, 0, 0)
8. White: RGB(1, 1, 1)

### Host-Authoritative Color Management

The system will use the host's color palette as the authoritative source for fallback colors:

1. **Color Palette Synchronization**:
   - Host's color palette is synchronized to all clients using NetworkList
   - Clients back up their local palette before using the host's
   - When leaving a session, clients restore their local palette

2. **Conflict Resolution**:
   - Try to use player's preferred color first
   - If that's taken, try to use the seat color from host's palette
   - If that's also taken, find any available color from host's palette
   - If all standard colors are taken, generate a unique color

3. **Color Reservation System**:
   - Players are prioritized based on join order or role
   - Host always gets highest priority for color conflicts
   - First player to join with a color gets priority for that color

### Event Notification System

The PlayerColorManager provides several events that game developers can subscribe to:

1. **OnPlayerColorChanged**:
   - Fired when any player's color changes
   - Provides playerID and newColor
   - If newColor is Color.clear, the player has left

2. **OnPlayerColorConflictResolved**:
   - Fired when a player's preferred color couldn't be assigned
   - Provides playerID, requestedColor, and assignedColor

3. **OnPlayerSeatChanged**:
   - Fired when a player's seat assignment changes
   - Provides playerID, oldSeatIndex, and newSeatIndex

4. **OnSeatColorChanged**:
   - Fired when a seat's color changes
   - Provides seatIndex and newColor

5. **OnActivePlayerChanged**:
   - Fired when the active player changes
   - Provides the new activePlayerIndex

6. **OnColorPaletteChanged**:
   - Fired when the color palette changes
   - Typically happens when joining a host with a different palette

### Seat Positioning and Player Mapping

For 5-8 players, seats will be positioned in regular polygon shapes:

1. **5 Players**: Pentagon (72° between seats)
2. **6 Players**: Hexagon (60° between seats)
3. **7 Players**: Heptagon (≈51.4° between seats)
4. **8 Players**: Octagon (45° between seats)

For 4 or fewer players, the existing physical order will be maintained:
- Player 0: Bottom (0°)
- Player 1: Top (180°)
- Player 2: Right (270°)
- Player 3: Left (90°)

### Seat Swapping Support

The system will maintain player color identity during seat swaps:

1. **Detecting Seat Swaps**:
   - Check if a player already has a registered color
   - If yes, it's a seat swap; if no, it's a new player

2. **Maintaining Color Identity**:
   - During seat swaps, maintain the player's existing color
   - Only update the seat registration, not the color assignment

3. **Updating Visualizations**:
   - Notify the VirtualSurfaceColorShader of seat changes
   - Update the shader to reflect new seat assignments without changing colors

### Active Player Highlighting Settings

The PlayerColorManager will include configurable settings for active player highlighting:

1. **Glow Color**:
   - Default color for the active player highlight effect
   - Configurable in the inspector (default: white/Color.white)
   - Network-synchronized to ensure consistent appearance across clients

2. **Glow Intensity**:
   - Controls the strength of the highlight effect
   - Range from 0 (no effect) to 1 (maximum effect)
   - Configurable in the inspector (default: 0.3)
   - Network-synchronized to ensure consistent appearance across clients

3. **Pulse Animation Speed**:
   - Controls how quickly the highlight effect pulses
   - Range from 0.1 (very slow) to 2 (very fast)
   - Configurable in the inspector (default: 1.0)
   - Network-synchronized to ensure consistent appearance across clients

4. **Enable Pulse Animation**:
   - Toggles whether the highlight effect pulses or remains static
   - Configurable in the inspector (default: true)
   - Network-synchronized to ensure consistent appearance across clients

5. **Network Synchronization**:
   - Host is the authoritative source for highlight settings
   - Changes are synchronized to all clients using ClientRpc methods
   - Event notification when settings change

```csharp
// Methods for setting highlight properties with network synchronization
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

// ClientRpc methods for synchronizing highlight settings
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
```

### Integration with VirtualSurfaceColorShader

The VirtualSurfaceColorShader will be updated to support 8 players:

1. **Shader Properties**:
   - Add properties for 8 player colors (_PlayerColor1 through _PlayerColor8)
   - Add properties for active player highlighting (_ActivePlayerIndex, _ActivePlayerGlowColor, _ActivePlayerGlowIntensity)

2. **Color Assignment**:
   - Get colors from PlayerColorManager based on seat assignments
   - Update shader properties when colors or seats change

3. **Active Player Highlighting**:
   - Implement glow effect for the active player's color segment
   - Control intensity and color of the highlight effect
   - Add animation support for dynamic effects

### Updating Unity Shader Graph for Active Player Color Effect

This section outlines the steps to modify the VirtualSurfaceColorShader to apply a special effect (like a glow or animation) to the color segment corresponding to the currently active player.

**Core Idea:**

We will pass the index of the active player to the shader. Inside the Custom Function, we'll determine which player's segment each pixel belongs to and apply an effect if it matches the active player's index.

#### Shader Properties Details

In the VirtualSurfaceColorShader Shader Graph's Blackboard, add the following properties:

- **Name:** `Active Player Index`
  - **Reference:** `_ActivePlayerIndex`
  - **Type:** Float
  - **Scope:** Per Material
  - **Default Value:** -1 (No active player)

- **Name:** `Active Player Glow Color`
  - **Reference:** `_ActivePlayerGlowColor`
  - **Type:** Color
  - **Scope:** Per Material
  - **Mode:** HDR (for brighter glows)
  - **Default Value:** White (1, 1, 1, 1)

- **Name:** `Active Player Glow Intensity`
  - **Reference:** `_ActivePlayerGlowIntensity`
  - **Type:** Float
  - **Scope:** Per Material
  - **Mode:** Slider (Range 0 to 5)
  - **Default Value:** 0.3

- **Name:** `Effect Animation Parameter`
  - **Reference:** `_EffectAnimationParameter`
  - **Type:** Float
  - **Scope:** Per Material
  - **Default Value:** 0

#### Custom Function Node Updates

Add the following inputs to the `GetPlayerBorderColor` Custom Function node:

- **Name:** `ActivePlayerIdx`
  - **Type:** Float
- **Name:** `GlowColor`
  - **Type:** Vector4
- **Name:** `GlowIntensity`
  - **Type:** Float
- **Name:** `AnimParam`
  - **Type:** Float

Connect the newly created Blackboard properties to the corresponding inputs on the Custom Function node.

#### HLSL Code Implementation

Update the HLSL code in the "Body" field of the Custom Function node to implement the active player highlighting effect:

```hlsl
// This is the content for the "Body" field of the Custom Function node.
// Assumes BlendWidth, ActivePlayerIdx, GlowColor, GlowIntensity, AnimParam are float inputs.

void GetPlayerBorderColor_float( // Ensure this matches your Node's Name + _float
    float NormAngle,
    float Sides,
    float4 C1, float4 C2, float4 C3, float4 C4,
    float4 C5, float4 C6, float4 C7, float4 C8,
    float BlendWidth, // Existing input
    float ActivePlayerIdx, // New input
    float4 GlowColor, // New input
    float GlowIntensity, // New input
    float AnimParam, // New input for animation
    out float4 OutColor)
{
    float4 initialPlayerColors[8];
    initialPlayerColors[0] = C1; initialPlayerColors[1] = C2;
    initialPlayerColors[2] = C3; initialPlayerColors[3] = C4;
    initialPlayerColors[4] = C5; initialPlayerColors[5] = C6;
    initialPlayerColors[6] = C7; initialPlayerColors[7] = C8;

    float4 colorsForBlending[8];
    float effectiveAngleForLogic;
    int numSides = (int)Sides;

    for(int i=0; i<8; ++i) { colorsForBlending[i] = initialPlayerColors[0]; } // Default fallback

    if (abs(Sides - 4.0) < 0.01) // Special case for 4 players
    {
        // This is the color order that you said "blue, purple, orange, yellow" CCW from blue (P1) at bottom
        // P1 (Blue) at Bottom
        // P4 (Purple) at Right (next CCW)
        // P2 (Orange) at Top (next CCW)
        // P3 (Yellow) at Left (next CCW)
        colorsForBlending[0] = initialPlayerColors[0]; // P1
        colorsForBlending[1] = initialPlayerColors[3]; // P4
        colorsForBlending[2] = initialPlayerColors[1]; // P2
        colorsForBlending[3] = initialPlayerColors[2]; // P3

        // Apply highlight to active player color
        if (ActivePlayerIdx >= 0 && ActivePlayerIdx < 4)
        {
            // Map active player index to the correct color index in the 4-player layout
            int mappedIndex = -1;
            if (ActivePlayerIdx == 0) mappedIndex = 0;      // P1 -> 0
            else if (ActivePlayerIdx == 1) mappedIndex = 2; // P2 -> 2
            else if (ActivePlayerIdx == 2) mappedIndex = 3; // P3 -> 3
            else if (ActivePlayerIdx == 3) mappedIndex = 1; // P4 -> 1

            if (mappedIndex >= 0)
            {
                // Apply highlight effect
                float pulseIntensity = GlowIntensity;
                if (AnimParam > 0)
                {
                    // Add pulsing effect if animation is enabled
                    pulseIntensity = GlowIntensity * (0.7 + 0.3 * sin(AnimParam * 3.14159));
                }
                colorsForBlending[mappedIndex] = lerp(colorsForBlending[mappedIndex], GlowColor, pulseIntensity);
            }
        }

        effectiveAngleForLogic = NormAngle; // Your successful finding for 4-player angle
    }
    else // For 3, 5, 6, 7, 8 players
    {
        // Players C1, C2, C3...
        for (int i = 0; i < numSides; ++i) {
            if (i < 8) colorsForBlending[i] = initialPlayerColors[i];
        }
        if (numSides > 0 && numSides < 8) { // Fill unused slots for safety
             for (int i = numSides; i < 8; ++i) {
                colorsForBlending[i] = initialPlayerColors[0];
            }
        }

        // Apply highlight to active player color
        if (ActivePlayerIdx >= 0 && ActivePlayerIdx < numSides)
        {
            // Apply highlight effect
            float pulseIntensity = GlowIntensity;
            if (AnimParam > 0)
            {
                // Add pulsing effect if animation is enabled
                pulseIntensity = GlowIntensity * (0.7 + 0.3 * sin(AnimParam * 3.14159));
            }
            colorsForBlending[ActivePlayerIdx] = lerp(colorsForBlending[ActivePlayerIdx], GlowColor, pulseIntensity);
        }

        // REVISED ANGLE LOGIC for N-players (Sides != 4):
        // Aim: If C1 is currently at Right, and order is CCW, this angle makes Right=0 and proceeds CCW.
        effectiveAngleForLogic = NormAngle;
        effectiveAngleForLogic = frac(effectiveAngleForLogic + 1.0);
    }

    if (numSides <= 0) {
        OutColor = C1;
        OutColor.a = 1.0;
        return;
    }

    // Rest of the function remains the same...
    // Calculate the final color based on the angle and blend width
    float scaledAngle = effectiveAngleForLogic * numSides;
    int colorBaseIndex = (int)floor(scaledAngle);
    float segmentLerpFactor = frac(scaledAngle);

    // Blend sharpness
    float halfBlendWidth = BlendWidth * 0.5;
    halfBlendWidth = max(halfBlendWidth, 0.0001);
    float sharpenedFactor = smoothstep(0.5 - halfBlendWidth, 0.5 + halfBlendWidth, segmentLerpFactor);

    int actualIndex1 = (colorBaseIndex % numSides + numSides) % numSides;
    int actualIndex2 = ((colorBaseIndex + 1) % numSides + numSides) % numSides;

    int maxValidIndex = min(numSides - 1, 7);
    actualIndex1 = clamp(actualIndex1, 0, maxValidIndex);
    actualIndex2 = clamp(actualIndex2, 0, maxValidIndex);

    float4 colorA = colorsForBlending[actualIndex1];
    float4 colorB = colorsForBlending[actualIndex2];

    OutColor = lerp(colorA, colorB, sharpenedFactor);
    OutColor.a = 1.0;
}
```

#### Animation Implementation in VirtualSurfaceColorShaderUpdater

The VirtualSurfaceColorShaderUpdater script will handle updating the animation parameter and will integrate with the PlayerColorManager's highlighting settings:

```csharp
[Header("Active Player Highlight")]
[SerializeField] private bool m_UsePlayerColorManagerSettings = true;
[SerializeField] private Color m_GlowColor = Color.white;
[SerializeField, Range(0f, 1f)] private float m_GlowIntensity = 0.3f;
[SerializeField] private bool m_EnablePulse = true;
[SerializeField, Range(0.1f, 2f)] private float m_PulseSpeed = 1f;

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
        m_Renderer.GetPropertyBlock(m_PropertyBlock);
        m_PropertyBlock.SetFloat(s_AnimParam, Time.time * pulseSpeed);
        m_Renderer.SetPropertyBlock(m_PropertyBlock);
    }
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
}

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
    if (m_UsePlayerColorManagerSettings)
    {
        m_PropertyBlock.SetColor(m_ActivePlayerGlowColorProperty, PlayerColorManager.Instance.ActivePlayerGlowColor);
        m_PropertyBlock.SetFloat(m_ActivePlayerGlowIntensityProperty, PlayerColorManager.Instance.ActivePlayerGlowIntensity);
    }
    else
    {
        m_PropertyBlock.SetColor(m_ActivePlayerGlowColorProperty, m_GlowColor);
        m_PropertyBlock.SetFloat(m_ActivePlayerGlowIntensityProperty, m_GlowIntensity);
    }

    // Set shape sides based on player count
    m_PropertyBlock.SetFloat(m_ShapeSidesProperty, m_CurrentPlayerCount);

    m_Renderer.SetPropertyBlock(m_PropertyBlock);
}
```

#### Important Considerations

- **Performance:** Adding complex effects can impact performance, especially on mobile. Keep the effect logic concise.
- **Transparency:** If the border is not fully opaque, you might need to adjust how the glow affects the alpha channel.
- **Effect Types:** The example adds color for a glow. You could modify the effect logic to apply other effects, like changing hue, saturation, or pulsing transparency.
- **Active Player Index Source:** The active player index will come from the PlayerColorManager, which will be updated by the game logic.

### Single-Player and Multiplayer Transitions

The system will handle transitions between modes gracefully:

1. **Joining a Multiplayer Session**:
   - Back up local color palette
   - Adopt host's palette for fallback colors
   - Try to use player's preferred color first

2. **Leaving a Multiplayer Session**:
   - Restore local color palette
   - Restore player's preferred color from PlayerPrefs
   - Clear all player-seat assignments

3. **Persistence**:
   - Save player color preferences to PlayerPrefs
   - Restore preferences when appropriate
   - Maintain separation between preferences and actual assignments

## Implementation Steps

1. **Create PlayerColorManager**:
   - Implement singleton pattern
   - Define default colors
   - Add core color management methods

2. **Add Network Synchronization**:
   - Implement NetworkList for host color palette
   - Add RPCs for color and seat change notifications
   - Handle session transitions

3. **Implement Conflict Resolution**:
   - Add methods to detect and resolve color conflicts
   - Implement priority-based resolution
   - Add unique color generation for edge cases

4. **Add Event System**:
   - Define all events
   - Trigger events at appropriate times
   - Document usage for game developers

5. **Update TableTop and TableTopSeatButton**:
   - Modify to use PlayerColorManager
   - Add seat tracking
   - Handle seat swaps

6. **Update VirtualSurfaceColorShader**:
   - Add support for 8 players
   - Implement active player highlighting
   - Update shader properties from PlayerColorManager

7. **Implement Color Selection UI**:
   - Create a dedicated color selection panel in the player menu
   - Display all available colors from PlayerColorManager
   - Indicate which colors are already taken by other players
   - Allow players to select and preview their color before joining a seat
   - Save color preferences to PlayerPrefs for persistence across sessions
   - Integrate with the host-authoritative color management system
   - Handle color conflicts gracefully with visual feedback

8. **Test and Validate**:
   - Test with different player counts
   - Verify color conflict resolution
   - Test seat swapping
   - Validate event notifications
   - Test color selection UI with multiple players

## Usage Examples

### Subscribing to Color Change Events

```csharp
void Start()
{
    PlayerColorManager.Instance.OnPlayerColorChanged += HandlePlayerColorChanged;
}

void HandlePlayerColorChanged(ulong playerID, Color newColor)
{
    // Update game pieces, UI elements, etc.
    if (newColor == Color.clear)
    {
        // Player left, clean up their elements
    }
    else
    {
        // Update player's elements to the new color
    }
}
```

### Registering a Player's Color

```csharp
// When a player joins or selects a color
Color preferredColor = XRINetworkGameManager.LocalPlayerColor.Value;
Color assignedColor = PlayerColorManager.Instance.RegisterPlayerColor(
    NetworkManager.Singleton.LocalClientId, preferredColor);

// Update the local player color to the assigned color
XRINetworkGameManager.LocalPlayerColor.Value = assignedColor;
```

### Updating Shader Properties

```csharp
// In VirtualSurfaceColorShaderUpdater
public void UpdateShaderProperties()
{
    // Get player colors from PlayerColorManager
    if (PlayerColorManager.Instance != null)
    {
        Color[] playerColors = PlayerColorManager.Instance.GetAllPlayerColors();

        // Set player colors in shader
        for (int i = 0; i < 8; i++)
        {
            int propertyId = GetPlayerColorPropertyId(i);
            m_PropertyBlock.SetColor(propertyId, playerColors[i]);
        }
    }

    // Set active player index
    int activePlayerIndex = PlayerColorManager.Instance?.ActivePlayerIndex ?? -1;
    m_PropertyBlock.SetFloat("_ActivePlayerIndex", activePlayerIndex);

    m_Renderer.SetPropertyBlock(m_PropertyBlock);
}
```

### Handling Seat Swaps

```csharp
// In TableTopSeatButton
public void AssignPlayerToSeat(XRINetworkPlayer player)
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
        Color preferredColor = XRINetworkGameManager.LocalPlayerColor.Value;
        Color assignedColor = PlayerColorManager.Instance.RegisterPlayerColor(
            player.OwnerClientId, preferredColor, m_SeatID);

        // Update the local player color
        if (player.IsLocalPlayer)
        {
            XRINetworkGameManager.LocalPlayerColor.Value = assignedColor;
        }
    }
}
```

### Handling Network Transitions

```csharp
// When joining a multiplayer session
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

    m_InNetworkedSession = true;

    if (IsServer)
    {
        // Host initializes the network color palette
        InitializeHostColorPalette();
    }
    else
    {
        // Clients back up their local palette
        BackupLocalColorPalette();
    }
}

// When leaving a multiplayer session
public override void OnNetworkDespawn()
{
    base.OnNetworkDespawn();

    m_InNetworkedSession = false;

    if (!IsServer)
    {
        // Restore local color palette
        RestoreLocalColorPalette();
    }
}
```

### Color Selection UI Implementation

The Color Selection UI will provide players with a dedicated interface to choose their preferred color. This UI will operate in two distinct modes depending on whether the player has joined a networked session, and will be integrated into the existing player menu system.

#### UI Components

1. **Color Selection Panel**:
   - A dedicated panel in the player menu
   - Grid or horizontal layout of color swatches
   - Mode indicator showing whether in Pre-join or In-game mode
   - Preview of the selected color
   - Confirmation button to apply the selected color

2. **Color Swatch**:
   - Visual representation of each available color
   - Selectable button with color preview
   - Visual state for available, taken, and selected
   - Tooltip or label showing color name

3. **Color Preview**:
   - Larger preview of the currently selected color
   - Applied to a player avatar or icon
   - Visual feedback for how the color will appear in-game

4. **Status Indicators**:
   - Visual feedback for color conflicts
   - Indication of which colors are already taken (In-game mode only)
   - Notification when a preferred color cannot be assigned
   - Clear indication of current mode (Pre-join vs In-game)

#### Two-Mode Operation

1. **Pre-join Mode**:
   - Active when player hasn't joined a networked session yet
   - Displays all available colors from the PlayerColorManager
   - Allows selection of any color as a preference
   - Saves preference to PlayerPrefs
   - Clearly indicates this is a preference that may change when joining a game
   - No indication of "taken" colors since this information isn't available yet
   - UI messaging emphasizes this is a preference only

2. **In-game Mode**:
   - Active once player has joined a networked session
   - Displays all colors from the PlayerColorManager
   - Visually indicates which colors are already taken by other players
   - Allows selection only from available colors
   - Shows the player's currently assigned color
   - Provides visual feedback when a preferred color cannot be assigned
   - UI messaging emphasizes these are the actual available options

#### Player Workflow

1. **Pre-join Color Selection**:
   - Player opens the player menu before joining a game
   - Navigates to the color selection panel (in Pre-join mode)
   - Views all possible colors
   - Selects a preferred color
   - Confirms selection
   - Color preference is saved to PlayerPrefs
   - UI indicates this is a preference only

2. **In-game Color Selection**:
   - Player opens the player menu after joining a game
   - Navigates to the color selection panel (in In-game mode)
   - Views available colors (with taken colors visually indicated)
   - Selects an available color
   - Confirms selection
   - Color is registered with PlayerColorManager
   - UI updates to show the player's assigned color

3. **Joining a Networked Session**:
   - Player joins a networked session with a saved color preference
   - System attempts to assign the preferred color
   - If color is available, it's assigned
   - If color is taken, conflict resolution is applied
   - Color selection UI updates to In-game mode
   - UI shows the player's assigned color and which colors are taken

4. **Color Conflict Resolution**:
   - If preferred color is taken, system finds an alternative
   - Player receives visual notification of the conflict
   - Player can open the color selection UI (in In-game mode)
   - UI shows which colors are available and which are taken
   - Player selects a new available color
   - Updated color is registered with PlayerColorManager

#### Integration with PlayerColorManager

```csharp
// In ColorSelectionUI.cs
public class ColorSelectionUI : MonoBehaviour
{
    public enum UIMode
    {
        PreJoin,    // Before joining a networked session
        InGame      // After joining a networked session
    }

    [Header("UI References")]
    [SerializeField] private GameObject m_ColorSwatchPrefab;
    [SerializeField] private Transform m_ColorSwatchContainer;
    [SerializeField] private Image m_ColorPreview;
    [SerializeField] private Button m_ConfirmButton;
    [SerializeField] private TextMeshProUGUI m_ModeIndicatorText;
    [SerializeField] private TextMeshProUGUI m_InstructionsText;
    [SerializeField] private GameObject m_ConflictNotificationPanel;
    [SerializeField] private TextMeshProUGUI m_ConflictMessageText;

    [Header("Mode-specific UI Elements")]
    [SerializeField] private GameObject m_PreJoinModeElements;
    [SerializeField] private GameObject m_InGameModeElements;

    [Header("UI Text")]
    [SerializeField] private string m_PreJoinModeText = "COLOR PREFERENCE";
    [SerializeField] private string m_InGameModeText = "COLOR SELECTION";
    [SerializeField] private string m_PreJoinInstructionsText = "Select your preferred color. This may change when joining a game if the color is already taken.";
    [SerializeField] private string m_InGameInstructionsText = "Select an available color. Colors already taken by other players are disabled.";
    [SerializeField] private string m_ConflictMessageFormat = "Your preferred color {0} was already taken. You've been assigned {1} instead.";

    private Color m_SelectedColor;
    private List<ColorSwatchUI> m_ColorSwatches = new List<ColorSwatchUI>();
    private UIMode m_CurrentMode = UIMode.PreJoin;

    private void Start()
    {
        // Initialize color swatches from PlayerColorManager
        if (PlayerColorManager.Instance != null)
        {
            Color[] availableColors = PlayerColorManager.Instance.GetAllPlayerColors();

            for (int i = 0; i < availableColors.Length; i++)
            {
                GameObject swatchObj = Instantiate(m_ColorSwatchPrefab, m_ColorSwatchContainer);
                ColorSwatchUI swatch = swatchObj.GetComponent<ColorSwatchUI>();

                swatch.Initialize(availableColors[i], i);
                swatch.OnColorSelected += HandleColorSelected;

                m_ColorSwatches.Add(swatch);
            }

            // Subscribe to PlayerColorManager events
            PlayerColorManager.Instance.OnPlayerColorChanged += HandlePlayerColorChanged;
            PlayerColorManager.Instance.OnColorPaletteChanged += HandleColorPaletteChanged;

            // Subscribe to network connection events
            XRINetworkGameManager.Connected.Subscribe(HandleNetworkConnectionChanged);
        }

        // Load saved color preference
        string savedColorHex = PlayerPrefs.GetString("PreferredPlayerColor", "");
        if (!string.IsNullOrEmpty(savedColorHex) && ColorUtility.TryParseHtmlString(savedColorHex, out Color savedColor))
        {
            SelectColor(savedColor);
        }
        else if (m_ColorSwatches.Count > 0)
        {
            // Default to first available color
            SelectColor(m_ColorSwatches[0].Color);
        }

        // Set up confirm button
        m_ConfirmButton.onClick.AddListener(ConfirmColorSelection);

        // Hide conflict notification initially
        if (m_ConflictNotificationPanel != null)
            m_ConflictNotificationPanel.SetActive(false);

        // Determine initial mode
        SetUIMode(DetermineCurrentMode());
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (PlayerColorManager.Instance != null)
        {
            PlayerColorManager.Instance.OnPlayerColorChanged -= HandlePlayerColorChanged;
            PlayerColorManager.Instance.OnColorPaletteChanged -= HandleColorPaletteChanged;
        }

        XRINetworkGameManager.Connected.Unsubscribe(HandleNetworkConnectionChanged);
    }

    private UIMode DetermineCurrentMode()
    {
        // Check if we're in a networked session
        bool isNetworked = PlayerColorManager.Instance != null && PlayerColorManager.Instance.IsNetworked;
        return isNetworked ? UIMode.InGame : UIMode.PreJoin;
    }

    private void SetUIMode(UIMode mode)
    {
        m_CurrentMode = mode;

        // Update UI elements based on mode
        if (m_ModeIndicatorText != null)
            m_ModeIndicatorText.text = (mode == UIMode.PreJoin) ? m_PreJoinModeText : m_InGameModeText;

        if (m_InstructionsText != null)
            m_InstructionsText.text = (mode == UIMode.PreJoin) ? m_PreJoinInstructionsText : m_InGameInstructionsText;

        if (m_PreJoinModeElements != null)
            m_PreJoinModeElements.SetActive(mode == UIMode.PreJoin);

        if (m_InGameModeElements != null)
            m_InGameModeElements.SetActive(mode == UIMode.InGame);

        // In Pre-join mode, all colors are available
        // In In-game mode, only untaken colors are available
        UpdateColorAvailability();
    }

    private void HandleNetworkConnectionChanged(bool connected)
    {
        // Update UI mode when network connection changes
        SetUIMode(connected ? UIMode.InGame : UIMode.PreJoin);

        if (connected)
        {
            // We just connected to a network session
            // Try to apply our preferred color
            ApplyPreferredColorInNetworkedSession();
        }
    }

    private void ApplyPreferredColorInNetworkedSession()
    {
        if (PlayerColorManager.Instance == null || !PlayerColorManager.Instance.IsNetworked)
            return;

        // Get our preferred color
        string savedColorHex = PlayerPrefs.GetString("PreferredPlayerColor", "");
        if (!string.IsNullOrEmpty(savedColorHex) && ColorUtility.TryParseHtmlString(savedColorHex, out Color preferredColor))
        {
            // Try to register this color with the PlayerColorManager
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            Color assignedColor = PlayerColorManager.Instance.RegisterPlayerColor(localClientId, preferredColor);

            // Update XRINetworkGameManager
            XRINetworkGameManager.LocalPlayerColor.Value = assignedColor;

            // If color was changed due to conflict, update UI and show notification
            if (assignedColor != preferredColor)
            {
                SelectColor(assignedColor);
                ShowColorConflictNotification(preferredColor, assignedColor);
            }
            else
            {
                SelectColor(preferredColor);
            }
        }
    }

    private void HandleColorSelected(Color color, int colorIndex)
    {
        SelectColor(color);
    }

    private void SelectColor(Color color)
    {
        m_SelectedColor = color;
        m_ColorPreview.color = color;

        // Update swatch selection states
        foreach (var swatch in m_ColorSwatches)
        {
            swatch.SetSelected(swatch.Color == color);
        }
    }

    private void ConfirmColorSelection()
    {
        // Save to PlayerPrefs regardless of mode
        PlayerPrefs.SetString("PreferredPlayerColor", "#" + ColorUtility.ToHtmlStringRGB(m_SelectedColor));

        // Update XRINetworkGameManager
        XRINetworkGameManager.LocalPlayerColor.Value = m_SelectedColor;

        if (m_CurrentMode == UIMode.InGame)
        {
            // In In-game mode, register with PlayerColorManager
            if (PlayerColorManager.Instance != null && PlayerColorManager.Instance.IsNetworked)
            {
                ulong localClientId = NetworkManager.Singleton.LocalClientId;
                Color assignedColor = PlayerColorManager.Instance.RegisterPlayerColor(localClientId, m_SelectedColor);

                // If color was changed due to conflict, update UI
                if (assignedColor != m_SelectedColor)
                {
                    SelectColor(assignedColor);
                    ShowColorConflictNotification(m_SelectedColor, assignedColor);
                }
            }
        }
        else
        {
            // In Pre-join mode, just save the preference
            // Actual registration will happen when joining a session
        }
    }

    private void HandlePlayerColorChanged(ulong playerID, Color newColor)
    {
        // Only update availability in In-game mode
        if (m_CurrentMode == UIMode.InGame)
        {
            UpdateColorAvailability();
        }
    }

    private void HandleColorPaletteChanged()
    {
        UpdateColorPalette();
    }

    private void UpdateColorAvailability()
    {
        if (PlayerColorManager.Instance == null)
            return;

        if (m_CurrentMode == UIMode.PreJoin)
        {
            // In Pre-join mode, all colors are available
            foreach (var swatch in m_ColorSwatches)
            {
                swatch.SetAvailable(true);
            }
        }
        else
        {
            // In In-game mode, only untaken colors are available
            foreach (var swatch in m_ColorSwatches)
            {
                bool isAvailable = !PlayerColorManager.Instance.IsColorTaken(swatch.Color);

                // If this is our currently assigned color, it's available to us
                ulong localClientId = NetworkManager.Singleton.LocalClientId;
                if (PlayerColorManager.Instance.GetPlayerColorByID(localClientId) == swatch.Color)
                {
                    isAvailable = true;
                }

                swatch.SetAvailable(isAvailable);
            }
        }
    }

    private void UpdateColorPalette()
    {
        if (PlayerColorManager.Instance == null)
            return;

        Color[] availableColors = PlayerColorManager.Instance.GetAllPlayerColors();

        // Update swatch colors
        for (int i = 0; i < m_ColorSwatches.Count && i < availableColors.Length; i++)
        {
            m_ColorSwatches[i].UpdateColor(availableColors[i]);
        }

        // Update availability
        UpdateColorAvailability();
    }

    private void ShowColorConflictNotification(Color requestedColor, Color assignedColor)
    {
        // Format color names for display
        string requestedColorName = ColorUtility.ToHtmlStringRGB(requestedColor);
        string assignedColorName = ColorUtility.ToHtmlStringRGB(assignedColor);

        // Show UI notification about color conflict
        Debug.Log($"Color conflict: Requested {requestedColorName}, assigned {assignedColorName}");

        // Update and show conflict notification panel
        if (m_ConflictNotificationPanel != null && m_ConflictMessageText != null)
        {
            string requestedColorHtml = $"<color=#{requestedColorName}>■</color>";
            string assignedColorHtml = $"<color=#{assignedColorName}>■</color>";

            m_ConflictMessageText.text = string.Format(m_ConflictMessageFormat,
                requestedColorHtml, assignedColorHtml);

            m_ConflictNotificationPanel.SetActive(true);

            // Hide after a delay
            StartCoroutine(HideConflictNotificationAfterDelay(5.0f));
        }
    }

    private IEnumerator HideConflictNotificationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (m_ConflictNotificationPanel != null)
            m_ConflictNotificationPanel.SetActive(false);
    }
}
```

#### ColorSwatchUI Component

```csharp
// In ColorSwatchUI.cs
public class ColorSwatchUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image m_ColorImage;
    [SerializeField] private GameObject m_SelectedIndicator;
    [SerializeField] private GameObject m_UnavailableIndicator;
    [SerializeField] private GameObject m_TakenIndicator;
    [SerializeField] private Button m_Button;
    [SerializeField] private TextMeshProUGUI m_ColorNameText;
    [SerializeField] private CanvasGroup m_CanvasGroup;

    [Header("Visual States")]
    [SerializeField] private float m_UnavailableOpacity = 0.5f;
    [SerializeField] private float m_AvailableOpacity = 1.0f;

    public Color Color { get; private set; }
    public int ColorIndex { get; private set; }

    public event Action<Color, int> OnColorSelected;

    private bool m_IsAvailable = true;
    private bool m_IsSelected = false;
    private bool m_IsTaken = false;

    public void Initialize(Color color, int index)
    {
        Color = color;
        ColorIndex = index;
        m_ColorImage.color = color;

        // Set color name if text component exists
        if (m_ColorNameText != null)
        {
            m_ColorNameText.text = GetColorName(color, index);
        }

        m_Button.onClick.AddListener(() => OnColorSelected?.Invoke(Color, ColorIndex));

        SetSelected(false);
        SetAvailable(true);
        SetTaken(false);
    }

    public void UpdateColor(Color newColor)
    {
        Color = newColor;
        m_ColorImage.color = newColor;

        // Update color name if text component exists
        if (m_ColorNameText != null)
        {
            m_ColorNameText.text = GetColorName(newColor, ColorIndex);
        }
    }

    public void SetSelected(bool selected)
    {
        m_IsSelected = selected;

        if (m_SelectedIndicator != null)
        {
            m_SelectedIndicator.SetActive(selected);
        }

        UpdateVisualState();
    }

    public void SetAvailable(bool available)
    {
        m_IsAvailable = available;

        if (m_UnavailableIndicator != null)
        {
            m_UnavailableIndicator.SetActive(!available);
        }

        m_Button.interactable = available;

        UpdateVisualState();
    }

    public void SetTaken(bool taken)
    {
        m_IsTaken = taken;

        if (m_TakenIndicator != null)
        {
            m_TakenIndicator.SetActive(taken);
        }

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        // Update opacity based on availability
        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = m_IsAvailable ? m_AvailableOpacity : m_UnavailableOpacity;
        }

        // Additional visual state updates can be added here
        // For example, changing border colors, scaling, etc.
    }

    private string GetColorName(Color color, int index)
    {
        // Try to determine a human-readable name for the color
        if (ColorUtility.ToHtmlStringRGB(color) == "0000FF") return "Blue";
        if (ColorUtility.ToHtmlStringRGB(color) == "FF8000") return "Orange";
        if (ColorUtility.ToHtmlStringRGB(color) == "FFFF00") return "Yellow";
        if (ColorUtility.ToHtmlStringRGB(color) == "800080") return "Purple";
        if (ColorUtility.ToHtmlStringRGB(color) == "FF0000") return "Red";
        if (ColorUtility.ToHtmlStringRGB(color) == "00B800") return "Green";
        if (ColorUtility.ToHtmlStringRGB(color) == "000000") return "Black";
        if (ColorUtility.ToHtmlStringRGB(color) == "FFFFFF") return "White";

        // Fallback to index-based name
        return $"Color {index + 1}";
    }
}
```

#### PlayerColorManager Extensions

```csharp
// Add to PlayerColorManager.cs
public bool IsColorTaken(Color color)
{
    return m_ColorToPlayerID.ContainsKey(color);
}

public bool IsNetworked => m_InNetworkedSession;

public ulong GetPlayerIDForColor(Color color)
{
    if (m_ColorToPlayerID.TryGetValue(color, out ulong playerID))
    {
        return playerID;
    }
    return 0; // 0 is an invalid player ID
}

public bool IsColorTakenByOtherPlayer(Color color, ulong currentPlayerID)
{
    if (m_ColorToPlayerID.TryGetValue(color, out ulong playerID))
    {
        return playerID != currentPlayerID;
    }
    return false;
}

public Dictionary<Color, ulong> GetAllTakenColors()
{
    return new Dictionary<Color, ulong>(m_ColorToPlayerID);
}

public void SaveColorPreference(Color color)
{
    PlayerPrefs.SetString("PreferredPlayerColor", "#" + ColorUtility.ToHtmlStringRGB(color));
}

public Color LoadColorPreference()
{
    string savedColorHex = PlayerPrefs.GetString("PreferredPlayerColor", "");
    if (!string.IsNullOrEmpty(savedColorHex) && ColorUtility.TryParseHtmlString(savedColorHex, out Color savedColor))
    {
        return savedColor;
    }

    // Return first color as default if no preference is saved
    return m_PlayerColors.Length > 0 ? m_PlayerColors[0] : Color.white;
}
```

## Testing Checklist

- [ ] Verify that the PlayerColorManager singleton works correctly
- [ ] Verify that player colors are consistent throughout the UI and game
- [ ] Verify that fallback color choices work correctly
- [ ] Verify that the VirtualSurfaceShaderUpdater correctly updates the shader properties
- [ ] Verify that the active player highlighting works correctly
  - [ ] Test the glow effect with different colors and intensities
  - [ ] Test the pulse animation effect with different speeds
  - [ ] Test enabling and disabling the pulse animation
  - [ ] Verify the effect only applies to the active player's color segment
  - [ ] Test with different active player indices
  - [ ] Verify that highlight settings are synchronized across the network
  - [ ] Test changing highlight settings during gameplay
  - [ ] Verify that VirtualSurfaceColorShaderUpdater correctly applies PlayerColorManager settings
- [ ] Test with different player counts (2-8)
  - [ ] Verify correct highlighting in 4-player layout
  - [ ] Verify correct highlighting in 5-8 player layouts
- [ ] Test color conflict resolution
  - [ ] Verify that players get unique colors even when requesting the same color
  - [ ] Verify that the host's palette is used for fallbacks
- [ ] Test seat swapping
  - [ ] Verify that players maintain their color when changing seats
  - [ ] Verify that the shader updates correctly after seat swaps
- [ ] Test network transitions
  - [ ] Verify that color preferences persist when leaving a session
  - [ ] Verify that the host's palette is synchronized to clients
- [ ] Test Color Selection UI
  - [ ] Test Pre-join Mode
    - [ ] Verify that all colors are selectable
    - [ ] Verify that no colors are marked as "taken"
    - [ ] Verify that the UI clearly indicates these are preferences
    - [ ] Verify that color preferences are saved to PlayerPrefs
    - [ ] Verify that the mode indicator shows "COLOR PREFERENCE"
  - [ ] Test In-game Mode
    - [ ] Verify that taken colors are visually indicated
    - [ ] Verify that only available colors are selectable
    - [ ] Verify that the player's current color is highlighted
    - [ ] Verify that the mode indicator shows "COLOR SELECTION"
  - [ ] Test Mode Transitions
    - [ ] Verify that the UI switches from Pre-join to In-game mode when joining a session
    - [ ] Verify that the UI switches from In-game to Pre-join mode when leaving a session
    - [ ] Verify that color preferences are applied when joining a session
    - [ ] Verify that conflict resolution works when joining with a taken color
  - [ ] Test General Functionality
    - [ ] Verify that selecting a color updates the preview
    - [ ] Verify that the UI updates when other players change colors
    - [ ] Test with multiple players selecting colors simultaneously
    - [ ] Verify that color conflict notifications are displayed correctly

## Conclusion

This comprehensive player color management system will provide a robust foundation for extending the tabletop system to support 8 players. It balances player personalization with the functional requirements of maintaining unique, consistent colors for gameplay purposes, while providing a flexible event notification system for game developers to respond to color changes.
