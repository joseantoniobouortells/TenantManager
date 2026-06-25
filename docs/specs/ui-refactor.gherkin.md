Feature: UI and Structure Refactoring
  As a developer and maintainer of the Tenant Manager application
  I want to refactor the UI code and fix design issues
  So that the app is maintainable, readable, and properly structured

  Scenario: Phase 1 - Centralize colors and improve contrast
    Given the application uses hardcoded colors in views and low-contrast text (e.g., "Gray")
    When the developer moves hardcoded colors to "App.axaml" as Application Resources
    And replaces "Gray" foregrounds with a darker, readable alternative
    And defines a proper background and hover style for "Button.Action" in "App.axaml"
    And updates the UI views to reference colors using "DynamicResource"
    Then the text should be clearly readable against light backgrounds
    And the application should compile without errors

  Scenario: Phase 2 - Extract Dashboard to a separate UserControl
    Given the developer creates a new Avalonia UserControl named "DashboardView" in the "Views" folder
    When the contents of the Dashboard TabItem are moved from "MainWindow.axaml" to "DashboardView.axaml"
    And the correct DataContext/Binding structure is preserved
    Then the Dashboard tab in the main window should display the "DashboardView" correctly
    And the application should compile without errors

  Scenario: Phase 3 - Extract remaining tabs to separate UserControls
    Given the developer creates "RoomsView", "TenantsView", "ContractsView", and "PaymentsView" UserControls
    When the respective contents of those TabItems are moved from "MainWindow.axaml" to the new views
    Then the main window should use these UserControls inside its TabControl
    And the overall "MainWindow.axaml" size should be drastically reduced
    And the application should compile and run without errors, displaying all tabs correctly
