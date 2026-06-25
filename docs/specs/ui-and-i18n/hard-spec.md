# Hard Specification: UI Refinements & Internationalization (i18n)

## 1. Problem Analysis: UI Refinements
### Missing Form Labels
**Observation:** Currently, forms across views (Rooms, Tenants, Contracts, Payments) rely exclusively on `PlaceholderText` in `TextBox`, `ComboBox`, and `NumericUpDown`. When a user fills in a field, the placeholder disappears, leaving the user with no context of what the data represents.
**Solution:**
- Add explicit descriptive labels (`TextBlock`) above every input field inside forms.
- Use a consistent style for these labels: `FontSize="12"`, `Foreground="{DynamicResource TextSecondaryBrush}"`, and a small bottom margin (`Margin="0 0 0 4"`).

### Decimal Format on Integer Fields
**Observation:** The `Year` and `Month` fields in the Payments view are defined as `int` in the domain and view model, but the `NumericUpDown` control defaults to showing decimal places (e.g., `2024.00`).
**Solution:**
- Set `FormatString="0"` on all `NumericUpDown` controls that represent integers (Year, Month).
- For currency or decimal fields (Rent, Deposit, Amount), consider using `FormatString="C"` or leaving the default decimal representation.

## 2. Multi-language (i18n) Support
**Requirement:** Support English (base) and Spanish (optional), switchable at runtime from the Settings screen.
**Strategy (DynamicResource Approach):**
To avoid pulling in heavy third-party localization frameworks while ensuring instant UI updates without a restart:
1. Create a dictionary folder `Assets/i18n/`.
2. Define Resource Dictionaries for languages: `en.axaml` and `es.axaml`.
3. Map every hardcoded string in the UI to a `<system:String x:Key="SomeKey">Translated Text</system:String>` inside these files.
4. Replace hardcoded `Text="Dashboard"` with `Text="{DynamicResource DashboardTitle}"`.
5. In `SettingsViewModel`, add language selection (RadioButtons or ComboBox for "English" and "Español").
6. When the language changes, dynamically remove the old language dictionary from `App.Current.Resources.MergedDictionaries` and inject the new one. Avalonia's `DynamicResource` bindings will instantly update all text across the app.

## 3. Execution Plan
1. Create the `en.axaml` and `es.axaml` files with all necessary string keys.
2. Update `App.axaml` to include the default language dictionary.
3. Update `SettingsViewModel` and `SettingsView` to include the Language selector.
4. Go through all Views (`DashboardView`, `RoomsView`, `TenantsView`, `ContractsView`, `PaymentsView`) and:
   - Add labels (`TextBlock`) above inputs.
   - Fix `FormatString="0"` on integer `NumericUpDown` controls.
   - Replace hardcoded text with `{DynamicResource KeyName}`.
