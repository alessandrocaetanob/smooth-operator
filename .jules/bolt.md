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
## 2026-04-30 - O(N) Array Filter in Computed Signals inside Loops
**Learning:** Using an O(N) array `.filter()` inside a computed signal or method that is called from an Angular `@for` loop creates an O(M*N) performance bottleneck. In this codebase, `getConnectionsForVault` was doing exactly this on every change detection cycle.
**Action:** Replaced the filtering logic with a single `computed` signal (`vaultConnectionCounts`) that iterates over the connections list once to build a lookup map of counts per vault. The template now performs an O(1) map lookup `vaultConnectionCounts().get(v.key)`, eliminating the bottleneck.
## 2026-05-01 - O(N) Array filtering in Angular Templates
**Learning:** Using O(N) array methods like `.filter()` inside Angular components (e.g., `getConnectionsForVault` called inside a `@for` loop) causes O(M*N) performance bottlenecks during change detection.
**Action:** Transformed reference arrays into a lookup Map via a shared `computed(() => new Map(...))` signal to provide O(1) lookups for multiple iterations.
## 2026-05-02 - N+1 Network Bottleneck in probeAll
**Learning:** Calling `/api/connections/{id}/probe` in a loop from the frontend created an N+1 network request bottleneck during connection status polling.
**Action:** Created a batched `probe-batch` endpoint on the backend using `Task.WhenAll` to fetch all statuses concurrently, reducing frontend requests and database query overhead.
## 2026-05-04 - O(N) Array Includes Lookups in Angular Templates
**Learning:** Using O(N) array methods like `.includes()` directly inside component methods that are called from Angular templates (e.g., `isVaultSelected(id)`) inside loops like `@for` causes O(M*N) performance bottlenecks during change detection.
**Action:** Transform array references into `Set` lookups using `computed` signals (`computed(() => new Set(...))`) and use `Set.prototype.has()` instead of `Array.prototype.includes()` to reduce lookup time complexity to O(1).
