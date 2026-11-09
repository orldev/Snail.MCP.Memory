Universal long-term memory: typed records searched by meaning, plus a directed knowledge graph between projects. mem0 (`mcp__mem0__*`) is the ONLY long-term memory — never write to file-based auto-memory (`~/.claude/projects/*/memory/`, MEMORY.md). Save autonomously as things happen — a decision, a lesson, a changed fact, a task — do not wait to be asked; hooks add the recall step at session start and a save checkpoint at stop.

LANGUAGE. Kinds, edge types, ids — English; record content — the conversation's language.

RECORDS. remember with a kind: fact, decision, lesson (what happened, why, how to avoid), preference, reference, task, event (expires by age), note. One true value: on a duplicate refusal (type duplicate, the similar record as data) — supersede (replaceId), store anyway (force: true), or stop; when knowledge changes, replace the old record, never add a second opinion. Fix or forget wrong records on sight.

RECALL. Empty projectName searches the whole memory — use it before proposing an architecture, stack or fix. projectName with an empty query returns the overview — the context-restore call.

GRAPH. Directed edges, closed types: uses, depends_on, design_of, part_of, implements, documents. Similarity is not an edge — recall derives it. A relation visible in a record: pass links to remember — record and edges in one call. Machine-extracted relations (a manifest, Obsidian wiki-links, an XML registry): sync_project_links — auto edges that never overwrite manual ones; gone from the source means marked stale, never deleted.

PENDING. A record mentioning a known but unlinked project queues a suggestion; it returns with every project-scoped recall and init until resolved: link_projects confirms (pick type and direction), dismiss_link rejects permanently. No third state — resolve on sight. A background audit backfills mentions older than the mentioned project.

ANSWERS. Every answer is JSON with an ok flag: data on success; on ok false read error and hint, type names the refusal (notFound, duplicate, invalidId, …) — change the call instead of repeating it.

LIFECYCLE. Only event expires by itself. review_memory nominates stale records; confirm with the user, then forget. Deletion is permanent. A whole project — records, edges, suggestions, registration — goes only on the user's request, in two calls: forget_project counts and deletes nothing; show the counts, and call again with confirm: true once they agree.

NAMES. A project is named after its root folder; init_project_memory without a name resolves it and refuses generic folders (design, projects, work, src). Unsure of a name — list_projects, do not guess. rename_project renames everywhere.

BRIEFING. init_project_memory with description returns the decisions and lessons of similar projects; seedFrom (names joined by '+') preloads named donors. Read the briefing before proposing anything.

COMMANDS. mem0-init, mem0-recall, mem0-remember, mem0-link, mem0-graph, mem0-list: use the memory tools exclusively, answer in the command's exact format, no reasoning, no preamble.
