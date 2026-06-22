Feature: Room Rent History

  Background:
    Given the application is a desktop multiplatform app using Avalonia and SQLite
    And the new hard specification is defined in "docs/specs/room-rent-history.hard-spec.md"

  Scenario: Implement domain entity
    Given the SQLite database exists
    When the RoomRentPeriod entity is added
    Then it has RoomId, MonthlyRent, StartDate, optional EndDate, and optional Notes
    And it is registered in AppDbContext

  Scenario: Validate rent period data
    Given a new RoomRentPeriod is created
    When the MonthlyRent is negative
    Or the EndDate is before the StartDate
    Then the domain or database should reject it

  Scenario: View and manage rent periods in UI
    Given the rooms management UI exists
    When a room is selected
    Then the user can view its associated rent periods
    And the user can create a new rent period for the room
    And the user can edit an existing rent period
