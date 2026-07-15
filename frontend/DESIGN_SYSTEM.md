# Operator Glass — Design System

> Reference guide for the **Smooth Operator** frontend. Intended for human developers and AI coding agents (GitHub Copilot, Jules, Cursor, etc.). When making UI changes, read this file first.

## Table of Contents

1. [Overview](#1-overview)
2. [Themes & Color System](#2-themes--color-system)
3. [Typography Scale](#3-typography-scale)
4. [Spacing & Layout](#4-spacing--layout)
5. [Utility Classes](#5-utility-classes)
6. [Component Classes](#6-component-classes)
7. [Shared Angular Components](#7-shared-angular-components)
8. [Mascot States](#8-mascot-states)
9. [Animation Library](#9-animation-library)
10. [Accessibility](#10-accessibility)
11. [Icons](#11-icons)
12. [Do & Don't Examples](#12-do--dont-examples)

---

## 1. Overview

**Operator Glass** is the design language of Smooth Operator. It draws inspiration from secure operations centres — dark, glassmorphic surfaces, precise typography, and deliberate motion that communicates system state.

**Key principles:**

- **Glass layers** — panels and cards use frosted-glass effects (`glass-panel`, `glass-card`) that adapt between light and dark themes.
- **Token-first** — every color, spacing, and type choice must reference a design token (CSS custom property) rather than a hardcoded value.
- **Accessibility-baseline** — WCAG AA contrast, `:focus-visible` rings, reduced-motion respected.
- **Motion is purposeful** — animations communicate state changes, not decoration.

---

## 2. Themes & Color System

### Switching Mechanism

Themes are managed by `ThemeService` (`frontend/src/app/services/theme.service.ts`):

- Adds/removes the `.dark` class on `<html>`.
- Persists preference to `localStorage`.
- Falls back to `prefers-color-scheme` on first visit.
- The `top-nav-bar` header button toggles theme via `toggleTheme()`.

Dark mode is class-based (`darkMode: "class"` in `tailwind.config.js`).

### Token Architecture

All colors are defined as **space-separated RGB integers** in `frontend/src/styles.css`:

```css
/* Light mode — :root */
--color-primary: 0 84 214;

/* Dark mode — .dark */
--color-primary: 179 197 255;
```

Tailwind color aliases are registered in `tailwind.config.js`:

```js
"primary": "rgb(var(--color-primary) / <alpha-value>)",
```

This format supports Tailwind opacity modifiers: `text-primary/70`, `bg-primary/10`, etc.

### Color Token Reference

| Token                            | Tailwind Class                | Purpose                                     |
| -------------------------------- | ----------------------------- | ------------------------------------------- |
| `--color-background`             | `bg-background`               | Page background                             |
| `--color-surface`                | `bg-surface`                  | Card/panel surface                          |
| `--color-surface-dim`            | `bg-surface-dim`              | Recessed surface                            |
| `--color-surface-bright`         | `bg-surface-bright`           | Elevated surface                            |
| `--color-surface-variant`        | `bg-surface-variant`          | Alternate surface                           |
| `--color-surface-container`      | `bg-surface-container`        | Input backgrounds, table rows               |
| `--color-surface-container-low`  | `bg-surface-container-low`    | Subtle container                            |
| `--color-surface-container-high` | `bg-surface-container-high`   | Hover state backgrounds                     |
| `--color-on-surface`             | `text-on-surface`             | Primary body text                           |
| `--color-on-surface-variant`     | `text-on-surface-variant`     | Secondary / helper text                     |
| `--color-outline`                | `text-outline`                | Placeholders, disabled text, subtle borders |
| `--color-outline-variant`        | `border-outline-variant`      | Dividers and light borders                  |
| `--color-primary`                | `text-primary` / `bg-primary` | Interactive elements, focus rings           |
| `--color-primary-container`      | `bg-primary-container`        | Filled button backgrounds                   |
| `--color-on-primary`             | `text-on-primary`             | Text on primary fill                        |
| `--color-on-primary-container`   | `text-on-primary-container`   | Text on primary-container fill              |
| `--color-secondary`              | `text-secondary`              | Secondary accent (cyan)                     |
| `--color-tertiary`               | `text-tertiary`               | Success / status accent (green)             |
| `--color-error`                  | `text-error`                  | Error messages, danger buttons              |
| `--color-error-container`        | `bg-error-container`          | Error background fills                      |
| `--color-on-error`               | `text-on-error`               | Text on error fill                          |

### Mascot CSS Variables

The mascot's color skin switches with the active theme:

| Variable                  | Description           |
| ------------------------- | --------------------- |
| `--mascot-chassis`        | Body frame fill       |
| `--mascot-chassis-stroke` | Body frame border     |
| `--mascot-screen`         | Screen / visor fill   |
| `--mascot-screen-stroke`  | Screen / visor border |
| `--mascot-eye`            | Pupil color           |
| `--mascot-hand`           | Hand / arm color      |
| `--mascot-antenna`        | Antenna bulb color    |

---

## 3. Typography Scale

**Typeface:** [Inter](https://rsms.me/inter/) — loaded via Google Fonts in `index.html`.

| Scale       | Tailwind Class    | Size | Weight | Line Height | Letter Spacing |
| ----------- | ----------------- | ---- | ------ | ----------- | -------------- |
| Heading 1   | `text-h1`         | 40px | 700    | 1.2         | −0.02em        |
| Heading 2   | `text-h2`         | 32px | 600    | 1.2         | −0.01em        |
| Heading 3   | `text-h3`         | 24px | 600    | 1.3         | —              |
| Body Large  | `text-body-lg`    | 18px | 400    | 1.6         | —              |
| Body Medium | `text-body-md`    | 16px | 400    | 1.6         | —              |
| Body Small  | `text-body-sm`    | 14px | 400    | 1.5         | —              |
| Label Caps  | `text-label-caps` | 12px | 600    | 1.0         | 0.05em         |
| Monospace   | `text-mono`       | 14px | 400    | 1.5         | —              |

**Note:** Page section headers (`page-header` component) use `text-xs font-bold uppercase tracking-widest`. This is the `text-label-caps` pattern expressed with Tailwind utilities rather than the semantic scale class.

---

## 4. Spacing & Layout

**Base unit:** 8px. All spacing should be multiples of 8px where possible.

### Named Spacing Tokens

| Token    | Tailwind         | Value | Use                      |
| -------- | ---------------- | ----- | ------------------------ |
| `xs`     | `p-xs`, `gap-xs` | 4px   | Tight padding, icon gaps |
| `sm`     | `p-sm`, `gap-sm` | 8px   | Small gaps, icon margins |
| `md`     | `p-md`, `gap-md` | 16px  | Standard padding         |
| `lg`     | `p-lg`, `gap-lg` | 24px  | Section padding          |
| `xl`     | `p-xl`, `gap-xl` | 40px  | Major section gaps       |
| `margin` | `mx-margin`      | 32px  | Page horizontal margins  |
| `gutter` | `gap-gutter`     | 24px  | Grid column gutters      |

### Container

Maximum content width: `1440px` (token: `container-max`). Applied to page layout wrappers.

### Border Radii

| Token          | Value  | Use                         |
| -------------- | ------ | --------------------------- |
| Default        | 4px    | Small chips, badges         |
| `rounded-lg`   | 8px    | Buttons, inputs, list items |
| `rounded-xl`   | 12px   | Cards, panels, dialogs      |
| `rounded-2xl`  | 16px   | Large modals                |
| `rounded-full` | 9999px | Avatars, pills, status dots |

---

## 5. Utility Classes

Defined in `@layer utilities` in `styles.css`.

### Glass Effects

| Class         | Description                                                                                          |
| ------------- | ---------------------------------------------------------------------------------------------------- |
| `glass-panel` | Elevated glass surface. Use for floating elements: modals, dialogs, nav bar, header. Higher opacity. |
| `glass-card`  | Recessed glass surface. Use for content panels, table wrappers, section cards. Lower opacity.        |

Both use `--glass-blur: 12px` backdrop blur and theme-aware `--glass-*-bg` / `--glass-*-border` CSS variables that auto-switch between light and dark.

### Glow Effects

| Class                | Description                                           |
| -------------------- | ----------------------------------------------------- |
| `primary-glow`       | Static box-shadow glow on an element                  |
| `primary-glow-hover` | Glow appears only on `:hover`                         |
| `text-glow`          | Text shadow glow — for headings                       |
| `status-pulse`       | Animated radial pulse — used on status indicator dots |

### Interactive States

| Class             | Description                                                                                                                     |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| `btn-interactive` | Lift-on-hover (`translateY(-1px)`) and press (`scale(0.97)`) micro-interactions. Automatically included in all `btn-*` classes. |

### Skeleton Loading

| Class      | Description                                                                                        |
| ---------- | -------------------------------------------------------------------------------------------------- |
| `skeleton` | Animated shimmer gradient for placeholder content. Apply to `<div>` placeholders while data loads. |

### CSS Animation Helpers

Apply these directly in HTML for Tailwind keyframe animations:

| Class                    | Effect                           | Duration |
| ------------------------ | -------------------------------- | -------- |
| `animate-slide-up`       | Fade + translateY(12px → 0)      | 300ms    |
| `animate-slide-in-right` | Fade + translateX(16px → 0)      | 300ms    |
| `animate-scale-in`       | Fade + scale(0.95 → 1)           | 250ms    |
| `animate-fade-in`        | Fade opacity(0 → 1)              | 300ms    |
| `animate-float`          | Continuous vertical float (−6px) | 4s       |
| `animate-shimmer`        | Skeleton shimmer sweep           | 2.5s     |
| `animate-pulse-glow`     | Radial glow pulse                | 2s       |

---

## 6. Component Classes

Defined in `@layer components` in `styles.css`. These are the canonical button and field styles.

### Buttons

| Class           | Use Case                                         | Examples               |
| --------------- | ------------------------------------------------ | ---------------------- |
| `btn-primary`   | Main call-to-action, form submit                 | Save, Connect, Invite  |
| `btn-secondary` | Alternative action with border                   | Edit, Export, Filter   |
| `btn-ghost`     | Tertiary / cancel action — minimal visual weight | Cancel, Dismiss, Close |
| `btn-danger`    | Destructive actions requiring confirmation       | Delete, Revoke, Kick   |

All `btn-*` classes include `flex items-center gap-2` — icons placed before text are naturally aligned.

For full-width centered buttons (e.g., auth form submit), add `w-full justify-center`:

```html
<button class="btn-primary w-full justify-center py-2.5 mt-xs">Sign in</button>
```

### Inputs

| Class          | Use Case                                         |
| -------------- | ------------------------------------------------ |
| `input-field`  | Text, email, password, number `<input>` elements |
| `select-field` | `<select>` dropdowns                             |

Both include a focus ring via `focus:border-primary` and adapt to both themes via `bg-surface-container`.

Always pair with the `app-form-field` shared component for label–input association and validation messages.

---

## 7. Shared Angular Components

All located in `frontend/src/app/shared/`. All are standalone Angular components.

### `app-page-header`

```html
<app-page-header title="Connections" subtitle="Manage your remote connections" icon="hub" />
```

Props: `title` (required), `subtitle?`, `icon?` (Material Symbol name), `actions` (ng-content slot for action buttons).

### `app-section-card`

```html
<app-section-card title="Active Sessions">
  <!-- table or list content -->
</app-section-card>
```

Wraps content in a `glass-card` panel with a consistent header. Props: `title?`, `icon?`.

### `app-empty-state-card`

```html
<app-empty-state-card
  [mascotState]="'idle'"
  [heading]="'No connections'"
  [body]="'Add your first connection to get started.'"
>
  <button class="btn-primary">New Connection</button>
</app-empty-state-card>
```

Centered empty state with `app-mascot`, a heading, and an optional body message. Inputs: `mascotState?` (`MascotState`, default `'idle'`), `heading`, `body?`. Project a CTA button/link via `<ng-content>`. Display when a list has no items — reused across the Hosts, Credentials, Connections, and Vault empty states.

### `app-loading-skeleton`

```html
<app-loading-skeleton [rows]="5" />
```

Renders animated skeleton rows. Props: `rows` (default: 3).

### `app-table-card`

```html
<app-table-card [columns]="columns" [rows]="data" />
```

Generic table wrapped in a `glass-card`. Use for uniform data tables.

### `app-form-field`

```html
<app-form-field label="Email" [control]="form.controls.email" errorMessage="Enter a valid email">
  <input class="input-field w-full" formControlName="email" type="email" />
</app-form-field>
```

Provides label, ng-content slot for the input, and a conditional validation error message. Handles `for` / `id` linkage.

### `app-confirm-dialog`

Add once in `app.component.html`. Trigger programmatically:

```ts
const confirmed = await this.confirmDialog.open({
  title: 'Delete Connection',
  message: 'This cannot be undone.',
  confirmLabel: 'Delete',
  tone: 'danger', // 'default' | 'danger'
});
```

- Tone `'danger'` renders a red confirm button (`btn-danger`).
- Dialog auto-focuses the confirm button on open.
- `Escape` key cancels via `@HostListener('document:keydown.escape')`.
- Clicking the backdrop also cancels.

### `app-toast`

Add once in `app.component.html`. Trigger via `ToastService`:

```ts
this.toast.show({ message: 'Saved!', type: 'success' });
// types: 'success' | 'error' | 'info' | 'warning'
```

### `app-side-nav-bar`

Fixed left navigation at `left-0 top-16`. Visibility and nav items are driven by the current user's role via `AuthService`. No configuration needed.

### `app-top-nav-bar`

Fixed header at `top-0`. Includes search input, theme toggle, and user menu with sign-out and settings. No configuration.

### `app-mascot`

See [Section 8 — Mascot States](#8-mascot-states).

---

## 8. Mascot States

The mascot is an animated robot that communicates system state. Drive it via `[state]`:

```html
<app-mascot [state]="mascotState()" />
```

| State      | Meaning                          | When to Use                                 |
| ---------- | -------------------------------- | ------------------------------------------- |
| `idle`     | Neutral, eyes open, gentle float | Default — any screen with no active process |
| `typing`   | Eyes animated, alert posture     | User is actively filling a form field       |
| `password` | Eyes closed / shielded           | Password field is focused                   |
| `loading`  | Antenna pulse, processing        | API call in progress                        |
| `success`  | Positive, raised hand or glow    | Action completed successfully               |
| `error`    | Distress signal                  | Action failed / error state displayed       |
| `thinking` | Pensive, slow antenna pulse      | Background processing / indeterminate wait  |
| `wave`     | Greeting wave                    | Login success, welcome screens, first visit |
| `sleep`    | Eyes closed, dormant             | Session idle timeout, session expired       |

**Small variant** — use `[isSmall]="true"` for the nav-bar mascot. Keep small variants in `idle` or simple nav-context states only.

**Pointer tracking** — use `[trackPointer]="true"` exclusively on the authentication page, where the mascot is the focal decorative element. Do not enable this elsewhere.

**Accessibility** — The mascot SVG has `aria-hidden="true"`. It is purely decorative and must not be the sole conveyor of critical state information to users.

---

## 9. Animation Library

### Angular Animation Triggers

Imported from `frontend/src/app/shared/animations.ts`. Declare in the component's `animations: []` array.

| Export             | Trigger Name        | Use                                             |
| ------------------ | ------------------- | ----------------------------------------------- |
| `fadeIn`           | `@fadeIn`           | Conditional blocks (`@if`), overlay backdrops   |
| `scaleIn`          | `@scaleIn`          | Dialogs, popovers, modals                       |
| `slideUp`          | `@slideUp`          | Page sections entering view                     |
| `slideInFromRight` | `@slideInFromRight` | Side panels, drawer content                     |
| `listStagger`      | `@listStagger`      | List containers — staggers child enters by 40ms |
| `routeFade`        | `@routeFade`        | Route outlet wrapper in `app.component.html`    |
| `toastEnter`       | `@toastEnter`       | Toast notifications                             |

**Usage:**

```ts
// component.ts
import { scaleIn } from '../shared/animations';
@Component({ animations: [scaleIn] })
```

```html
<!-- component.html -->
<div @scaleIn>...</div>
```

### Tailwind CSS Keyframe Animations

Apply via `animate-*` classes in HTML. Keyframes defined in `tailwind.config.js`.

| Class                    | Keyframe         | Use                    |
| ------------------------ | ---------------- | ---------------------- |
| `animate-float`          | `float`          | Mascot idle floating   |
| `animate-fade-in`        | `fade-in`        | Static fade in         |
| `animate-slide-up`       | `slide-up`       | Static slide + fade    |
| `animate-scale-in`       | `scale-in`       | Static scale + fade    |
| `animate-slide-in-right` | `slide-in-right` | Static right-slide     |
| `animate-shimmer`        | `shimmer`        | Skeleton loading sweep |
| `animate-pulse-glow`     | `pulse-glow`     | Status dot pulse       |

### Guideline: Angular vs Tailwind

Use **Angular triggers** for enter/leave transitions on elements that appear/disappear with `@if` or `@for`.

Use **Tailwind `animate-*`** for always-on or CSS-only animations (loading states, continuous glow, floating mascot, skeleton).

### Reduced Motion

`styles.css` includes a `@media (prefers-reduced-motion: reduce)` block that collapses all animations to `0.01ms`. Angular animation triggers also respect this natively via the browser. No extra configuration is required.

---

## 10. Accessibility

### Focus Rings

`:focus-visible` is globally defined in `styles.css`:

```css
:focus-visible {
  outline: 2px solid rgb(var(--color-primary));
  outline-offset: 2px;
  border-radius: 4px;
}
```

This applies to all interactive elements in both themes. **Do not suppress `outline` on focusable elements.**

### ARIA Patterns

| Pattern            | Implementation                                                                                                                |
| ------------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| Dialog             | `role="dialog"` + `aria-modal="true"` + `aria-labelledby="<h-id>"` + auto-focus confirm + Escape-to-close via `@HostListener` |
| Toggle button      | Dynamic `[attr.aria-label]` that changes with state (e.g., `'Switch to light mode'` / `'Switch to dark mode'`)                |
| Menu button        | `aria-haspopup="true"` + `[attr.aria-expanded]="menuOpen"`                                                                    |
| Decorative icons   | `aria-hidden="true"` on `<span class="material-symbols-outlined">` when adjacent to visible text                              |
| Presentational SVG | `aria-hidden="true"` on `<svg>` — already applied to mascot                                                                   |

### Keyboard Navigation

All interactive elements are reachable via Tab. The confirm dialog:

- Auto-focuses the confirm button on open (via `effect()` + `queueMicrotask`).
- Closes on `Escape`.
- Tab cycles naturally within the two dialog buttons.

### Color Contrast

- `text-on-surface` on `background` / `surface`: ≥ 7:1 both themes (AAA).
- `text-on-surface-variant` on `surface-container`: ≥ 4.5:1 both themes (AA).
- `text-outline` on `surface`: ≥ 3:1 — meets AA for non-text (borders, icons). Use `text-on-surface-variant` for any text that must pass AA text contrast.
- `btn-primary` (`on-primary-container` on `primary-container`): ≥ 4.5:1 (AA).
- `btn-danger` (`on-error` on `error`): ≥ 4.5:1 (AA).

---

## 11. Icons

**Icon set:** [Google Material Symbols Outlined](https://fonts.google.com/icons) — loaded via Google Fonts CDN in `index.html`.

**Usage:**

```html
<span class="material-symbols-outlined" aria-hidden="true">settings</span>
```

**Always add `aria-hidden="true"`** when the icon is adjacent to visible text label, or when the parent has an `aria-label`. This prevents screen readers from announcing the icon ligature name (e.g., "settings") alongside the visible label.

When an icon is the **sole accessible label** (icon-only button with no adjacent text), do NOT add `aria-hidden`. Instead, rely on the parent element's `aria-label`:

```html
<!-- Icon-only button — aria-hidden NOT added, button has aria-label -->
<button aria-label="Open settings">
  <span class="material-symbols-outlined">settings</span>
</button>
```

**Variation settings** — icons support fill, weight, grade, and optical-size axes:

```html
<span class="material-symbols-outlined" style="font-variation-settings: 'FILL' 1">star</span>
```

Default variation (`FILL` 0, `wght` 400) is set globally in `styles.css`.

---

## 12. Do & Don't Examples

### Colors

```html
<!-- ✅ DO — use design tokens -->
<p class="text-on-surface-variant">Helper text</p>
<div class="border border-outline-variant/50">...</div>

<!-- ❌ DON'T — hardcode Tailwind palette colors -->
<p class="text-slate-400">Helper text</p>
<div class="border border-white/10">...</div>
```

### Buttons

```html
<!-- ✅ DO — use semantic button classes -->
<button class="btn-primary">Save</button>
<button class="btn-ghost">Cancel</button>

<!-- ❌ DON'T — assemble button styles manually -->
<button class="bg-blue-600 hover:bg-blue-500 text-white px-4 py-2 rounded-lg">Save</button>
```

### Panels and Cards

```html
<!-- ✅ DO — use glass utilities -->
<div class="glass-card rounded-xl p-4">...</div>
<!-- content panel -->
<div class="glass-panel rounded-xl p-6">...</div>
<!-- floating dialog/modal -->

<!-- ❌ DON'T — use hardcoded glassmorphism -->
<div class="bg-slate-800/60 backdrop-blur-md border border-white/10 rounded-xl p-4">...</div>
```

### Decorative Icons

```html
<!-- ✅ DO — aria-hidden on icon next to text -->
<a routerLink="/settings">
  <span class="material-symbols-outlined" aria-hidden="true">settings</span>
  <span>Settings</span>
</a>

<!-- ✅ DO — button already has aria-label; icon is redundant to AT -->
<button aria-label="Delete item" class="btn-ghost">
  <span class="material-symbols-outlined" aria-hidden="true">delete</span>
</button>

<!-- ❌ DON'T — icon-only interactive with no accessible name -->
<button>
  <span class="material-symbols-outlined">delete</span>
</button>
```

### Exceptions (hardcoded colors are intentional)

These cases use hardcoded colors and must **not** be tokenized:

- **Terminal output lines** — `text-green-500` for `[OK]` console markers in the connections page.
- **Active-session overlay** — `bg-slate-950/80` and `bg-slate-950/90` are intentionally opaque to isolate the session canvas from the page theme.
- **Decorative gradient accent lines** — in the connecting-state overlay for visual flair.
