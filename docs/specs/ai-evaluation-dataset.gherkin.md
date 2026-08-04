Feature: AI Evaluation Dataset

  Scenario: Evaluate deterministic expectations against LLM outputs
    Given a defined AI evaluation scenario with ID "payments-last-complete-month-es"
    And a fixed reference date of "2026-07-16"
    And the deterministic fixture dataset is loaded into the in-memory database
    When the evaluation runner submits the text "¿Cuánto se ingresó el mes pasado?" to the LLM
    Then the generated SemanticRequest must match intent "data_query"
    And the generated SemanticRequest must match resource "payments"
    And the generated SemanticRequest must resolve year 2026 and month 6
    And the query execution must be "required"
    And the resulting formatted answer must contain "810"

  Scenario: Evaluate previous-result context preservation
    Given a defined AI evaluation scenario with ID "payments-previous-period-reference-es"
    And a prior successful query resolved to "payments" for June 2026
    When the evaluation runner submits the text "¿A qué mes corresponden esos ingresos?"
    Then the generated SemanticRequest must match intent "previous_result_query"
    And the query execution must be "forbidden"
    And the resulting formatted answer must contain "junio"
    And the resulting formatted answer must contain "2026"
