# Hard Spec - Local AI Assistant

## Objective

Add a read-only, local-first AI assistant chat interface to the application. The assistant will answer user questions about application data (such as tenant move-out dates, available rooms, pending payments) using a deterministic query approach, without granting the LLM direct access to the SQLite database.

## Context

The user wants to naturally query the data stored in the application, such as asking "When does Erik Artigas move out?". The application is a local-first desktop app with strict privacy constraints. The AI integration must respect these constraints, operating entirely locally via an OpenAI-compatible endpoint (like LM Studio) and functioning as a strictly read-only assistant.

## User

Individual property owner managing rooms and tenants.

## Scope

- A new Assistant/AI tab or screen in the UI.
- Configuration settings to specify the local OpenAI-compatible endpoint URL and model name.
- Deterministic intent resolution and data retrieval by the application code.
- Structured, minimal context injection into the LLM prompt.
- Natural language response generation by the local LLM.
- Graceful error handling when the local AI server (e.g., LM Studio) is unavailable.
- The assistant is disabled by default.

## Out of scope

- Semantic Kernel or large AI frameworks.
- Cloud AI APIs (e.g., OpenAI, Anthropic, Gemini).
- Autonomous agentic actions or tool execution by the LLM.
- Direct database querying (SQL generation) by the LLM.
- Modifications (writes, updates, deletes) to rooms, tenants, contracts, or payments via chat.
- Transmitting sensitive PII (phone numbers, emails, full contract paths, private notes) unless explicitly required by a future intent.
- Authentication or user management.

## Functional requirements

- FR-001: The user can enable or disable the AI assistant in the application settings.
- FR-002: The user can configure the local endpoint URL (defaulting to `http://localhost:1234/v1/chat/completions`) and the model name (suggesting `qwen3.5-4b`).
- FR-003: The user can access a new "Assistant" chat interface.
- FR-004: The user can send natural language questions to the assistant.
- FR-005: The application deterministically resolves the user's intent based on the input.
- FR-006: The application queries the local SQLite database to gather the required context for the resolved intent.
- FR-007: The application sends the minimal required context and the user's question to the local LLM.
- FR-008: The LLM generates a natural language response based *only* on the provided context.
- FR-009: If the application cannot resolve the intent or find the data, the assistant responds that it does not have enough information.
- FR-010: The application displays a clear error message if the local AI server is offline or unreachable.

## Technical requirements

- TR-001: Use `HttpClient` for API communication with the local LLM. Do not use the OpenAI SDK or Semantic Kernel unless strongly justified.
- TR-002: Communication must adhere to the standard OpenAI chat completions API format to ensure compatibility with tools like LM Studio.
- TR-003: The database schema should not be modified unless strictly necessary for storing AI configuration preferences.
- TR-004: Context building must be deterministic and executed by the C# application logic.

## Privacy and safety requirements

- PSR-001: The assistant must be completely local-first. No data leaves the local network.
- PSR-002: The assistant is strictly read-only.
- PSR-003: Phone numbers, emails, private notes, and full contract file paths must be redacted or omitted from the LLM context.
- PSR-004: No real tenant data or database files may be committed to the repository.

## Data access rules

- DAR-001: The LLM is strictly prohibited from executing arbitrary SQL.
- DAR-002: The LLM is strictly prohibited from directly accessing the SQLite database.
- DAR-003: The application controls all database queries using Entity Framework Core and passes only the results (as text context) to the LLM.

## Local AI configuration

- Feature Toggle: AI Assistant enabled/disabled (default: disabled).
- Endpoint URL: Configurable, default `http://localhost:1234/v1/chat/completions`.
- Model Name: Configurable, suggested `qwen3.5-4b`.

## Supported intents for the first version

- Tenant move-out date by name.
- Tenant current room by name.
- Active tenants summary.
- Available rooms.
- Pending or late payments for the current month.
- Missing or broken contract file paths (as aggregate/actionable information without exposing full paths).
- Dashboard summary.

## Acceptance criteria

- AC-001: The AI assistant can be enabled and configured in settings.
- AC-002: When disabled, the AI chat interface is hidden or indicates it is turned off.
- AC-003: The user can ask "When does [Tenant Name] move out?" and receive a correct natural language answer.
- AC-004: The assistant refuses to answer or states ignorance if the question is outside the supported intents or context.
- AC-005: The application does not crash if the local LLM server is down; it shows a friendly error.
- AC-006: Inspecting the network traffic or application logs confirms that only specific, minimal context is sent to the LLM, excluding raw SQL and sensitive PII.

## Expected tests

- Test AI configuration parsing and defaults.
- Test intent resolution logic for supported questions.
- Test context builder for correct data retrieval and PII redaction.
- Test graceful failure when the `HttpClient` request to the local LLM times out or fails.
- Test privacy boundaries (ensuring emails/phones are not in the generated context string).

## Risks

- Intent resolution might be brittle if implemented with simple regex or keywords; users might ask questions in unexpected ways.
- The local LLM might hallucinate despite being provided context.
- LM Studio or the selected local model might be too slow for a good UX on older hardware.

## Decisions made

- The assistant will be read-only for the first version.
- The application will act as a strict mediator between the user, the database, and the LLM.
- No heavy AI frameworks (Semantic Kernel) will be used to keep the architecture simple and maintainable.
- `HttpClient` will be used to communicate with the OpenAI-compatible endpoint.

## Open questions

- How sophisticated should the intent resolution be? (e.g., regex matching vs. a cheap local embedding model vs. a first-pass classification prompt to the LLM).
- Should conversation history be persisted in SQLite, or just kept in memory for the current session?
