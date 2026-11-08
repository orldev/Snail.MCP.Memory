# The knowledge graph between projects

## <code>link_<wbr>projects</code>

Creates or updates a directed, typed edge between two projects. Call it by itself the moment a dependency becomes visible: one project uses another, holds its design, documents it. Only directed facts belong here — 'these two are similar' is not stored, recall finds similarity by meaning. A confirmed edge also settles the pair's pending link suggestion.

*only adds · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `from` | `string` | yes | — | Source project of the directed edge. |
| `to` | `string` | yes | — | Target project of the directed edge. |
| `type` | `uses` \| `depends_on` \| `design_of` \| `part_of` \| `implements` \| `documents` | yes | — | Edge type: uses, depends_on, design_of, part_of, implements or documents; all types are directed, from source to target. |
| `note` | `string` | — | `` | What exactly connects them — one line. Optional. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "from": {
      "description": "Source project of the directed edge.",
      "type": "string"
    },
    "to": {
      "description": "Target project of the directed edge.",
      "type": "string"
    },
    "type": {
      "description": "Edge type: uses, depends_on, design_of, part_of, implements or documents; all types are directed, from source to target.",
      "type": "string",
      "enum": [
        "uses",
        "depends_on",
        "design_of",
        "part_of",
        "implements",
        "documents"
      ]
    },
    "note": {
      "description": "What exactly connects them \u2014 one line. Optional.",
      "type": "string",
      "default": ""
    }
  },
  "required": [
    "from",
    "to",
    "type"
  ]
}
```

</details>

## <code>dismiss_<wbr>link</code>

Rejects a pending link suggestion: the pair stops being proposed, permanently. Use it when a mentioned project is not actually related; when the relation is real, confirm it with link_projects instead.

*only adds · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `from` | `string` | yes | — | First project of the suggested pair. |
| `to` | `string` | yes | — | Second project of the suggested pair. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "from": {
      "description": "First project of the suggested pair.",
      "type": "string"
    },
    "to": {
      "description": "Second project of the suggested pair.",
      "type": "string"
    }
  },
  "required": [
    "from",
    "to"
  ]
}
```

</details>

## <code>unlink_<wbr>projects</code>

Removes edges between two projects regardless of direction. Empty type removes every edge between them.

*may overwrite or delete · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `from` | `string` | yes | — | First project. |
| `to` | `string` | yes | — | Second project. |
| `type` | `string` | — | `` | Edge type. Empty — remove every edge between these projects. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "from": {
      "description": "First project.",
      "type": "string"
    },
    "to": {
      "description": "Second project.",
      "type": "string"
    },
    "type": {
      "description": "Edge type. Empty \u2014 remove every edge between these projects.",
      "type": "string",
      "default": ""
    }
  },
  "required": [
    "from",
    "to"
  ]
}
```

</details>

## <code>sync_<wbr>project_<wbr>links</code>

Syncs machine-extracted relations into the graph from any source of truth, not only code: a project manifest (csproj, package.json), an Obsidian vault's wiki-links, an XML registry. The caller parses the source and passes the relations; the server upserts them as auto edges (note 'auto: &lt;source&gt;'). Manual edges are never overwritten, an auto edge that disappeared from its source is only marked stale — never deleted — and a target with no memory is skipped and reported. Idempotent: the same payload twice changes nothing.

*only adds · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `projectName` | `string` | yes | — | Project name — the identifier the memory was created with. Case-insensitive. |
| `links` | `array` | yes | — | The relations extracted from the source, each directed from this project to its 'to'. |
| `source` | `string` | yes | — | What was parsed to get them — csproj, package.json, obsidian, xml, … — stamped into the auto note. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "projectName": {
      "description": "Project name \u2014 the identifier the memory was created with. Case-insensitive.",
      "type": "string"
    },
    "links": {
      "description": "The relations extracted from the source, each directed from this project to its \u0027to\u0027.",
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "to": {
            "description": "Target project of the edge; the source is the record\u0027s project.",
            "type": "string"
          },
          "type": {
            "description": "Edge type; directed from the record\u0027s project to \u0027to\u0027.",
            "type": "string",
            "enum": [
              "uses",
              "depends_on",
              "design_of",
              "part_of",
              "implements",
              "documents"
            ]
          },
          "note": {
            "description": "What exactly connects them \u2014 one line. Optional.",
            "type": "string",
            "default": ""
          }
        },
        "required": [
          "to",
          "type"
        ]
      }
    },
    "source": {
      "description": "What was parsed to get them \u2014 csproj, package.json, obsidian, xml, \u2026 \u2014 stamped into the auto note.",
      "type": "string"
    }
  },
  "required": [
    "projectName",
    "links",
    "source"
  ]
}
```

</details>

## <code>get_<wbr>graph</code>

Answers the directed edges around the selected projects (from, type, to, note) without loading any records. Empty projectName answers the whole graph.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `projectName` | `string` | — | `` | Project name, or several joined by '+'. Empty — the whole graph. |
| `depth` | `integer` | — | `1` | How many edge hops to expand (1-5). |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "projectName": {
      "description": "Project name, or several joined by \u0027\u002B\u0027. Empty \u2014 the whole graph.",
      "type": "string",
      "default": ""
    },
    "depth": {
      "description": "How many edge hops to expand (1-5).",
      "type": "integer",
      "default": 1
    }
  }
}
```

</details>

