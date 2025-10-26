# Documentation Plan for 8-Player Table System

## Overview
This plan outlines the documentation that will be created for the 8-player table system to help users understand the changes and how to update their existing projects.

## Documentation Types

### 1. README.md
A high-level overview of the 8-player table system, including:
- Introduction to the 8-player table system
- Key features and changes
- How to get started
- Basic usage examples
- Links to more detailed documentation

### 2. API Documentation
Detailed documentation of the API for developers, including:
- Class references
- Method references
- Property references
- Event references
- Code examples

### 3. User Guide
A comprehensive guide for users, including:
- How to set up the 8-player table system
- How to configure player colors
- How to use the seat positioning system
- How to use the player mapping system
- How to use the VirtualSurfaceColorShader
- Troubleshooting common issues

### 4. Migration Guide
A guide for users updating from the 4-player system to the 8-player system, including:
- Changes from the 4-player system
- How to update existing projects
- Breaking changes and how to handle them
- Backward compatibility considerations

### 5. Example Projects
Example projects demonstrating the 8-player table system, including:
- Basic setup example
- Player color customization example
- Seat positioning example
- Player mapping example
- VirtualSurfaceColorShader example

## Documentation Content

### 1. README.md
```markdown
# 8-Player Table System

## Introduction
The 8-Player Table System is an extension of the original 4-player table system, allowing up to 8 players to participate in tabletop games. It provides a flexible and easy-to-use framework for creating multiplayer tabletop experiences in Unity.

## Key Features
- Support for up to 8 players
- Flexible seat positioning system
- Logical player mapping for consistent player order
- Centralized player color management
- Enhanced VirtualSurfaceColorShader with active player highlighting
- Backward compatibility with existing 4-player projects

## Getting Started
1. Import the MRTabletopAssets package into your Unity project
2. Add the PlayerColorManager prefab to your scene
3. Configure the player colors in the PlayerColorManager
4. Use the TableTop component to manage seat positions
5. Use the NetworkTableTopManager to handle player seating

## Basic Usage
```csharp
// Get player color from the PlayerColorManager
Color playerColor = PlayerColorManager.Instance.GetPlayerColor(playerIndex);

// Set the active player
PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;

// Update seat positions based on player count
tableTop.UpdateSeatPositions(playerCount);

// Get logical player index from physical seat index
int logicalIndex = tableTop.GetLogicalPlayerIndex(physicalIndex, playerCount);
```

## Documentation
- [API Documentation](API_Documentation.md)
- [User Guide](User_Guide.md)
- [Migration Guide](Migration_Guide.md)

## Examples
- [Basic Setup Example](Examples/BasicSetup.md)
- [Player Color Customization Example](Examples/PlayerColorCustomization.md)
- [Seat Positioning Example](Examples/SeatPositioning.md)
- [Player Mapping Example](Examples/PlayerMapping.md)
- [VirtualSurfaceColorShader Example](Examples/VirtualSurfaceColorShader.md)
```

### 2. API Documentation
```markdown
# API Documentation

## PlayerColorManager
The `PlayerColorManager` is a singleton class that centralizes player color definitions and provides access to player colors throughout the codebase.

### Properties
- `Instance`: Gets the singleton instance of the PlayerColorManager.
- `ActivePlayerIndex`: Gets or sets the active player index for highlighting.

### Methods
- `GetPlayerColor(int playerIndex)`: Gets the color for the specified player index.
- `GetAllPlayerColors()`: Gets all player colors.
- `GetPlayerColors(int playerCount)`: Gets player colors for the specified player count.
- `GetActivePlayerColor()`: Gets the color for the active player.
- `SetPlayerColors(Color[] colors)`: Sets the player colors.

### Events
- `OnActivePlayerChanged`: Event that is triggered when the active player changes.

## TableTop
The `TableTop` class manages the seat positions and player mapping for the table.

### Properties
- `seats`: Gets the array of table seats.
- `seatDistance`: Gets the distance of seats from the center.
- `seatOffset`: Gets or sets the offset of seats from the center.

### Methods
- `GetSeat(int seatIdx)`: Gets the transform of the specified seat.
- `UpdateSeatPositions(int playerCount)`: Updates the seat positions based on the number of active players.
- `GetLogicalPlayerIndex(int physicalSeatIndex, int totalActivePlayers)`: Maps physical seat index to logical player index.
- `GetPhysicalSeatIndex(int logicalPlayerIndex, int totalActivePlayers)`: Maps logical player index to physical seat index.
- `UpdateShapeSides(int playerCount)`: Updates the ShapeSides parameter in the shader.

## VirtualSurfaceShaderUpdater
The `VirtualSurfaceShaderUpdater` class updates the VirtualSurfaceColorShader shader with player colors and highlight effect.

### Properties
- `HighlightIntensity`: Gets or sets the intensity of the highlight effect.
- `HighlightColor`: Gets or sets the color of the highlight effect.
- `PulseSpeed`: Gets or sets the speed of the pulse effect.
- `PulseAmount`: Gets or sets the amount of the pulse effect.
- `EnablePulse`: Gets or sets whether the pulse effect is enabled.

### Methods
- `UpdateShaderProperties()`: Updates the shader properties with player colors and highlight effect.
- `UpdateShapeSides(int sides)`: Updates the shape sides parameter in the shader.
- `SetActivePlayerIndex(int index)`: Sets the active player index.
```

### 3. User Guide
```markdown
# User Guide

## Setting Up the 8-Player Table System
1. Import the MRTabletopAssets package into your Unity project
2. Add the PlayerColorManager prefab to your scene
3. Configure the player colors in the PlayerColorManager
4. Use the TableTop component to manage seat positions
5. Use the NetworkTableTopManager to handle player seating

## Configuring Player Colors
The PlayerColorManager allows you to centralize player color definitions and access them throughout your codebase.

### Setting Player Colors
You can set player colors in the Inspector by selecting the PlayerColorManager GameObject and configuring the Player Colors array.

### Accessing Player Colors
You can access player colors from anywhere in your code using the PlayerColorManager singleton:

```csharp
// Get player color from the PlayerColorManager
Color playerColor = PlayerColorManager.Instance.GetPlayerColor(playerIndex);

// Get all player colors
Color[] allColors = PlayerColorManager.Instance.GetAllPlayerColors();

// Get player colors for a specific player count
Color[] colors = PlayerColorManager.Instance.GetPlayerColors(playerCount);

// Get the active player color
Color activeColor = PlayerColorManager.Instance.GetActivePlayerColor();
```

### Setting the Active Player
You can set the active player for highlighting:

```csharp
// Set the active player
PlayerColorManager.Instance.ActivePlayerIndex = activePlayerIndex;
```

## Using the Seat Positioning System
The TableTop component manages seat positions based on the number of active players.

### Updating Seat Positions
You can update seat positions based on the number of active players:

```csharp
// Update seat positions based on player count
tableTop.UpdateSeatPositions(playerCount);
```

### Getting Seat Transforms
You can get the transform of a specific seat:

```csharp
// Get seat transform
Transform seatTransform = tableTop.GetSeat(seatIndex);
```

## Using the Player Mapping System
The TableTop component provides mapping between physical seat indices and logical player indices.

### Mapping Physical to Logical
You can map a physical seat index to a logical player index:

```csharp
// Get logical player index from physical seat index
int logicalIndex = tableTop.GetLogicalPlayerIndex(physicalIndex, playerCount);
```

### Mapping Logical to Physical
You can map a logical player index to a physical seat index:

```csharp
// Get physical seat index from logical player index
int physicalIndex = tableTop.GetPhysicalSeatIndex(logicalIndex, playerCount);
```

## Using the VirtualSurfaceColorShader
The VirtualSurfaceShaderUpdater component updates the VirtualSurfaceColorShader shader with player colors and highlight effect.

### Updating Shader Properties
You can update the shader properties:

```csharp
// Update shader properties
virtualSurfaceShaderUpdater.UpdateShaderProperties();
```

### Updating Shape Sides
You can update the shape sides parameter in the shader:

```csharp
// Update shape sides
virtualSurfaceShaderUpdater.UpdateShapeSides(playerCount);
```

### Setting the Active Player
You can set the active player for highlighting:

```csharp
// Set active player index
virtualSurfaceShaderUpdater.SetActivePlayerIndex(activePlayerIndex);
```
```

### 4. Migration Guide
```markdown
# Migration Guide

## Changes from the 4-Player System
The 8-player table system extends the original 4-player system with the following changes:
- Support for up to 8 players
- New seat positioning system for 5-8 players
- New player mapping system for consistent player order
- Centralized player color management
- Enhanced VirtualSurfaceColorShader with active player highlighting

## Updating Existing Projects
To update your existing project from the 4-player system to the 8-player system, follow these steps:

1. Import the updated MRTabletopAssets package
2. Add the PlayerColorManager prefab to your scene
3. Configure the player colors in the PlayerColorManager
4. Update any scripts that reference player colors to use the PlayerColorManager
5. Update any scripts that reference seat positions to use the new TableTop methods
6. Update any scripts that reference player indices to use the new mapping methods

## Breaking Changes
The following breaking changes have been made:

1. Player colors are now centralized in the PlayerColorManager
   - Update any scripts that define player colors to use the PlayerColorManager

2. Seat positioning has been updated for 5-8 players
   - Update any scripts that position seats to use the new TableTop methods

3. Player mapping has been updated for 5-8 players
   - Update any scripts that map player indices to use the new mapping methods

## Backward Compatibility
The 8-player system maintains backward compatibility with the 4-player system:

1. For 4 or fewer players, the original seat positioning is maintained
   - Seats are positioned at 0°, 180°, 270°, and 90°

2. For 4 or fewer players, the original player mapping is maintained
   - Logical player order is 0, 3, 1, 2

3. Scripts that don't use the PlayerColorManager will fall back to default colors
   - Add fallback color handling to your scripts
```

### 5. Example Projects
```markdown
# Example: Basic Setup

## Overview
This example demonstrates how to set up the 8-player table system in a new project.

## Steps
1. Import the MRTabletopAssets package
2. Create a new scene
3. Add the PlayerColorManager prefab to the scene
4. Add the TableTop prefab to the scene
5. Add the NetworkTableTopManager prefab to the scene
6. Configure the player colors in the PlayerColorManager
7. Run the scene and test with different player counts

## Code Example
```csharp
using UnityEngine;
using MRTabletopAssets;

public class ExampleSetup : MonoBehaviour
{
    [SerializeField] private TableTop m_TableTop;
    [SerializeField] private NetworkTableTopManager m_NetworkTableTopManager;
    [SerializeField] private int m_PlayerCount = 4;

    private void Start()
    {
        // Ensure PlayerColorManager exists
        if (PlayerColorManager.Instance == null)
        {
            Debug.LogError("PlayerColorManager not found. Add the PlayerColorManager prefab to the scene.");
            return;
        }

        // Update seat positions based on player count
        m_TableTop.UpdateSeatPositions(m_PlayerCount);

        // Set active player
        PlayerColorManager.Instance.ActivePlayerIndex = 0;
    }

    public void SetPlayerCount(int count)
    {
        m_PlayerCount = Mathf.Clamp(count, 2, 8);
        m_TableTop.UpdateSeatPositions(m_PlayerCount);
    }

    public void SetActivePlayer(int index)
    {
        PlayerColorManager.Instance.ActivePlayerIndex = index;
    }
}
```
```

## Documentation Schedule
- Initial documentation: After implementing each component
- Integration documentation: After implementing all components
- Final documentation: Before release

## Documentation Review
- Review documentation for accuracy
- Review documentation for completeness
- Review documentation for clarity
- Review documentation for consistency
