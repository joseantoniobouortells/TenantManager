# Hard Specification: Settings & Design Contrast Fixes

## 1. Problem Analysis: Contrast Issues on Hover
**Context:** The application currently exhibits contrast issues when the user hovers over interactive components (such as `Button`, `TabItem`, and `ListBoxItem` rows) across various screens.
**Root Cause:** Avalonia's `FluentTheme` provides default behavior for the `:pointerover` pseudo-class. These defaults often target the `ContentPresenter#PART_ContentPresenter` to change both `Background` and text `Foreground`. When we apply custom background brushes for hover states (e.g., `PrimaryHoverBrush`, `ActionHoverBrush`) without explicitly overriding the `TextBlock.Foreground` in the template, the default Fluent Theme foreground takes over. This results in poor contrast (e.g., light gray text on a light gray hover background or dark text on a dark primary button).
**Solution:**
1. Explicitly define `TextBlock.Foreground` (or `Foreground`) within the `:pointerover /template/ ContentPresenter#PART_ContentPresenter` selectors for all interactive components.
2. Target `Button.Primary`, `Button.Action`, `TabItem`, and `ListBoxItem` within tables.
3. Use high-contrast dynamic resources (e.g., `TextPrimaryBrush`, `White`) to guarantee readability regardless of the theme variant.

## 2. Settings Screen Definition
**Purpose:** Provide the user with a centralized location to configure global application preferences, notably the visual theme.

**Location:** Add a new "Settings" tab in the `MainWindow` navigation, backed by a `SettingsView` UserControl and a `SettingsViewModel`.

**Features & Content:**
1. **Appearance Section:**
   - **Theme Selector:** A set of RadioButtons or a ComboBox to select the application theme.
   - Options: "Light", "Dark", "System Default".
   - Implementation: Modifying the `Application.Current.RequestedThemeVariant` property at runtime.
2. **About / Information Section (Optional but recommended):**
   - **Database Path:** Display the local SQLite database file path (read-only) so the user knows where their data is stored locally.
   - **App Version:** Display the current application version.

## 3. Light / Dark Mode Support
**Implementation Strategy:**
- Ensure all explicit colors defined in `<Application.Resources>` (e.g., `#F0F2F5`, `#111827`) are appropriately extracted into `<ResourceDictionary.ThemeDictionaries>` with `Light` and `Dark` variants, rather than hardcoded global brushes.
- **Light Theme Resources:** AppBackgroundBrush = `#F0F2F5`, TextPrimaryBrush = `#111827`, CardBackground = `White`.
- **Dark Theme Resources:** AppBackgroundBrush = `#121212` (or similar), TextPrimaryBrush = `#F3F4F6`, CardBackground = `#1E1E1E`.
- Data bind the theme toggle in the Settings screen to switch `App.Current.RequestedThemeVariant`.

## 4. Execution Plan
1. Refactor `App.axaml` to use `ThemeDictionaries` for Light and Dark variants.
2. Fix hover contrast in `App.axaml` and `MainWindow.axaml` by enforcing `TextBlock.Foreground` in `:pointerover`.
3. Create `SettingsViewModel` with a `ThemePreference` property.
4. Create `SettingsView.axaml` with the Appearance and Information panels.
5. Add the new view to `MainWindow.axaml` TabControl.
