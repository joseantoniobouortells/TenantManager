# AI Assistant Evaluation Dataset

## Purpose
This specification defines a versioned evaluation dataset contract to measure the deterministic behavior and language understanding capabilities of the AI assistant. This dataset acts as the input for a future, offline evaluation runner.

## Live Models vs. Deterministic Tests
- **Deterministic Tests**: Evaluate fixed, predictable, and isolated behaviors of the `SemanticQueryPlan` execution, resolving specific intents. They are run against the real SQLite memory database with predefined unit test scenarios.
- **Live-Model Evaluations**: Rely on an actual LLM (like LM Studio or OpenAI models) to parse natural language, infer intents, extract relative periods, and output the JSON expected by the `SemanticRequest` layer. Because LLM outputs are non-deterministic, these scenarios define structural expectations (`answerContains`, `intent`, `resolvedYear`) rather than requiring a bit-for-bit string match.

## Scenario Structure
The dataset relies on a simple, extensible JSON Schema (`evaluation/schemas/assistant-evaluation-scenario.schema.json`).
Each scenario defines:
- A unique `id` and a target `language` (`en` or `es`).
- A `referenceDate` to establish a fixed 'today' for relative time expressions (e.g., "last month").
- A list of conversational `messages`, representing a multi-turn interaction.
- Deterministic expectations (`expected`) for each message, checking fields like:
  - `intent`, `resource`, `operation`, `projection`, `requestedOutputs`
  - resolved dates (`resolvedYear`, `resolvedMonth`)
  - `queryExecution`: specifies whether a database query is `required`, `forbidden`, or `optional`.
  - Content validations (`answerContains`, `answerNotContains`).

## Fixture Data Requirements
The evaluation scenarios depend on a deterministic synthetic dataset located in `evaluation/data/deterministic-fixture.json`.
- **Requirements**:
  - The dataset represents exactly **one property**, with **multiple rooms and tenants**.
  - Includes **historical and current rental contracts**.
  - Incorporates **payments and expenses** across multiple months and years.
  - Generates clear financial scenarios for **income, expenses, and profit** over several months.
- **Constraints**:
  - **No Real Data**: Real PII, real addresses, or copies of the user's real database are strictly prohibited.
  - **Easy validation**: Amounts and dates are selected so that human validation of the expected aggregates is trivial (e.g., rounded amounts like 300, 350).
  - **No binary SQLite files**: Only JSON definitions are provided.

## Bilingual Support
The dataset ensures equivalent coverage in both Spanish (`es`) and English (`en`). 
Exact duplicate translations for every single edge case are not strictly required unless they provide distinct evaluation value, but both languages must cover:
- Core tenant lookup queries.
- Relative period queries.
- Previous-result references.
- Financial aggregates.
- Relevant localized expected answer fragments.

## Maintenance and Regression
**Workflow for dataset updates:**
1. A real failure or hallucination is discovered in the live app.
2. The user reproduces it.
3. The expected semantic behavior (intent, resolved dates, required query) is defined.
4. If required, the synthetic fixture data is augmented to support the case.
5. An evaluation scenario is added to the relevant `.json` file inside `evaluation/scenarios/`.
6. The scenario is manually reviewed by a developer.
7. The scenario is run later through the evaluation runner.

**Critical Rule**: A model-generated result must **never** automatically become an approved expected result. All expectations must be reviewed and asserted by a human developer.
