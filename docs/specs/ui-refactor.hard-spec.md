# Hard Spec - UI/UX and Structure Refactor

## Goal

Refactor the Avalonia UI of the Tenant Manager application to fix contrast issues, remove hardcoded colors, and split the monolithic `MainWindow.axaml` into manageable `UserControl` components, maintaining the existing business logic and behavior.

## Context

A recent code review identified that the MVP was built with poor color contrast (e.g., light gray text on white backgrounds), hardcoded colors that bypass Avalonia's theming system, and a monolithic `MainWindow.axaml` (over 500 lines) containing the markup for all tabs. This refactoring phase is anchored in improving maintainability and user experience (UX) without adding new functional features.

## Scope

- **Contrast & Colors:**
  - Replace low-contrast text colors (`Foreground="Gray"`) with more readable theme-aware brushes or darker explicit colors (e.g., `#4A4A4A` or system resources).
  - Move hardcoded colors (e.g., `#0F52BA`, `#C5221F`, `#137333`) to `<Application.Resources>` or `<Application.Styles>` in `App.axaml`.
  - Update all `.axaml` files to reference these colors using `DynamicResource`.
  - Add explicit styling (background/hover) for `Button.Action` so it is clearly identifiable as interactive.
- **View Refactoring:**
  - Split the contents of `MainWindow.axaml`'s tabs into separate Avalonia `UserControl` files inside the `src/TenantManager.App/Views/` folder:
    - `DashboardView.axaml`
    - `RoomsView.axaml`
    - `TenantsView.axaml`
    - `ContractsView.axaml`
    - `PaymentsView.axaml`
  - Update `MainWindow.axaml` so it only contains the main layout (`TabControl`) and references the newly created `UserControl` files.
- **Layout Tweaks:**
  - Ensure grid definitions do not aggressively truncate text when resizing, within the limits of the current layout pattern.

## Out of Scope

- Introducing new functional features (e.g., CRM, new entities).
- Changing the underlying database schema or Domain logic.
- Refactoring the `ViewModels` (they should remain as they are, just bound to the new `UserControl` files).

## Acceptance Criteria

- AC-001: The application builds and runs without errors (`dotnet build` succeeds).
- AC-002: Hardcoded colors are no longer present in `MainWindow.axaml` or the new `*View.axaml` files; they use `DynamicResource`.
- AC-003: Text elements that previously used `Gray` are replaced with a higher contrast alternative.
- AC-004: `MainWindow.axaml` is refactored, and its tab contents are moved to their respective `Views/*View.axaml` files.
- AC-005: All tabs (Dashboard, Rooms, Tenants, Contracts, Payments) load and function correctly when the application is executed.
