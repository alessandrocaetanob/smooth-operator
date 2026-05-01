---
name: tavily
description: "Search the live web, fetch URLs, and do deep research using Tavily — for current information beyond the model's training cutoff."
---

# Tavily Skill

## Purpose

Use Tavily when you need current, real-world information that isn't in this repository or in library documentation: recent CVEs, GitHub issues, error messages, community patterns, blog posts, release notes, or any time-sensitive facts.

## When to Use

- The user asks about something recent ("latest", "current", "as of…")
- You encounter an error message or stack trace with no obvious match in Context7
- You need to compare approaches, look up community patterns, or triage a GitHub issue
- You're researching a security advisory or dependency vulnerability
- Context7 returned no useful results for a niche or new library

**Do NOT use Tavily for this repository's code** — use ContextStream `search()` for that.  
**Do NOT use Tavily when Context7 already has good docs** — it's faster and more structured.

## Tool Selection

Tavily exposes multiple tools — pick the right one:

| Tool | When to use |
|------|-------------|
| `tavily-tavily_search` | General web search. Default first choice. |
| `tavily-tavily_extract` | Fetch full content from a specific URL (docs page, GitHub issue, PR). |
| `tavily-tavily_research` | Deep multi-source research on a broad topic. Rate-limited — use sparingly. |
| `tavily-tavily_crawl` | Structured crawl of a docs site or wiki. |
| `tavily-tavily_map` | Map the structure (URL list) of a site before crawling. |

## Workflow

### General web search

```
tavily-tavily_search(
  query="<specific question>",
  search_depth="basic"
)
```

For tougher or niche questions, use advanced depth:

```
tavily-tavily_search(
  query="<specific question>",
  search_depth="advanced"
)
```

### Fetch a specific page

When you already have a URL (e.g., from a search result or the user):

```
tavily-tavily_extract(
  urls=["https://example.com/relevant-page"],
  query="<what you're looking for on this page>"
)
```

Use `extract_depth="advanced"` for LinkedIn, protected sites, or pages with tables/embedded content.

### Deep research (use sparingly)

For broad, multi-subtopic research questions:

```
tavily-tavily_research(
  input="<comprehensive description of what you need to know>",
  model="auto"
)
```

Use `model="mini"` for narrow tasks, `model="pro"` for broad multi-subtopic questions.

### Crawl a docs site

```
tavily-tavily_crawl(
  url="https://docs.example.com",
  max_depth=2,
  max_breadth=10,
  instructions="Return pages covering authentication and configuration"
)
```

## Query Tips

- Ask a full question, not just keywords: `"How to fix CORS error in ASP.NET Core minimal API"` beats `"ASP.NET CORS"`
- Include version context when relevant: `"Angular 18 standalone component routing"`
- For CVEs / security: include the package name and version range: `"CVE ITfoxtec.Identity.Saml2 2024"`
- Use `include_domains` to scope to trusted sources (e.g., `["docs.microsoft.com", "github.com"]`)
- Use `exclude_domains` to filter out low-quality results

## Always Cite Sources

When Tavily provides the answer, include the source URL(s) in your response so the user can verify.

## Tool Selection Order

1. **ContextStream `search()`** — for this repo's code
2. **Context7** — for third-party library/framework docs
3. **Tavily** — for live web, recent CVEs, GitHub issues, news ← this skill
4. **`web_fetch`** — for fetching a single known URL directly

## Anti-Patterns

- Do not use `tavily_research` for narrow questions — `tavily_search` is faster and cheaper
- Do not skip Context7 when the question is about a well-documented library
- Do not paste secrets, credentials, or proprietary code into queries
- Do not treat Tavily results as authoritative without checking the source URL
