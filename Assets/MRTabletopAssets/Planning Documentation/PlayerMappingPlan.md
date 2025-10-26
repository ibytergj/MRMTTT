# Player Mapping Implementation Plan

## Overview
This plan details the implementation of the mapping between physical player seats and logical player order (clockwise) for up to 8 players, while maintaining backward compatibility with the existing 4-player layout.

## Current Implementation
- The `TableTop.cs` script already has methods for mapping between physical and logical player indices:
  - `GetLogicalPlayerIndex(int physicalSeatIndex, int totalActivePlayers)` - Maps physical seat index to logical player index
  - `GetPhysicalSeatIndex(int logicalPlayerIndex, int totalActivePlayers)` - Maps logical player index to physical seat index

- The `NetworkTableTopManager.cs` script has methods for:
  - `GetActivePlayerCount()` - Gets the number of active players based on occupied seats
  - `RequestSeat(int newSeatChoice)` - Converts logical seat choice to physical if needed

## Implementation Steps

### 1. Review and Update TableTop.cs
- Review the existing `GetLogicalPlayerIndex` method to ensure it correctly maps physical seat indices to logical player indices
- Review the existing `GetPhysicalSeatIndex` method to ensure it correctly maps logical player indices to physical seat indices
- Ensure both methods handle the transition between 4-player and 5-8 player layouts correctly

### 2. Update Player Mapping Logic
- For 4 or fewer players:
  - Maintain the existing mapping:
    - Physical layout: 0->0°, 1->180°, 2->270°, 3->90°
    - Logical clockwise order: 0, 3, 1, 2
  - Ensure the mapping is consistent with the seat positioning

- For 5-8 players:
  - Use a direct mapping where physical and logical indices are the same
  - This works because the seats are already positioned in a clockwise order

### 3. Test Player Mapping
- Test with 2-4 players:
  - Verify that the logical player order is clockwise: 0, 3, 1, 2
  - Verify that the physical seat positions match the expected angles
- Test with 5-8 players:
  - Verify that the logical player order is clockwise: 0, 1, 2, 3, 4, 5, 6, 7
  - Verify that the physical seat positions match the expected angles

### 4. Update NetworkTableTopManager.cs
- Ensure `RequestSeat` correctly converts logical seat choice to physical seat index
- Verify that the mapping is used consistently throughout the codebase

## Code Changes

### TableTop.cs
- Review and update the `GetLogicalPlayerIndex` method:
  ```csharp
  /// <summary>
  /// Maps physical seat index to logical player index.
  /// For 4 or fewer players, maintains the current mapping.
  /// For 5+ players, physical and logical indices are the same.
  /// </summary>
  public int GetLogicalPlayerIndex(int physicalSeatIndex, int totalActivePlayers)
  {
      if (totalActivePlayers <= 4)
      {
          // For 4 or fewer players, maintain the current mapping
          // Current physical layout: 0->0°, 1->180°, 2->270°, 3->90°
          // Logical clockwise order: 0, 3, 1, 2
          switch (physicalSeatIndex)
          {
              case 0: return 0;
              case 1: return 2;
              case 2: return 3;
              case 3: return 1;
              default: return physicalSeatIndex;
          }
      }
      else
      {
          // For 5+ players, physical and logical indices are the same
          return physicalSeatIndex;
      }
  }
  ```

- Review and update the `GetPhysicalSeatIndex` method:
  ```csharp
  /// <summary>
  /// Maps logical player index to physical seat index.
  /// For 4 or fewer players, maintains the current mapping.
  /// For 5+ players, physical and logical indices are the same.
  /// </summary>
  public int GetPhysicalSeatIndex(int logicalPlayerIndex, int totalActivePlayers)
  {
      if (totalActivePlayers <= 4)
      {
          // For 4 or fewer players, maintain the current mapping
          // Logical clockwise order: 0, 3, 1, 2
          // Current physical layout: 0->0°, 1->180°, 2->270°, 3->90°
          switch (logicalPlayerIndex)
          {
              case 0: return 0;
              case 1: return 3;
              case 2: return 1;
              case 3: return 2;
              default: return logicalPlayerIndex;
          }
      }
      else
      {
          // For 5+ players, physical and logical indices are the same
          return logicalPlayerIndex;
      }
  }
  ```

### NetworkTableTopManager.cs
- Review and update the `RequestSeat` method:
  ```csharp
  public void RequestSeat(int newSeatChoice)
  {
      int activePlayers = GetActivePlayerCount();
      // Convert logical seat choice to physical if needed
      int physicalSeatChoice = m_TableTop.GetPhysicalSeatIndex(newSeatChoice, activePlayers);
      RequestSeatServerRpc(NetworkManager.Singleton.LocalClientId, TableTop.k_CurrentSeat, physicalSeatChoice);
  }
  ```

### TableSeatSystem.cs
- Review and update the `GetRotationAngleBasedOnSeatNum` method:
  ```csharp
  float GetRotationAngleBasedOnSeatNum(int seatNum)
  {
      int totalSeats = m_TableTop.seats.Length;
      int activePlayers = GetActivePlayerCount();

      if (activePlayers <= 4)
      {
          // Original 4-player layout with non-sequential numbering
          switch (seatNum)
          {
              case 0: return 0f;      // First position
              case 1: return 180f;    // Opposite position
              case 2: return 270f;    // Left position
              case 3: return 90f;     // Right position
              default: return 0f;
          }
      }
      else
      {
          // Sequential clockwise numbering for 5-8 players
          float anglePerSeat = 360f / activePlayers;
          return seatNum * anglePerSeat;
      }
  }
  ```

## Testing Checklist
- [ ] Verify logical player order for 2 players
- [ ] Verify logical player order for 3 players
- [ ] Verify logical player order for 4 players
- [ ] Verify logical player order for 5 players
- [ ] Verify logical player order for 6 players
- [ ] Verify logical player order for 7 players
- [ ] Verify logical player order for 8 players
- [ ] Verify that the mapping is used consistently throughout the codebase
- [ ] Verify that the UI correctly displays player colors based on the logical player order
