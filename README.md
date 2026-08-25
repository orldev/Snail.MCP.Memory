# Snail.MCP.Memory

> 📖 **Docs & site:** [orldev.github.io/Snail.MCP.Memory](https://orldev.github.io/Snail.MCP.Memory) (source in [docs/](docs/) — enable GitHub Pages: *Settings → Pages → Deploy from a branch → `main` / `docs`*)

Long-term memory for your AI agent. Connect this MCP server to Claude Code, Claude Desktop, VS Code or any other MCP client — and the agent starts remembering your decisions, lessons and preferences between sessions, across projects, in your language.

**What changes for you once it is connected:**

- You clear the chat, come back tomorrow — the agent recalls what was decided and why.
- You start a new project — the agent recognizes "I have seen a project like this" and briefs itself with the lessons and decisions from similar past work, so old mistakes are not repeated.
- You ask "what problems did we have with X?" — the answer comes from *meaning*, not keywords: memory is searched with a multilingual embedding model, a question in Russian finds a lesson recorded in English and vice versa.
- Memory keeps itself clean: near-duplicates are refused, short-lived events expire, stale records are nominated for deletion — but nothing durable is ever deleted without your confirmation.

Everything runs on your machine: a single SQLite file and an in-process embedding model by default, no accounts, no cloud, no API keys. When you outgrow one machine, the same server speaks PostgreSQL.

## Quick start

**1. Install** (needs the [.NET 10 SDK](https://dotnet.microsoft.com/download)):

```bash
dotnet tool install --global Snail.MCP.Memory --prerelease
```

This gives you the `snail-mcp-memory` command (`~/.dotnet/tools` must be on `PATH` — the .NET installer puts it there). Update later with `dotnet tool update --global Snail.MCP.Memory --prerelease`.

*Alternative, no install:* the SDK ships the `dnx` runner — `dnx Snail.MCP.Memory --yes --prerelease` fetches the package from NuGet on first use, caches it and runs it.

**2. Connect** it to your client under the name `mem0` (details per client below):

```bash
claude mcp add mem0 --scope user -- snail-mcp-memory
```

**3. Use it.** In your next session say something like:

> init memory for this project — we are building a REST API in Go with PostgreSQL

The agent calls `init_project_memory`, and if your memory already knows similar projects, the reply includes a briefing: which past projects look alike and what lessons they carry. From then on the agent saves decisions and lessons as you work and recalls them when they matter.

On the very first semantically-searched call the server downloads the embedding model (~123 MB, once, into `~/.snail-mcp-memory/models/`). Until it arrives — and on machines where it never can — everything still works, with text search instead of semantic search.

## Connecting to a client

The server is a stdio MCP server: every client config boils down to "run the server command" — `snail-mcp-memory` (the global tool), or `dnx Snail.MCP.Memory --yes --prerelease` to skip installation. The examples below register it as `mem0`; the name is yours to choose, but the slash commands in this repo pre-approve tools under `mem0`. Zero configuration is required — settings only change the defaults.

### Claude Code

```bash
claude mcp add mem0 --scope user -- snail-mcp-memory
```

Or in `.mcp.json` at the project root:

```json
{
  "mcpServers": {
    "mem0": {
      "command": "snail-mcp-memory"
    }
  }
}
```

Running through `dnx` instead: `"command": "dnx", "args": ["Snail.MCP.Memory", "--yes", "--prerelease"]`.

### Claude Desktop

Config file: macOS — `~/Library/Application Support/Claude/claude_desktop_config.json`, Windows — `%APPDATA%\Claude\claude_desktop_config.json`.

```json
{
  "mcpServers": {
    "mem0": {
      "command": "snail-mcp-memory"
    }
  }
}
```

If the client cannot find the command, use the full path: `/Users/<you>/.dotnet/tools/snail-mcp-memory` (with `dnx` — its full path next to `dotnet`).

### VS Code

`.vscode/mcp.json`:

```json
{
  "servers": {
    "mem0": {
      "type": "stdio",
      "command": "snail-mcp-memory"
    }
  }
}
```

### Every setting in one config

All settings are optional environment variables. This example lists **all of them** with their default values — copy it and keep only the lines you change:

```json
{
  "mcpServers": {
    "mem0": {
      "command": "snail-mcp-memory",
      "env": {
        "SNAIL_MCP_MEMORY_PROVIDER": "Sqlite",
        "SNAIL_MCP_MEMORY_DIRECTORY": "~/.snail-mcp-memory",
        "SNAIL_MCP_MEMORY_CONNECTION_STRING": "",
        "SNAIL_MCP_MEMORY_WORKSPACE": "",
        "SNAIL_MCP_MEMORY_EMBEDDING_ENABLED": "true",
        "SNAIL_MCP_MEMORY_EMBEDDING_MODEL_PATH": "~/.snail-mcp-memory/models/model.onnx",
        "SNAIL_MCP_MEMORY_EMBEDDING_TOKENIZER_PATH": "~/.snail-mcp-memory/models/sentencepiece.bpe.model",
        "SNAIL_MCP_MEMORY_EMBEDDING_MODEL_URL": "https://huggingface.co/Xenova/multilingual-e5-small/resolve/main/onnx/model_quantized.onnx",
        "SNAIL_MCP_MEMORY_EMBEDDING_TOKENIZER_URL": "https://huggingface.co/intfloat/multilingual-e5-small/resolve/main/sentencepiece.bpe.model",
        "SNAIL_MCP_MEMORY_EMBEDDING_DIMENSIONS": "384",
        "SNAIL_MCP_MEMORY_DUPLICATE_THRESHOLD": "0.95",
        "SNAIL_MCP_MEMORY_SIMILARITY_FLOOR": "0.75",
        "SNAIL_MCP_MEMORY_IDLE_TIMEOUT_MINUTES": "60"
      }
    }
  }
}
```

The same keys can live in a JSON file instead — `~/.snail-mcp-memory.json` (also read: `SNAIL_MCP_MEMORY_CONFIG` path, `./.snail-mcp-memory.json`, `./snail-mcp-memory.json`, `~/.config/snail-mcp-memory/config.json`) — with bare names inside:

```json
{
  "Provider": "Sqlite",
  "Workspace": "backend",
  "SimilarityFloor": "0.8"
}
```

Environment variables win over the file.

### What each setting means

| Setting | Default | Meaning |
|---|---|---|
| `PROVIDER` | `Sqlite` | Storage backend: `Sqlite` (single local file, [sqlite-vec](https://github.com/asg017/sqlite-vec)) or `Postgres` (server database, [pgvector](https://github.com/pgvector/pgvector)) |
| `DIRECTORY` | `~/.snail-mcp-memory` | Where the database and the models live; understands a leading `~` |
| `CONNECTION_STRING` | — | SQLite: a file path or `Data Source=...` (default `{Directory}/memory.db`). PostgreSQL: the full connection string, **required** |
| `WORKSPACE` | *(empty)* | Partition of a shared database: your writes are tagged with it, your reads see only it. Empty — the whole base. See [Sharing one database](#sharing-one-database) |
| `EMBEDDING_ENABLED` | `true` | `false` turns semantic search off; records are stored without vectors and recall falls back to text matching |
| `EMBEDDING_MODEL_PATH` | `{Directory}/models/model.onnx` | Where the ONNX model lives; downloaded on first use |
| `EMBEDDING_TOKENIZER_PATH` | `{Directory}/models/sentencepiece.bpe.model` | Where the SentencePiece tokenizer lives; downloaded on first use |
| `EMBEDDING_MODEL_URL` / `EMBEDDING_TOKENIZER_URL` | multilingual-e5-small | Where the files are downloaded from when missing |
| `EMBEDDING_DIMENSIONS` | `384` | Vector length of the configured model — change only together with the model |
| `DUPLICATE_THRESHOLD` | `0.95` | Similarity above which `remember` refuses a near-duplicate and offers to supersede the old record instead |
| `SIMILARITY_FLOOR` | `0.75` | Similarity below which a search hit is dropped. Raise to ~`0.8` if recall feels noisy, lower if it misses things |
| `IDLE_TIMEOUT_MINUTES` | `60` | Minutes without a tool call before an orphaned server process shuts itself down |

## What the agent can do with it

You normally never call these yourself — the agent does, guided by the tool descriptions and by the memory rules you wire in (see [Integration](#integration-copy-two-things-from-this-repo)). Knowing them helps you phrase requests.

| Tool | What it does |
|---|---|
| `init_project_memory` | registers a project and returns the experience briefing; without a name, uses the folder the client was started in; `description` finds similar past projects, `seedFrom` preloads named donors |
| `remember` | stores one typed record — `fact`, `decision`, `lesson`, `preference`, `reference`, `task`, `event`, `note`; refuses a near-duplicate (supersede with `replaceId`, keep both with `force`) |
| `recall` | semantic search; empty project — the whole memory (that is how experience crosses projects), empty query — an overview of the newest records |
| `forget` | deletes records by id, several ids comma-separated |
| `review_memory` | nominates stale records for deletion; the deletion itself stays a confirmed `forget` |
| `list_projects` | the projects that have memory, optionally narrowed by a name `prefix` |
| `rename_project` | renames a project everywhere: registry, records, edges |
| `link_projects` | a directed, typed edge between projects: `uses`, `depends_on`, `design_of`, `part_of`, `implements`, `documents` |
| `unlink_projects` | removes the edges of a project pair |
| `get_graph` | the edges around a project, depth 1–5, without loading records |

Symmetric "these are related" links are deliberately not stored: vector similarity derives them at query time.

## Integration: copy two things from this repo

The protocol carries the tool descriptions — enough for basic use. The full integration is **two files you copy yourself** from git into your agent's setup; nothing is installed automatically:

1. **Slash commands** — [.claude/commands/](.claude/commands/) holds `mem0-init`, `mem0-recall`, `mem0-remember`, `mem0-link`, `mem0-graph`, `mem0-list`: manual one-shot shortcuts with the `mem0` tools pre-approved (`allowed-tools`), so they run without permission prompts. Copy them into `~/.claude/commands/` (globally) or `<project>/.claude/commands/`:

   ```bash
   cp .claude/commands/mem0-*.md ~/.claude/commands/
   ```

   If you registered the server under a name other than `mem0`, adjust the `mcp__mem0__` prefix inside the files to match.

2. **Memory rules for the agent** — [server-instructions.md](server-instructions.md) is the full instruction set: the kind vocabulary, when to save without being asked, when to search before proposing, how to self-heal wrong records. Paste its content into your agent's instruction file — `~/.claude/CLAUDE.md` for Claude Code, the system prompt of a custom agent, or wherever your client keeps standing instructions. The agent works without it, but with it the memory maintains itself: the rules are what turns the tools into a habit.

## Sharing one database

Two situations, two mechanisms:

- **Several projects, one person** — nothing to configure. Projects live side by side in one database; that is what makes cross-project experience transfer work.
- **Several people (or separate directions of work), one database** — give each their own `WORKSPACE`. Everything a server writes is tagged with its workspace and its reads never leave it, so teammates do not see each other's memory. A server with an *empty* workspace reads the whole base — useful for an overview across all of them.

## PostgreSQL

For a shared or backed-up memory, point the server at PostgreSQL with the [pgvector](https://github.com/pgvector/pgvector) extension available (the `pgvector/pgvector` Docker images ship it):

```json
"env": {
  "SNAIL_MCP_MEMORY_PROVIDER": "Postgres",
  "SNAIL_MCP_MEMORY_CONNECTION_STRING": "Host=db.example.com;Database=memory;Username=snail;Password=..."
}
```

Nothing else changes — same tools, same behavior, tables and vector indexes are created on first use.

## Good to know

- **Language**: record content is stored in the language of your conversation; the embedding model is multilingual, so search works across languages.
- **Offline**: after the one-time model download the server needs no network at all.
- **Lifecycle**: `event` records expire after 90 days; durable kinds are only ever deleted by an explicit, confirmed `forget`.
- **Housekeeping**: an orphaned server process shuts itself down after an hour of silence (`IDLE_TIMEOUT_MINUTES`).
- **Your data** is yours: one SQLite file at `~/.snail-mcp-memory/memory.db`, inspectable with any SQLite browser.

## Development

```bash
dotnet test          # SQLite suite; PostgreSQL contract tests run when a Docker daemon is reachable (Testcontainers, pgvector image)
dotnet pack src -c Release -o out
```
