# VirtualSurfaceColorShader Enhancement Plan

## Overview
This plan details the implementation of a shader mechanism to highlight the active player in the VirtualSurfaceColorShader shader. The enhancement will apply a special effect to the active player's color on the inner border of the board.

## Current Implementation
- The `VirtualSurfaceColorShader.shadergraph` shader currently:
  - Uses player colors defined in the shader properties
  - Has a ShapeSides parameter to support up to 8 players
  - Uses a custom function node with code from `CustomCode.hsl` to blend player colors

- The `CustomCode.hsl` file:
  - Defines an array of player colors
  - Has special handling for 4 players vs. 5-8 players
  - Blends colors based on the angle

## Implementation Steps

### 1. Update VirtualSurfaceColorShader.shadergraph
- Add a new shader property `_ActivePlayerIndex` to track the active player
- Add a new shader property `_HighlightIntensity` to control the highlight effect
- Add a new shader property `_HighlightColor` for the highlight color
- Modify the shader graph to apply the highlight effect to the active player's color

### 2. Update CustomCode.hsl
- Modify the custom function to take the active player index as input
- Implement the highlight effect for the active player's color
- Ensure the effect only applies to the inner border

### 3. Create VirtualSurfaceShaderUpdater.cs
- Create a script to update the shader properties
- Update the active player index when it changes
- Update the highlight intensity and color

### 4. Test the Shader Enhancement
- Test with different player counts (2-8)
- Verify that the highlight effect only applies to the active player's color
- Verify that the effect looks good with different player colors

## Code Implementation

### CustomCode.hsl Updates
```hlsl
// This is the content for the "Body" field of the Custom Function node.
// Assumes BlendWidth is a float input to the Custom Function node.

float4 initialPlayerColors[8];
initialPlayerColors[0] = C1; initialPlayerColors[1] = C2;
initialPlayerColors[2] = C3; initialPlayerColors[3] = C4;
initialPlayerColors[4] = C5; initialPlayerColors[5] = C6;
initialPlayerColors[6] = C7; initialPlayerColors[7] = C8;

float4 colorsForBlending[8];
float effectiveAngleForLogic;
int numSides = (int)Sides;
int activePlayer = (int)ActivePlayerIndex;
float highlightIntensity = HighlightIntensity;
float4 highlightColor = HighlightColor;

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
    if (activePlayer >= 0 && activePlayer < 4)
    {
        // Map active player index to the correct color index in the 4-player layout
        int mappedIndex = -1;
        if (activePlayer == 0) mappedIndex = 0;      // P1 -> 0
        else if (activePlayer == 1) mappedIndex = 2; // P2 -> 2
        else if (activePlayer == 2) mappedIndex = 3; // P3 -> 3
        else if (activePlayer == 3) mappedIndex = 1; // P4 -> 1

        if (mappedIndex >= 0)
        {
            // Apply highlight effect
            colorsForBlending[mappedIndex] = lerp(colorsForBlending[mappedIndex], highlightColor, highlightIntensity);
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
    if (activePlayer >= 0 && activePlayer < numSides)
    {
        // Apply highlight effect
        colorsForBlending[activePlayer] = lerp(colorsForBlending[activePlayer], highlightColor, highlightIntensity);
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
```

### VirtualSurfaceShaderUpdater.cs
```csharp
using UnityEngine;

namespace MRTabletopAssets
{
    /// <summary>
    /// Updates the VirtualSurfaceColorShader shader with player colors and highlight effect.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class VirtualSurfaceShaderUpdater : MonoBehaviour
    {
        [SerializeField] private float m_HighlightIntensity = 0.3f;
        [SerializeField] private Color m_HighlightColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float m_PulseSpeed = 2f;
        [SerializeField] private float m_PulseAmount = 0.2f;
        [SerializeField] private bool m_EnablePulse = true;

        private Renderer m_Renderer;
        private MaterialPropertyBlock m_PropertyBlock;

        // Shader property IDs
        private static readonly int s_PlayerColor1 = Shader.PropertyToID("_PlayerColor1");
        private static readonly int s_PlayerColor2 = Shader.PropertyToID("_PlayerColor2");
        private static readonly int s_PlayerColor3 = Shader.PropertyToID("_PlayerColor3");
        private static readonly int s_PlayerColor4 = Shader.PropertyToID("_PlayerColor4");
        private static readonly int s_PlayerColor5 = Shader.PropertyToID("_PlayerColor5");
        private static readonly int s_PlayerColor6 = Shader.PropertyToID("_PlayerColor6");
        private static readonly int s_PlayerColor7 = Shader.PropertyToID("_PlayerColor7");
        private static readonly int s_PlayerColor8 = Shader.PropertyToID("_PlayerColor8");
        private static readonly int s_ShapeSides = Shader.PropertyToID("_ShapeSides");
        private static readonly int s_ActivePlayerIndex = Shader.PropertyToID("_ActivePlayerIndex");
        private static readonly int s_HighlightIntensity = Shader.PropertyToID("_HighlightIntensity");
        private static readonly int s_HighlightColor = Shader.PropertyToID("_HighlightColor");

        private void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            UpdateShaderProperties();

            // Subscribe to active player changed event
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnActivePlayerChanged += OnActivePlayerChanged;
            }
        }

        private void Update()
        {
            if (m_EnablePulse && PlayerColorManager.Instance != null && PlayerColorManager.Instance.ActivePlayerIndex >= 0)
            {
                // Pulse the highlight intensity
                float pulseIntensity = m_HighlightIntensity + Mathf.Sin(Time.time * m_PulseSpeed) * m_PulseAmount;

                m_Renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetFloat(s_HighlightIntensity, pulseIntensity);
                m_Renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from active player changed event
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnActivePlayerChanged -= OnActivePlayerChanged;
            }
        }

        private void OnActivePlayerChanged(int newActivePlayerIndex)
        {
            UpdateShaderProperties();
        }

        /// <summary>
        /// Updates the shader properties with player colors and highlight effect.
        /// </summary>
        public void UpdateShaderProperties()
        {
            if (m_Renderer == null)
                return;

            m_Renderer.GetPropertyBlock(m_PropertyBlock);

            // Get player colors from the PlayerColorManager if available
            if (PlayerColorManager.Instance != null)
            {
                Color[] playerColors = PlayerColorManager.Instance.GetAllPlayerColors();

                // Set player colors
                m_PropertyBlock.SetColor(s_PlayerColor1, playerColors[0]);
                m_PropertyBlock.SetColor(s_PlayerColor2, playerColors[1]);
                m_PropertyBlock.SetColor(s_PlayerColor3, playerColors[2]);
                m_PropertyBlock.SetColor(s_PlayerColor4, playerColors[3]);
                m_PropertyBlock.SetColor(s_PlayerColor5, playerColors[4]);
                m_PropertyBlock.SetColor(s_PlayerColor6, playerColors[5]);
                m_PropertyBlock.SetColor(s_PlayerColor7, playerColors[6]);
                m_PropertyBlock.SetColor(s_PlayerColor8, playerColors[7]);

                // Set active player index
                m_PropertyBlock.SetFloat(s_ActivePlayerIndex, PlayerColorManager.Instance.ActivePlayerIndex);
            }

            // Set highlight properties
            m_PropertyBlock.SetFloat(s_HighlightIntensity, m_HighlightIntensity);
            m_PropertyBlock.SetColor(s_HighlightColor, m_HighlightColor);

            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }

        /// <summary>
        /// Updates the shape sides parameter in the shader.
        /// </summary>
        public void UpdateShapeSides(int sides)
        {
            if (m_Renderer == null)
                return;

            m_Renderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetFloat(s_ShapeSides, sides);
            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }

        /// <summary>
        /// Sets the active player index.
        /// </summary>
        public void SetActivePlayerIndex(int index)
        {
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.ActivePlayerIndex = index;
            }
            else
            {
                m_Renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetFloat(s_ActivePlayerIndex, index);
                m_Renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}
```

## Testing Checklist
- [ ] Verify that the shader properties are correctly updated
- [ ] Verify that the highlight effect only applies to the active player's color
- [ ] Verify that the effect looks good with different player colors
- [ ] Test with different player counts (2-8)
- [ ] Test the pulse effect
- [ ] Verify that the highlight effect works with both 4-player and 5-8 player layouts
