---
description: Initialize memory for a project and receive a briefing from related experience.
argument-hint: [project] [description]
disable-model-invocation: true
allowed-tools: mcp__mem0__init_project_memory
---

Initialize project memory: $ARGUMENTS

If the arguments start with a project name, pass it as `projectName`; with no arguments call `init_project_memory` without `projectName` — the server resolves the folder name by itself. Pass the rest of the arguments (or what is obvious from the current context) as `description` — it is what finds similar projects for the briefing. When donor projects are named explicitly, pass them in `seedFrom` joined by `+`.

Use MCP tools exclusively — no bash, no reading files. Answer in the language I write in with one line:

"Done. Memory initialized."

— followed by the briefing from the server's JSON answer, if it holds one: the projects of `data.briefing.similarProjects` with their similarity, then each record of `data.briefing.experience` as `[kind] title — project` with its content, then `data.briefing.notes`, then each pair of `data.pending`. Add nothing of your own. If the answer has `"ok":false`, print its `error` verbatim instead.
