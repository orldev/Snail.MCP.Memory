---
description: Store one fact into long-term memory as a typed record.
argument-hint: [project] <what to remember>
disable-model-invocation: true
allowed-tools: mcp__mem0__remember
---

Store this in memory: $ARGUMENTS

If the text starts with a project name, use it; otherwise use the current project.

Pick the `kind` from the canonical core — `fact` (current state of things), `decision` (what was chosen and why), `lesson` (a mistake: what happened, why, how to avoid), `preference` (how I like things done), `reference` (external URL or ticket), `task` (a work item), `event` (something that happened, expires with time), `note` (anything else).

Call `remember` ONCE. If the server answers that a very similar record exists, decide: the old record is outdated — call `remember` again with `replaceId`; both are needed — call `remember` again with `force: true`; nothing new — stop.

Use MCP tools exclusively — no bash, no reading files. Answer with one line and nothing else, in the language I write in:

"Recorded [kind] (id ...)."

If the server refused because of a similar record and you stopped, say what exists instead.
