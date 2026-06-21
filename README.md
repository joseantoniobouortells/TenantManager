# Tenant Manager

A local-first, cross-platform desktop application for managing rented rooms, tenants, contracts, and monthly payments.

**Status:** Early MVP / Work in progress

## Goal

Provide individual property owners with a simple tool to centralize room occupancy, tenant information, rental contract paths, and payment tracking — all stored locally without cloud dependencies.

## Tech stack

- **.NET 10** with C#
- **Avalonia** (cross-platform desktop UI)
- **SQLite** (local database)
- **Entity Framework Core** (data access)

## Current scope

- Room management (name, monthly rent, status, notes)
- Tenant management (name, contact info, move-in/out dates, deposit, notes)
- Tenant-to-room assignment
- Contract management via local file paths (no content stored)
- Monthly payment tracking (year, month, amounts, status, notes)
- Summary view of occupied rooms and pending payments
- Cross-platform (Windows, macOS, Linux)

## Out of scope (for now)

- CRM for candidate tenants
- Multi-property management
- User accounts or login
- Cloud synchronization or online backup
- HTTP API or separate backend
- Mobile app
- Digital signatures or automatic contract generation
- Online payments or bank integration
- Invoicing or tax features
- Automatic notifications

## How to build

```bash
dotnet restore src/TenantManager.App/TenantManager.App.csproj
dotnet build src/TenantManager.App/TenantManager.App.csproj
```

The application targets `net10.0`. Ensure you have the .NET 10 SDK installed.

## Repository structure

```
TenantManager.sln
src/
  TenantManager.App/
    Domain/        # Domain entities (Room, Tenant, RentalContract, MonthlyPayment)
    Data/          # EF Core DbContext and SQLite configuration
    Services/      # Application services (future)
    ViewModels/    # Avalonia view models
    Views/         # Avalonia views (auto-generated)
docs/
  specs/           # Specification and Gherkin scenarios
```

## Notes

- The SQLite database is stored locally at `~/Library/Application Support/TenantManager/tenantmanager.db` (macOS) or the equivalent platform-specific location.
- Contract files are not copied or stored in the database — only their file system paths are saved.
- Contract file existence is validated at runtime.

## Privacy notice

This application stores tenant personal data (names, phone numbers, email addresses) and rental information locally. If you fork or clone this repository, **do not commit real tenant data, local database files (\*.db, \*.sqlite), or contract documents**. The `.gitignore` is configured to exclude these files, but be mindful when adding or modifying ignore rules.
