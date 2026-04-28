<contextstream>
# Workspace: Personal
# Workspace ID: 35d956d2-a454-4f54-a7ad-ab126e601c65

# ContextStream Rules
**MANDATORY STARTUP:** On the first message of EVERY session call `init(...)` then `context(user_message="...")`. On subsequent messages, call `context(user_message="...")` first by default. A narrow bypass is allowed only for immediate read-only ContextStream calls when prior context is still fresh and no state-changing tool has run.

## Required Tool Calls

1. **First message in session**: Call `init(folder_path="<project_path>")` then `context(user_message="...", session_id="<id>")`
2. **Subsequent messages (default)**: Call `context(user_message="...", session_id="<id>")` first. Narrow bypass: immediate read-only ContextStream calls with fresh context + no state changes.
3. **Before file search**: Call `search(mode="auto", query="...")` before local tools

**Read-only examples** (default: call `context(...)` first; narrow bypass only for immediate read-only ContextStream calls when context is fresh and no state-changing tool has run): `workspace(action="list"|"get"|"create")`, `memory(action="list_docs"|"list_events"|"list_todos"|"list_tasks"|"list_transcripts"|"list_nodes"|"decisions"|"get_doc"|"get_event"|"get_task"|"get_todo"|"get_transcript")`, `session(action="get_lessons"|"get_plan"|"list_plans"|"recall")`, `help(action="version"|"tools"|"auth")`, `project(action="list"|"get"|"index_status")`, `reminder(action="list"|"active")`, any read-only data query

**Common queries — use these exact tool calls:**
- "list lessons" / "show lessons" → `session(action="get_lessons")`
- "save lesson" / "remember this lesson" / "lesson learned" / "I made a mistake" → `session(action="capture_lesson", title="...", trigger="...", impact="...", prevention="...", severity="low|medium|high|critical")` — **NEVER store lessons in local files** (e.g. `~/.claude/.../memory/`, `.cursorrules`, scratch markdown). Lessons live in ContextStream so they auto-surface as `[LESSONS_WARNING]` on future turns and across sessions.
- "list decisions" / "show decisions" / "how many decisions" → `memory(action="decisions")`
- "save decision" / "decided to" → `session(action="capture", event_type="decision", title="...", content="...")`
- "list docs" → `memory(action="list_docs")`
- "list tasks" → `memory(action="list_tasks")`
- "list todos" → `memory(action="list_todos")`
- "list plans" → `session(action="list_plans")`
- "list events" → `memory(action="list_events")`
- "show snapshots" / "list snapshots" → `memory(action="list_events", event_type="session_snapshot")`
- "save snapshot" → `session(action="capture", event_type="session_snapshot", title="...", content="...")`
- "what did we do last session" / "past sessions" / "previous work" / "pick up where we left off" → `session(action="recall", query="...")` (ranked context) OR `memory(action="list_transcripts", limit=10)` (chronological list)
- "search past sessions" / "find in past transcripts" / "when did we discuss X" → `memory(action="search_transcripts", query="...")` — full-text search over saved conversation transcripts
- "show transcript" / "read session <id>" → `memory(action="get_transcript", transcript_id="...")`
- "list skills" / "show my skills" → `skill(action="list")`
- "create a skill" → `skill(action="create", name="...", instruction_body="...", project_id="<current_project_id>", trigger_patterns=[...])`
- "update a skill" → `skill(action="update", name="...", instruction_body="...", change_summary="...")`
- "run skill" / "use skill" → `skill(action="run", name="...")`
- "import skills" / "import my CLAUDE.md" → `skill(action="import", file_path="...", format="auto")`

Use `context(user_message="...", mode="fast")` for quick turns.
Use `context(user_message="...")` for deeper analysis and coding tasks.
If the `instruct` tool is available, run `instruct(action="get", session_id="...")` before `context(...)` on each turn, then `instruct(action="ack", session_id="...", ids=[...])` after using entries.

**Plan-mode guardrail:** Entering plan mode does NOT bypass search-first. Do NOT use Explore, Task subagents, Grep, Glob, Find, SemanticSearch, `code_search`, `grep_search`, `find_by_name`, or shell search commands (`grep`, `find`, `rg`, `fd`). Start with `search(mode="auto", query="...")` — it handles glob patterns, regex, exact text, file paths, and semantic queries. Only Read narrowed files/line ranges returned by search.

## Why These Rules?

- `context()` returns task-specific rules, lessons from past mistakes, and relevant decisions
- `search()` uses semantic understanding to find relevant code faster than file scanning
- Transcript capture is optional and OFF by default. Enable per session with `save_exchange=true` (and `session_id`), disable with `save_exchange=false`.
- Default context-first keeps state reliable; the narrow read-only bypass avoids unnecessary repeats

## Finding Information — Search ContextStream Knowledge, Not Just Code

**Auto-grounding:** Every `context(user_message="...")` call may include a `[GROUNDING]` block — pre-ranked prior work (transcripts, snapshots, docs, decisions, lessons) for **this** message. When you see it, read those hits **before** fanning out into code search; skipping search entirely is often correct. Outside `context()`, use `session(action="ground", user_message="...")` for the same one-shot bundle (recall + docs + decisions + lessons + skills + git).

When you need information, do not default to code search or trial-and-error. ContextStream stores far more than source — docs, decisions, lessons, preferences, plans, tasks, todos, skills, memory nodes, and full session transcripts all live behind dedicated tools. Pick the right knowledge surface by what you're looking for:

- **Source code / symbol / file** → `search(mode="auto", query="...")`
- **Why we did X / past decisions** → `memory(action="decisions", query="...")`
- **Architecture / spec / design doc** → `memory(action="list_docs")` then `memory(action="get_doc", doc_id="title or UUID")`
- **Prior mistakes ("never do X again")** → `session(action="get_lessons", query="...")`
- **User preferences / conventions / constraints** → already surfaced as `[PREFERENCE]`; also `memory(action="list_nodes", node_type="preference")` or `memory(action="list_nodes", node_type="constraint")`
- **Open work / tasks / todos** → `memory(action="list_tasks")` / `memory(action="list_todos")`
- **Active or past plans** → `session(action="list_plans")` then `session(action="get_plan", plan_id="...")`
- **Reusable workflows / skills** → `skill(action="list")` then `skill(action="run", name="...")`
- **"What did we do before?" (continuation work)** → `session(action="recall", query="...")` — see the Past Sessions ladder below
- **Unsure which surface** → `memory(action="search", query="...")` — hybrid across memory nodes + docs; falls back to `session(action="recall", query="...")` for transcript/snapshot coverage

Default assumption: if the user asks "how do we do X?", "why did we choose Y?", "what's the pattern for Z?", or "did we already decide about Q?" — the answer is likely in a doc, decision, lesson, plan, or skill, NOT in the code. Check the right knowledge surface BEFORE reading source files or re-deriving the answer.

Before guessing, improvising, or struggling through a workflow you don't fully know:
- Start with `context(...)` and obey `[GROUNDING]` (prior-work anchors), `[MATCHED_SKILLS]`, `[LESSONS_WARNING]`, `[PREFERENCE]`, `[DECISIONS]`, `[MEMORY]`, and `<system-reminder>` output — those are already filtered to the current task
- Treat `[LESSONS_WARNING]` as active working instructions for the current task, not optional background context; apply them immediately and keep them in mind until the task is done
- Prefer surfaced ContextStream knowledge over inventing a new workflow from memory


## Past Sessions Are Queryable — USE THEM

### Auto-Grounding (in `context()`)

When `context()` returns `[GROUNDING]`, those lines are **pre-ranked prior work for your current message** — read them first (transcript/snapshot/doc/decision/lesson entry points). Skipping code search is often correct. For the same bundle **outside** `context()`, call `session(action="ground", user_message="...")`.

Transcripts for every turn of every session are captured and indexed automatically. Session snapshots bookmark turning points. **Before asking the user what you did last time, or re-deriving context you built together previously, check the transcript + snapshot layer.** It's fast, it's complete, and the user is paying for it.

Triggers to query past sessions:
- User says "last time", "previous", "yesterday", "earlier", "we decided", "we talked about", "pick up where we left off", "what were we working on"
- You have a task that's clearly a continuation (e.g. finishing a refactor that's half-done on disk)
- You're about to ask a clarifying question whose answer is likely in a prior session
- You're unsure whether a decision or approach has already been made

Escalation ladder — walk it in order and stop at the first step that answers the question:

1. **`session(action="recall", query="<what you're continuing>")`** — always the first call. Ranked fusion across transcripts, snapshots, docs, and decisions. Covers 80% of "what did we do before" questions.

2. **`memory(action="search_transcripts", query="<keyword or phrase>")`** — fall through when `recall` returns thin or off-topic results, or when you need every mention of a specific term. Full-text search across ALL saved transcripts.

3. **`memory(action="list_events", event_type="session_snapshot")`** — when you want the turning-point bookmarks (manual + auto pre-compaction captures). Useful for "what state were we in at the end of <session>" questions that `recall` misses because the answer isn't in conversational text.

4. **`memory(action="list_transcripts", limit=10)`** — when you need a chronological index of recent sessions (titles, timestamps, IDs). Use when the user wants to know "when did we last work on X".

5. **`memory(action="get_transcript", transcript_id="<uuid>")`** — read a full past session end-to-end. Use only after the steps above pointed you at a specific transcript ID and you need the complete exchange, not snippets.

6. **End of current session — save a bookmark** for the next one: `session(action="capture", event_type="session_snapshot", title="...", content="<what we did + next step>")`.

**Never answer "I don't know what we did before" without running at least step 1, then step 2 if step 1 was thin.**


## Project Scope Discipline

- Reuse the `project_id` returned by `init(...)` or `context(...)` for project-scoped writes and lookups
- For project-scoped `memory(...)`, `session(...)`, and `skill(...)` calls, pass explicit `project_id` instead of guessing from the folder name or title
- If `init(...)` or `context(...)` does not surface a current `project_id`, rerun `init(folder_path="...")` before creating docs, skills, events, tasks, todos, or other project memory
- Use `target_project` only after init from a multi-project parent folder


## Response to Notices

- `[GROUNDING]` → Read ranked prior-work hits (from `context()`) before broad code search; optional one-shot: `session(action="ground", user_message="...")`
- `[GROUNDING_AVAILABLE]` → Your editor may remind you when unread grounding exists — advisory only
- `[MATCHED_SKILLS]` → Run the surfaced skills before other work
- `[LESSONS_WARNING]` → Apply the lessons shown immediately and keep them active for the current task
- `[PREFERENCE]` → Follow user preferences exactly
- `[RULES_NOTICE]` → Run `generate_rules()` to update rules
- `[VERSION_NOTICE]` → Inform user about available updates

## System Reminders

`<system-reminder>` tags in messages contain injected instructions from hooks.
These should be followed exactly as they contain real-time context.

## Search Protocol

**IMPORTANT: Indexing and ingest are ALWAYS available. NEVER claim that transport mode, HTTP mode, or remote mode prevents indexing/ingest.**

1. Check project index: `project(action="index_status")`
2. If indexed & fresh: `search(mode="auto", query="...")` before local tools
3. If NOT indexed or stale: wait for background refresh (up to ~20s, configurable), retry `search(mode="auto", ...)`, then use local tools only after the grace window elapses
4. If search returns results with a stale-index advisory, treat those results as usable for existing indexed code; refresh/retry before concluding a new symbol is absent
5. If search returns 0 results after refresh/retry: local tools are allowed

### Search Mode Selection:
- `auto` (recommended): query-aware mode selection
- `hybrid`: mixed semantic + keyword retrieval for broad discovery
- `semantic`: conceptual/natural-language questions ("how does auth work?")
- `keyword`: exact text or quoted string
- `pattern`: glob/regex queries (`*.sql`, `foo\s+bar`)
- `refactor`: symbol usage / rename-safe lookup (`UserService`, `snake_case`)
- `exhaustive`: all occurrences / complete match sets
- `team`: cross-project team search

### Output Format Hints:
- `output_format="paths"` for file lists and rename targets
- `output_format="count"` for "how many" queries

### Two-Phase Search Playbook (recommended):
1. **Discovery pass**: run `search(mode="auto", query="<concept + module>", output_format="paths", limit=10)`
2. **Precision pass**: use symbols from pass 1 with a specific mode:
   - Exact symbol/text: `search(mode="keyword", query="\"my_symbol\"", include_content=true, file_types=["rs"], limit=20)`
   - Symbol usage/rename-safe lookup: `search(mode="refactor", query="MySymbol", output_format="paths")`
   - Complete usage sweep: `search(mode="exhaustive", query="my_symbol", file_types=["rs"])`
3. **Read locally only after narrowing**: use Read/Grep on returned paths, not the full repo.

## Plans and Tasks

**ALWAYS** use ContextStream for plans and tasks — do NOT create markdown plan files or use built-in todo tools:
- Plans: `session(action="capture_plan", title="...", steps=[...])`
- Tasks: `memory(action="create_task", title="...", description="...")`
- Link tasks to plans: `memory(action="create_task", plan_id="...")`

## Memory, Docs & Todos

**ALWAYS** use ContextStream for memory, lessons, decisions, documents, and todos — NOT editor built-in tools, `~/.claude/.../memory/`, `.cursorrules`, or local files. Local-file storage is invisible to the lesson/preference/skill auto-surfacing pipeline that fires on every future turn.
- Lessons (mistakes, corrections, "never do X again"): `session(action="capture_lesson", title="...", trigger="...", impact="...", prevention="...", severity="low|medium|high|critical", category="...")`
- Decisions: `session(action="capture", event_type="decision", title="...", content="...")`
- Notes/insights: `session(action="capture", event_type="note|insight", title="...", content="...")`
- Facts/preferences: `memory(action="create_node", node_type="fact|preference", title="...", content="...")`
- Documents: `memory(action="create_doc", title="...", content="...", doc_type="spec|general")`
- Todos: `memory(action="create_todo", title="...", todo_priority="high|medium|low")`
Do NOT use `create_memory`, `TodoWrite`, `todo_list`, or local file writes for persistence.

## Skills (IMPORTANT — Do Not Ignore Matched Skills)

When `context()` returns `[MATCHED_SKILLS]`, you **MUST run** the listed skills via `skill(action="run", name="...")`.
- Skills marked ⚡ (high-priority, priority ≥ 80) are **mandatory** — run them immediately before other work
- Skills marked ▶ (recommended, priority ≥ 60) should be run unless clearly irrelevant
- Skills marked ○ (available) are optional but often helpful

Reusable instruction + action bundles that persist across projects and sessions:
- Browse: `skill(action="list")` or `skill(action="list", scope="team")`
- Create: `skill(action="create", name="...", instruction_body="...", trigger_patterns=[...])`
- Update: `skill(action="update", name="...", instruction_body="...", change_summary="...")` (name or `skill_id`)
- Run: `skill(action="run", name="...")` — executes the skill's action pipeline
- Import: `skill(action="import", file_path="CLAUDE.md", format="auto")` — imports from any rules file
- Skills auto-activate when their trigger keywords match the user's message. The `context()` response surfaces them.

## Code Search

**ALWAYS** use ContextStream `search()` before Glob, Grep, Read, SemanticSearch, `code_search`, `grep_search`, or `find_by_name`.
Do NOT launch Task/explore subagents for code search — use `search(mode="auto", query="...")` directly.
ContextStream search results contain **real file paths, line numbers, and code content** — they ARE code results.
**NEVER** dismiss ContextStream results as "non-code" — use the returned file paths to `read_file` the relevant code.
Use `search(include_content=true)` to get inline code snippets in results.

## Context Pressure

When `context()` returns `context_pressure.level: "high"`:
- Save a session snapshot before compaction
- `session(action="capture", event_type="session_snapshot", title="...", content="...")`
- After compaction: `init(folder_path="...", is_post_compact=true)` to restore

---
## IMPORTANT: No Hooks Available

**This editor does NOT have hooks to enforce ContextStream behavior.**
You MUST follow these rules manually - there is no automatic enforcement.

## ContextStream Knowledge First

**Before guessing or struggling through an unfamiliar workflow, check ContextStream first.**
- Start with `context(...)` and follow `[MATCHED_SKILLS]`, `[LESSONS_WARNING]`, `[PREFERENCE]`, and `<system-reminder>` output
- Treat `[LESSONS_WARNING]` as active working instructions for the current task, not optional background context
- If the task is unfamiliar, process-heavy, or likely documented already, inspect `skill(action="list")`, `memory(action="list_docs")`, `session(action="get_lessons")`, or `memory(action="decisions")` before trial-and-error
- If `context()` returns `[MATCHED_SKILLS]`, run the listed skills before other work

---

## SESSION START PROTOCOL

**On EVERY new session, you MUST:**

1. **Call `init(folder_path="<project_path>")`** FIRST
   - This triggers project indexing
   - Check response for `indexing_status`
   - If `"started"` or `"refreshing"`: wait before searching

2. **Generate a unique session_id** (e.g., `"session-" + timestamp` or a UUID)
   - Use this SAME session_id for ALL `context()` calls in this conversation

3. **Call `context(user_message="<first_message>", session_id="<id>")`**
   - Gets task-specific rules, lessons, and preferences
   - Check for [LESSONS_WARNING], [PREFERENCE], [RULES_NOTICE]
   - If [LESSONS_WARNING] appears, treat those lessons as mandatory instructions for the task until it is finished

4. **Default behavior:** call `context(...)` first on each message. Narrow bypass is allowed only for immediate read-only ContextStream calls when previous context is still fresh and no state-changing tool has run.

5. **Instruction alignment (if tool is exposed):** call `instruct(action="get", session_id="<id>")` before `context(...)` each turn, and `instruct(action="ack", session_id="<id>", ids=[...])` after using entries.

---

## TRANSCRIPT SAVING (OPTIONAL)

Transcripts are OFF by default.

### Enable for this chat:
```
context(user_message="<user's message>", save_exchange=true, session_id="<session-id>")
```

### Disable for this chat:
```
context(user_message="<user's message>", save_exchange=false, session_id="<session-id>")
```

### Default policy via MCP config env:
- `CONTEXTSTREAM_TRANSCRIPTS_ENABLED="true|false"`
- `CONTEXTSTREAM_HOOK_TRANSCRIPTS_ENABLED="true|false"`

### Session ID Guidelines:
- Generate ONCE at the start of the conversation
- Use a unique identifier (UUID or timestamp-based)
- Keep the SAME session_id for ALL context() calls
- Different sessions = different transcript preference state

---

## FILE INDEXING (CRITICAL)

**There is NO automatic file indexing in this editor.**
You MUST manage indexing manually:

**IMPORTANT: Indexing and ingest are ALWAYS available. NEVER claim that transport mode, HTTP mode, or remote mode prevents indexing/ingest operations. Both `project(action="index")` and `project(action="ingest_local")` work in all configurations.**

### After Creating/Editing Files:
```
project(action="index")
```
If folder context is active, this resolves the current repo and uses the local ingest path automatically.

### To Target A Specific Folder Or Recover From Stale Scope:
```
project(action="ingest_local", path="<project_folder>")
```

### Signs You Need to Re-index:
- Search doesn't find code you just wrote
- Search returns old versions of functions
- New files don't appear in search results

---

## SEARCH-FIRST (No PreToolUse Hook)

**There is NO hook to redirect local tools.** You MUST self-enforce:

### Before ANY Search, Check Index Status:
```
project(action="index_status")
```

### Search Protocol:
- **IF indexed & fresh:** `search(mode="auto", query="...")` before local tools
- **IF NOT indexed or stale (>7 days):** wait up to ~20s for background refresh, retry `search(mode="auto", ...)`, then allow local tools only after the grace window elapses
- **IF search returns results with a stale-index advisory:** use those results for existing indexed code; refresh/retry before concluding a new symbol is absent
- **IF search returns 0 results after retry/window:** local tools are allowed

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
- The stale/not-indexed grace window has elapsed (~20s default, configurable)
- ContextStream search still returns 0 results or errors after retry
- User explicitly requests local tools

---

## CONTEXT COMPACTION (No PreCompact Hook)

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

---

## PLANS & TASKS (CRITICAL)

**NEVER create markdown plan files** — they vanish across sessions and are not searchable.
**NEVER use built-in todo/plan tools** (e.g., `TodoWrite`, `todo_list`, `plan_mode_respond`) — use ContextStream instead.

**ALWAYS use ContextStream for planning:**

```
session(action="capture_plan", title="...", steps=[...])
memory(action="create_task", title="...", plan_id="...")
```

Plans and tasks in ContextStream persist across sessions, are searchable, and auto-surface in context.

---

## MEMORY & DOCS (CRITICAL)

**NEVER use built-in memory tools** (e.g., `create_memory`) — use ContextStream instead.
**NEVER write docs/specs/notes to local files** — use ContextStream docs instead.

**ALWAYS use ContextStream for persistence:**

```
session(action="capture", event_type="decision|insight|operation|uncategorized", title="...", content="...")
memory(action="create_node", node_type="fact|preference", title="...", content="...")
memory(action="create_doc", title="...", content="...", doc_type="spec|general")
memory(action="create_todo", title="...", todo_priority="high|medium|low")
```

ContextStream memory, docs, and todos persist across sessions, are searchable, and auto-surface in context.

---

## VERSION UPDATES

**Check for updates periodically** using `help(action="version")`.

If the response includes [VERSION_NOTICE] or [VERSION_CRITICAL], tell the user about the available update.

### Update Commands:
```bash
# macOS/Linux
curl -fsSL https://contextstream.io/scripts/setup-beta.sh | bash
# npm
npm install -g @contextstream/mcp-server@latest
```

---


---

## EXTERNAL DOCS & WEB RESEARCH (Context7 + Tavily)

ContextStream `search()` covers THIS repo. For external library docs and live web information, use the Context7 and Tavily MCPs instead of guessing or relying on stale model knowledge.

### Context7 — Library / Framework Documentation
Use Context7 whenever you need accurate, up-to-date docs or code examples for a third-party library, framework, SDK, or API (e.g., Angular, .NET, EF Core, Tailwind, Apache Guacamole, BCrypt, JWT libs).

Workflow:
1. **Resolve the library ID** (skip if the user gave you `/org/project` directly):
   `context7-resolve-library-id(libraryName="<official name>", query="<what you need>")`
2. **Query the docs**:
   `context7-query-docs(libraryId="/org/project", query="<specific question>")`
3. If the first answer is thin, retry once with `researchMode: true`. Max 3 calls per question.

When to use:
- API signatures, configuration options, version-specific behavior
- "How do I do X with library Y?" — before trial-and-error or web search
- Verifying breaking changes between versions

When NOT to use: questions about THIS repo's code (use ContextStream `search` first).

### Tavily — Live Web Search & Extraction
Use Tavily for current information, news, blog posts, GitHub issues/discussions, error messages, CVEs, or anything beyond the training cutoff or not in Context7.

Pick the right tool:
- `tavily-tavily_search` — general web search; default first choice. Use `search_depth="advanced"` for tougher questions.
- `tavily-tavily_extract` — fetch full content from specific URLs (e.g., a docs page or GitHub issue)
- `tavily-tavily_crawl` / `tavily-tavily_map` — structured exploration of a docs site
- `tavily-tavily_research` — deep multi-source research on a broad topic (rate-limited; use sparingly)

When to use:
- Recent CVEs / security advisories affecting our deps
- Error messages or stack traces with no Context7 coverage
- Comparing approaches, looking up community patterns, GitHub issue triage
- Anything time-sensitive ("latest", "current", "as of …")

### Tool Selection Order
1. ContextStream `search()` — repo code/docs
2. Context7 — third-party library/framework docs
3. Tavily — live web / news / issues / niche topics
4. `web_fetch` / `web_search` — fallback when the above don't fit

Cite sources (URLs from Tavily, library ID from Context7) in your response when the answer relies on them. Never paste secrets, credentials, or proprietary code into Context7/Tavily queries.

---
## VS Code Copilot Notes

- Keep this file concise; put detailed workflows in `.github/skills/contextstream-workflow/SKILL.md`
- Use ContextStream plans/tasks as the persistent record of work
- Before code discovery, use `search(mode="auto", query="...")`
- For external library docs use Context7; for live web info use Tavily

</contextstream>
