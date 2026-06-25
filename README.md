# Tenant Manager

A local-first, cross-platform desktop application for managing properties, rooms, tenants, contracts (including PDF storage), expenses, and monthly payments.

**Status:** Early MVP / Work in progress

## Goal

Provide individual property owners with a clean, visually rich, and secure tool to centralize multi-property management, room occupancy, tenant information, rental contract documents, expenses, and payment tracking — all stored locally without cloud dependencies.

## Tech stack

- **.NET 10** with C#
- **Avalonia** (cross-platform desktop UI, v12.0.4)
- **SQLite** (local database via SQLitePCLRaw)
- **Entity Framework Core** (data access via EF Core Sqlite 10.0.x with migrations)

## Current Scope

- **Multi-Property Management:** Create, edit, and deactivate properties (all lists, rooms, and payments are dynamically filtered by the active property).
- **Room Management:** CRUD operations, room active status, base rent fallback, and property mapping.
- **Tenant Management:** CRUD operations, contact info, Move-in/out dates, security deposits, room assignments, active status, and property mapping.
- **Contract & Extensions Management:**
  - Link contracts and contract extensions to tenants and properties.
  - **Contract PDF Storage:** Upload and store contract/extension PDF files directly in the database as binary data (`FileContent`).
  - Validation of local contract file existence.
  - Direct opening of contract PDFs using default system viewers.
- **Expenses & Invoice Management:**
  - Register expense invoices by property, type, date, and amount.
  - **Invoice PDF Storage:** Upload, clear, and open attached PDF invoice files stored directly in the database.
- **Monthly Payments:**
  - Automated batch generation of monthly payments for active tenants.
  - Expected rent and expense calculation based on active contract/extension details (fixed or variable expense splitting).
  - Tracking of payments by status (Paid, Pending, Partial, Late, Waived), paid dates, and amount.
  - Keyboard-driven multi-selection deletion (Delete/Backspace).
- **Dashboard:**
  - Room occupancy circular progress rings.
  - Monthly income collection progress rings (expected vs. collected).
  - Native vector-based Donut Chart showing total payment status distribution.
  - Warning/Alert lists for pending payments, available rooms, and missing contract files.
- **Internationalization (i18n):** Complete localized UI support for both English and Spanish (switchable at runtime).
- **Settings Persistence:** Locally persisted JSON settings for active property, language, and general options.

## Out of Scope (For Now)

- CRM for candidate tenants.
- User accounts, authentication, or login.
- Cloud synchronization or online backups.
- HTTP API or separate backend.
- Mobile application.
- Digital signatures or automatic contract generation.
- Online payments or bank integration.

## How to Build

Run the following commands to restore and build the application:
```bash
dotnet restore src/TenantManager.App/TenantManager.App.csproj
dotnet build src/TenantManager.App/TenantManager.App.csproj
```

The application targets `net10.0`. Ensure you have the .NET 10 SDK installed.

## How to Run Tests

Run the domain unit test suite:
```bash
dotnet test
```

## Repository Structure

```
TenantManager.sln
src/
  TenantManager.App/
    Domain/        # Domain entities (Property, Room, Tenant, RentalContract, RentalContractExtension, ExpenseInvoice, MonthlyPayment)
    Data/          # EF Core DbContext, migrations, and local settings persistence
    Assets/        # UI Assets and i18n localization dictionaries (en.axaml, es.axaml)
    ViewModels/    # ViewModels (MainViewModel, PropertyListViewModel, RoomListViewModel, TenantListViewModel, etc.)
    Views/         # Standalone reusable UI views (PropertiesView, RoomsView, DashboardView, etc.)
tests/
  TenantManager.Tests/  # Domain behavior and database persistence unit tests
docs/
  specs/           # Product specifications and Gherkin scenarios
```

## Notes

- The SQLite database is stored locally at `~/Library/Application Support/TenantManager/tenantmanager.db` (macOS) or the equivalent platform-specific location.
- PDF contract/invoice documents are stored in the database. For external file paths, existence checks are validated at runtime.

## Privacy Notice

This application stores tenant personal data (names, phone numbers, email addresses) and rental contracts locally. If you fork or clone this repository, **do not commit real tenant data, local database files (*.db, *.sqlite), or contract documents**. The `.gitignore` is configured to exclude these files.
