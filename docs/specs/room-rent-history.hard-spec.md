# Hard Spec - Room Rent History

## Goal

Add support for historical monthly rent periods per room to track price changes over time without breaking existing data structures.

## Context

The current `Room.MonthlyRent` stores a single static value. To reflect real-world scenarios where rent changes across different lease terms, the application needs a historical record of rent periods for each room.

## Scope

- Add a new `RoomRentPeriod` domain entity.
- A rent period has a `RoomId`, `MonthlyRent` (decimal >= 0), `StartDate` (required), optional `EndDate`, and optional `Notes`.
- Validate that `EndDate` (if present) is >= `StartDate`.
- Provide a minimal UI to view, add, and edit rent periods for a selected room.
- Leave the existing `Room.MonthlyRent` field as the default/current fallback rent to avoid breaking other parts of the application.

## Out of Scope

- Automatic recalculation of existing monthly payments based on rent period changes.
- Full UI redesign.
- Breaking database migrations (we will use SQLite EnsureCreated, which implies the local db will need a reset if the schema changes).

## Constraints

- Continue using SQLite with EF Core.
- Follow existing architecture rules (no generic repositories, keep it simple).

## Acceptance Criteria

- A room can have multiple rent periods.
- Rent periods are persisted in the SQLite database.
- The UI allows viewing and managing rent periods for a room.
- The `EndDate` must be >= `StartDate`.
- Monthly rent cannot be negative.
- Overlapping rent periods for the same room are rejected (including open-ended periods).
- Non-overlapping periods for the same room are allowed.
- Overlapping periods for different rooms are allowed.
- Editing a rent period does not falsely reject itself.
- The UI shows a clear validation message when overlap is detected.
- Existing features still work.
- The build succeeds and tests pass.
