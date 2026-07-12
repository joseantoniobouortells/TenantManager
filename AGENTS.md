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
6. **Commits:** Do NOT create commits or push code automatically without asking for explicit user validation first.
7. **Documentos y Planes:** Cualquier plan de implementación, especificación (`*_plan.md`, `*_spec.md`) o archivo generado para revisión humana DEBE guardarse SIEMPRE dentro de la carpeta `docs/specs/` (no en la raíz ni en los artefactos temporales).
8. **Core library:** All reusable application/domain logic that is not purely UI-specific must live in `TenantManager.Core`. This includes: business rules, prompt builders, intent parsers, tenant matching, deterministic answer generation, data-query services, AI request/response DTOs, and AI conversation context. The Avalonia app (`TenantManager.App`) must stay focused on UI, ViewModels, Views, XAML styling, app startup, and UI-specific composition. `TenantManager.Core` must NOT reference Avalonia or any UI framework. Future web/mobile frontends must be able to reuse Core logic without changes.

## Validation Pipeline
Run after changes:
```bash
dotnet build src/TenantManager.App/TenantManager.App.csproj && dotnet test
git status -s
git ls-files | grep -E '(/bin/|/obj/|\.(db|db-shm|db-wal|sqlite|sqlite3)$)' || true
```

## Specification Tracking and Commits

9. **Spec files are repository artifacts:** Hard-spec (`*.hard-spec.md`) and Gherkin (`*.gherkin.md`) files under `docs/specs/` are first-class repository artifacts and must always be tracked by Git. They must never be left untracked after the corresponding feature work.
10. **Spec + implementation commits:** When a feature changes behavior, its hard-spec and Gherkin must be updated and included in the same commit, or in a clearly preceding specification commit. Implementation commits must not silently omit relevant spec changes.
11. **Check for untracked specs before committing:** Agents must run `git status --short` and check for untracked `docs/specs/` files before creating any commit. Untracked spec files must be staged and included.
12. **Spec commit messages in English:** All specification file content and commit messages referencing specifications must be written in English.
13. **AI query planning in Core:** AI query planning logic, QueryPlan models, the semantic query catalog, query validators, query executors, domain semantic resolvers, and answer formatters must all reside in `TenantManager.Core` with no Avalonia dependency.