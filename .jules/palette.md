## 2026-04-29 - Missing `for` Attributes on Custom Selects
**Learning:** Found multiple instances where the `<label>` element lacked a corresponding `for` attribute referencing an `id` on the `<select>` or `<input>`. While they were visually styled correctly, they were not programmatically associated, preventing screen readers from correctly identifying the fields and blocking users from clicking labels to focus fields.
**Action:** Always ensure that form fields explicitly connect labels to inputs using `for` and `id` tags to maintain keyboard accessibility and screen reader support.
## 2026-05-01 - Invite Form Input Accessibility
**Learning:** The invite form inputs lacked explicit `id` attributes, making their `<label for="...">` associations non-functional. Even when labels are visually connected, screen readers and click-to-focus behaviour require a matching `id` on each input.
**Action:** Always verify that input fields have an `id` matching their label's `for` attribute in Angular templates.
## 2026-05-03 - Form Field Accessibility in Email Settings
**Learning:** Similar to the invite form issue, multiple form fields in the email settings page (`email.html`) lacked explicit `id` attributes that matched the `for` attributes of their labels, and the test email field lacked any label.
**Action:** Consistently link Angular form inputs with their descriptive labels using explicit `for` and `id` properties to improve screen reader capabilities and keyboard functionality. Add `aria-label` when text labels aren't applicable.
## 2026-05-06 - Password Visibility Toggle
**Learning:** For authentication forms, users need the ability to verify their input, particularly for passwords, reducing input errors and friction. Providing a password visibility toggle with proper ARIA attributes ('aria-label', 'aria-pressed') ensures screen readers can accurately interpret the state, enhancing both general usability and accessibility.
**Action:** Always include a password visibility toggle button on password fields, ensuring it is positioned carefully within a relative container, uses accessible icons, and dynamically updates its ARIA state to reflect visibility.
## 2026-05-09 - Form Accessibility Labels
**Learning:** In complex angular pages like Administration, nested forms and repeated items in tables or modals frequently lack explicit id/for label connections, especially when visual labels are not present or implicitly wrapped. Implicit wrapping does not provide perfect screen-reader support compared to explicitly linked labels.
**Action:** Always add explicit `id` attributes to inputs/selects and associate them strongly with explicitly declared `<label>` elements using the `for` attribute, even if the label is screen-reader-only (`sr-only`), particularly in dynamically rendered lists and tables (e.g. `[id]="'id-' + item.id"`).
## 2026-05-10 - Missing Clear Button on Search Fields
**Learning:** Search inputs in this application frequently lack a clear button to easily remove the search term. For instance, the administration page had a search input but no clear button, unlike other pages which had the button but lacked ARIA labels.
**Action:** When working on search or filter inputs, always ensure a clear action is provided for ease of use, and verify that the button has an appropriate `aria-label` (e.g., `aria-label="Clear search"`) for screen reader accessibility. Also, update empty states to offer a one-click way to clear the search when no results are found.
## 2024-05-12 - Missing ARIA Labels on Icon-Only Buttons
**Learning:** Icon-only buttons (using Material Symbols) throughout the data tables (Connections, Hosts, Credentials) were relying solely on the `title` attribute for accessibility. While `title` provides a tooltip on hover, it is often insufficient for screen readers or keyboard navigation without a clear `aria-label`.
**Action:** Ensure that all icon-only interactive elements explicitly include an `aria-label` attribute in addition to any tooltips to provide full accessibility for screen reader users.
