## 2024-04-27 - O(N) Array lookups in Angular Templates
**Learning:** Using O(N) array methods like `.find()` directly inside Angular template bindings (e.g., `hostName(id)` called in a `@for` loop) causes O(M*N) performance bottlenecks because Angular's change detection executes these methods frequently.
**Action:** Transform these reference arrays into lookup Maps using `computed` signals (`computed(() => new Map(...))`) to replace O(N) array searches with O(1) Map lookups.
## 2026-04-27 - Connections and Hosts Unification
**Learning:** Combining related creation flows (like creating a Host while defining a Connection) significantly reduces UI friction and creates a more cohesive user experience akin to professional tools like Termius.
**Action:** Implemented a unified `ConnectionsManager` form that supports inline `Host` creation if a new address is provided, streamlining the two-step backend process into a single frontend interaction.
## 2026-04-27 - SLN Configuration Error
**Learning:** The smooth-operator.sln file contained a duplicate project entry for the 'backend' folder, which causes dotnet restore to fail with MSB5004.
**Action:** Removed the duplicate Solution Folder entry for 'backend' to restore successful builds.
