---
description: Create or update a directed, typed edge between two projects.
argument-hint: <from> <type> <to> [note]
disable-model-invocation: true
allowed-tools: mcp__mem0__link_projects
---

Create a graph edge: $ARGUMENTS

Call `link_projects` with the source project, the type and the target project from the arguments (plus `note` when given).

The type must come from this closed list: `uses`, `depends_on`, `design_of`, `part_of`, `implements`, `documents`. Every type is directed, from source to target. Similarity is not linked — `recall` finds it by meaning. If the type given is anything else, do not guess — print the server error verbatim.

Use MCP tools exclusively — no bash, no reading files. Answer with one line and nothing else, in the language I write in:

"Link created: [from] --[type]--> [to]."

Say "Link updated: …" instead if the server reported an update.
