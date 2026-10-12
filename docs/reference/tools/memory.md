# Projects and their records

## <code>init_<wbr>project_<wbr>memory</code>

Initializes memory for a project and returns a briefing from related experience: similar projects, their decisions and lessons. Call it when starting work on a project that has no memory yet. Pass description — it is what finds the similar projects; pass seedFrom with existing project names (joined by '+') to preload their experience explicitly. Without projectName the server names the project after its working folder and refuses generic folders (src, projects, work, design).

*only adds · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `projectName` | `string` | — | `` | Project name. Optional: when omitted, the server uses the name of the working folder the client was started in, refusing generic folders (src, projects, work, design). |
| `description` | `string` | — | `` | Short description of what the project will be — it is embedded and finds similar projects. Optional. |
| `seedFrom` | `string` | — | `` | Existing project names to preload experience from, joined by '+'. Optional. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "projectName": {
      "description": "Project name. Optional: when omitted, the server uses the name of the working folder the client was started in, refusing generic folders (src, projects, work, design).",
      "type": "string",
      "default": ""
    },
    "description": {
      "description": "Short description of what the project will be \u2014 it is embedded and finds similar projects. Optional.",
      "type": "string",
      "default": ""
    },
    "seedFrom": {
      "description": "Existing project names to preload experience from, joined by \u0027\u002B\u0027. Optional.",
      "type": "string",
      "default": ""
    }
  }
}
```

</details>

## <code>list_<wbr>projects</code>

Lists the projects that have memory. Call it before guessing how a project was named.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `prefix` | `string` | — | `` | Leading characters of the project name, case-insensitive. Empty — every project. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "prefix": {
      "description": "Leading characters of the project name, case-insensitive. Empty \u2014 every project.",
      "type": "string",
      "default": ""
    }
  }
}
```

</details>

## <code>remember</code>

Stores one record into long-term memory. Call it by itself whenever something worth keeping happens: a decision is made (kind 'decision'), a mistake is understood (kind 'lesson' — what happened, why, how to avoid), a stable fact changes (kind 'fact'), the user states a preference (kind 'preference'). When the server answers that a very similar record exists, do not retry blindly: either supersede it (replaceId) or store anyway (force: true). When an existing record turned out wrong or outdated, replace it with replaceId instead of adding a second opinion next to it. When the record itself reveals a relation to another project, pass links — the record and its edges land in one call. A mentioned project with no edge yet comes back as a pending link suggestion: confirm it with link_projects or reject it with dismiss_link, do not ignore it.

*may overwrite or delete · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `projectName` | `string` | yes | — | Project name — the identifier the memory was created with. Case-insensitive. |
| `content` | `string` | yes | — | The knowledge itself, in the language of the conversation. |
| `kind` | `string` | — | `` | Record kind. Canonical: fact, decision, lesson, preference, reference, note, task, event; other tags are allowed. Empty on recall — every kind; empty on remember — 'note'. |
| `title` | `string` | — | `` | Short label of the record. Optional. |
| `replaceId` | `string` | — | `` | Id of the record this one supersedes; the old record is deleted. Optional. |
| `force` | `boolean` | — | `false` | Store even when a very similar record exists. |
| `links` | `array?` | — | `null` | Graph edges to create together with the record — the atomic form of link_projects: each element links this record's project to its 'to' project. Optional. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "projectName": {
      "description": "Project name \u2014 the identifier the memory was created with. Case-insensitive.",
      "type": "string"
    },
    "content": {
      "description": "The knowledge itself, in the language of the conversation.",
      "type": "string"
    },
    "kind": {
      "description": "Record kind. Canonical: fact, decision, lesson, preference, reference, note, task, event; other tags are allowed. Empty on recall \u2014 every kind; empty on remember \u2014 \u0027note\u0027.",
      "type": "string",
      "default": ""
    },
    "title": {
      "description": "Short label of the record. Optional.",
      "type": "string",
      "default": ""
    },
    "replaceId": {
      "description": "Id of the record this one supersedes; the old record is deleted. Optional.",
      "type": "string",
      "default": ""
    },
    "force": {
      "description": "Store even when a very similar record exists.",
      "type": "boolean",
      "default": false
    },
    "links": {
      "description": "Graph edges to create together with the record \u2014 the atomic form of link_projects: each element links this record\u0027s project to its \u0027to\u0027 project. Optional.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "object",
          "null"
        ],
        "properties": {
          "to": {
            "description": "Target project of the edge; the source is the record\u0027s project.",
            "type": [
              "string",
              "null"
            ]
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
            "type": [
              "string",
              "null"
            ],
            "default": ""
          }
        },
        "required": [
          "to",
          "type"
        ]
      },
      "default": null
    }
  },
  "required": [
    "projectName",
    "content"
  ]
}
```

</details>

## <code>recall</code>

Searches memory by meaning. Call it before proposing an architecture, a stack or a fix — past decisions and lessons apply across projects, so leave projectName empty to search the whole memory (that is the default that transfers experience); set it to restore the context of one project. An empty query returns an overview of the newest records — use that to recover after a context reset. If a returned record is visibly wrong or outdated, fix the memory immediately: remember with replaceId, or forget. A project-scoped recall also lists the project's pending link suggestions until each is confirmed (link_projects) or rejected (dismiss_link).

*only adds · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `query` | `string` | — | `` | What to look for, in natural language. Empty — an overview of the newest records. |
| `projectName` | `string` | — | `` | Project name — the identifier the memory was created with. Case-insensitive. Empty — the whole memory (transfers experience across projects). |
| `kind` | `string` | — | `` | Record kind. Canonical: fact, decision, lesson, preference, reference, note, task, event; other tags are allowed. Empty on recall — every kind; empty on remember — 'note'. |
| `limit` | `integer` | — | `8` | How many records to return (1-50). |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "query": {
      "description": "What to look for, in natural language. Empty \u2014 an overview of the newest records.",
      "type": "string",
      "default": ""
    },
    "projectName": {
      "description": "Project name \u2014 the identifier the memory was created with. Case-insensitive. Empty \u2014 the whole memory (transfers experience across projects).",
      "type": "string",
      "default": ""
    },
    "kind": {
      "description": "Record kind. Canonical: fact, decision, lesson, preference, reference, note, task, event; other tags are allowed. Empty on recall \u2014 every kind; empty on remember \u2014 \u0027note\u0027.",
      "type": "string",
      "default": ""
    },
    "limit": {
      "description": "How many records to return (1-50).",
      "type": "integer",
      "default": 8
    }
  }
}
```

</details>

## <code>forget</code>

Deletes records by id (several ids comma-separated). Deletion is permanent; for bulk cleanups confirm the list with the user first — review_memory nominates candidates.

*may overwrite or delete · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `ids` | `string` | yes | — | Record id, or several ids comma-separated. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "ids": {
      "description": "Record id, or several ids comma-separated.",
      "type": "string"
    }
  },
  "required": [
    "ids"
  ]
}
```

</details>

## <code>review_<wbr>memory</code>

Lists deletion candidates: durable records that recall has never returned and that grew old, plus tasks untouched for as long. Candidates are nominations, not decisions — confirm with the user, then forget the confirmed ids.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `projectName` | `string` | — | `` | Project name — the identifier the memory was created with. Case-insensitive. Empty — the whole memory. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "projectName": {
      "description": "Project name \u2014 the identifier the memory was created with. Case-insensitive. Empty \u2014 the whole memory.",
      "type": "string",
      "default": ""
    }
  }
}
```

</details>

## <code>rename_<wbr>project</code>

Renames a project everywhere: the registry, every record, every graph edge. The old name stops existing.

*may overwrite or delete · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `oldName` | `string` | yes | — | Current name of the project. |
| `newName` | `string` | yes | — | New name of the project. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "oldName": {
      "description": "Current name of the project.",
      "type": "string"
    },
    "newName": {
      "description": "New name of the project.",
      "type": "string"
    }
  },
  "required": [
    "oldName",
    "newName"
  ]
}
```

</details>

