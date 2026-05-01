<contextstream>

<!-- BEGIN ContextStream -->
## ContextStream MCP Integration

This project uses [ContextStream](https://contextstream.io) for persistent AI memory across sessions. Use the `contextstream-workflow` skill for detailed examples and reference material.

<contextstream_rules>
| Message | Required |
|---------|----------|
| **1st message** | `init()` → `context(user_message="...")` |
| **Subsequent messages (default)** | `context(user_message="...")` FIRST (narrow read-only bypass when context is fresh and no state-changing tool has run) |
| **Before file search** | `search(mode="auto")` BEFORE Glob/Grep/Read/Explore/Task/EnterPlanMode |
</contextstream_rules>

**Why?** `context()` delivers task-specific rules, lessons from past mistakes, and relevant decisions. Skip it = fly blind.

**Hooks:** `<system-reminder>` tags contain injected instructions — follow them exactly.

**Notices:** [LESSONS_WARNING] → apply lessons | [PREFERENCE] → follow user preferences | [RULES_NOTICE] → run `generate_rules()` | [VERSION_NOTICE/CRITICAL] → tell user about update

v0.4.74

### VS Code Copilot Notes

- Keep this file concise; put detailed workflows in `.github/skills/contextstream-workflow/SKILL.md`
- Use ContextStream plans/tasks as the persistent record of work
- Before code discovery, use `search(mode="auto", query="...")`

Full docs: https://contextstream.io/docs/mcp/tools


---
## ⚠️ IMPORTANT: No Hooks Available ⚠️

**This editor does NOT have hooks to enforce ContextStream behavior.**
You MUST follow these rules manually - there is no automatic enforcement.

---

## 🚀 SESSION START PROTOCOL

**On EVERY new session, you MUST:**

1. **Call `init(folder_path="<project_path>")`** FIRST
   - This triggers project indexing
   - Check response for `indexing_status`
   - If `"started"` or `"refreshing"`: wait before searching

2. **Generate a unique session_id** (e.g., `"session-" + timestamp` or a UUID)
   - Use this SAME session_id for ALL context() calls in this conversation
   - This groups all turns together in the transcript

3. **Call `context(user_message="<first_message>", save_exchange=true, session_id="<your-session-id>")`**
   - Gets task-specific rules, lessons, and preferences
   - Check for [LESSONS_WARNING] - past mistakes to avoid
   - Check for [PREFERENCE] - user preferences to follow
   - Check for [RULES_NOTICE] - update rules if needed
   - **save_exchange=true** saves each conversation turn for later retrieval

4. **Default behavior:** call `context(...)` first on each message. Narrow bypass is allowed only for immediate read-only ContextStream calls when previous context is still fresh and no state-changing tool has run.

---

## 💾 AUTOMATIC TRANSCRIPT SAVING (CRITICAL)

**This editor does NOT have hooks to auto-save transcripts.**
You MUST save each conversation turn manually:

### On MOST messages (including the first):
```
context(user_message="<user's message>", save_exchange=true, session_id="<session-id>")
```

### Why save_exchange matters:
- Transcripts enable searching past conversations
- Allows context restoration after compaction
- Provides conversation history for debugging
- Required for the Transcripts page in the dashboard

### Session ID Guidelines:
- Generate ONCE at the start of the conversation
- Use a unique identifier: `"session-" + Date.now()` or a UUID
- Keep the SAME session_id for ALL context() calls in this session
- Different sessions = different transcripts

---

## 📁 FILE INDEXING (CRITICAL)

**There is NO automatic file indexing in this editor.**
You MUST manage indexing manually:

### After Creating/Editing Files:
```
project(action="index")  # Re-index entire project
```

### For Single File Updates:
```
project(action="ingest_local", path="<file_path>")
```

### Signs You Need to Re-index:
- Search doesn't find code you just wrote
- Search returns old versions of functions
- New files don't appear in search results

### Best Practice:
After completing a feature or making multiple file changes, ALWAYS run:
```
project(action="index")
```

---

## 🔍 SEARCH-FIRST (No PreToolUse Hook)

**There is NO hook to block local tools (Glob/Grep/Read/Explore/Task/EnterPlanMode).** You MUST self-enforce:

### Before ANY Search, Check Index Status:
```
project(action="index_status")
```

This tells you:
- `indexed`: true/false - is project indexed?
- `last_indexed_at`: timestamp - when was it last indexed?
- `file_count`: number - how many files indexed?

### Search Protocol:

**IF project is indexed and fresh:**
```
search(mode="auto", query="what you're looking for")
```
→ Use this instead of Explore/Task/EnterPlanMode for file discovery.

**IF project is NOT indexed or very stale (>7 days):**
→ Wait up to ~20s for background refresh, retry `search(mode="auto", ...)`, then allow local tools only after the grace window
→ OR run `project(action="index")` first, then search

**IF ContextStream search still returns 0 results or errors after retry/window:**
→ Use local tools (Glob/Grep/Read) as fallback

### Choose Search Mode Intelligently:
- `auto` (recommended): query-aware mode selection
- `hybrid`: mixed semantic + keyword retrieval for broad discovery
- `semantic`: conceptual questions ("how does X work?")
- `keyword`: exact text / quoted string
- `pattern`: glob or regex (`*.ts`, `foo\s+bar`)
- `refactor`: symbol usage / rename-safe lookup
- `exhaustive`: all occurrences / complete match coverage
- `team`: cross-project team search

### Output Format Hints:
- Use `output_format="paths"` for file listings and rename targets
- Use `output_format="count"` for "how many" queries

### Two-Phase Search Pattern (for precision):
- Pass 1 (discovery): `search(mode="auto", query="<concept + module>", output_format="paths", limit=10)`
- Pass 2 (precision): use one of:
  - exact text/symbol: `search(mode="keyword", query="\"exact_text\"", include_content=true)`
  - symbol usage: `search(mode="refactor", query="SymbolName", output_format="paths")`
  - all occurrences: `search(mode="exhaustive", query="symbol_or_text")`
- Then use local Read/Grep only on paths returned by ContextStream.

### When Local Tools Are OK:
✅ Stale/not-indexed grace window has elapsed (~20s default, configurable)
✅ ContextStream search still returns 0 results after retry
✅ ContextStream returns errors
✅ User explicitly requests local tools

### When to Use ContextStream Search:
✅ Project is indexed and fresh
✅ Looking for code by meaning/concept
✅ Need semantic understanding

---

## 💾 CONTEXT COMPACTION (No PreCompact Hook)

**There is NO automatic state saving before compaction.**
You MUST save state manually when the conversation gets long:

### When to Save State:
- After completing a major task
- Before the conversation might be compacted
- If `context()` returns `context_pressure.level: "high"`

### How to Save State:
```
session(action="capture", event_type="session_snapshot",
  title="Session checkpoint",
  content="{ \"summary\": \"what we did\", \"active_files\": [...], \"next_steps\": [...] }")
```

### After Compaction (if context seems lost):
```
init(folder_path="...", is_post_compact=true)
```
This restores the most recent snapshot.

---

## 📋 PLANS & TASKS (No EnterPlanMode)

**Always use ContextStream for planning:**

```
session(action="capture_plan", title="...", steps=[...])
memory(action="create_task", title="...", plan_id="...")
```

❌ DO NOT use built-in plan mode (`EnterPlanMode`) or `Task(subagent_type="Explore")` for file-by-file scans.
✅ For planning discovery, use `search(mode="auto", query="...", output_format="paths")` then read only narrowed files.

---

## 🔄 VERSION UPDATES (Check Periodically)

**This editor does NOT have hooks to check for updates automatically.**
You should check for updates using `help(action="version")` periodically (e.g., at session start).

### If the response includes [VERSION_NOTICE] or [VERSION_CRITICAL]:

**Tell the user** about the available update in a helpful, non-annoying way:
- Frame it as "new features and improvements available"
- Provide the update commands (user can choose their preferred method)
- Don't nag repeatedly - mention once, then only if user asks

### Update Commands (provide all options):

**macOS/Linux:**
```bash
curl -fsSL https://contextstream.io/scripts/setup.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://contextstream.io/scripts/setup.ps1 | iex
```

**npm (requires Node.js 18+):**
```bash
npm install -g @contextstream/mcp-server@latest
```

After updating, user should restart their AI tool.

---
<!-- END ContextStream -->
</contextstream>

## 🔎 EXTERNAL KNOWLEDGE: Context7 + Tavily

Use these two tools to answer questions that go beyond this repository's code.

### Tool Selection Order

1. **ContextStream `search()`** — this repo's code and docs
2. **Context7** — third-party library/framework documentation
3. **Tavily** — live web, recent CVEs, GitHub issues, news
4. **`web_fetch`** — fallback for a single known URL

---

### Context7 — Library & Framework Docs

Use Context7 **before guessing or trial-and-error** when you need accurate API signatures, configuration options, or usage examples for any external dependency (Angular, .NET, EF Core, Tailwind, JWT libs, etc.).

**Workflow:**
1. Resolve the library ID (skip if user gave `/org/project` directly):
   ```
   context7-resolve-library-id(libraryName="<official name>", query="<what you need>")
   ```
2. Query the docs:
   ```
   context7-query-docs(libraryId="/org/project", query="<specific question>")
   ```
3. If thin, retry once with `researchMode: true`. Max 3 calls per question.

Full reference: `.github/skills/context7/SKILL.md`

---

### Tavily — Live Web Search

Use Tavily for current information beyond the training cutoff: recent CVEs, GitHub issues, error messages, release notes, community patterns.

**Pick the right tool:**

| Tool | Use when |
|------|----------|
| `tavily-tavily_search` | General web search (default first choice) |
| `tavily-tavily_extract` | Fetch full content from a specific URL |
| `tavily-tavily_research` | Deep multi-source research (rate-limited, use sparingly) |
| `tavily-tavily_crawl` | Structured crawl of a docs site |

**Always cite source URLs** in your response when Tavily provides the answer.

Full reference: `.github/skills/tavily/SKILL.md`
