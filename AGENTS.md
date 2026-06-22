# AGENTS.md — Tenant Manager

## Project overview

Tenant Manager is a local-first, cross-platform desktop application for individual property owners to manage rented rooms, tenants, rental contract file paths, and monthly payments. Data is stored in a local SQLite database — no cloud, no backend, no authentication. The repository is public on GitHub; do not commit real tenant data or local database files.

Current MVP scope: room CRUD, tenant CRUD with room assignment, contract file path management with existence checking and system-file open, monthly payment tracking with duplicate prevention, and a minimal dashboard showing occupied rooms and pending payments.

## Tech stack

- .NET 10 / C#
- Avalonia (cross-platform desktop UI, v12.0.4)
- SQLite (via SQLitePCLRaw.lib.e_sqlite3 3.50.3)
- Entity Framework Core (Microsoft.EntityFrameworkCore.Sqlite 10.0.x)
- xUnit + Microsoft.NET.Test.Sdk for tests

## Repository structure

```
TenantManager.sln
src/
  TenantManager.App/
    Domain/          # Room.cs, Tenant.cs, RentalContract.cs, MonthlyPayment.cs, PaymentStatus.cs
    Data/            # AppDbContext.cs, DatabasePath.cs
    ViewModels/      # ViewModelBase.cs, RelayCommand.cs, per-feature VMs
    App.axaml[.cs]   # Application entry, EnsureCreated on startup
    MainWindow.axaml # TabControl with Dashboard / Rooms / Tenants / Contracts / Payments
tests/
  TenantManager.Tests/  # DomainTests.cs with 11 focused tests
docs/
  specs/             # mvp-local-tenant-manager.hard-spec.md, gherkin.md
```

## Product scope

**In scope:** Rooms, Tenants, Contract file paths (not contents), Monthly payments (with statuses: Pending, Paid, Partial, Late, Waived), Minimal dashboard.

**Out of scope (do not implement):** CRM, multi-property support, authentication/login, cloud sync, backend/HTTP API, digital signatures, online payments/bank integration, storing contract file contents in SQLite, generic repositories, Unit of Work.

## Architecture rules

- Keep it simple. Avoid unnecessary abstractions.
- No backend, no HTTP API, no cloud dependency.
- Use `AppDbContext` directly. No generic repositories or Unit of Work.
- Store only contract file paths — never file contents.
- DbContext is created per ViewModel (no DI container in MVP).
- Schema changes use `EnsureCreated`. If entities change, delete the local DB file manually.
- Do not introduce navigation properties on domain entities.
- Use `x:CompileBindings="False"` unless there is a strong reason to change.

## Data and privacy rules

- **Never commit real tenant names, phone numbers, emails, or other PII.**
- **Never commit local SQLite database files** (`.db`, `.db-shm`, `.db-wal`, `.sqlite`, `.sqlite3`).
- **Never commit contract documents or private files.**
- **Never commit `bin/` or `obj/` directories.**
- Treat all personal data as sensitive. The `.gitignore` covers the above, but remain vigilant.

## Development workflow

1. Read `docs/specs/mvp-local-tenant-manager.hard-spec.md` and the Gherkin scenarios before changing feature behavior.
2. Implement one focused task at a time. Prefer small, reviewable diffs.
3. Validate with `dotnet build` and `dotnet test`.
4. Do not silently change scope. If a requested change conflicts with the spec, report it before changing the spec.

## Testing guidance

- Write focused tests for domain/data behavior, not UI automation.
- Use the in-memory SQLite approach from `DomainTests.cs` (open `SqliteConnection("Data Source=:memory:")`, pass `DbContextOptions<AppDbContext>`).
- Keep tests lightweight. Do not add UI automation unless explicitly requested.

## UI guidance

- Keep Avalonia UI simple and maintainable.
- Preserve existing TabControl layout and ViewModel-per-tab pattern.
- Use user-friendly labels instead of raw boolean values where it improves clarity.
- Do not add heavy UI frameworks without justification.

## Git guidance

- English commit messages.
- Do not commit generated artifacts (`bin/`, `obj/`, `.user`, `.suo`, etc.).
- Do not force push unless explicitly instructed.
- Keep commits focused on a single logical change.
- Do not include local DB files, contracts, or private data.

## Validation commands

```bash
dotnet build src/TenantManager.App/TenantManager.App.csproj
dotnet test
git status --short
git ls-files | grep -E '(^|/)(bin|obj)/' || true
git ls-files | grep -E '\.(db|db-shm|db-wal|sqlite|sqlite3)$' || true
```

## Agent response style

When completing a task, include:
- **Files modified** (paths)
- **Summary** of what was done
- **Validation performed or recommended** (build, test, git status)
- **Risks or doubts** (e.g., schema changes requiring DB deletion, scope edge cases)
