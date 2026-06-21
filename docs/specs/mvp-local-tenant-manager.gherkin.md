Feature: MVP Local Tenant Manager

  Background:
    Given the hard specification is defined in "docs/specs/mvp-local-tenant-manager.hard-spec.md"
    And the application is a desktop multiplatform app
    And the UI framework is Avalonia
    And the database is SQLite
    And contract files are stored as local file paths
    And no out-of-scope functionality must be added
    And agents must not compile or execute tests unless explicitly requested

  Scenario: Phase 1 - Create Avalonia project structure
    Given no application code exists yet
    When the initial project structure is created
    Then there is a .NET solution
    And there is an Avalonia desktop application project
    And the project contains folders for Domain, Data, Services, ViewModels and Views
    And no separate backend or HTTP API is created
    And no authentication is added
    And no cloud dependency is added
    And no domain entities are implemented yet

  Scenario: Phase 2 - Implement domain entities
    Given the Avalonia project structure exists
    When the domain model is implemented
    Then Room, Tenant, RentalContract and MonthlyPayment entities exist
    And PaymentStatus exists
    And contract files are represented by a FilePath string
    And Candidate does not exist
    And Property does not exist
    And entities do not depend on Avalonia UI code

  Scenario: Phase 3 - Configure SQLite persistence
    Given the domain entities exist
    When local persistence is implemented
    Then Entity Framework Core is configured with SQLite
    And AppDbContext exposes Rooms, Tenants, RentalContracts and MonthlyPayments
    And MonthlyPayment prevents duplicates by TenantId, Year and Month
    And the database is stored locally
    And no repository abstraction is added unless clearly justified

  Scenario: Phase 4 - Implement room management
    Given SQLite persistence exists
    When room management is implemented
    Then the user can list rooms
    And the user can create rooms
    And the user can edit rooms
    And the user can deactivate rooms
    And rooms have name, monthly rent, active status and optional notes

  Scenario: Phase 5 - Implement tenant management
    Given room management exists
    When tenant management is implemented
    Then the user can list tenants
    And the user can create tenants
    And the user can edit tenants
    And the user can deactivate tenants
    And the user can associate a tenant with a room
    And tenants have name, optional phone, optional email, move-in date, optional move-out date, deposit amount and optional notes

  Scenario: Phase 6 - Implement contract path management
    Given tenant management exists
    When contract management is implemented
    Then the user can associate one or more contract file paths with a tenant
    And the application shows whether each contract file path exists
    And the user can open an existing contract file with the system default application
    And the application does not copy the contract file
    And the application does not store contract file contents in SQLite

  Scenario: Phase 7 - Implement monthly payments
    Given tenant management exists
    When monthly payment management is implemented
    Then the user can create monthly payments for tenants
    And the user can edit monthly payments
    And each payment has year, month, expected amount, paid amount, status, optional paid date and optional notes
    And payment status can be pending, paid, partial, late or waived
    And duplicate payments for the same tenant, year and month are rejected

  Scenario: Phase 8 - Implement minimal dashboard
    Given rooms, tenants, contracts and payments exist
    When the minimal dashboard is implemented
    Then the user can see occupied rooms
    And the user can see active tenants by room
    And the user can see pending payments for the current month
    And no CRM candidate functionality is implemented
    And no multi-property functionality is implemented

  Scenario: Phase 9 - Add focused tests
    Given the MVP core functionality exists
    When focused tests are added
    Then there are tests for room creation
    And there are tests for tenant creation
    And there are tests for tenant-room association
    And there are tests for contract path existence validation
    And there are tests for monthly payment creation
    And there are tests for duplicate monthly payment prevention
    And there are tests for pending payment queries
    And tests avoid UI automation unless explicitly needed

  Scenario: Phase 10 - Final local validation
    Given all MVP phases have been implemented
    When final validation is performed
    Then the application can be built manually
    And the test suite can be executed manually
    And the user can create sample rooms, tenants, contracts and payments
    And data persists after restarting the application
    And no out-of-scope functionality has been added
