---
description: List the projects that have memory in the memory server.
argument-hint: [prefix]
disable-model-invocation: true
allowed-tools: mcp__mem0__list_projects
---

List projects in memory: $ARGUMENTS

With no arguments call `list_projects` once, with no prefix at all — that lists every project. Do not invent a prefix and do not guess one from the current folder. With an argument, pass it as `prefix`.

Use MCP tools exclusively — no bash, no reading files. Print the returned list verbatim and add nothing to it — no comments, no guesses about what a project is.
