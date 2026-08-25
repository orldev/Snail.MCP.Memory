---
description: Load context from memory - a project overview, or a semantic search on a question.
argument-hint: [project] [question]
disable-model-invocation: true
allowed-tools: mcp__mem0__recall
---

Load from memory: $ARGUMENTS

Call `recall` EXACTLY ONCE:

- With no question, pass the project name and an empty `query` — that returns the overview of the newest records.
- With a question, pass it as `query`. Keep `projectName` only when the answer must come from that project; an empty `projectName` searches the whole memory and is the right default for questions about approaches, stacks and past mistakes.

Use MCP tools exclusively — no bash, no reading files. Answer with one line and nothing else, in the language I write in:

"Context loaded. Ready."
