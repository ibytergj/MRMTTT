# Testing Plan for 8-Player Table System

## Overview
This plan outlines the testing procedures for the 8-player table system to ensure that all components work correctly with different player counts and configurations.

## Test Environments
- Unity Editor
- VR Headset (if available)
- AR Device (if available)

## Test Categories

### 1. Seat Positioning Tests

#### 1.1 Basic Seat Positioning
- **Test 2 Players**
  - Verify seats are positioned at 0° and 180°
  - Verify seats are properly activated/deactivated
  - Verify seat rotations face the center

- **Test 3 Players**
  - Verify seats are positioned at 0°, 180°, and 270°
  - Verify seats are properly activated/deactivated
  - Verify seat rotations face the center

- **Test 4 Players**
  - Verify seats are positioned at 0°, 180°, 270°, and 90°
  - Verify seats are properly activated/deactivated
  - Verify seat rotations face the center

- **Test 5 Players**
  - Verify seats are evenly distributed in a pentagon (72° apart)
  - Verify seats are properly activated/deactivated
  - Verify seat rotations face the center

- **Test 6 Players**
  - Verify seats are evenly distributed in a hexagon (60° apart)
  - Verify seats are properly activated/deactivated
  - Verify seat rotations face the center

- **Test 7 Players**
  - Verify seats are evenly distributed in a heptagon (~51.4° apart)
  - Verify seats are properly activated/deactivated
  - Verify seat rotations face the center

- **Test 8 Players**
  - Verify seats are evenly distributed in an octagon (45° apart)
  - Verify seats are properly activated/deactivated
  - Verify seat rotations face the center

#### 1.2 Dynamic Seat Positioning
- **Test Adding Players**
  - Start with 2 players
  - Add players one by one up to 8
  - Verify seat positions update correctly
  - Verify seat activations update correctly

- **Test Removing Players**
  - Start with 8 players
  - Remove players one by one down to 2
  - Verify seat positions update correctly
  - Verify seat activations update correctly

- **Test Transition from 4 to 5 Players**
  - Start with 4 players (original layout)
  - Add a 5th player
  - Verify seat positions transition correctly to the pentagon layout
  - Verify all seats are properly activated

- **Test Transition from 5 to 4 Players**
  - Start with 5 players (pentagon layout)
  - Remove a player to have 4 players
  - Verify seat positions transition correctly to the original layout
  - Verify all seats are properly activated/deactivated

### 2. Player Mapping Tests

#### 2.1 Logical Player Order
- **Test 2-4 Players**
  - Verify logical player order is clockwise: 0, 3, 1, 2
  - Verify physical seat positions match the expected angles
  - Verify player colors are assigned correctly based on logical order

- **Test 5-8 Players**
  - Verify logical player order is clockwise: 0, 1, 2, 3, 4, 5, 6, 7
  - Verify physical seat positions match the expected angles
  - Verify player colors are assigned correctly based on logical order

#### 2.2 Mapping Functions
- **Test GetLogicalPlayerIndex**
  - Test with different physical seat indices and player counts
  - Verify the function returns the correct logical player index

- **Test GetPhysicalSeatIndex**
  - Test with different logical player indices and player counts
  - Verify the function returns the correct physical seat index

### 3. Player Colors Tests

#### 3.1 PlayerColorManager
- **Test Singleton Behavior**
  - Verify the singleton instance is created correctly
  - Verify the singleton instance is accessible throughout the codebase

- **Test Color Access Methods**
  - Test GetPlayerColor with different indices
  - Test GetAllPlayerColors
  - Test GetPlayerColors with different player counts
  - Test GetActivePlayerColor
  - Test SetPlayerColors

#### 3.2 Color Consistency
- **Test UI Color Consistency**
  - Verify player colors are consistent in the UI
  - Verify seat button colors match the player colors
  - Verify player name tag colors match the player colors

- **Test Game Color Consistency**
  - Verify player colors are consistent in the game
  - Verify avatar colors match the player colors
  - Verify any game-specific player colors match the player colors

#### 3.3 Fallback Colors
- **Test Fallback Color Behavior**
  - Test with invalid player indices
  - Verify fallback colors are used correctly
  - Test with missing PlayerColorManager
  - Verify scripts handle the missing manager gracefully

### 4. VirtualSurfaceColorShader Tests

#### 4.1 Shader Properties
- **Test Shader Property Updates**
  - Verify player colors are correctly updated in the shader
  - Verify the ShapeSides parameter is correctly updated
  - Verify the ActivePlayerIndex parameter is correctly updated
  - Verify the HighlightIntensity parameter is correctly updated
  - Verify the HighlightColor parameter is correctly updated

#### 4.2 Active Player Highlighting
- **Test Highlight Effect**
  - Verify the highlight effect only applies to the active player's color
  - Verify the effect looks good with different player colors
  - Test with different player counts (2-8)
  - Test the pulse effect
  - Verify the highlight effect works with both 4-player and 5-8 player layouts

### 5. Integration Tests

#### 5.1 UI Integration
- **Test Seat Button UI**
  - Verify seat buttons display the correct player colors
  - Verify seat buttons show the correct player information
  - Verify seat buttons respond correctly to player interactions
  - Test with different player counts (2-8)

- **Test Player Menu UI**
  - Verify player menu displays the correct player colors
  - Verify player menu shows the correct player information
  - Verify player menu responds correctly to player interactions
  - Test with different player counts (2-8)

#### 5.2 Game Integration
- **Test Game Initialization**
  - Verify the game initializes correctly with different player counts
  - Verify player colors are assigned correctly
  - Verify seat positions are set correctly

- **Test Player Interactions**
  - Verify players can interact with the game correctly
  - Verify player turns work correctly
  - Verify active player highlighting works correctly

## Test Reporting
For each test, record the following information:
- Test name
- Test description
- Expected result
- Actual result
- Pass/Fail status
- Notes/Issues

## Issue Tracking
For any issues found during testing, record the following information:
- Issue description
- Steps to reproduce
- Expected behavior
- Actual behavior
- Severity (Critical, Major, Minor, Cosmetic)
- Priority (High, Medium, Low)
- Screenshots/Videos (if applicable)

## Test Schedule
- Initial testing: After implementing each component
- Integration testing: After implementing all components
- Final testing: Before release
