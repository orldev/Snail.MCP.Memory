---
description: Show the knowledge-graph edges around a project, without loading any records.
argument-hint: [project] [depth]
disable-model-invocation: true
allowed-tools: mcp__mem0__get_graph
---

Show the knowledge graph: $ARGUMENTS

Call `get_graph` with `projectName` from the arguments (empty means the whole graph) and `depth` when a number is given (default 1).

Use MCP tools exclusively — no bash, no reading files. Print the edge table from the server response as is, with no commentary before or after it.
