Feature: Contracts View Simplification and Status Filtering

  Background:
    Given the desktop application is running with Avalonia and SQLite
    And the contracts view hard specification is defined in "docs/specs/contracts-view-simplification.hard-spec.md"

  Scenario: Default view displays only active contracts
    Given property "Vivienda Centro" has 2 active contracts and 3 expired contracts
    When the user navigates to the Contracts tab
    Then exactly 2 contracts are visible in the table
    And both visible contracts have the active green status indicator
    And the status filter displays "Active"

  Scenario: Switching status filter to expired contracts
    Given property "Vivienda Centro" has 2 active contracts and 3 expired contracts
    When the user selects the "Expired" option in the status filter ComboBox
    Then exactly 3 contracts are visible in the table
    And all 3 visible contracts have the expired status indicator

  Scenario: Switching status filter to all contracts
    Given property "Vivienda Centro" has 2 active contracts and 3 expired contracts
    When the user selects the "All" option in the status filter ComboBox
    Then all 5 contracts are visible in the table

  Scenario: Text search combined with status filter
    Given the user has selected "Active" contracts filter
    And active contracts exist for "Erik Artigas" and "Maria Lopez"
    And an expired contract exists for "Erik Pradas"
    When the user types "Erik" into the search box
    Then only the active contract for "Erik Artigas" is displayed
    And the expired contract for "Erik Pradas" remains hidden

  Scenario: Text search in expired contracts
    Given the user has selected "Expired" contracts filter
    And an expired contract exists for "Erik Pradas"
    When the user types "Erik" into the search box
    Then the expired contract for "Erik Pradas" is displayed

  Scenario: Table displays monthly rent formatted as currency
    Given a contract has a monthly rent of 350.00
    When the contract is displayed in the table
    Then the rent column displays "350,00 €" (or localized currency format)

  Scenario: Direct PDF opening from row button
    Given a contract has an attached document file
    When the contract row is rendered in the table
    Then the PDF document icon button is enabled
    And clicking the PDF icon opens the document without entering edit mode

  Scenario: PDF button disabled when no file exists
    Given a contract has no attached document and no local file path
    When the contract row is rendered in the table
    Then the PDF document icon button is disabled
