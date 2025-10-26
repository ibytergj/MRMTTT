# Seat Positioning Implementation Plan

## Overview
This plan details the implementation of proper seat positioning for up to 8 players in a regular polygon arrangement, while maintaining backward compatibility with the existing 4-player layout.

## Current Implementation
- The `TableTop.cs` script already has methods for positioning seats:
  - `UpdateSeatPositions(int playerCount)` - Main method that decides which positioning to use
  - `PositionSeatsForFourOrLess(int playerCount)` - Handles 2-4 players with the original layout
  - `PositionSeatsSequentially(int playerCount)` - Handles 5-8 players with sequential layout

- The `NetworkTableTopManager.cs` script has methods for:
  - `UpdateSeatPositionsBasedOnPlayerCount()` - Updates seat positions based on occupied seats
  - `CountOccupiedSeats()` - Counts how many seats are currently occupied

## Implementation Steps

### 1. Review and Update TableTop.cs
- Review the existing `PositionSeatsForFourOrLess` method to ensure it correctly positions seats for 2-4 players
- Review the existing `PositionSeatsSequentially` method to ensure it correctly positions seats for 5-8 players
- Ensure the `UpdateShapeSides` method correctly updates the shader parameter

### 2. Update Seat Positioning Logic
- For 4 or fewer players:
  - Maintain the existing physical order: 0° (seat 0), 180° (seat 1), 270° (seat 2), 90° (seat 3)
  - Ensure seats are properly activated/deactivated based on player count

- For 5-8 players:
  - Use sequential seating with evenly distributed angles
  - Calculate angles based on a regular polygon (pentagon for 5, hexagon for 6, etc.)
  - Position seats at the calculated angles
  - Ensure seats are properly activated/deactivated based on player count

### 3. Test Seat Positioning
- Test with 2 players: Seats at 0° and 180°
- Test with 3 players: Seats at 0°, 180°, and 270°
- Test with 4 players: Seats at 0°, 180°, 270°, and 90°
- Test with 5 players: Seats evenly distributed in a pentagon (72° apart)
- Test with 6 players: Seats evenly distributed in a hexagon (60° apart)
- Test with 7 players: Seats evenly distributed in a heptagon (~51.4° apart)
- Test with 8 players: Seats evenly distributed in an octagon (45° apart)

### 4. Update NetworkTableTopManager.cs
- Ensure `UpdateSeatPositionsBasedOnPlayerCount` correctly determines the number of seats to show
- Verify that seat positions are updated when players join or leave

## Code Changes

### TableTop.cs
- Review and update the `PositionSeatsForFourOrLess` method:
  ```csharp
  private void PositionSeatsForFourOrLess(int playerCount)
  {
      // Activate/deactivate seats as needed
      for (int i = 0; i < m_Seats.Length; i++)
      {
          bool isActive = i < playerCount;
          if (m_Seats[i].seatTransform.gameObject != null)
              m_Seats[i].seatTransform.gameObject.SetActive(isActive);
      }

      // Position seats using existing layout
      if (playerCount >= 1)
      {
          // Seat 0: 0 degrees
          m_Seats[0].seatTransform.localRotation = Quaternion.Euler(0, 0, 0);
          Vector3 direction = m_Seats[0].seatTransform.forward;
          m_Seats[0].seatTransform.localPosition = direction * m_SeatDistance;
      }

      if (playerCount >= 2)
      {
          // Seat 1: 180 degrees
          m_Seats[1].seatTransform.localRotation = Quaternion.Euler(0, 180, 0);
          Vector3 direction = m_Seats[1].seatTransform.forward;
          m_Seats[1].seatTransform.localPosition = direction * m_SeatDistance;
      }

      if (playerCount >= 3)
      {
          // Seat 2: 270 degrees
          m_Seats[2].seatTransform.localRotation = Quaternion.Euler(0, 270, 0);
          Vector3 direction = m_Seats[2].seatTransform.forward;
          m_Seats[2].seatTransform.localPosition = direction * m_SeatDistance;
      }

      if (playerCount >= 4)
      {
          // Seat 3: 90 degrees
          m_Seats[3].seatTransform.localRotation = Quaternion.Euler(0, 90, 0);
          Vector3 direction = m_Seats[3].seatTransform.forward;
          m_Seats[3].seatTransform.localPosition = direction * m_SeatDistance;
      }
  }
  ```

- Review and update the `PositionSeatsSequentially` method:
  ```csharp
  private void PositionSeatsSequentially(int playerCount)
  {
      // Calculate angle step based on player count
      float angleStep = 360f / playerCount;

      // Activate/deactivate and position seats
      for (int i = 0; i < m_Seats.Length; i++)
      {
          bool isActive = i < playerCount;
          if (m_Seats[i].seatTransform.gameObject != null)
              m_Seats[i].seatTransform.gameObject.SetActive(isActive);

          if (isActive)
          {
              // Calculate angle for this seat (clockwise starting from 0)
              float angle = i * angleStep;

              // Set rotation to face the center
              m_Seats[i].seatTransform.localRotation = Quaternion.Euler(0, angle, 0);

              // Position the seat at the calculated angle
              Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
              m_Seats[i].seatTransform.localPosition = direction * m_SeatDistance;
          }
      }

      // Log the arrangement
      Debug.Log($"Positioned {playerCount} seats in a {GetPolygonName(playerCount)} arrangement");
  }
  ```

### NetworkTableTopManager.cs
- Review and update the `UpdateSeatPositionsBasedOnPlayerCount` method:
  ```csharp
  private void UpdateSeatPositionsBasedOnPlayerCount()
  {
      if (IsServer && m_TableTop != null)
      {
          int occupiedSeatCount = CountOccupiedSeats();

          // Determine how many seats to show based on occupied seats
          int seatsToShow;

          if (occupiedSeatCount <= 4)
          {
              // For 1-4 players, use the standard 4-player layout
              seatsToShow = 4;
          }
          else
          {
              // For 5-8 players, use the exact number of players
              // This creates a pentagon for 5, hexagon for 6, etc.
              seatsToShow = occupiedSeatCount;
              seatsToShow = Mathf.Min(8, seatsToShow); // Cap at 8 players
          }

          Debug.Log($"Updating seat positions for {seatsToShow} seats (occupied seats: {occupiedSeatCount})");
          m_TableTop.UpdateSeatPositions(seatsToShow);
      }
  }
  ```

## Testing Checklist
- [ ] Verify seat positions for 2 players
- [ ] Verify seat positions for 3 players
- [ ] Verify seat positions for 4 players
- [ ] Verify seat positions for 5 players
- [ ] Verify seat positions for 6 players
- [ ] Verify seat positions for 7 players
- [ ] Verify seat positions for 8 players
- [ ] Verify that seats are properly activated/deactivated
- [ ] Verify that seat positions are updated when players join or leave
