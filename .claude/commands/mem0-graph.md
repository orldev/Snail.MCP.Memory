---
description: Show the knowledge-graph edges around a project, without loading any records.
argument-hint: [project] [depth]
disable-model-invocation: true
allowed-tools: mcp__mem0__get_graph
---

Show the knowledge graph: $ARGUMENTS

Call `get_graph` with `projectName` from the arguments (empty means the whole graph) and `depth` when a number is given (default 1).

Use MCP tools exclusively — no bash, no reading files. The server answers JSON: print `data.edges` as a Markdown table with the columns From, Relation, To, Note (one row per edge, `type` as the relation, an empty cell for a missing note), or `data.note` when there are no edges, with no commentary before or after it. If the answer has `"ok":false`, print its `error` verbatim instead.
