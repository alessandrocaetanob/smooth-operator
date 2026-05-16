## 2026-05-09 - Improving floating item contrast

**Learning:** Floating items and tooltips over remote desktop connections (which can be any color, including pure white) need explicit high-contrast backgrounds instead of relying on transparent or low-opacity glass panels that blend into the canvas in light themes.

**Action:** Updated `.tool-btn` and tooltips to use explicitly high contrast inverse-surface colors and visible translucent backgrounds in both light and dark themes to ensure constant visibility.
## 2026-05-09 - Form Accessibility Labels
**Learning:** In complex angular pages like Administration, nested forms and repeated items in tables or modals frequently lack explicit id/for label connections, especially when visual labels are not present or implicitly wrapped. Implicit wrapping does not provide perfect screen-reader support compared to explicitly linked labels.
**Action:** Always add explicit `id` attributes to inputs/selects and associate them strongly with explicitly declared `<label>` elements using the `for` attribute, even if the label is screen-reader-only (`sr-only`), particularly in dynamically rendered lists and tables (e.g. `[id]="'id-' + item.id"`).
## 2026-05-10 - Missing Clear Button on Search Fields
**Learning:** Search inputs in this application frequently lack a clear button to easily remove the search term. For instance, the administration page had a search input but no clear button, unlike other pages which had the button but lacked ARIA labels.
**Action:** When working on search or filter inputs, always ensure a clear action is provided for ease of use, and verify that the button has an appropriate `aria-label` (e.g., `aria-label="Clear search"`) for screen reader accessibility. Also, update empty states to offer a one-click way to clear the search when no results are found.
## 2024-05-12 - Missing ARIA Labels on Icon-Only Buttons
**Learning:** Icon-only buttons (using Material Symbols) throughout the data tables (Connections, Hosts, Credentials) were relying solely on the `title` attribute for accessibility. While `title` provides a tooltip on hover, it is often insufficient for screen readers or keyboard navigation without a clear `aria-label`.
**Action:** Ensure that all icon-only interactive elements explicitly include an `aria-label` attribute in addition to any tooltips to provide full accessibility for screen reader users.

## 2024-05-16 - Icon-only Button Accessibility
**Learning:** Icon-only buttons using `title` attributes are insufficient for screen readers. The `title` attribute is often read as a tooltip on hover, but `aria-label` provides explicit, reliable accessible names for these controls.
**Action:** Always ensure that icon-only interactive elements (like copy, edit, or delete buttons) include an explicit `aria-label` matching or expanding upon their visual intent.
