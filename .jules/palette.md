## 2024-05-24 - Missing Clear Button on Search Fields

**Learning:** Search inputs in this application frequently lack a clear button to easily remove the search term. For instance, the administration page had a search input but no clear button, unlike other pages which had the button but lacked ARIA labels.
**Action:** When working on search or filter inputs, always ensure a clear action is provided for ease of use, and verify that the button has an appropriate `aria-label` (e.g., `aria-label="Clear search"`) for screen reader accessibility. Also, update empty states to offer a one-click way to clear the search when no results are found.
