# Specification: Tenant and Contract Domain Refactor

## 1. Overview
Currently, the application models the rental deposit (`DepositAmount`) and the assigned room (`RoomId`) as properties of the `Tenant` entity. To properly reflect the real-world domain where a single tenant can have multiple consecutive contracts with varying rooms and deposits over time, these properties must be moved to the `Contract` entity.

## 2. Hard Specifications

### 2.1. Domain Model Changes
- **`Tenant.cs`:** 
  - Remove `public int? RoomId { get; set; }`
  - Remove `public decimal DepositAmount { get; set; }`
- **`Contract.cs`:**
  - Add `public int RoomId { get; set; }` (A contract must be tied to a specific room).
  - Add `public decimal DepositAmount { get; set; }` (The deposit agreed upon for this specific contract).

### 2.2. Database & Migrations
- Generate a new Entity Framework Core migration (e.g., `MoveRoomAndDepositToContract`).
- Data Loss Prevention: If existing data needs to be preserved, the migration should contain logic to migrate existing `Tenant.RoomId` and `Tenant.DepositAmount` to their corresponding active `Contract`. Since we are in early development, a standard column drop/add may suffice, but relationships must be updated.

### 2.3. UI and ViewModels Updates
- **`TenantsView.axaml` & `TenantListViewModel.cs`:**
  - Remove UI inputs for assigning a room and entering a deposit amount when creating/editing a tenant.
  - The tenant list table should either calculate the current deposit/room via a join on the active contract, or these columns should be removed from the master tenant list.
- **`ContractsView.axaml` & `ContractListViewModel.cs` (or equivalent):**
  - Add UI inputs to select a `Room` and specify the `DepositAmount` when creating or editing a contract.

### 2.4. Dashboard and Payments Logic
- Any existing queries in `DashboardViewModel.cs` or `MonthlyPaymentListViewModel.cs` that filter tenants by `RoomId` directly (`t.RoomId == roomId`) must be refactored to filter based on the active `Contract`'s `RoomId`.

---

## 3. BDD Scenarios (Gherkin)

```gherkin
Feature: Contract-Based Room and Deposit Management
  As a property owner
  I want room assignments and deposit amounts to be tied to rental contracts rather than the tenant's profile
  So that a tenant can have a correct historical record of different rooms and deposits over time.

  Scenario: Creating a new tenant profile requires only personal data
    Given I am on the Tenants management view
    When I click "New Tenant"
    Then the form should only ask for "Full Name", "Phone", "Email", and "Notes"
    And the form should not ask for a "Room" or "Deposit Amount"
    When I save the tenant
    Then the tenant profile is successfully created in the database.

  Scenario: Creating a new rental contract includes room and deposit information
    Given I have an active tenant "John Doe"
    And I have an active room "Room A"
    And I am creating a new Contract for "John Doe"
    When I fill in the contract dates
    And I select "Room A" as the assigned room
    And I enter "300.00" as the deposit amount
    And I save the contract
    Then the contract is saved successfully
    And "John Doe" is officially occupying "Room A" with a deposit of 300.00 for the duration of the contract.

  Scenario: A tenant signs a new contract for a different room
    Given "John Doe" has a past expired contract for "Room A" with a deposit of "300.00"
    When I create a new contract for "John Doe"
    And I select "Room B"
    And I enter a new deposit amount of "350.00"
    And I save the contract
    Then "John Doe" is now occupying "Room B"
    And the system correctly reflects the current deposit of 350.00
    And the historical data for the old contract on "Room A" is preserved.

  Scenario: Calculating occupied rooms relies on active contracts
    Given "Room A" has an active contract ending next year
    And "Room B" has no active contracts
    When I view the dashboard occupancy
    Then "Room A" is marked as occupied
    And "Room B" is marked as available.
```
