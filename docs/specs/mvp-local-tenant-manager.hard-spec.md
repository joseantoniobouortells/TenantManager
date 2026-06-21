# Hard Spec - MVP Local Tenant Manager

## Goal

Create a cross-platform desktop application to manage rented rooms, tenants, contracts linked by local file paths, and monthly payments.

## Context

The user rents out rooms in an apartment and needs a simple tool to centralize operational information: occupancy, contracts, and payments.

The application is initially for personal, local use. It is not planned as a SaaS product at this stage.

## Target user

Individual property owner managing a single apartment rented by rooms.

## Scope

This version includes:

- Room management.
- Tenant management.
- Tenant-to-room assignment.
- Contract management via local file paths.
- Contract file existence validation.
- Contract opening with the system default application.
- Manual monthly payment management.
- Minimal dashboard view with occupancy and pending payments.
- Local SQLite database.
- Cross-platform desktop application with Avalonia.

## Out of scope

This version does not include:

- CRM for candidate tenants.
- Multi-property management.
- Users or login.
- Cloud synchronization.
- Separate backend.
- HTTP API.
- Mobile app.
- Tenant portal.
- Digital signature.
- Automatic contract generation.
- Online payments.
- Bank integration.
- Document storage as BLOB.
- Invoicing or tax features.
- Automatic notifications.

## Functional requirements

- FR-001: The user can create, edit, list, and deactivate rooms.
- FR-002: A room has a name, monthly rent, active/inactive status, and optional notes.
- FR-003: The user can create, edit, list, and deactivate tenants.
- FR-004: A tenant has a name, optional phone, optional email, move-in date, optional move-out date, deposit amount, and optional notes.
- FR-005: An active tenant can be assigned to a room.
- FR-006: The user can associate one or more contracts with a tenant.
- FR-007: Each contract stores a local file path.
- FR-008: The application does not store contract content in the database.
- FR-009: The application shows whether the contract path exists or is broken.
- FR-010: The user can open the contract from the application using the system default application.
- FR-011: The user can create and edit monthly payments per tenant.
- FR-012: A monthly payment has year, month, expected amount, paid amount, status, optional paid date, and optional notes.
- FR-013: No two payments for the same tenant, year, and month are allowed.
- FR-014: The application shows a summary view with occupied rooms and pending payments for the current month.

## Technical requirements

- TR-001: The application will be a cross-platform desktop app.
- TR-002: The application will use .NET and C#.
- TR-003: The UI will be implemented with Avalonia.
- TR-004: Persistence will use SQLite.
- TR-005: Data access will use Entity Framework Core.
- TR-006: The database will be stored locally.
- TR-007: Contracts will be stored as file system paths.
- TR-008: The application must work offline.
- TR-009: The project must be maintainable by a single person.
- TR-010: The MVP must avoid unnecessary architecture.

## Constraints

- Do not implement functionality outside scope.
- Do not add authentication.
- Do not add a separate backend.
- Do not add an HTTP API.
- Do not add cloud synchronization.
- Do not add online payments.
- Do not store files in SQLite.
- Do not introduce complex architecture.
- Do not add dependencies without justification.
- Do not implement CRM at this stage.
- Do not implement multi-property support at this stage.

## Acceptance criteria

- AC-001: A room can be created.
- AC-002: A tenant can be created and assigned to a room.
- AC-003: A contract path can be saved for a tenant.
- AC-004: The application indicates whether the contract file exists.
- AC-005: The application can open the contract from its path.
- AC-006: A monthly payment can be created for a tenant.
- AC-007: Duplicate payments for the same tenant, year, and month are rejected.
- AC-008: Pending payments for the current month can be viewed.
- AC-009: Occupied rooms can be viewed.
- AC-010: Data persists when closing and reopening the application.
- AC-011: The application works offline.

## Expected tests

- Test for room creation.
- Test for tenant creation.
- Test for tenant-to-room assignment.
- Test for contract path creation.
- Test for existing path detection.
- Test for broken path detection.
- Test for monthly payment creation.
- Test for duplicate payment prevention.
- Test for pending payment query for the current month.

## Risks

- Contract paths may break if files are moved.
- The application may grow too large if CRM is added before validating the core.
- Avalonia may require OS-specific adjustments for file opening.
- Personal data management will require more care if the project evolves into a commercial product.
- Visual design may consume time without providing real MVP validation.

## Decisions made

- The first version will use a single Avalonia project with internal folders for Domain, Data, Services, ViewModels, and Views.
- The MVP will be a cross-platform desktop application.
- Avalonia will be used for the UI.
- .NET and C# will be used.
- SQLite will be used for persistence.
- Entity Framework Core will be used for data access.
- Contracts will be linked by local file path.
- Contracts will not be embedded in the database.
- CRM is excluded from the first MVP.
- The application will be local with no separate backend.

## Open questions

- Exact location of the local database.
- Whether contract paths will be absolute or relative to a configurable base folder.
- Whether tests will be created from the initial phase or after stabilizing domain and persistence.
