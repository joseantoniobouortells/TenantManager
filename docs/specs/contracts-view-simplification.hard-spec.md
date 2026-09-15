# Hard Spec - Contracts View Simplification & Modernization

## Goal

Simplify and modernize the contracts view (`ContractsView.axaml` and `ContractListViewModel.cs`), enabling users to focus by default on active rental contracts, filter or search through historical contracts when needed, open attached contract documents with a single click, and view essential financial metrics directly in the list.

## Context

The current contracts screen presents several usability limitations:
1. Active and long-expired contracts are mixed together in a single table, with only a small status LED distinguishing them.
2. The table devotes two full columns to file paths (`FilePath`, showing truncated filesystem paths like `/Users/...`) and file existence status (`FileStatus`, showing "Yes"/"No").
3. Essential lease information like `MonthlyRent` is absent from the table, requiring the user to click into edit mode just to see the rent amount.
4. Clicking any contract row immediately triggers edit mode (`IsEditing = true`), hiding the contracts list entirely.

## Scope

- **Status Filtering:**
  - Introduce a status filter selector (`All`, `Active`, `Expired`) in the top bar of the contracts view.
  - Default view to `Active` contracts upon loading.
  - Filter in-memory using `ContractDisplayItem.IsActive` so response times are instantaneous without repeated SQLite queries.
  - Combine status filtering seamlessly with the real-time textual `SearchQuery` (matching tenant name, room name, or notes).

- **Enriched Contract Display Items:**
  - Expand `ContractDisplayItem` with:
    - `MonthlyRent` (`decimal`)
    - `DepositAmount` (`decimal`)
    - `PaymentDay` (`int`)
    - `HasFile` (`bool`) — true if `Contract.FileContent != null` or if `Contract.FilePath` exists on disk.
    - `ExtensionCount` (`int`) — number of registered extensions for this contract.

- **Streamlined Table Columns:**
  - Remove plain text `FilePath` and `FileStatus` columns from the table.
  - Add `MonthlyRent` as a sortable column formatted as currency.
  - Add a dedicated direct-action button with a document/PDF icon (`PathIcon`) per row, bound to `OpenFileCommand`, enabled only when `HasFile` is true (matching the pattern in `ExpensesView`).
  - Retain the row-level delete button with modal confirmation.
  - Retain bidirectional sorting on Tenant, Start Date, and End Date (and extend to Monthly Rent).

- **Internationalization (i18n):**
  - Add localized strings for the new status filters (`FilterActiveContracts`, `FilterExpiredContracts`, `FilterAllContracts`, `RentHeader`, etc.) in `src/TenantManager.App/Assets/i18n/es.axaml` and `en.axaml`.

## Out of Scope

- Changes to SQLite schema or EF Core entity models (`RentalContract` and `RentalContractExtension` remain unchanged).
- Redesigning the extensions data model or calculation logic.
- Background sync or cloud storage of documents.

## Architectural & Technical Decisions

1. **In-Memory Filtering:** Contracts are already loaded into memory per property (`_allContracts`). Status filtering (`Active`, `Expired`, `All`) will be applied client-side in `ApplyFiltersAndSort()`, maintaining zero database latency and avoiding SQLite date evaluation limitations.
2. **Direct Row Actions:** `OpenFileCommand` will accept `ContractDisplayItem` as an optional parameter (fallback to `SelectedItem`) to allow opening the PDF without requiring row selection or entering edit mode.
3. **No Breaking Changes to Editing Flow:** The edit card and extensions management retain their existing bindings and commands, functioning as expected when a user intends to edit a contract or create a new one.

## Acceptance Criteria

- **AC-001:** By default, upon opening the Contracts tab or switching properties, only contracts where `IsActive == true` are displayed in the list.
- **AC-002:** The user can change the status filter ComboBox to "Expired" (to see only ended contracts) or "All" (to see all contracts for the current property).
- **AC-003:** When a status filter is selected, typing in `SearchQuery` filters only within the chosen status subset.
- **AC-004:** The table displays `MonthlyRent` formatted as currency for each contract.
- **AC-005:** The table no longer displays raw text file paths or "Yes/No" text columns.
- **AC-006:** Each row contains a PDF icon button that is enabled when `HasFile == true` and disabled when `HasFile == false`. Clicking it opens the associated PDF.
- **AC-007:** The row-level delete button continues to trigger the confirmation modal and safely deletes the contract upon confirmation.
- **AC-008:** All UI labels and ComboBox options support dynamic switching between Spanish and English via `DynamicResource`.
- **AC-009:** `dotnet build` compiles cleanly with 0 errors and domain/unit tests pass.
