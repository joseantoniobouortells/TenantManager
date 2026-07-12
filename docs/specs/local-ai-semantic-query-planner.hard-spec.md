# Hard Spec — Local AI Semantic Query Planner

## 1. Objective

Replace the current brittle intent-based routing in the Local AI Assistant with a safe, extensible **Semantic Query Planner** that:

- Allows the local LLM to translate natural-language questions into a validated, structured `QueryPlan` object.
- Allows the application to execute deterministic, read-only EF Core queries based on that plan.
- Produces localized, deterministic answers for factual queries without requiring a second LLM call.
- Remains entirely local-first, read-only, and property-scoped.

The LLM must never generate SQL. The LLM must never access the database directly.

---

## 2. Current Problem

The existing assistant uses a limited set of predefined intents (`tenant_move_out_date`, `tenant_current_room`, `dashboard_summary`, etc.) and routes questions to hardcoded query functions. This causes:

- Questions outside the predefined set to fail silently or return incorrect generic summaries.
  - `"¿Hay pagos atrasados?"` → incorrectly returns a room/tenant count summary.
  - `"¿Cuántos contratos están activos?"` → incorrectly returns the same generic summary.
- No mechanism to extend supported questions without writing new routing code.
- Deterministic answers are only possible for a handful of hardcoded queries.

---

## 3. User Value

| User goal | Currently supported | With Semantic Query Planner |
|---|---|---|
| "¿Hay pagos atrasados?" | ❌ Wrong answer | ✅ Correct count/list |
| "¿Cuántos contratos activos?" | ❌ Wrong answer | ✅ Correct count |
| "¿Qué habitaciones están libres?" | Partial | ✅ Correct list |
| "¿Cuánto queda por cobrar?" | ❌ | ✅ Sum of pending amounts |
| "When does Erik move out?" | ✅ | ✅ (preserved) |
| "¿Y Namratha?" | ✅ (with context) | ✅ (preserved) |

---

## 4. Scope

- A `QueryPlan` model with resource, operation, filters, projection, sort, limit, language, and confidence.
- A **Semantic Query Catalog** defining which resources, operations, fields, and operators are allowed.
- A **QueryPlan Builder** that uses the local LLM to produce a `QueryPlan` from a natural-language question.
- A **QueryPlan Validator** that checks the plan against the catalog before execution.
- A **Query Executor** that runs deterministic, read-only EF Core queries based on valid plans.
- **Domain Semantic Resolvers** for computed concepts (effective end date, room occupancy, payment status, etc.).
- A **Deterministic Answer Formatter** that produces localized answers.
- Integration with the existing `AssistantContext` conversation state.
- Graceful fallback when LM Studio is unavailable or the plan is invalid.

All logic except UI concerns must reside in `TenantManager.Core`.

---

## 5. Out of Scope

- Arbitrary SQL generation by the LLM.
- Executing model-generated SQL strings.
- Database writes of any kind.
- Chat-based CRUD operations.
- Autonomous or agentic actions.
- Email or messaging delivery.
- Cloud AI APIs (OpenAI, Anthropic, Gemini, etc.).
- Vector databases or RAG over documents.
- Contract document content analysis.
- Semantic Kernel or large AI orchestration frameworks.
- OpenAI SDK (unless already present and strongly justified).
- Generic repositories or Unit of Work patterns.
- A public backend or HTTP API.
- Authentication changes.
- Multi-agent workflows.
- GraphQL or OData interfaces.

---

## 6. Architecture Overview

```
User message
     │
     ▼
AssistantViewModel (Avalonia — UI only)
     │  passes userMessage + AssistantContext
     ▼
SemanticQueryPlanner (Core)
     │  calls LocalAiClient.BuildQueryPlanAsync(userMessage, context)
     │  → LLM returns QueryPlan JSON
     │
     ▼
QueryPlanValidator (Core)
     │  validates resource, operation, fields, operators, values, limits
     │  injects active property scope
     │
     ▼
QueryExecutor (Core)
     │  executes deterministic EF Core read-only query
     │  uses DomainSemanticResolvers for computed fields
     │
     ▼
AnswerFormatter (Core)
     │  formats result in detected language (es/en)
     │
     ▼
AssistantViewModel
     │  appends message to chat
     ▼
AssistantView (XAML — display only)
```

The LLM is called at most once per turn (for plan extraction). A second LLM call is optional and must never be required for simple factual answers.

---

## 7. Core Library Responsibilities

All of the following must live in `TenantManager.Core` (no Avalonia dependencies):

- `QueryPlan`, `QueryFilter`, `QuerySort` models.
- `SemanticQueryCatalog` — resource/field/operator/operation allow-list.
- `SemanticQueryPlanner` — calls the LLM, parses the JSON response, validates structure.
- `QueryPlanValidator` — validates the plan, injects property scope, enforces limits.
- `QueryExecutor` — runs EF Core queries deterministically.
- `DomainSemanticResolvers` — effective end date, room occupancy, payment status, etc.
- `AnswerFormatter` — deterministic localized answer generation.
- `AssistantContext` — conversation state (already in Core).
- All unit and integration tests targeting Core logic.

---

## 8. Avalonia Application Responsibilities

`TenantManager.App` must contain only:

- `AssistantView.axaml` and `AssistantView.axaml.cs` — XAML and code-behind.
- `AssistantViewModel` — `IsLoading`, `SendCommand`, `Messages` (observable), session composition.
- Active property injection: the ViewModel reads the currently selected property and passes its `Id` to Core services.
- App startup, dependency wiring, and navigation.
- `RelayCommand`, `ViewModelBase`, and other UI helpers.
- Settings UI.

`TenantManager.App` must NOT contain business logic, query planning, validation, execution, formatting, or domain semantic resolution.

---

## 9. Semantic Query Catalog

This catalog defines the initial safe allowlist. It is illustrative and will be refined during implementation. The application must reject any resource, field, operation, or operator not in this catalog.

### 9.1 `rooms`

| Operation | Description |
|---|---|
| `count` | Count rooms matching filters |
| `list` | List rooms matching filters |

| Semantic field | Type | Description |
|---|---|---|
| `active` | bool | Room is active (not archived) |
| `occupied` | bool | Room has an active tenant/contract |
| `available` | bool | Room is active and not occupied |
| `currentRent` | decimal | Current base rent or rent-period amount |
| `name` | string | Room display name |

### 9.2 `tenants`

| Operation | Description |
|---|---|
| `count` | Count tenants matching filters |
| `list` | List tenants |
| `lookup` | Find a single tenant by name |

| Semantic field | Type | Description |
|---|---|---|
| `active` | bool | Has an active contract |
| `fullName` | string | Full display name |
| `currentRoom` | string | Room name from active contract |
| `moveInDate` | date | Start date of current/latest contract |
| `effectiveMoveOutDate` | date | Latest extension EndDate, or contract EndDate, or null |

### 9.3 `contracts`

| Operation | Description |
|---|---|
| `count` | Count contracts matching filters |
| `list` | List contracts |

| Semantic field | Type | Description |
|---|---|---|
| `active` | bool | Contract is currently active (started, not yet expired by effective end date) |
| `tenantName` | string | Associated tenant's full name |
| `roomName` | string | Associated room's name |
| `startDate` | date | Contract start date |
| `baseEndDate` | date | Raw contract EndDate (before extensions) |
| `effectiveEndDate` | date | Latest valid extension EndDate, or baseEndDate if no extensions |
| `hasExtensions` | bool | Contract has at least one extension record |
| `missingFile` | bool | FilePath is null/empty and FileContent is null |

### 9.4 `payments`

| Operation | Description |
|---|---|
| `count` | Count payment records matching filters |
| `list` | List payment records |
| `sum` | Sum a numeric field (e.g. expectedAmount, paidAmount) |

| Semantic field | Type | Description |
|---|---|---|
| `status` | enum | `pending`, `paid`, `late`, `partial` (maps to `PaymentStatus`) |
| `year` | int | Payment year |
| `month` | int | Payment month (1–12) |
| `expectedAmount` | decimal | Expected/charged amount |
| `paidAmount` | decimal | Amount actually paid |
| `tenantName` | string | Associated tenant's full name |
| `pending` | bool | Shorthand: status is pending |
| `late` | bool | Shorthand: status is late |

### 9.5 `expenses`

Supported only if the domain already has `ExpenseInvoice` with the following fields. Do not invent fields not present in the existing entity.

| Operation | Description |
|---|---|
| `count` | Count expense records |
| `list` | List expense records |
| `sum` | Sum a numeric field |

| Semantic field | Type | Description |
|---|---|---|
| `category` | string | Expense category name |
| `amount` | decimal | Invoice amount |
| `date` | date | Invoice date |
| `isPaid` | bool | Whether the expense is marked paid (if field exists) |

### 9.6 `dashboard`

| Operation | Description |
|---|---|
| `summary` | Returns a high-level summary of the current property |

Derived from: room count, active tenants, pending payments count, late payments count. No individual names or PII in summary.

---

## 10. QueryPlan Structure

The following is **illustrative**. The exact shape may be refined during implementation.

```json
{
  "language": "es",
  "resource": "contracts",
  "operation": "count",
  "filters": [
    {
      "field": "active",
      "operator": "equals",
      "value": true
    }
  ],
  "projection": [],
  "sort": [],
  "limit": 20,
  "confidence": 0.95
}
```

### Fields

| Field | Type | Description |
|---|---|---|
| `language` | string | Detected language: `"es"` or `"en"` |
| `resource` | string | Target resource (see catalog) |
| `operation` | string | Requested operation (see catalog) |
| `filters` | array | Zero or more filter conditions |
| `projection` | array | Requested output fields (optional; may be empty) |
| `sort` | array | Sort directives (optional) |
| `limit` | int | Max results for list operations; default 20, max 50 |
| `confidence` | float | Model confidence 0.0–1.0 |

### QueryFilter

```json
{
  "field": "status",
  "operator": "equals",
  "value": "late"
}
```

### QuerySort

```json
{
  "field": "effectiveEndDate",
  "direction": "asc"
}
```

---

## 11. Query Validation Rules

The validator must reject a plan if any of the following is true:

- `resource` is not in the catalog.
- `operation` is not supported for the given `resource`.
- Any `filter.field` is not in the catalog for the given `resource`.
- Any `filter.operator` is not in the allowed operator list.
- An operator is used that is incompatible with the field type (e.g. `greater_than` on a boolean field).
- A filter value does not match the expected type (e.g. string where int is required).
- A filter value for an enum field is not a valid enum value.
- A date value is not parseable as ISO 8601.
- `limit` exceeds the absolute maximum (50).
- `limit` is zero or negative.
- `confidence` is below the minimum threshold (configurable; suggested default 0.6).
- The plan is null or missing required fields.
- The plan contains an unknown top-level key that would imply a write action.

The validator must always inject the active property scope. The plan must never specify a `propertyId` directly; it is always set by the application.

---

## 12. Query Execution Rules

- All queries must use EF Core with `AsNoTracking()`.
- No raw SQL, no `FromSqlRaw`, no `ExecuteSql`.
- All filters must be translated to LINQ predicates by the executor, not by the LLM.
- The active property ID must always be the first filter applied.
- `count` operations return a single integer.
- `list` operations return at most `limit` records (default 20, hard max 50).
- `sum` operations return a single decimal.
- `summary` operations return a structured summary DTO.
- DateTimeOffset fields must be handled client-side (`.ToList()` before LINQ date comparisons) to avoid SQLite translation errors.
- The executor must not expose fields not present in the semantic catalog.
- The executor must not include phone numbers, emails, private notes, or file contents in results.

---

## 13. Domain Semantics

These definitions align with existing application and domain behavior. Do not implement them inconsistently.

### Active Contract
A `RentalContract` where `StartDate <= now` and `EffectiveEndDate >= now`. Effective end date is computed by the resolver.

### Effective Contract End Date
1. Load all `RentalContractExtension` records for the contract where `EndDate IS NOT NULL`.
2. If any exist, use `MAX(EndDate)` among them.
3. Otherwise use `RentalContract.EndDate`.
4. If neither is set, the contract has no registered end date.

### Effective Tenant Move-Out Date
1. Find the tenant's latest contract by `StartDate DESC`.
2. Apply the effective contract end date rule to that contract.
3. If no contract or no date exists, return null.
4. Do not fall back to a `Tenant.MoveOutDate` property (which does not exist on the current entity).

### Occupied Room
A room that is referenced by at least one currently active contract.

### Available Room
A `Room` where `IsActive = true` and no currently active contract references it.

### Pending Payment
A `MonthlyPayment` with `Status = PaymentStatus.Pending` (or equivalent per current `PaymentStatus` enum).

### Late Payment
A `MonthlyPayment` with `Status = PaymentStatus.Late` (or equivalent per current `PaymentStatus` enum).

### Current Month
Derived from the application's local clock (`DateTime.Now` or `DateTimeOffset.Now`). Not configurable by the LLM.

### Current Rent
The `MonthlyRent` field from the tenant's latest active `RentalContract`, or its latest active `RentalContractExtension` if present and the extension overrides rent.

---

## 14. Language and Conversation Behavior

- The `QueryPlan` must include the detected `language` (`"es"` or `"en"`).
- All answers must use the language from the plan or conversation context.
- Short follow-up questions (`"¿Y Namratha?"`, `"And Namratha?"`) may inherit the previous conversation language via `AssistantContext.LastLanguage`.
- If no language is detectable, use the application UI culture.
- Default language if nothing is known: `"en"`.
- The `AnswerFormatter` must produce deterministic localized strings for all count, list, sum, and no-data cases.
- A second LLM call for formatting is optional and must not be required for simple factual answers.
- Conversation context (`AssistantContext`) is preserved across turns, not persisted to the database.

---

## 15. Privacy and Security

The following must never be included in LLM prompts, query results, or formatted answers, unless an explicitly approved future feature requires them:

- Tenant phone numbers.
- Tenant email addresses.
- Private notes (tenant, contract, or room notes).
- Full contract file paths.
- Contract file contents.
- Document contents of any kind.
- Local database file paths.
- Local machine user account paths.
- Any secret or credential.

The `QueryExecutor` result DTOs must explicitly exclude these fields. The `AnswerFormatter` must not access them. The `SemanticQueryPlanner` must not include them in the extraction prompt context.

---

## 16. Performance and Limits

- Default list result limit: **20** records.
- Absolute maximum list result: **50** records.
- The plan's `limit` field must not exceed 50.
- Intent extraction and query planning must each use `max_tokens` ≤ 200 to avoid reasoning overflow.
- Temperature must be 0 for deterministic extraction.
- `stream: false` must always be set.
- A second LLM call (for formatting) must use `max_tokens` ≤ 150.
- The application must not block indefinitely; a request timeout should be applied.

---

## 17. Error and Clarification Behavior

| Situation | Application response |
|---|---|
| LM Studio unavailable | Friendly localized error message; no retry loop |
| Empty or null QueryPlan returned | "I could not understand the question. Please try rephrasing." (localized) |
| Plan confidence below threshold | Ask for clarification (localized) |
| Unknown resource or field | Fallback: "I can only answer questions about rooms, tenants, contracts, payments, and expenses." (localized) |
| Tenant name matches 0 records | "I cannot find a tenant named X." (localized) |
| Tenant name matches multiple records | Ask clarification with list of names (localized) |
| No data matches filters | "No records match your query." (localized) |
| Query result is empty list | "No {resource} found matching those criteria." (localized) |
| Plan validation fails | Log the failure; return localized error; never partially execute |
| Invalid date or value in plan | Reject plan; return localized error |

---

## 18. Functional Requirements

- **FR-SQP-001:** The application must convert a natural-language question into a `QueryPlan` using the local LLM.
- **FR-SQP-002:** The `QueryPlan` must be validated against the `SemanticQueryCatalog` before execution.
- **FR-SQP-003:** The application must execute the plan using deterministic EF Core LINQ queries only.
- **FR-SQP-004:** The result must be scoped to the currently active property. The active property must never be selected by the model.
- **FR-SQP-005:** The formatted answer must use the language specified in the `QueryPlan` or conversation context.
- **FR-SQP-006:** `"¿Hay pagos atrasados?"` must return the correct count/list of late payments.
- **FR-SQP-007:** `"¿Cuántos contratos están activos?"` must return the correct count of active contracts.
- **FR-SQP-008:** `"¿Qué habitaciones están libres?"` must return the correct list of available rooms.
- **FR-SQP-009:** `"¿Cuánto queda por cobrar este mes?"` must return the sum of pending payment amounts for the current month.
- **FR-SQP-010:** The existing direct tenant questions (`"When does Erik move out?"`, `"¿Y Namratha?"`) must continue to work.
- **FR-SQP-011:** The assistant must remain strictly read-only.
- **FR-SQP-012:** Unsupported or ambiguous questions must produce a localized clarification or error message, not a wrong answer.
- **FR-SQP-013:** Plan validation failures must never result in partial query execution.
- **FR-SQP-014:** The loading indicator must remain visible during query planning and execution.
- **FR-SQP-015:** Send must be disabled while processing to prevent duplicate submissions.

---

## 19. Technical Requirements

- **TR-SQP-001:** All Core logic must be free of Avalonia dependencies.
- **TR-SQP-002:** `QueryPlan` and related models must be serializable/deserializable via `System.Text.Json`.
- **TR-SQP-003:** The LLM call must use the existing `HttpClient`-based `LocalAiClient`. Do not introduce a new HTTP client or SDK.
- **TR-SQP-004:** Use JSON Schema or structured output hints in the extraction prompt if LM Studio supports them. Fall back to plain JSON instructions otherwise.
- **TR-SQP-005:** All EF Core queries must use `AsNoTracking()`.
- **TR-SQP-006:** DateTimeOffset comparisons that cannot be translated by SQLite must be resolved client-side via `.ToList()` before LINQ date predicates.
- **TR-SQP-007:** The semantic catalog and validation rules must be unit-testable independently of the database.
- **TR-SQP-008:** The executor must be integration-testable against an in-memory SQLite database (`DataSource=:memory:`).
- **TR-SQP-009:** The answer formatter must be unit-testable with mock query results.
- **TR-SQP-010:** No raw SQL, `FromSqlRaw`, or `ExecuteSqlRaw` calls are permitted in the executor.

---

## 20. Acceptance Criteria

- [ ] `"¿Hay pagos atrasados?"` returns the correct late-payment count or list.
- [ ] `"¿Cuántos contratos están activos?"` returns the correct active-contract count.
- [ ] `"¿Qué habitaciones están libres?"` returns the correct available-room list.
- [ ] `"¿Cuánto queda por cobrar este mes?"` returns the correct pending sum.
- [ ] `"When does Erik Artigas move out?"` still returns the correct date including extensions.
- [ ] `"¿Y Namratha?"` still inherits the previous intent and language.
- [ ] An invalid resource in the plan causes rejection and a localized error, not a crash.
- [ ] The active property is always injected by the application, never by the model.
- [ ] No PII fields appear in LLM prompts or formatted answers.
- [ ] All result lists respect the 50-record absolute maximum.
- [ ] The loading indicator is visible during processing and clears on completion.
- [ ] Build passes with 0 errors and 0 warnings.
- [ ] All tests pass.
- [ ] No database files, secrets, or bin/obj folders are committed.

---

## 21. Expected Tests

### Unit tests (no database)

- `QueryPlanValidator_RejectsUnknownResource`
- `QueryPlanValidator_RejectsUnknownOperation`
- `QueryPlanValidator_RejectsUnknownField`
- `QueryPlanValidator_RejectsUnsupportedOperator`
- `QueryPlanValidator_RejectsInvalidEnumValue`
- `QueryPlanValidator_RejectsLimitExceedingMax`
- `QueryPlanValidator_RejectsLowConfidencePlan`
- `QueryPlanValidator_InjectsActivePropertyScope`
- `AnswerFormatter_FormatsCountInSpanish`
- `AnswerFormatter_FormatsCountInEnglish`
- `AnswerFormatter_FormatsEmptyListInSpanish`
- `AnswerFormatter_FormatsEmptyListInEnglish`
- `SemanticQueryCatalog_ContainsExpectedResources`

### Integration tests (in-memory SQLite)

- `QueryExecutor_CountsLatePaymentsCorrectly`
- `QueryExecutor_CountsActiveContractsCorrectly`
- `QueryExecutor_ListsAvailableRoomsCorrectly`
- `QueryExecutor_SumsPendingPaymentsForCurrentMonth`
- `QueryExecutor_ResolvesEffectiveContractEndDateWithExtensions`
- `QueryExecutor_ResolvesEffectiveTenantMoveOutDate`
- `QueryExecutor_ScopesResultsToActiveProperty`
- `QueryExecutor_RejectsWriteAttempt` (if applicable)

### Conversation/integration tests

- `SemanticPlanner_ParsesSpanishLatePaymentsQuestion`
- `SemanticPlanner_ParsesEnglishActiveContractsQuestion`
- `SemanticPlanner_InheritsLanguageForFollowUp`
- `AssistantViewModel_ShowsLoadingIndicatorDuringQuery`
- `AssistantViewModel_DisablesSendDuringQuery`

---

## 22. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Local LLM returns malformed QueryPlan JSON | High | Medium | Robust parsing with fallback; reject and ask clarification |
| Local LLM reasons into `reasoning_content` instead of `content` | High | Medium | Existing retry logic in `LocalAiClient`; `max_tokens` limit |
| LM Studio does not support JSON Schema response format | Medium | Low | Fall back to plain JSON instructions in the prompt |
| Model returns a SQL string instead of a QueryPlan | Low | High | Validation rejects non-catalog resources; executor never interprets strings as SQL |
| Complex filter combinations exceed simple LINQ | Medium | Medium | Start with simple equality filters; expand incrementally |
| DateTimeOffset SQLite translation errors | Known | Medium | Resolve client-side with `.ToList()` before date LINQ |
| Ambiguous tenant names cause wrong results | Low | High | Existing safe matching logic preserved |

---

## 23. Decisions Made

- **The LLM will produce a QueryPlan, not SQL.** The application is responsible for all query execution.
- **Core library holds all reusable logic.** The Avalonia app provides only UI and composition.
- **Deterministic formatting is preferred.** A second LLM call is optional, not required.
- **Active property scope is always injected by the application.** The model never specifies it.
- **The existing `AssistantContext` session state is preserved and extended** to store the last resolved `QueryPlan` or intent.
- **The existing direct-tenant-question flow** (`tenant_move_out_date`, `tenant_current_room`) is preserved alongside the new planner to handle short follow-ups.
- **Result limits** (default 20, max 50) are enforced in the validator, not by the LLM.
- **All specification files are in English** and must be committed to the repository.

---

## 24. Open Questions

1. Should the answer formatter call the LLM for complex summaries (e.g. dashboard), or always produce deterministic text?
2. Is JSON Schema response format available in the version of LM Studio used in production?
3. Should `confidence` thresholds be user-configurable via Settings, or hardcoded as constants?
4. Should the existing direct-tenant-question flow be retired eventually in favor of the planner, or maintained in parallel?
5. Should the `projection` field in the QueryPlan be used for field selection in list results, or always return a fixed safe DTO?
6. Should `sort` directives be supported in the first implementation, or added in a follow-up?
7. Should the `expenses` resource be included in the first implementation? The domain entity (`ExpenseInvoice`) exists, but fields need verification.
