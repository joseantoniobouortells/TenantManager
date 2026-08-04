Feature: Variable Expense Percentage
  As a property owner
  I want to assign a specific percentage of variable expenses to each rental contract
  So that I can distribute the property expenses among tenants fairly and accurately

  Background:
    Given the property owner is logged in
    And a property exists with 3 rooms
    And the property has variable expenses enabled

  Scenario: Creating a new contract with default variable expense percentage
    Given the user is creating a new rental contract
    When the user selects "Variable" as the expense payment type
    Then the "Variable Expense Percentage" numeric field should be visible
    And the field should default to 33.33 (which is 100% / 3 rooms)

  Scenario: Customizing the variable expense percentage for a tenant
    Given the user is creating a new rental contract
    When the user selects "Variable" as the expense payment type
    And the user changes the "Variable Expense Percentage" to 40.0
    And the user saves the contract
    Then the contract should be saved with a VariableExpensePercentage of 40.0

  Scenario: Fixed expense contracts hide the variable percentage field
    Given the user is creating a new rental contract
    When the user selects "Fixed" as the expense payment type
    Then the "Variable Expense Percentage" numeric field should be hidden or disabled

  Scenario: Monthly payment calculation uses the assigned variable percentage
    Given a property has a total variable expense of 100.00 for the month of August
    And there is an active rental contract for that property with a VariableExpensePercentage of 40.0
    When the monthly payments are calculated for August
    Then the variable expense portion of the tenant's payment should be 40.00
