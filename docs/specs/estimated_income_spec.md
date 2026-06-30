# Specification: Estimated Income & Payment Day Tracking

## 1. Business Requirements (Hard-Spec)

### 1.1 Data Model Changes
- The `RentalContract` entity MUST include a new property `int PaymentDay` (Allowed values: 1 to 31).
- The `RentalContractExtension` entity SHOULD inherit or explicitly use the contract's payment day logic. For simplicity, the `PaymentDay` on the base contract dictates the expected payment date for any active period.
- A new EF Core migration MUST be generated to add `PaymentDay` to the database schema. The default value for existing contracts MUST be `1`.

### 1.2 User Interface (Contracts)
- The "Contracts" view (`ContractsView.axaml`) MUST include a numeric input (or dropdown) for `PaymentDay` when creating or editing a contract.
- The default value for a new contract MUST be `1` to `5` (e.g., standard payment window). We will set the default to `1`.

### 1.3 Dashboard Estimation Logic
- The Dashboard MUST calculate the "Ingresos Previstos" (Estimated Income) for the **next calendar month**.
- **Calculation Rule:** For each property, iterate through all contracts that will be active during the next month.
- **Prorating Logic:** If a contract starts after the 1st of the next month, or ends before the last day of the next month, the expected revenue MUST be prorated based on the number of days the contract is active during that month.
  - *Formula:* `(MonthlyRent + FixedExpenseAmount) * (ActiveDaysInTargetMonth / TotalDaysInTargetMonth)`
- The calculated total MUST be displayed in a new financial card on the `DashboardView.axaml`.

---

## 2. Acceptance Criteria (Gherkin Scenarios)

```gherkin
Feature: Contract Payment Day and Next Month Estimated Income
  As a property owner
  I want to specify the payment day in a rental contract
  So that the system can accurately estimate my income for the next month on the dashboard.

  Background:
    Given a property exists
    And the current date is "2026-06-15"
    And the next month is "July 2026" (31 days)

  Scenario: Assigning a Payment Day to a new contract
    When I create a new contract for "Tenant A"
    And I set the Monthly Rent to 500
    And I set the Payment Day to 5
    And I save the contract
    Then the contract should be saved successfully
    And the Payment Day should be stored as 5 in the database.

  Scenario: Estimating income for a full month
    Given a contract exists with a Monthly Rent of 600 and 0 fixed expenses
    And the contract starts on "2026-01-01" and has no end date
    When I view the dashboard
    Then the "Ingresos Previstos" for the next month should include 600 from this contract.

  Scenario: Estimating prorated income for a contract ending mid-month
    Given a contract exists with a Monthly Rent of 620
    And the contract ends on "2026-07-15"
    When I view the dashboard
    Then the "Ingresos Previstos" for July 2026 should calculate the rent for 15 days
    And the prorated amount added to the estimation should be exactly 300.

  Scenario: Excluding expired contracts from estimation
    Given a contract exists with a Monthly Rent of 800
    And the contract ended on "2026-05-31"
    When I view the dashboard
    Then the "Ingresos Previstos" for July 2026 should not include this contract's rent.
```
