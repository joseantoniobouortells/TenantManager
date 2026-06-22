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

  Scenario: Prevent overlapping rent periods
    Given a room has an existing rent period from 2026-01-01 to 2026-12-31
    When a new rent period is created for the same room from 2026-06-01 to 2026-07-31
    Then the system rejects it with an overlap error message

  Scenario: Allow non-overlapping periods for the same room
    Given a room has an existing rent period from 2026-01-01 to 2026-06-30
    When a new rent period is created for the same room from 2026-07-01 onward
    Then the system accepts it

  Scenario: Allow overlapping periods for different rooms
    Given room A has a rent period from 2026-01-01 to 2026-12-31
    And room B exists
    When a rent period is created for room B from 2026-06-01 to 2026-07-31
    Then the system accepts it
