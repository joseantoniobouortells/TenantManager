# Tenant Manager — Agent Spec

## Project & Scope
Local-first desktop app for property owners to manage rooms, tenants, rental contracts (including storing PDF file contents in the database), and monthly payments.
- **Stack:** .NET 10 (C#), Avalonia v12.0.4, SQLite (SQLitePCLRaw 3.50.3 / EF Core Sqlite 10.0.x), xUnit.
- **Strictly Out of Scope:** Cloud sync, HTTP API/backend, auth/login, CRM, digital signatures, Unit of Work, generic repositories, entity navigation properties.
- **Agent Memory:** Always read `docs/memory.md` to get context on recent architectural decisions, known issues, and pending roadmap tasks. You MUST update this document every time a new feature is implemented, a significant refactor is done, or a key decision is made.

## Architecture & Data Rules
1. **DB Context:** Use `AppDbContext` directly per ViewModel (no DI container). 
2. **Schema updates:** Use EF Core Migrations. Generate new migrations when entities change and apply them automatically on startup using `db.Database.Migrate()`.
3. **UI:** Preserve ViewModel-per-tab pattern in `MainWindow.axaml`. Default to `x:CompileBindings="False"`.
4. **Testing:** Domain tests use strictly in-memory SQLite (`SqliteConnection("Data Source=:memory:")`). No UI automation.
5. **Zero-Leak Policy:** NEVER commit PII (names/contacts), local DB files (`.db*`, `.sqlite*`), real contract documents, or `bin`/`obj` folders.

## Validation Pipeline
Run after changes:
```bash
dotnet build src/TenantManager.App/TenantManager.App.csproj && dotnet test
git status -s
git ls-files | grep -E '(/bin/|/obj/|\.(db|db-shm|db-wal|sqlite|sqlite3)$)' || true