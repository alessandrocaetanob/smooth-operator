# Inefficient Array Checking in AuthService

## Description
Repeating `toLowerCase` array mapping inside `hasAnyRole` which might be called frequently.

## Location
frontend/src/app/services/auth.service.ts:114

## Rationale
Creating a new Set and iterating to lowercase every time access checks are made. Can be cached.

## Code context
```cs
  hasAnyRole(...roleNames: string[]): boolean {
    const roleSet = new Set(this.currentRoles().map((r) => r.toLowerCase()));
    return roleNames.some((roleName) => roleSet.has(roleName.toLowerCase()));
  }
```
# N+1 Query in Role Seeder
## Description
Fetching roles one by one in a loop instead of fetching all required roles at once.

## Location
backend/src/SmoothOperator.Infrastructure/Services/RoleSeeder.cs:26

## Rationale
Clear N+1 pattern in database seeding loop. Can be optimized by querying all defaults first.
## Code context
```cs
  var existingRoles = await context.Roles.ToListAsync();
            foreach (var roleName in AppRoles.Defaults)
            {
                if (existingRoles.Any(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase)))
```

# N+1 Query Pattern in GetEffectiveUsersQuery

## Description
Calling `FirstOrDefault` inside a loop iterating over all user IDs.

## Location 
backend/src/SmoothOperator.Application/Features/ConnectionGroups/Queries/GetEffectiveUsersQuery.cs:56

## Rationale
The `FirstOrDefault` is called on a collection `vault.Users` loaded into memory, making it an O(N^2) operation. Can be optimized by converting `vault.Users` to a dictionary first.

## Code context
```cs
            var users = new List<EffectiveUserSourceDto>();

            foreach (var uid in allUserIds)
            {
                var directUser = vault.Users.FirstOrDefault(u => u.Id == uid);
                groupedByUser.TryGetValue(uid, out var viaGroups);

                var name = directUser?.Name ?? viaGroups![0].User.Name;
```

# Hardcoded database credentials in DesignTimeDbContextFactory

## Description
Hardcoded database connection string containing a username and password was found in the design-time DbContext factory.


## Location
backend/src/SmoothOperator.Infrastructure/Data/DesignTimeDbContextFactory.cs:14

## Rationale
The fix requires moving the connection string to configuration or using environment variables, which is a standard pattern and low effort.


## Code context
```cs
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=smoothoperator;Username=postgres;Password=postgres")
                .Options;
```

# Token accessed from localStorage in exportCsv

## Description
An authentication token is read from localStorage to authorize a CSV export download, which implies the token is stored in a way susceptible to XSS.

## Location
frontend/src/app/pages/auditing/logs/audit-logs.ts:121

## Rationale
While the backend correctly uses HttpOnly cookies, the frontend attempts to read it from localStorage for a fetch request, indicating a potential vulnerability or logic flaw in how tokens are handled during frontend downloads.

## Code context
```ts
  private authHeaders(): HeadersInit {
    const t = localStorage.getItem('smooth-operator.token');
    return t ? { Authorization: `Bearer ${t}` } : {};
  }
```
