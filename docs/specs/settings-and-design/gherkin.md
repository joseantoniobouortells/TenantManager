Feature: Settings and Design Improvements
  As a user of Tenant Manager
  I want a clear, high-contrast user interface and a settings screen
  So that I can comfortably read data on hover and customize the app to dark or light mode

  Scenario: High contrast is maintained when hovering over primary buttons
    Given a primary button with a colored background
    When the user hovers over the button
    Then the button background changes to the hover color
    And the button text remains white or clearly legible against the background

  Scenario: High contrast is maintained when hovering over action buttons
    Given an action button with a light background
    When the user hovers over the button
    Then the button background darkens slightly
    And the button text explicitly remains a high-contrast dark color

  Scenario: User accesses the Settings screen
    Given the user is on the main application window
    When the user clicks the "Settings" tab in the navigation menu
    Then the Settings view is displayed
    And the view contains an "Appearance" section
    And the view contains an "Information" section showing the database path

  Scenario: User changes the application theme to Dark mode
    Given the user is on the Settings view
    And the application is currently in Light mode
    When the user selects "Dark" from the theme options
    Then the application immediately applies the Dark theme variant
    And the background, text, and cards update to dark mode colors without requiring a restart

  Scenario: User changes the application theme to System Default
    Given the user is on the Settings view
    When the user selects "System Default" from the theme options
    Then the application aligns its theme with the host operating system's theme
