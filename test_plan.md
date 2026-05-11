1. **Optimize `GetEffectiveUsersQuery.cs` O(N) linear scan**
   - In `backend/src/SmoothOperator.Application/Features/ConnectionGroups/Queries/GetEffectiveUsersQuery.cs`, there is a performance bottleneck in the loop building `EffectiveUserSourceDto`.
   - The loop iterates over `allUserIds`, and inside it calls `var directUser = vault.Users.FirstOrDefault(u => u.Id == uid);`
   - Since `vault.Users` is a list, `FirstOrDefault` causes an O(N) lookup. For M users, this creates an O(M * N) complexity.
   - We will replace `vault.Users` lookup with an O(1) Dictionary lookup: `var directUsersMap = vault.Users.ToDictionary(u => u.Id);` before the loop.
   - And then inside the loop use `directUsersMap.TryGetValue(uid, out var directUser)`.

2. **Pre-commit checks**
   - Run the necessary testing commands (`dotnet test`) and formatting.

3. **Submit the PR**
   - Follow Bolt's PR format and submit the PR.
