## 2026-04-29 - Missing `for` Attributes on Custom Selects
**Learning:** Found multiple instances where the `<label>` element lacked a corresponding `for` attribute referencing an `id` on the `<select>` or `<input>`. While they were visually styled correctly, they were not programmatically associated, preventing screen readers from correctly identifying the fields and blocking users from clicking labels to focus fields.
**Action:** Always ensure that form fields explicitly connect labels to inputs using `for` and `id` tags to maintain keyboard accessibility and screen reader support.
## 2026-05-01 - Invite Form Input Accessibility
**Learning:** The invite form inputs lacked explicit `id` attributes, making their `<label for="...">` associations non-functional. Even when labels are visually connected, screen readers and click-to-focus behaviour require a matching `id` on each input.
**Action:** Always verify that input fields have an `id` matching their label's `for` attribute in Angular templates.
