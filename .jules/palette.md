## 2026-05-09 - Improving floating item contrast

**Learning:** Floating items and tooltips over remote desktop connections (which can be any color, including pure white) need explicit high-contrast backgrounds instead of relying on transparent or low-opacity glass panels that blend into the canvas in light themes.

**Action:** Updated `.tool-btn` and tooltips to use explicitly high contrast inverse-surface colors and visible translucent backgrounds in both light and dark themes to ensure constant visibility.
## 2026-05-09 - Form Accessibility Labels
**Learning:** In complex angular pages like Administration, nested forms and repeated items in tables or modals frequently lack explicit id/for label connections, especially when visual labels are not present or implicitly wrapped. Implicit wrapping does not provide perfect screen-reader support compared to explicitly linked labels.
**Action:** Always add explicit `id` attributes to inputs/selects and associate them strongly with explicitly declared `<label>` elements using the `for` attribute, even if the label is screen-reader-only (`sr-only`), particularly in dynamically rendered lists and tables (e.g. `[id]="'id-' + item.id"`).
## 2026-05-10 - Missing Clear Button on Search Fields
**Learning:** Search inputs in this application frequently lack a clear button to easily remove the search term. For instance, the administration page had a search input but no clear button, unlike other pages which had the button but lacked ARIA labels.
**Action:** When working on search or filter inputs, always ensure a clear action is provided for ease of use, and verify that the button has an appropriate `aria-label` (e.g., `aria-label="Clear search"`) for screen reader accessibility. Also, update empty states to offer a one-click way to clear the search when no results are found.
