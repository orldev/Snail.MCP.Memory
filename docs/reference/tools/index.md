# Tools

12 tools in 2 families.
The names, descriptions and schemas are generated from the server itself, so they are exactly what the model receives in `tools/list`.

Long-term memory for an AI agent: records of decisions, lessons and preferences searched by meaning, per project and across projects, and a directed knowledge graph between projects. Everything runs on the machine — a single SQLite file and an in-process embedding model by default; the same server speaks PostgreSQL.

| Tool | Family | What it does |
| --- | --- | --- |
| [`init_project_memory`](memory.md#init_project_memory) | Projects and their records | Initializes memory for a project and returns a briefing from related experience: similar projects, their decisions and lessons. |
| [`list_projects`](memory.md#list_projects) | Projects and their records | Lists the projects that have memory. |
| [`remember`](memory.md#remember) | Projects and their records | Stores one record into long-term memory. |
| [`recall`](memory.md#recall) | Projects and their records | Searches memory by meaning. |
| [`forget`](memory.md#forget) | Projects and their records | Deletes records by id (several ids comma-separated). |
| [`review_memory`](memory.md#review_memory) | Projects and their records | Lists deletion candidates: durable records that recall has never returned and that grew old, plus tasks untouched for as long. |
| [`rename_project`](memory.md#rename_project) | Projects and their records | Renames a project everywhere: the registry, every record, every graph edge. |
| [`link_projects`](graph.md#link_projects) | The knowledge graph between projects | Creates or updates a directed, typed edge between two projects. |
| [`dismiss_link`](graph.md#dismiss_link) | The knowledge graph between projects | Rejects a pending link suggestion: the pair stops being proposed, permanently. |
| [`unlink_projects`](graph.md#unlink_projects) | The knowledge graph between projects | Removes edges between two projects regardless of direction. |
| [`sync_project_links`](graph.md#sync_project_links) | The knowledge graph between projects | Syncs machine-extracted relations into the graph from any source of truth, not only code: a project manifest (csproj, package.json), an Obsidian vault's wiki… |
| [`get_graph`](graph.md#get_graph) | The knowledge graph between projects | Answers the directed edges around the selected projects (from, type, to, note) without loading any records. |
