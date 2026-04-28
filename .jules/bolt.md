## 2024-04-27 - O(N) Array lookups in Angular Templates
**Learning:** Using O(N) array methods like `.find()` directly inside Angular template bindings (e.g., `hostName(id)` called in a `@for` loop) causes O(M*N) performance bottlenecks because Angular's change detection executes these methods frequently.
**Action:** Transform these reference arrays into lookup Maps using `computed` signals (`computed(() => new Map(...))`) to replace O(N) array searches with O(1) Map lookups.
## 2024-04-28 - Optimize Array Lookups in Computed Signals
**Learning:** Using `.find()` on an array inside a `computed` signal causes O(N) linear scan whenever dependencies change. For multiple components accessing a shared list, this O(N) lookup becomes inefficient.
**Action:** Transform reference arrays into a lookup Map via a shared `computed(() => new Map(...))` signal to provide O(1) lookups for components.
