# Gherkin — Semantic Request Interpretation Layer (MVP)

## Overview

This document describes the Gherkin acceptance criteria for the SemanticRequest interpretation
layer. All logic lives in `TenantManager.Core` (no Avalonia dependencies). No database schema
changes are required.

---

## Phase 1 — SemanticRequest contract

**Goal:** Define the immutable `SemanticRequest` record and its supporting types.

```gherkin
Feature: SemanticRequest contract

  Scenario: SemanticRequest.Empty is the null-object fallback
    Given the SemanticRequest.Empty constant
    Then its Intent is Unknown
    And its Confidence is 0
    And IsActionable returns false

  Scenario: DataQuery request with high confidence is actionable
    Given a SemanticRequest with Intent=DataQuery and Confidence=0.95
    Then IsActionable returns true

  Scenario: DataQuery request with low confidence is not actionable
    Given a SemanticRequest with Intent=DataQuery and Confidence=0.3
    Then IsActionable returns false

  Scenario: Unknown intent is never actionable regardless of confidence
    Given a SemanticRequest with Intent=Unknown and Confidence=1.0
    Then IsActionable returns false

  Scenario: SemanticPeriod.ToString formats year+month
    Given a SemanticPeriod with Year=2026, Month=6
    Then ToString returns "2026-06"

  Scenario: SemanticPeriod.ToString formats year only
    Given a SemanticPeriod with Year=2026, Month=null
    Then ToString returns "2026"

  Scenario: SemanticPeriod.ToString formats month only
    Given a SemanticPeriod with Year=null, Month=6
    Then ToString returns "month 6"

  Scenario: SemanticPeriod.Empty has no period
    Given SemanticPeriod.Empty
    Then HasPeriod returns false
```

**Acceptance criteria:**
- `SemanticRequest`, `SemanticPeriod`, `RequestedOutput`, `SemanticRequestIntent`, `ResponsePresentation` exist in `TenantManager.Core`.
- `SemanticRequest` is an immutable `record`.
- No Avalonia dependency.
- Build passes.

---

## Phase 2 — SemanticRequestBuilder

**Goal:** Convert raw LLM DTO into typed `SemanticRequest`.

```gherkin
Feature: SemanticRequestBuilder

  Scenario: Build from data_query DTO
    Given a SemanticRequestDto with Intent="data_query", Resource="payments", Operation="sum",
          PeriodYear=2026, PeriodMonth=6, Confidence=0.95,
          and RequestedOutputs=[{field="paidAmount", label="Importe"}]
    When SemanticRequestBuilder.Build is called
    Then the resulting SemanticRequest has Intent=DataQuery
    And Language="es" (from DTO)
    And Period.Year=2026 and Period.Month=6
    And RequestedOutputs has 1 element with Field="paidAmount"
    And Presentation=ValueOnly

  Scenario: Build sets MultiField presentation when multiple outputs requested
    Given a SemanticRequestDto with 2 RequestedOutputs
    When SemanticRequestBuilder.Build is called
    Then Presentation=MultiField

  Scenario: Build from previous_result_query DTO
    Given a SemanticRequestDto with Intent="previous_result_query", Confidence=0.9
    When SemanticRequestBuilder.Build is called
    Then Intent=PreviousResultQuery

  Scenario: Build maps unrecognized intent string to Unknown
    Given a SemanticRequestDto with Intent="something_unexpected"
    When SemanticRequestBuilder.Build is called
    Then Intent=Unknown

  Scenario: Build tolerates null RequestedOutputs list
    Given a SemanticRequestDto with RequestedOutputs=null
    When SemanticRequestBuilder.Build is called
    Then the resulting SemanticRequest has RequestedOutputs as an empty collection
```

---

## Phase 3 — SemanticRequestResolver: PreviousResultQuery

**Goal:** Deterministic resolution of questions about the previous result.

```gherkin
Feature: SemanticRequestResolver — TryResolvePreviousResult

  Scenario: Returns null for non-PreviousResultQuery intent
    Given a SemanticRequest with Intent=DataQuery
    When TryResolvePreviousResult is called with any context
    Then the result is null

  Scenario: Returns "no context" message when context is null
    Given a SemanticRequest with Intent=PreviousResultQuery, Language="es"
    And context is null
    When TryResolvePreviousResult is called
    Then the result is a Spanish "no previous context" message

  Scenario: Returns period in Spanish when context has year and month
    Given a SemanticRequest with Intent=PreviousResultQuery, Language="es"
    And RequestedOutputs contains field="period"
    And context has LastYear=2026, LastMonth=6
    When TryResolvePreviousResult is called
    Then the result contains "junio" and "2026"

  Scenario: Returns period in English when context has year and month
    Given a SemanticRequest with Intent=PreviousResultQuery, Language="en"
    And RequestedOutputs contains field="period"
    And context has LastYear=2026, LastMonth=6
    When TryResolvePreviousResult is called
    Then the result contains "June" and "2026"

  Scenario: Falls back to LastFormattedAnswer when no period is available
    Given a SemanticRequest with Intent=PreviousResultQuery, Language="es"
    And no period fields in RequestedOutputs or Projection
    And context has LastFormattedAnswer="Se han ingresado 540,00 €." but no LastYear/LastMonth
    When TryResolvePreviousResult is called
    Then the result contains "540"

  Scenario: Returns "not enough context" when context has no useful data
    Given a SemanticRequest with Intent=PreviousResultQuery, Language="es"
    And context exists but has no LastYear, no LastMonth, no LastFormattedAnswer
    When TryResolvePreviousResult is called
    Then the result is a Spanish "not enough context" message
```

---

## Phase 4 — SemanticRequestResolver: Keyword heuristics

**Goal:** Deterministic keyword-based detection of period meta-questions, avoiding LLM round-trips.

```gherkin
Feature: SemanticRequestResolver — TryResolvePreviousResultByKeywords

  Scenario: Detects "a qué mes corresponde" in Spanish
    Given context with LastYear=2026, LastMonth=6, LastResolvedIntent="payments_sum"
    When TryResolvePreviousResultByKeywords is called with "a qué mes corresponde" and isSpanish=true
    Then the result contains "junio" and "2026"

  Scenario: Detects "¿A qué mes corresponde?" with punctuation
    Given context with LastYear=2026, LastMonth=6
    When TryResolvePreviousResultByKeywords is called with "¿A qué mes corresponde?" and isSpanish=true
    Then the result is non-null and contains "2026"

  Scenario: Detects "qué mes era" in Spanish
    Given context with LastYear=2026, LastMonth=6
    When TryResolvePreviousResultByKeywords is called with "qué mes era" and isSpanish=true
    Then the result is non-null

  Scenario: Detects "what month" in English
    Given context with LastYear=2026, LastMonth=6
    When TryResolvePreviousResultByKeywords is called with "what month was that" and isSpanish=false
    Then the result is non-null and contains "June"

  Scenario: Detects "which month" in English
    Given context with LastYear=2026, LastMonth=6
    When TryResolvePreviousResultByKeywords is called with "which month does it correspond to" and isSpanish=false
    Then the result is non-null

  Scenario: Returns null when message is a data query, not a period question
    Given context with LastYear=2026, LastMonth=6
    When TryResolvePreviousResultByKeywords is called with "cuánto se ha ingresado este mes" and isSpanish=true
    Then the result is null

  Scenario: Returns null when context has no year or month
    Given context with no LastYear and no LastMonth
    When TryResolvePreviousResultByKeywords is called with "a qué mes corresponde" and isSpanish=true
    Then the result is null

  Scenario: Returns null when context has no previous query (HasContext=false)
    Given a fresh AssistantContext with no data
    When TryResolvePreviousResultByKeywords is called with "a qué mes corresponde" and isSpanish=true
    Then the result is null
```

---

## Phase 5 — SemanticRequestResolver: EnrichFormattedAnswer

**Goal:** Append extra requested outputs to the primary answer when multiple outputs were requested.

```gherkin
Feature: SemanticRequestResolver — EnrichFormattedAnswer

  Scenario: Returns primary answer unchanged when only one output requested
    Given a SemanticRequest with 1 RequestedOutput (field="paidAmount")
    When EnrichFormattedAnswer is called with primaryAnswer="Se han ingresado 540,00 €."
    Then the result is "Se han ingresado 540,00 €." (unchanged)

  Scenario: Appends period label when "period" is a secondary output
    Given a SemanticRequest with RequestedOutputs=[{paidAmount, "Importe"}, {period, "Mes"}]
    And context with LastYear=2026, LastMonth=6
    When EnrichFormattedAnswer is called with primaryAnswer="Se han ingresado 540,00 €."
    Then the result contains "540"
    And the result contains "junio" (or "June" for English)
    And the result contains "2026"

  Scenario: Does not duplicate primary field in extras
    Given a SemanticRequest with Projection[0]="paidAmount" and RequestedOutputs=[{paidAmount}, {period}]
    When EnrichFormattedAnswer is called
    Then the period line is appended but paidAmount is not duplicated
```

---

## Phase 6 — AiQueryService integration: fast path

**Goal:** The service short-circuits for PreviousResultQuery intents before making any LLM call.

```gherkin
Feature: AiQueryService — fast path for period follow-ups

  Scenario: Follow-up period question resolved without LLM
    Given the previous query was a payments sum for year=2026, month=6
    And context has LastYear=2026, LastMonth=6, LastResolvedIntent="payments_sum"
    When the user asks "a qué mes corresponde"
    Then no LLM call is made
    And the response contains "junio" and "2026"

  Scenario: Non-period follow-up still goes through LLM
    Given context has LastYear=2026, LastMonth=6
    When the user asks "cuánto se ha ingresado el mes pasado"
    Then the standard LLM query plan pipeline is invoked

  Scenario: Fast path is skipped when context has no prior query
    Given a fresh AssistantContext with no data
    When the user asks "a qué mes corresponde"
    Then the fast path does not short-circuit
    And the standard pipeline is invoked (which will likely return an error)
```

---

## Phase 7 — AssistantContext extensions

**Goal:** Context stores last formatted answer and execution result.

```gherkin
Feature: AssistantContext — extended state for follow-ups

  Scenario: LastFormattedAnswer is stored after a successful query
    Given a successful payments sum query
    When the formatted answer is produced
    Then context.LastFormattedAnswer equals the formatted answer text

  Scenario: LastExecutionResult is stored after a successful query
    Given a successful payments sum query returning decimal 540.00
    When the query completes
    Then context.LastExecutionResult equals 540.00m

  Scenario: Reset clears LastFormattedAnswer, LastSemanticRequest, LastExecutionResult
    Given context with LastFormattedAnswer="some answer" and LastExecutionResult=100m
    When context.Reset() is called
    Then LastFormattedAnswer is null
    And LastExecutionResult is null
    And LastSemanticRequest is null
```

---

## Phase 8 — Multi-output projection support

**Goal:** When a query plan's projection includes "period", the formatted answer includes the period.

```gherkin
Feature: AiQueryService — multi-output period injection

  Scenario: Period appended when plan projection contains "period"
    Given a SemanticQueryPlan with Resource=Payments, Operation=Sum, Projection=["paidAmount", "period"]
    And the query executes successfully returning 540.00
    And context is updated with LastYear=2026, LastMonth=6
    When the answer is formatted
    Then the response contains the payment amount
    And the response contains the period (month and year)

  Scenario: Period not duplicated when already present in primary answer
    Given the formatted answer already contains the period string
    When the multi-output enrichment runs
    Then the period line is not appended again
```

---

## Constraints

- All new classes (`SemanticRequest`, `SemanticRequestDto`, `SemanticRequestBuilder`, `SemanticRequestResolver`) must live in `TenantManager.Core`.
- No Avalonia dependency in Core.
- No new database migrations.
- `SemanticQueryPlan`, `SemanticQueryPlanValidator`, `SemanticQueryExecutor`, `SemanticAnswerFormatter` must not be structurally modified.
- The fast path must not make any LLM call.
- All existing tests must continue to pass.
