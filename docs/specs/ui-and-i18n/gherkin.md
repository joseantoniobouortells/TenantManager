Feature: UI Refinements and Multi-language Support
  As a user of Tenant Manager
  I want form fields to be clearly labeled and formatted, and I want to use the app in my preferred language
  So that data entry is intuitive and the interface is accessible

  Scenario: Form fields have persistent labels
    Given the user is creating or editing a record (e.g., Room)
    When the user views the form
    Then there is a descriptive label above every input field
    And the label remains visible even after the user enters data into the field

  Scenario: Integer fields do not display decimal places
    Given the user is logging a Payment
    When the user looks at the "Year" and "Month" fields
    Then the values are displayed as whole numbers (e.g., 2024, 6)
    And no decimal places (e.g., 2024.00) are shown

  Scenario: Changing the application language
    Given the application is running in English
    And the user is on the Settings view
    When the user selects "Español" from the language options
    Then the application language changes to Spanish immediately
    And all navigation menus, form labels, and buttons are translated to Spanish without requiring a restart
    And the change persists or remains active for the current session

  Scenario: Translating the Dashboard
    Given the application is running in Spanish
    When the user navigates to the Dashboard
    Then the "Total Rooms" card displays "Total de Habitaciones"
    And the "Expected Income" displays "Ingresos Esperados"
