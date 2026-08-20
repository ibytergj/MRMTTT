# 8-Player Table Joining Behavior Implementation Plan

## Overview
This plan details the implementation of modified 8-player table joining behavior where the table immediately switches to an 8-player configuration when the 5th player joins, with table scaling, player repositioning, and camera fade effects.

## Current Behavior Analysis

### Existing Implementation
- **Players 1-4**: Join using standard 4-player layout (0°, 180°, 270°, 90°)
- **Player 5+**: Table dynamically adjusts to show exact player count (pentagon for 5, hexagon for 6, etc.)
- **Table Size**: Remains constant at `m_SeatDistance = 0.75f`
- **Player Repositioning**: Handled by `RepositionAllPlayers()` when transitioning from 4 to 5+ players
- **No Camera Fade**: Players see instant repositioning

### Key Files Identified
1. **NetworkTableTopManager.cs** - Server-side seat assignment and network synchronization
2. **TableTop.cs** - Table geometry and seat positioning
3. **TableSeatSystem.cs** - Player teleportation and seat rotation
4. **TableTopSeatButton.cs** - UI seat button display
5. **FadePlaneMaterial.cs** - Existing fade effect system (reference for camera fade)

## New Requirements

### Immediate 8-Player Table Switch
- When 5th player joins, immediately switch to 8-player table (not pentagon)
- Skip incremental sizing (no pentagon, hexagon, heptagon)
- Table remains at 8-player size for entire session, even if player count drops below 5

### Table Scaling (2x)
- Scale physical table size by 2x when switching to 8-player mode
- Adjust `m_SeatDistance` from 0.75f to 1.5f
- Scale table transform or adjust seat distance calculation

### Player Repositioning
- **Players 1, 2, 3, 4**: Maintain their original angular positions (0°, 180°, 270°, 90°)
- **Movement Type**: Radial outward movement only (backwards from table center)
- **Distance**: Move 0.75f outward to accommodate 2x table scale (from 0.75f to 1.5f radius)
- **No Angular Rotation**: Players do NOT rotate to new angles, only move away from center
- **Automatic Repositioning**: The game code automatically repositions each player's XR Origin/camera rig locally (players do NOT physically move themselves)
- **Network Synchronization**: Position updates are broadcast over network so all clients see correct player positions

### Table and UI GameObject Repositioning
When the table expands, several GameObjects must be repositioned/scaled to maintain proper layout:

**GameObjects to Scale (1.0 → 2.0):**
- `/Virtual Table/TableSystem/TableTop` - Table geometry and seats (children auto-adjust)
- `/Virtual Table/TableSystem/Hover Visuals` - Seat hover visual indicators (children auto-adjust)
- `/Virtual Table/TableSystem/TableManipulationOffset/PassthroughVolume` - Passthrough masking volume

**GameObjects to Move Radially Outward 0.75f (NO scaling):**
- `/UI/World Space Canvas/Table UI` - Main UI panel (local instance per player, not networked)
- `/Virtual Table/TableSystem/TableManipulationOffset/TableManipulator - Only Rotation` - Rotation handles
- `/Virtual Table/TableSystem/TableManipulationOffset/TableManipulator - Free Move` - Free move handles
- `/Virtual Table/TableSystem/TableManipulationOffset/HandleVisual` - Handle visual indicators

**Important Notes:**
- Each player has their own **local instance** of "Table UI" that is **NOT networked**
- Each client moves their own Table UI instance independently (no RPC needed)
- Do NOT scale or move the parent `TableManipulationOffset` - only move specific children
- Do NOT scale the parent `TableSystem` - only scale specific children

### 8-Player Seating Arrangement
```
Player 1: 0° (original angle, moves radially outward 0.75f)
Player 2: 180° (original angle, moves radially outward 0.75f)
Player 3: 270° (original angle, moves radially outward 0.75f)
Player 4: 90° (original angle, moves radially outward 0.75f)
Player 5: 45° (new player - positioned between Player 1 and Player 4)
Player 6: 135° (new player - positioned between Player 4 and Player 2)
Player 7: 225° (new player - positioned between Player 2 and Player 3)
Player 8: 315° (new player - positioned between Player 3 and Player 1)
```

### Camera Fade During Repositioning
- Fade camera to black before repositioning
- Reposition player while screen is black
- Fade camera back in after repositioning complete
- Prevents motion sickness from sudden movement

### UI Updates
- Join button UI must show current player count
- Update when 3rd and subsequent players join

### Preserve Existing Functionality
- Keep all dynamic table size adjustment code intact (do not remove)
- Maintain seat-swapping functionality
- Preserve 1-4 player behavior exactly as-is

## Implementation Tasks

### Phase 1: Core Table Behavior Changes

#### Task 1.1: Add 8-Player Lock State to NetworkTableTopManager
**File**: `NetworkTableTopManager.cs`
**Changes**:
- Add `NetworkVariable<bool> m_Is8PlayerModeLocked` to track if table is locked to 8-player mode
- Add `NetworkVariable<float> m_TableScaleFactor` to sync table scale (1.0f or 2.0f)
- Modify `UpdateSeatPositionsBasedOnPlayerCount()` to check lock state
- When 5th player joins, set `m_Is8PlayerModeLocked = true` and `m_TableScaleFactor = 2.0f`
- When locked, always use 8 seats regardless of occupied count

**Estimated Lines**: ~50 lines

#### Task 1.2: Implement 8-Player Seat Positioning in TableTop
**File**: `TableTop.cs`
**Changes**:
- Add new method `PositionSeatsForEightPlayers()` with fixed 8-player layout
  - Players 1-4: Maintain original angles (0°, 180°, 270°, 90°) at 1.5f radius
  - Players 5-8: New positions at 45° intervals (45°, 135°, 225°, 315°) at 1.5f radius
- Modify `UpdateSeatPositions()` to accept optional `force8PlayerMode` parameter
- Add `SetTableScale(float scaleFactor)` method to adjust `m_SeatDistance`
- Update seat positioning logic to use scaled distance

**Estimated Lines**: ~60 lines

#### Task 1.3: Update Seat Assignment Logic
**File**: `NetworkTableTopManager.cs`
**Changes**:
- Modify `GetAnyAvailableSeats()` to respect 8-player mode
- Update seat assignment to use 8-player positions when locked
- Add logic to trigger table expansion when 5th player joins

**Estimated Lines**: ~30 lines

### Phase 2: Player Repositioning System

#### Task 2.1: Create Player Repositioning Manager
**New File**: `Assets/MRTabletopAssets/Scripts/Table/PlayerRepositionManager.cs`
**Purpose**: Handle local player repositioning with camera fade AND GameObject repositioning/scaling
**Features**:
- `RepositionPlayerWithFade(int oldSeatIndex, int newSeatIndex, float scaleFactor)`
- Camera fade out/in using coroutines
- Calculate new position based on scale factor (radial movement only)
- Scale GameObjects: TableTop, Hover Visuals, PassthroughVolume (1.0 → 2.0)
- Move GameObjects radially: Table UI, TableManipulator objects, HandleVisual (+0.75f, no scale)
- Broadcast position update via RPC

**GameObject References Required**:
- **Scale to 2.0**: `/Virtual Table/TableSystem/TableTop`, `/Virtual Table/TableSystem/Hover Visuals`, `/Virtual Table/TableSystem/TableManipulationOffset/PassthroughVolume`
- **Move Radially (No Scale)**: `/UI/World Space Canvas/Table UI`, `/Virtual Table/TableSystem/TableManipulationOffset/TableManipulator - Only Rotation`, `/Virtual Table/TableSystem/TableManipulationOffset/TableManipulator - Free Move`, `/Virtual Table/TableSystem/TableManipulationOffset/HandleVisual`

**Important Notes**:
- Table UI is a **local instance per player** (NOT networked) - each client moves their own instance
- Do NOT scale or move parent `TableManipulationOffset` - only move specific children
- Do NOT scale parent `TableSystem` - only scale specific children
- Store table center position BEFORE any transformations for accurate radial calculations

**Estimated Lines**: ~200 lines

#### Task 2.2: Integrate Camera Fade System
**File**: `PlayerRepositionManager.cs`
**Changes**:
- Create fade overlay UI (black panel)
- Implement fade coroutines (fade out, wait, fade in)
- Configurable fade duration (default: 0.5s out, 0.5s in)
- Use Unity's CanvasGroup for smooth alpha transitions

**Estimated Lines**: ~80 lines (included in 2.1)

#### Task 2.3: Calculate Repositioning Offsets
**File**: `PlayerRepositionManager.cs`
**Changes**:
- Calculate radial outward movement distance for players 1, 2, 3, 4
- Movement is purely radial (away from table center), NO angular rotation
- Use player's current position vector from table center, normalized
- Distance = (new_seat_distance - old_seat_distance) = (1.5f - 0.75f) = 0.75f
- New position = old position + (normalized direction × 0.75f)
- Players maintain their original angular positions (0°, 180°, 270°, 90°)
- Apply same radial movement logic to Table UI and other GameObjects

**Estimated Lines**: ~40 lines (included in 2.1)

### Phase 3: Network Synchronization

#### Task 3.1: Add Position Broadcast RPCs
**File**: `NetworkTableTopManager.cs`
**Changes**:
- Add `[Rpc(SendTo.Server)] BroadcastPlayerPositionServerRpc(ulong playerID, Vector3 position, Quaternion rotation)`
- Add `[Rpc(SendTo.NotServer)] UpdatePlayerPositionClientRpc(ulong playerID, Vector3 position, Quaternion rotation)`
- Ensure all clients receive position updates

**Estimated Lines**: ~30 lines

#### Task 3.2: Trigger Repositioning on Table Expansion
**File**: `NetworkTableTopManager.cs`
**Changes**:
- Modify `UpdateSeatPositionsBasedOnPlayerCount()` to detect 4→8 transition
- Call `TriggerTableExpansionRpc()` when 5th player joins
- RPC triggers local repositioning on all clients

**Estimated Lines**: ~40 lines

### Phase 4: UI Updates

#### Task 4.1: Update Join Button Player Count Display
**File**: `TableTopSeatButton.cs`
**Changes**:
- Add method `UpdatePlayerCountDisplay(int currentPlayers, int maxPlayers)`
- Display format: "Join (3/8)" or "Join (5/8)"
- Update when `AssignPlayerToSeat()` is called

**Estimated Lines**: ~20 lines

#### Task 4.2: Create Fade Overlay UI
**New File**: `Assets/MRTabletopAssets/Prefabs/UI/CameraFadeOverlay.prefab`
**Purpose**: Black panel for camera fade effect
**Components**:
- Canvas with CanvasGroup
- Full-screen black Image
- Initially invisible (alpha = 0)

**Estimated Lines**: N/A (prefab)

### Phase 5: Testing and Validation

#### Task 5.1: Test 1-4 Player Behavior
- Verify no changes to 1-4 player joining
- Confirm original seat positions maintained
- Test seat swapping functionality

#### Task 5.2: Test 5th Player Join
- Verify immediate switch to 8-player table
- Confirm table scales to 2x
- Validate players 1, 2, 3, 4 move radially outward (0.75f) while maintaining original angles
- Verify NO angular rotation for players 1-4 (they keep 0°, 180°, 270°, 90°)
- Check camera fade works smoothly
- Verify Player 5 joins at 45° position

#### Task 5.3: Test 6-8 Player Joins
- Confirm table remains at 8-player size
- Verify new players join at correct positions
- Test seat swapping with 5+ players

#### Task 5.4: Test Player Disconnect
- Verify table stays at 8-player size when players leave
- Confirm no repositioning when count drops below 5
- Test rejoining after disconnect

## File Modification Summary

### Files to Modify
1. **NetworkTableTopManager.cs** - Add 8-player lock, scale factor, repositioning triggers
2. **TableTop.cs** - Add 8-player positioning method, scale adjustment
3. **TableSeatSystem.cs** - Update to use scaled distances
4. **TableTopSeatButton.cs** - Add player count display

### Files to Create
1. **PlayerRepositionManager.cs** - Handle repositioning with camera fade
2. **CameraFadeOverlay.prefab** - UI for fade effect
3. **8PlayerJoiningBehaviorPlan.md** - This document

### Files to Reference (No Changes)
1. **FadePlaneMaterial.cs** - Reference for fade implementation
2. **SeatChangeListener.cs** - Currently empty, may be used for future enhancements

## Implementation Order

1. **Phase 1** (Core Table Behavior) - Foundation for 8-player mode
2. **Phase 2** (Player Repositioning) - Local radial repositioning with fade (no angular rotation)
3. **Phase 3** (Network Sync) - Ensure all clients stay synchronized
4. **Phase 4** (UI Updates) - Polish and user feedback
5. **Phase 5** (Testing) - Comprehensive validation

## Risk Assessment

### High Risk
- **Network synchronization timing** - Players may reposition at different times
  - Mitigation: Use server-authoritative timing, wait for all clients to acknowledge
- **Camera fade timing** - Fade may not complete before repositioning
  - Mitigation: Use coroutines with guaranteed wait times

### Medium Risk
- **Seat swapping conflicts** - Players swapping seats during expansion
  - Mitigation: Disable seat swapping during expansion transition
- **Player disconnect during expansion** - May leave table in inconsistent state
  - Mitigation: Handle disconnect events, rollback if necessary

### Low Risk
- **UI display issues** - Player count may not update immediately
  - Mitigation: Force UI refresh after seat assignment
- **Existing functionality preservation** - May accidentally break 1-4 player mode
  - Mitigation: Comprehensive testing, feature flags for rollback

## Success Criteria

1. ✅ Players 1-4 join with no behavior changes
2. ✅ 5th player triggers immediate 8-player table
3. ✅ Table scales to 2x size (radius: 0.75f → 1.5f)
4. ✅ Players 1, 2, 3, 4 move radially outward 0.75f with camera fade
5. ✅ Players 1, 2, 3, 4 maintain original angular positions (0°, 180°, 270°, 90°) - NO rotation
6. ✅ Players 5-8 join at correct positions (45°, 135°, 225°, 315°)
7. ✅ Table remains 8-player size for entire session
8. ✅ Seat swapping works correctly
9. ✅ UI shows accurate player count
10. ✅ All existing functionality preserved

## Documentation Updates Required

### After Implementation
1. Update `8PlayerImplementationInvestigation.md` with new behavior
2. Create `8PlayerJoiningBehaviorAsBuilt.md` documenting final implementation
3. Update `8PlayerTableSystemPlan.md` to mark tasks complete
4. Add usage examples for developers

## Next Steps

1. **Review and Approval** - Get stakeholder approval on this plan
2. **Create Detailed Task List** - Break down into granular, trackable tasks
3. **Begin Phase 1** - Implement core table behavior changes
4. **Iterative Testing** - Test after each phase completion
5. **Documentation** - Update as-built docs throughout implementation

