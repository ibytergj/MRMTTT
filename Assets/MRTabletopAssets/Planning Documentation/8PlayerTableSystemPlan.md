# 8-Player Table System Implementation Plan

## Overview
This document outlines the plan for extending the table system from supporting 4 players to 8 players, while ensuring backward compatibility and making it easy for developers to update existing projects based on this template.

## Completed Tasks
- Extended player support from 4 to 8 players
- Added 4 additional seat GameObjects
- Updated the m_Seats array
- Added hover visuals for all seats
- Updated the seat button prefab to support 8 colors
- Modified the VirtualSurfaceColorShader to support up to 8 players via the ShapeSides parameter
- Updated the UI to display all 8 seat buttons
- Implemented horizontal scrolling for the seat buttons

## Remaining Tasks

### 1. Seat Positioning
- [x] Implement proper positioning for all 8 seats in a regular polygon arrangement
- [ ] Ensure correct positioning for 5-8 players with evenly distributed angles
- [ ] Maintain the existing physical order for 4 or fewer players
- [ ] Test seat positioning with different player counts

### 2. Player Mapping
- [ ] Implement the mapping between physical player seats and logical player order (clockwise)
- [ ] Ensure the numbering follows a sequential clockwise system starting from player 0
- [ ] Test the mapping with different player counts

### 3. Player Colors
- [ ] Create a PlayerColorManager to centralize player color definitions
- [ ] Identify all places where player colors are used
- [ ] Update scripts to use the PlayerColorManager with fallback color choices
- [ ] Update the VirtualSurfaceColorShader shader to use the PlayerColorManager
- [ ] Update prefabs that need to support player colors
- [ ] Document how other games can access the player colors

### 4. VirtualSurfaceColorShader Enhancement
- [ ] Implement a shader mechanism to highlight the active player
- [ ] Apply a special effect to the active player's color on the inner border
- [ ] Ensure the effect only affects the active player's color

### 5. Testing
- [ ] Test the system with different player counts (2-8)
- [ ] Verify that the UI correctly displays the appropriate number of seats
- [ ] Ensure the seat positioning is correct for each player count
- [ ] Test the player color system with different player counts

### 6. Documentation
- [ ] Update documentation to reflect the changes
- [ ] Provide guidance for users updating existing projects based on this template
- [ ] Create examples of how to use the new PlayerColorManager

## Detailed Implementation Plans
See the following files for detailed implementation plans for each task:
- [Seat Positioning Plan](SeatPositioningPlan.md)
- [Player Mapping Plan](PlayerMappingPlan.md)
- [Player Colors Plan](PlayerColorsPlan.md)
- [VirtualSurfaceColorShader Enhancement Plan](ShaderEnhancementPlan.md)
- [Testing Plan](TestingPlan.md)
- [Documentation Plan](DocumentationPlan.md)
