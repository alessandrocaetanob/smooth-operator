## 2024-04-27 - O(N) Array lookups in Angular Templates
**Learning:** Using O(N) array methods like `.find()` directly inside Angular template bindings (e.g., `hostName(id)` called in a `@for` loop) causes O(M*N) performance bottlenecks because Angular's change detection executes these methods frequently.
**Action:** Transform these reference arrays into lookup Maps using `computed` signals (`computed(() => new Map(...))`) to replace O(N) array searches with O(1) Map lookups.
## 2024-04-28 - Optimize Array Lookups in Computed Signals
**Learning:** Using `.find()` on an array inside a `computed` signal causes O(N) linear scan whenever dependencies change. For multiple components accessing a shared list, this O(N) lookup becomes inefficient.
**Action:** Transform reference arrays into a lookup Map via a shared `computed(() => new Map(...))` signal to provide O(1) lookups for components.
## 2026-04-27 - Connections and Hosts Unification
**Learning:** Combining related creation flows (like creating a Host while defining a Connection) significantly reduces UI friction and creates a more cohesive user experience akin to professional tools like Termius.
**Action:** Implemented a unified `ConnectionsManager` form that supports inline `Host` creation if a new address is provided, streamlining the two-step backend process into a single frontend interaction.
## 2026-04-27 - SLN Configuration Error
**Learning:** The smooth-operator.sln file contained a duplicate project entry for the 'backend' folder, which causes dotnet restore to fail with MSB5004.
**Action:** Removed the duplicate Solution Folder entry for 'backend' to restore successful builds.
## 2026-05-01 - O(N) Array filtering in Angular Templates
**Learning:** Using O(N) array methods like `.filter()` inside Angular components (e.g., `getConnectionsForVault` called inside a `@for` loop) causes O(M*N) performance bottlenecks during change detection.
**Action:** Transformed reference arrays into a lookup Map via a shared `computed(() => new Map(...))` signal to provide O(1) lookups for multiple iterations.
