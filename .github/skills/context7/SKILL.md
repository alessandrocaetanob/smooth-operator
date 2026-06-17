---
name: context7
description: "Fetch accurate, up-to-date documentation and code examples for third-party libraries using Context7."
---

# Context7 Skill

## Purpose

Use Context7 to get precise, version-aware documentation and code examples for any external library, framework, SDK, or API — before falling back to memory or web search. This prevents hallucinated API signatures and outdated usage patterns.

## When to Use

- You need API signatures, configuration options, or method behavior for a third-party library
- The user asks "how do I do X with library Y?"
- You're unsure whether a behavior changed between versions
- You're about to guess at an API you're not 100% certain about

**Do NOT use Context7 for questions about this repository's own code** — use Serena (symbol-level code navigation) for that.

## Workflow

### 1. Resolve the library ID

Always resolve first unless the user already provided a `/org/project` ID:

```
context7-resolve-library-id(
  libraryName="<official library name>",
  query="<what you need to know>"
)
```

Examples:
- `libraryName="Angular"` → resolves to `/angular/angular`
- `libraryName="Entity Framework Core"` → resolves to `/dotnet/efcore`
- `libraryName="Tailwind CSS"` → resolves to `/tailwindlabs/tailwindcss`

Pick the result with the best combination of: name match, high source reputation, and high benchmark/snippet count.

### 2. Query the docs

```
context7-query-docs(
  libraryId="/org/project",
  query="<specific question or use case>"
)
```

For version-specific behavior, use the versioned ID:

```
context7-query-docs(
  libraryId="/org/project/v18.0.0",
  query="<question>"
)
```

### 3. Retry with deep research if needed

If the first response is thin or off-topic, retry once with `researchMode: true`:

```
context7-query-docs(
  libraryId="/org/project",
  query="<question>",
  researchMode=true
)
```

**Do not call each tool more than 3 times per question.**

## Query Tips

- Be specific: `"How to configure lazy loading for modules in Angular 17"` beats `"lazy loading"`
- Include the use case: `"JWT verification with JWKS endpoint using Microsoft.IdentityModel.Tokens"`
- Mention the version if behavior is version-dependent

## Quick Reference

| Need | Call |
|------|------|
| Find library ID | `context7-resolve-library-id(libraryName="...", query="...")` |
| Get docs / examples | `context7-query-docs(libraryId="...", query="...")` |
| Deep research retry | `context7-query-docs(..., researchMode=true)` |

## Tool Selection Order

1. **Serena** — for this repo's code (symbols, references, structure)
2. **Context7** — for third-party library/framework docs ← this skill
3. **GitHub** — for PRs, issues, releases, remote code search
4. **`web_fetch` / WebSearch** — for a specific URL or live web info

## Anti-Patterns

- Do not skip resolve step and guess a library ID
- Do not use Context7 for questions about this project's own source code
- Do not call resolve + query more than 3 times for the same question
- Do not paste secrets, credentials, or proprietary code into queries
