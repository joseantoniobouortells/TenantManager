# Gherkin — Local AI Semantic Query Planner

## Overview

This document contains the phased Gherkin implementation plan for the Semantic Query Planner. Each phase is small, self-contained, and agent-executable. A phase must not implement anything defined in a later phase. Agents must read and confirm each phase's acceptance criteria before proceeding to the next.

All implementation must follow `AGENTS.md`. Reusable logic belongs in `TenantManager.Core`. The Avalonia app is responsible only for UI and composition.

---

## Phase 1 — Define Core SemanticQueryPlan Models

**Goal:** Add the foundational data models for the Semantic Query Planner to `TenantManager.Core`. No Avalonia dependencies. No database queries. No LLM calls.

```gherkin
Feature: SemanticQueryPlan core models

  Scenario: SemanticQueryPlan model is defined in Core with no Avalonia dependency
    Given a developer adds a SemanticQueryPlan model to TenantManager.Core
    When the Core project is built
    Then the build succeeds
    And TenantManager.Core has no reference to Avalonia assemblies

  Scenario: SemanticQueryPlan contains all required fields
    Given a SemanticQueryPlan instance is created
    Then it must have: language, resource, operation, filters, projection, sort, limit, confidence
    And filters is a list of SemanticQueryFilter
    And sort is a list of SemanticQuerySort
    And limit defaults to 20

  Scenario: SemanticQueryFilter contains required fields
    Given a SemanticQueryFilter instance is created
    Then it must have: field, operator, value

  Scenario: SemanticQuerySort contains required fields
    Given a SemanticQuerySort instance is created
    Then it must have: field, direction (asc or desc)

  Scenario: Supported enums or constants are defined
    Given the models are defined
    Then there must be a defined set of known resource names (rooms, tenants, contracts, payments, expenses, dashboard)
    And a defined set of known operations (count, list, lookup, sum, summary)
    And a defined set of known operators (equals, not_equals, greater_than, greater_than_or_equal, less_than, less_than_or_equal, contains, in, between)
    And a defined set of known sort directions (asc, desc)

  Scenario: SemanticQueryPlan is serializable via System.Text.Json
    Given a valid SemanticQueryPlan instance
    When it is serialized to JSON and deserialized back
    Then the result is equivalent to the original instance
```

**Acceptance criteria:**
- `SemanticQueryPlan`, `SemanticQueryFilter`, `SemanticQuerySort` exist in `TenantManager.Core`.
- All models use `System.Text.Json` attributes.
- No Avalonia dependency in Core.
- Build passes.

**Restriction:** Do not implement the catalog, validator, executor, planner, or formatter in this phase.

---

## Phase 2 — Define the Semantic Query Catalog

**Goal:** Define the allowlist of resources, fields, operations, and operators. This catalog will be used by the validator and executor.

```gherkin
Feature: Semantic Query Catalog

  Scenario: Catalog contains all initial resources
    Given the SemanticQueryCatalog is initialized
    Then it must contain entries for: rooms, tenants, contracts, payments, expenses, dashboard

  Scenario: Each resource defines its allowed operations
    Given the catalog entry for "rooms"
    Then its allowed operations are: count, list
    Given the catalog entry for "tenants"
    Then its allowed operations are: count, list, lookup
    Given the catalog entry for "contracts"
    Then its allowed operations are: count, list
    Given the catalog entry for "payments"
    Then its allowed operations are: count, list, sum
    Given the catalog entry for "expenses"
    Then its allowed operations are: count, list, sum
    Given the catalog entry for "dashboard"
    Then its allowed operations are: summary

  Scenario: Each resource defines its semantic fields
    Given the catalog entry for "payments"
    Then its defined semantic fields include: status, year, month, expectedAmount, paidAmount, tenantName, pending, late

  Scenario: Each semantic field defines compatible operators
    Given the field "status" of resource "payments"
    Then its compatible operators are: equals, not_equals, in
    Given the field "year" of resource "payments"
    Then its compatible operators are: equals, greater_than, less_than, greater_than_or_equal, less_than_or_equal, between

  Scenario: Catalog is unit-testable without a database
    Given the catalog is queried in a unit test
    Then no database connection is required
```

**Acceptance criteria:**
- `SemanticQueryCatalog` class exists in `TenantManager.Core`.
- All resources, operations, fields, and compatible operators are defined.
- Catalog can be queried in unit tests without a database.
- Build passes.

**Restriction:** Do not implement the planner, validator, executor, or formatter yet.

---

## Phase 3 — Add Structured LM Studio Query Planner

**Goal:** Add a `SemanticQueryPlanner` to Core that calls the local LLM and parses the response into a `QueryPlan`. Extend `LocalAiClient` with a `BuildQueryPlanAsync` method.

```gherkin
Feature: Structured LM Studio Query Planner

  Scenario: A natural-language question is sent to LM Studio with a structured extraction prompt
    Given LM Studio is running and the AI assistant is enabled
    When a user asks "¿Hay pagos atrasados?"
    Then the application sends a prompt to LM Studio requesting a QueryPlan JSON
    And the prompt includes the list of supported resources, operations, and fields
    And the prompt includes the current conversation language if known
    And the prompt does NOT include tenant names, payment amounts, contract details, or any PII

  Scenario: A valid QueryPlan JSON response is parsed correctly
    Given LM Studio returns a valid QueryPlan JSON for "¿Hay pagos atrasados?"
    When the planner parses the response
    Then the resulting QueryPlan has resource="payments", operation="count", language="es"
    And it has a filter: field="late", operator="equals", value=true
    And confidence >= 0.6

  Scenario: Model returns markdown-wrapped JSON
    Given LM Studio wraps the JSON in triple backticks
    When the planner parses the response
    Then the markdown wrapper is stripped before parsing
    And the QueryPlan is still parsed correctly

  Scenario: Model returns empty or invalid JSON
    Given LM Studio returns an empty string or malformed JSON
    When the planner parses the response
    Then the result is null
    And no exception propagates to the caller

  Scenario: LM Studio is unavailable
    Given LM Studio is offline
    When the planner attempts to build a QueryPlan
    Then the planner returns null
    And the caller receives a graceful null result with no unhandled exception

  Scenario: Conversation context is included in the extraction prompt
    Given a previous resolved intent of "tenant_move_out_date" and language "es"
    When the user asks "¿Y Namratha?"
    Then the extraction prompt includes the previous intent and language as context hints
```

**Acceptance criteria:**
- `SemanticQueryPlanner` exists in `TenantManager.Core`.
- `LocalAiClient.BuildQueryPlanAsync(userMessage, context)` exists and is called by the planner.
- Valid JSON is parsed into a `QueryPlan`.
- Markdown code-block wrappers are stripped robustly.
- Invalid JSON returns null without throwing.
- LM Studio unavailability is handled gracefully.
- No PII is included in the extraction prompt.
- Core has no Avalonia dependency.
- Build passes.

**Restriction:** Do not implement the validator or executor. Do not integrate with the ViewModel yet.

---

## Phase 4 — Add QueryPlan Validator

**Goal:** Add `QueryPlanValidator` to Core that checks a `QueryPlan` against the `SemanticQueryCatalog` and enforces safety rules before execution.

```gherkin
Feature: QueryPlan Validator

  Scenario: Unknown resource is rejected
    Given a QueryPlan with resource="invoices"
    When the validator validates the plan
    Then validation fails with a "unknown resource" error
    And no query is executed

  Scenario: Unknown operation is rejected
    Given a QueryPlan with resource="rooms" and operation="delete"
    When the validator validates the plan
    Then validation fails with an "unsupported operation" error

  Scenario: Unknown field in filter is rejected
    Given a QueryPlan with resource="payments" and a filter on field="creditCard"
    When the validator validates the plan
    Then validation fails with an "unknown field" error

  Scenario: Unsupported operator is rejected
    Given a QueryPlan with a filter: field="active", operator="contains", value=true
    When the validator validates the plan
    Then validation fails with an "unsupported operator" error

  Scenario: Invalid enum value is rejected
    Given a QueryPlan with filter: field="status", operator="equals", value="overdue"
    When the validator validates the plan
    Then validation fails with an "invalid value" error
    (Note: valid values are: pending, paid, late, partial)

  Scenario: Limit exceeding maximum is rejected
    Given a QueryPlan with limit=100
    When the validator validates the plan
    Then validation fails with a "limit exceeded" error

  Scenario: Low-confidence plan is rejected
    Given a QueryPlan with confidence=0.3
    When the validator validates the plan
    Then validation fails with a "low confidence" error

  Scenario: Active property scope is always injected
    Given a valid QueryPlan for resource="tenants"
    And the currently active property has ID=5
    When the validator processes the plan
    Then the validated plan includes a property scope constraint set to ID=5
    And the plan did not contain a propertyId field before validation

  Scenario: Valid plan passes validation
    Given a QueryPlan with resource="payments", operation="count", filter: field="late", operator="equals", value=true, confidence=0.9
    When the validator validates the plan
    Then validation succeeds
    And the plan is ready for execution
```

**Acceptance criteria:**
- `QueryPlanValidator` exists in `TenantManager.Core`.
- All rejection rules are implemented and unit-tested.
- Active property injection is implemented.
- No Avalonia dependency.
- Build and unit tests pass.

**Restriction:** Do not implement the executor or ViewModel integration yet.

---

## Phase 5 — Add Deterministic Query Executor

**Goal:** Add `QueryExecutor` to Core that executes valid, validated `QueryPlan` instances using EF Core LINQ queries. No raw SQL. No model-generated SQL.

```gherkin
Feature: Query Executor

  Scenario: Count late payments for the active property
    Given a validated QueryPlan: resource="payments", operation="count", filter: field="late", equals=true, propertyId=1
    And the database contains 3 late payments for property 1
    When the executor executes the plan
    Then the result is 3

  Scenario: Count active contracts for the active property
    Given a validated QueryPlan: resource="contracts", operation="count", filter: field="active", equals=true, propertyId=1
    And the database contains 5 active contracts for property 1
    When the executor executes the plan
    Then the result is 5

  Scenario: List available rooms for the active property
    Given a validated QueryPlan: resource="rooms", operation="list", filter: field="available", equals=true, propertyId=1
    And the database contains 2 available rooms for property 1
    When the executor executes the plan
    Then the result is a list of 2 rooms
    And the result does not include occupied rooms

  Scenario: Sum pending payment amounts for the current month
    Given a validated QueryPlan: resource="payments", operation="sum", filter: field="pending", equals=true, year=current, month=current
    And the database contains 2 pending payments with amounts 500 and 300 for the active property
    When the executor executes the plan
    Then the result is 800

  Scenario: Results are scoped to the active property
    Given payments exist for both property 1 and property 2
    And the validated plan has propertyId=1
    When the executor executes the plan
    Then only results from property 1 are returned

  Scenario: List result respects the limit
    Given a QueryPlan with limit=5 and resource="tenants", operation="list"
    And the database contains 20 tenants
    When the executor executes the plan
    Then at most 5 results are returned

  Scenario: EF Core queries use AsNoTracking
    Given any executor query
    When the executor runs the query
    Then all EF Core queries must use AsNoTracking()

  Scenario: No raw SQL is used
    Given any executor query
    Then the codebase must not contain FromSqlRaw or ExecuteSqlRaw calls in the executor
```

**Acceptance criteria:**
- `QueryExecutor` exists in `TenantManager.Core`.
- All operations (count, list, sum, summary) are implemented for at least rooms, tenants, contracts, payments.
- All queries use `AsNoTracking()`.
- No raw SQL in the executor.
- Results are always scoped to the active property.
- Integration tests pass against in-memory SQLite.
- Build passes.

**Restriction:** Do not implement the domain semantic resolvers or ViewModel integration in this phase.

---

## Phase 6 — Add Domain Semantic Resolvers

**Goal:** Implement the computed concepts that require domain logic: effective contract end date (with extensions), room occupancy, available rooms, current rent, effective tenant move-out date.

```gherkin
Feature: Domain Semantic Resolvers

  Scenario: Effective contract end date without extensions
    Given a RentalContract with EndDate=2026-06-30
    And no RentalContractExtension records for that contract
    When the resolver computes the effective end date
    Then the result is 2026-06-30

  Scenario: Effective contract end date with one extension
    Given a RentalContract with EndDate=2026-06-30
    And a RentalContractExtension with EndDate=2026-08-31
    When the resolver computes the effective end date
    Then the result is 2026-08-31

  Scenario: Effective contract end date with multiple extensions
    Given a RentalContract with EndDate=2026-06-30
    And extensions with EndDates: 2026-08-31, 2026-10-31
    When the resolver computes the effective end date
    Then the result is 2026-10-31

  Scenario: Contract is active when current date is within effective range
    Given a contract where StartDate <= today and EffectiveEndDate >= today
    When the resolver evaluates active status
    Then the contract is considered active

  Scenario: Room is available when active and not referenced by an active contract
    Given a Room where IsActive=true
    And no active contract references that room
    When the resolver evaluates occupancy
    Then the room is considered available

  Scenario: Room is occupied when referenced by an active contract
    Given an active contract that references RoomId=3
    When the resolver evaluates occupancy for Room 3
    Then the room is considered occupied

  Scenario: Effective tenant move-out date uses extension date when available
    Given a tenant with a contract that has extensions
    When the resolver computes the effective move-out date
    Then the result uses the latest valid extension EndDate

  Scenario: Effective tenant move-out date falls back to contract EndDate when no extensions
    Given a tenant with a contract with EndDate=2026-09-30
    And no extensions for that contract
    When the resolver computes the effective move-out date
    Then the result is 2026-09-30
```

**Acceptance criteria:**
- All domain semantic resolvers exist in `TenantManager.Core`.
- All scenarios are covered by unit tests against in-memory SQLite.
- Resolvers are used by the executor for computed fields.
- No Avalonia dependency.
- Build and tests pass.

**Restriction:** Do not integrate with the ViewModel or modify the UI in this phase.

---

## Phase 7 — Add Deterministic Answer Formatter

**Goal:** Add `AnswerFormatter` to Core that produces localized, deterministic answer strings from query results. No LLM call required for simple factual answers.

```gherkin
Feature: Answer Formatter

  Scenario: Count answer in Spanish
    Given a count result of 3 for resource="payments" with filter late=true
    And the detected language is "es"
    When the formatter formats the result
    Then the answer is "Hay 3 pagos con retraso." (or equivalent Spanish phrasing)

  Scenario: Count answer in English
    Given a count result of 3 for resource="payments" with filter late=true
    And the detected language is "en"
    When the formatter formats the result
    Then the answer is "There are 3 late payments." (or equivalent English phrasing)

  Scenario: Count of zero in Spanish
    Given a count result of 0 for resource="payments" with filter late=true
    And the detected language is "es"
    When the formatter formats the result
    Then the answer is "No hay pagos con retraso." (or equivalent)

  Scenario: List answer in Spanish
    Given a list result of 2 rooms: "Habitación 1", "Habitación 2"
    And the detected language is "es"
    When the formatter formats the result
    Then the answer includes the room names in a readable format

  Scenario: Sum answer in Spanish
    Given a sum result of 800.00 for resource="payments" with filter pending=true
    And the detected language is "es"
    When the formatter formats the result
    Then the answer includes the amount in a readable Spanish format

  Scenario: No data answer
    Given an empty query result
    When the formatter formats the result
    Then the answer states no records were found in the correct language

  Scenario: Clarification when tenant name is ambiguous
    Given a lookup result with multiple matching tenant names
    When the formatter formats the result
    Then the answer lists the matching names and asks the user to clarify in the correct language

  Scenario: Error message when plan is rejected
    Given plan validation failed
    When the formatter formats the error
    Then a localized, user-friendly error message is returned
    And no technical details are exposed
```

**Acceptance criteria:**
- `AnswerFormatter` exists in `TenantManager.Core`.
- Spanish and English templates exist for count, list, sum, no-data, clarification, and error cases.
- Formatter is unit-testable with mock result objects.
- No LLM call required for formatting simple factual answers.
- No Avalonia dependency.
- Build and unit tests pass.

**Restriction:** Do not integrate with the ViewModel yet. Do not modify AssistantViewModel in this phase.

---

## Phase 8 — Integrate Planner into Assistant Conversation Flow

**Goal:** Wire the `SemanticQueryPlanner`, `QueryPlanValidator`, `QueryExecutor`, and `AnswerFormatter` into the existing `AssistantViewModel` conversation flow. Preserve existing tenant-question handling.

```gherkin
Feature: Semantic Planner Integration

  Scenario: User asks "¿Hay pagos atrasados?" and receives a correct answer
    Given the AI assistant is enabled and LM Studio is running
    And the database has late payments
    When the user types "¿Hay pagos atrasados?" and sends
    Then a loading indicator appears
    And the application builds a QueryPlan via the planner
    And the plan is validated
    And the executor returns the correct late payment count
    And a Spanish answer is displayed in the chat
    And the loading indicator disappears

  Scenario: Existing "When does Erik Artigas move out?" still works
    Given a tenant "Erik Artigas" with a contract including an extension
    When the user asks "When does Erik Artigas move out?"
    Then the correct move-out date including the extension is returned in English
    And no semantic planner regression occurs

  Scenario: Follow-up "¿Y Namratha?" inherits previous intent
    Given the user previously asked a Spanish move-out question
    When the user asks "¿Y Namratha?"
    Then the application infers tenant_move_out_date intent from context
    And returns a Spanish move-out answer for Namratha

  Scenario: Unsupported question produces a localized fallback
    Given the user asks "¿Puedes reservar una habitación?"
    When the planner returns null or an invalid plan
    Then a Spanish fallback message is displayed
    And no incorrect data is shown
    And no write operation occurs

  Scenario: Send is disabled while processing
    Given the user sends a question
    When processing is in progress
    Then the Send button is disabled
    And a visual-only loading indicator is visible

  Scenario: Send re-enables after response or error
    Given processing has completed (successfully or with error)
    Then the Send button is re-enabled
    And the loading indicator is hidden

  Scenario: LM Studio is unavailable during a query
    Given LM Studio is offline
    When the user sends a question
    Then a localized connection error message is shown
    And the loading indicator is cleared
    And the app does not crash
```

**Acceptance criteria:**
- `AssistantViewModel` routes broader natural-language questions through the semantic planner.
- Existing direct-tenant-question flow is preserved.
- `AssistantContext` is updated after successful answers.
- `IsLoading` state is correctly managed in `try/finally`.
- Localized fallback messages use the correct language.
- Build passes. All prior tests continue to pass.

**Restriction:** Do not add write operations. Do not expose PII in formatted answers. Do not redesign the UI.

---

## Phase 9 — Add Privacy and Safety Tests

**Goal:** Add focused tests that verify privacy constraints, safety rules, and executor boundaries.

```gherkin
Feature: Privacy and Safety

  Scenario: Phone numbers are not included in formatted answers
    Given a tenant with a phone number
    When the executor builds a result and the formatter formats it
    Then the formatted answer does not contain the phone number

  Scenario: Email addresses are not included in formatted answers
    Given a tenant with an email address
    When the executor builds a result
    Then the formatted answer does not contain the email address

  Scenario: Private notes are not included in formatted answers
    Given a contract or tenant with notes
    When the executor builds a result
    Then the formatted answer does not contain the notes text

  Scenario: Full contract file paths are not exposed
    Given a contract with a FilePath value
    When the executor builds a result
    Then the formatted answer does not contain the file path

  Scenario: Unknown resource in plan is rejected before execution
    Given a QueryPlan with resource="users"
    When the validator processes the plan
    Then validation fails
    And the executor is never called

  Scenario: Active property is always enforced in results
    Given payments exist for properties 1 and 2
    And the active property is 1
    When the executor runs a payment query
    Then no results from property 2 are included

  Scenario: Result limit is enforced
    Given 100 tenant records exist for the active property
    And the plan has limit=20
    When the executor runs a list query
    Then at most 20 records are returned

  Scenario: No raw SQL is generated or executed
    Given any user question
    When the semantic planner and executor process the question
    Then no call to FromSqlRaw or ExecuteSqlRaw is made in any code path
```

**Acceptance criteria:**
- All privacy tests exist in `TenantManager.Tests`.
- All tests pass.
- No PII is exposed in any tested code path.
- Build passes.

**Restriction:** Do not add new features. Do not modify UI. Focus only on safety and privacy test coverage.

---

## Phase 10 — Add Functional Tests

**Goal:** Add end-to-end functional tests for the most important user-facing questions.

```gherkin
Feature: Functional Test Coverage

  Scenario: Late payment count is correct
    Given 2 late payments and 1 pending payment for the active property
    When the user asks "¿Hay pagos atrasados?"
    Then the answer reports 2 late payments

  Scenario: Active contract count is correct
    Given 4 active contracts and 1 expired contract for the active property
    When the user asks "¿Cuántos contratos están activos?"
    Then the answer reports 4 active contracts

  Scenario: Available rooms list is correct
    Given 3 rooms: 2 occupied, 1 available
    When the user asks "¿Qué habitaciones están libres?"
    Then the answer lists exactly 1 available room by name

  Scenario: Pending payment sum is correct
    Given 2 pending payments with amounts 450 and 350 for the current month
    When the user asks "¿Cuánto queda por cobrar este mes?"
    Then the answer reports 800

  Scenario: Move-out date with extension
    Given a tenant with a base contract end date of 2026-06-30 and an extension to 2026-08-31
    When the user asks "Cuando se va [TenantName]?"
    Then the answer reports 2026-08-31

  Scenario: English question returns English answer
    Given the user asks "Which rooms are currently available?"
    When the planner detects language="en"
    Then the formatted answer is in English

  Scenario: Spanish question returns Spanish answer
    Given the user asks "¿Hay pagos atrasados?"
    When the planner detects language="es"
    Then the formatted answer is in Spanish

  Scenario: Follow-up question inherits previous language and intent
    Given the user previously asked "¿Cuándo se va Erik?" in Spanish
    When the user asks "¿Y Namratha?"
    Then the answer for Namratha's move-out date is returned in Spanish

  Scenario: Invalid question returns localized fallback
    Given the user asks "¿Puedes enviarme un email?"
    When the plan is invalid or unsupported
    Then a Spanish clarification message is returned
    And no data is exposed
    And no write action occurs

  Scenario: Ambiguous question returns clarification
    Given two tenants both named "Erik"
    When the user asks about "Erik"
    Then the assistant asks which Erik is meant, in the correct language
```

**Acceptance criteria:**
- All functional test scenarios exist in `TenantManager.Tests`.
- All tests pass against in-memory SQLite.
- Build passes.
- No PII is committed to the test code.

**Restriction:** Do not add new features. Fix only what is needed to make the tests pass.

---

## Phase 11 — Final Validation

**Goal:** Run the full validation pipeline to confirm the feature is complete, clean, and ready.

```gherkin
Feature: Final Validation

  Scenario: Build passes with no errors
    Given all phases have been implemented
    When the build is run
    Then it completes with 0 errors and 0 warnings

  Scenario: All tests pass
    Given all functional and unit tests exist
    When dotnet test is run
    Then all tests pass with 0 failures

  Scenario: No untracked spec files
    When git status is run
    Then docs/specs/local-ai-semantic-query-planner.hard-spec.md is tracked
    And docs/specs/local-ai-semantic-query-planner.gherkin.md is tracked
    And docs/specs/local-ai-assistant.hard-spec.md is tracked
    And docs/specs/local-ai-assistant.gherkin.md is tracked

  Scenario: No committed database or secret files
    When git ls-files is run
    Then no .db, .sqlite, .db-shm, or .db-wal files are tracked
    And no bin/ or obj/ paths are tracked
    And no secrets or credentials are tracked

  Scenario: Documentation matches the implementation plan
    Given the hard-spec and Gherkin for the Semantic Query Planner
    When the implementation is reviewed
    Then all acceptance criteria in the hard-spec are met
    And all Gherkin phase acceptance criteria are met
```

**Acceptance criteria:**
- Build: 0 errors, 0 warnings.
- Tests: all pass.
- Git: clean working tree with all spec files tracked.
- No PII, secrets, database files, or generated folders committed.
- All specification and implementation acceptance criteria are satisfied.

**Restriction:** No new features after this phase. Only clean-up and documentation fixes if needed.
