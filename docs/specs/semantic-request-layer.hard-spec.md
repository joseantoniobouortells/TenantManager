# Hard Spec — Semantic Request Interpretation Layer (MVP)

## Version
0.1.0 — July 2026

## Purpose

Introduce a thin, typed semantic interpretation layer (`SemanticRequest`) that sits in front
of the existing `SemanticQueryPlan` pipeline. This layer enables the assistant to:

1. Understand **multiple requested outputs** within a single user question (e.g. "How much was
   collected last month? What month does it correspond to?") and include all of them in the
   formatted answer.
2. Detect **relative temporal expressions** (`last month`, `this year`, `mes pasado`, etc.)
   and resolve them deterministically before the LLM query plan is generated.
3. Answer **questions about the previous successful result** (e.g. "What month was that?")
   without issuing a new database query, using conversation context already stored in
   `AssistantContext`.

## Scope (MVP)

This spec covers only the minimum viable product. Future phases may extend `SemanticRequest`
with additional fields (e.g. cross-resource queries, multi-turn aggregation, negation).

## Architecture

### Pipeline — Data Query

```
User message
  → keyword-based PreviousResultQuery detector [new — deterministic, no LLM]
      → (if match) deterministic answer from AssistantContext.LastFormattedAnswer / LastYear / LastMonth
  → existing SemanticQueryPlan pipeline (LLM + validation + execution + formatting) [unchanged]
      → EnrichFormattedAnswer [new] — appends requested period/label fields to primary answer
      → store LastFormattedAnswer, LastExecutionResult in AssistantContext [new]
```

### Pipeline — Follow-up about the previous result

```
User message (e.g. "A qué mes corresponde?")
  → TryResolvePreviousResultByKeywords [new — deterministic heuristics]
      → returns localized period string from AssistantContext.LastYear / LastMonth
      → short-circuits: no LLM call, no DB query
```

## Contracts

### SemanticRequest (immutable record)

| Field | Type | Description |
|---|---|---|
| Language | string | ISO 639-1 code: `es` or `en` |
| Intent | SemanticRequestIntent | DataQuery, PreviousResultQuery, Unknown |
| Resource | string | Logical resource name (payments, expenses, …) |
| Operation | string | Logical operation (sum, count, list, …) |
| Filters | IReadOnlyList\<KeyValuePair\<string,string\>\> | Pre-resolved filter pairs |
| Projection | IReadOnlyList\<string\> | Requested projection fields |
| Period | SemanticPeriod | Resolved year/month (nulls when not applicable) |
| RequestedOutputs | IReadOnlyList\<RequestedOutput\> | All outputs the user explicitly requested |
| Presentation | ResponsePresentation | ValueOnly, MultiField, Narrative |
| Confidence | decimal | LLM confidence score 0.0–1.0 |

`SemanticRequest.Empty` acts as a null-object fallback (Intent=Unknown, Confidence=0).

`IsActionable` returns `true` when Intent ≠ Unknown AND Confidence ≥ 0.5.

### SemanticPeriod

| Field | Type |
|---|---|
| Year | int? |
| Month | int? |

`HasPeriod` returns `true` when either Year or Month is non-null.

`ToString()` returns `"YYYY-MM"`, `"YYYY"`, or `"month M"` depending on available fields.

### RequestedOutput

A strongly-typed projection item containing a logical field name and a human-readable label.

### SemanticRequestIntent

| Value | Meaning |
|---|---|
| DataQuery | Standard database query; feed into existing SemanticQueryPlan pipeline |
| PreviousResultQuery | User is asking about metadata of the previous result (no new DB query) |
| Unknown | LLM could not determine a valid intent |

### ResponsePresentation

| Value | Meaning |
|---|---|
| ValueOnly | Return the primary numeric/text result only |
| MultiField | Return all requested outputs listed together |
| Narrative | Return narrative prose |

MultiField is automatically selected when `RequestedOutputs.Count > 1`.

## New Classes

| Class | Location | Responsibility |
|---|---|---|
| `SemanticRequest` | TenantManager.Core | Immutable typed record |
| `SemanticRequestDto` | TenantManager.Core | JSON-deserialized DTO from LLM |
| `SemanticRequestBuilder` | TenantManager.Core | Converts DTO → SemanticRequest |
| `SemanticRequestResolver` | TenantManager.Core | Deterministic resolution (no DB, no LLM) |

## SemanticRequestResolver Behaviours

### TryResolvePreviousResultByKeywords(message, context, isSpanish)

Detects period meta-questions using deterministic keyword heuristics:
- Spanish keywords: `a qué mes`, `qué mes`, `de qué mes`, `qué periodo`, `mes corresponde`
- English keywords: `what month`, `which month`, `what period`, `which period`, `month correspond`

Returns null if:
- No keyword match
- Context has no year/month data
- Context has no previous query

Returns a localized string (e.g. "La consulta anterior correspondía a junio de 2026.") otherwise.

### TryResolvePreviousResult(request, context)

Handles `SemanticRequestIntent.PreviousResultQuery` requests explicitly:
- Returns null if intent ≠ PreviousResultQuery
- Returns a "no previous context" message if context is null/empty
- Checks `RequestedOutputs` and `Projection` for period fields; if present and context has year/month,
  returns the localized period string
- Falls back to `context.LastFormattedAnswer` if available
- Otherwise returns a "not enough context" message

### EnrichFormattedAnswer(primaryAnswer, request, context)

Post-processes the formatted answer to append additional requested outputs:
- Returns `primaryAnswer` unchanged if `RequestedOutputs.Count <= 1`
- For each extra RequestedOutput not already the primary field, appends a localized line
- Period fields are resolved from `context.LastMonth` / `context.LastYear`

## AssistantContext Extensions

Three new properties added to `AssistantContext`:
- `LastFormattedAnswer` — the last successful formatted answer text
- `LastSemanticRequest` — the `SemanticRequest` that produced the last answer
- `LastExecutionResult` — the raw execution result object

All three are cleared by `Reset()`.

## AiQueryService Modifications

### Fast path (deterministic, before any LLM call)

After `context.LastPropertyId = propertyId;` and before `onProgress?.Invoke(PreparingRequest)`:

```
if context has data → TryResolvePreviousResultByKeywords(userMessage, context, isSpanish)
  → if non-null: invoke Completed, return answer immediately
```

### After formatting

After `SemanticAnswerFormatter.Format(...)`:
1. Store `context.LastFormattedAnswer = formattedAnswer`
2. Store `context.LastExecutionResult = executionResult`
3. If `rawPlan.Projection` contains `"period"` and context has year: append period line to answer

## LLM Prompt Extension

The `BuildQueryPlanAsync` planner prompt gains one additional planning rule:
> "When the user requests multiple pieces of information in one question, include all requested
> fields in the projection list (e.g. `["paidAmount", "period"]`)."

## Constraints

- No new database tables or migrations required.
- `SemanticQueryPlan`, `SemanticQueryPlanValidator`, `SemanticQueryExecutor`, and
  `SemanticAnswerFormatter` must not be modified (except `AiQueryService` integration points).
- `TenantManager.Core` must not reference Avalonia.
- The fast path must NOT add a second LLM call — it is purely deterministic.
- All existing tests must continue to pass.

## Validation

```bash
dotnet build src/TenantManager.App/TenantManager.App.csproj && dotnet test
```

All pre-existing tests pass. New `SemanticRequestTests` suite added with coverage of:
- `SemanticRequest` contract (Empty, IsActionable, SemanticPeriod.ToString)
- `SemanticRequestBuilder` (DataQuery, PreviousResultQuery, MultiField, Unknown)
- `SemanticRequestResolver` (TryResolvePreviousResult ES/EN, keyword heuristics, EnrichFormattedAnswer)
