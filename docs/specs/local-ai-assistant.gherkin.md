# Gherkin - Local AI Assistant

## Phase 1: Add local AI settings model and defaults

Feature: AI Configuration
  As a user
  I want to configure the local AI assistant settings
  So that I can control whether it is enabled and where it connects

  Scenario: Default AI settings
    Given the application is installed for the first time
    When I view the AI settings
    Then the AI assistant should be disabled by default
    And the default endpoint should be "http://localhost:1234/v1/chat/completions"
    And the default model name should be empty or a suggested placeholder like "qwen3.5-4b"

  Scenario: Toggling the AI assistant
    Given the AI assistant is disabled
    When I enable the AI assistant in settings
    Then the application should persist this configuration

  # Restriction: Do not implement the chat UI or API client in this phase. Focus only on the settings model, persistence, and SettingsView UI updates.

## Phase 2: Add OpenAI-compatible local LLM client

Feature: Local LLM Client
  As a developer
  I want a simple HTTP client to communicate with LM Studio
  So that the application can send prompts and receive completions

  Scenario: Sending a chat completion request
    Given the AI assistant is enabled
    And LM Studio is running on the configured endpoint
    When the application sends a system prompt and a user message via HttpClient
    Then the application should parse the OpenAI-compatible JSON response
    And return the assistant's message text

  Scenario: Handling unavailable local server
    Given the AI assistant is enabled
    But the local LLM server is offline
    When the application attempts to send a request
    Then it should catch the connection exception
    And return a graceful failure message

  # Restriction: Do not use Semantic Kernel or the official OpenAI SDK. Use a plain HttpClient and JSON serialization. Do not integrate with the UI yet.

## Phase 3: Add safe prompt/context builder

Feature: Safe Context Builder
  As a user
  I want my private data protected
  So that sensitive information is not unnecessarily sent to the AI

  Scenario: Redacting sensitive PII
    Given a tenant exists with a phone number and email
    When the context builder generates the data summary for this tenant
    Then the resulting text must NOT contain the phone number
    And the resulting text must NOT contain the email

  Scenario: Formatting deterministic context
    Given the application has retrieved data for a specific intent
    When the context builder constructs the system prompt
    Then it should clearly instruct the LLM to answer ONLY based on the provided context
    And instruct the LLM to state it does not know if the answer is not in the context

  # Restriction: Do not query the database in this phase. Create the formatting and redaction logic using mock data objects.

## Phase 4: Add deterministic data query service for supported intents

Feature: Deterministic Data Querying
  As a developer
  I want a service that fetches exact data based on recognized intents
  So that the LLM has accurate, sandboxed information to answer questions

  Scenario: Resolving move-out date intent
    Given a user asks "When does Erik Artigas move out?"
    When the intent resolver processes the question
    Then it should identify the intent as "TenantMoveOutDate"
    And extract the tenant name "Erik Artigas"
    And query the database for this specific tenant's contracts
    And return a structured data object with the move-out date

  # Restriction: Do not connect this to the LLM yet. Focus purely on the C# logic for matching questions to database queries using EF Core.

## Phase 5: Add assistant ViewModel and chat UI

Feature: Assistant Chat UI
  As a user
  I want a dedicated screen to interact with the AI
  So that I can ask questions about my properties

  Scenario: Chat interface visibility
    Given the AI assistant is disabled
    When I open the application
    Then the Assistant tab should be hidden or show a "Disabled" message

  Scenario: Sending a message
    Given the AI assistant is enabled
    When I navigate to the Assistant tab
    And I type a message and press send
    Then my message should appear in the chat history
    And a loading indicator should appear while waiting for the response

  # Restriction: Mock the actual LLM response in this phase. Focus on the Avalonia UI, ViewModel binding, and chat history display.

## Phase 6: Add first supported intent: tenant move-out date

Feature: Tenant Move-out Intent Integration
  As a user
  I want to ask about tenant move-out dates
  So that I can plan for vacancies

  Scenario: Asking for a move-out date
    Given the AI assistant is fully integrated
    And tenant "Erik Artigas" has a contract ending on "2026-08-31"
    When I ask "When does Erik Artigas move out?"
    Then the app should query the database for Erik's contract
    And build a safe context string
    And send it to the local LLM
    And display the LLM's natural language response (e.g., "Erik Artigas will move out on August 31, 2026.")

  # Restriction: Only implement the move-out date intent. Ensure all components (UI, Query, Context, Client) work together for this single use case.

## Phase 7: Add additional read-only intents for dashboard/rooms/payments/contracts

Feature: Expanded AI Intents
  As a user
  I want to ask various questions about my properties
  So that I get a quick overview of my business

  Scenario Outline: Querying different application areas
    Given the AI assistant is enabled
    When I ask about <topic>
    Then the app should retrieve the relevant data deterministically
    And the LLM should summarize it accurately

    Examples:
      | topic |
      | available rooms |
      | active tenants summary |
      | pending payments |
      | missing contract files |
      | dashboard summary |

  # Restriction: Maintain the strict read-only and no-SQL-generation rules for all new intents.

## Phase 8: Add tests for privacy, prompt redaction and intent/data query behavior

Feature: AI System Validation
  As a developer
  I want automated tests for the AI integration
  So that privacy and correctness are guaranteed

  Scenario: Testing PII redaction
    Given a test suite for the ContextBuilder
    When the tests run
    Then they should assert that emails and phone numbers are never included in the output strings

  Scenario: Testing offline fallback
    Given a test suite for the LLM Client
    When the client attempts to connect to an invalid port
    Then the test should assert that a graceful fallback message is returned instead of crashing

  # Restriction: Use strictly in-memory SQLite and mocked HttpMessageHandlers. Do not require LM Studio to be running during unit tests.

## Phase 9: Final validation

Feature: End-to-End AI Validation
  As a user
  I want the assistant to be fully polished
  So that it provides a seamless experience

  Scenario: Attempting unsupported actions
    Given the AI assistant is enabled
    When I ask "Can you delete the room Kitchen?"
    Then the application should either fail to resolve the intent
    Or provide context that it cannot perform actions
    And the LLM should respond that it is read-only

  # Restriction: Ensure zero data leaks, verify no database files are tracked by git, and confirm performance is acceptable.
