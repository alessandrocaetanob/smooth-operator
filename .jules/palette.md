## 2024-05-01 - Form Accessibility
**Learning:** Found some Angular forms without proper explicit labels. Using `[ngModel]` or `formControlName` without an `id` on the input element prevents labels from being properly connected if the label uses the `for` attribute, causing issues with screen reader compatibility and keyboard navigation.
**Action:** Always verify that input fields have an `id` matching their label's `for` attribute in Angular templates.
