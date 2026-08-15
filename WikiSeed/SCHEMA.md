# SCHEMA — NINA LLM Wiki (second brain)

Operating spec for every writer (plugins, agents, humans). Read before writing.

## Three levels

| Level   | Folder      | Who writes                        | Mutable |
|---------|-------------|-----------------------------------|---------|
| Sources | `raw/`      | plugins (append-only) and humans  | append-only, never edited |
| Wiki    | `wiki/`     | the ingest agent (and humans)     | yes     |
| Schema  | `SCHEMA.md` | humans                            | yes     |

- `raw/` holds immutable observations: daily digests from AI Weather, consented
  notes from the AI Assistant chat, session extracts. One file per writer per day
  (`raw/<writer>-YYYY-MM-DD.md`). Never rewrite past entries.
- `wiki/` holds consolidated knowledge, produced from `raw/` by the ingest agent:
  - `wiki/entities/` — one page per named thing: each hardware component, each
    observing site, software.
  - `wiki/concepts/` — techniques, recurring problems with their solutions, ideas.
  - `wiki/syntheses/` — cross-cutting analyses (e.g. seasonal sky patterns).
- `index.md` is the catalog (what exists, one hook line per page).
  `log.md` is the chronology (what happened, one line per change).

## Page format (wiki/)

Frontmatter + TL;DR + body + links:

```markdown
---
title: Readable Title
type: entity | concept | synthesis
created: YYYY-MM-DD
updated: YYYY-MM-DD
tags: [tag1, tag2]
---

# Readable Title

> TL;DR in 1-2 lines.

## Body sections...

## Collegamenti
- See also: [[other-page-slug]]
```

- Files are `kebab-case.md`. Wiki-links use `[[slug]]`.
- Every non-obvious claim about hardware behavior cites its origin: a `raw/` file
  and date.
- Contradictions are never silently resolved: keep both claims, cite both, tag the
  page with `#to-resolve`.

## Rules for the in-session assistant

1. Search the wiki before answering questions about the user's equipment, site, or
   recurring problems. Wiki facts beat general knowledge; cite the page used.
2. Writes go ONLY to `raw/` via wiki_append, and ONLY after the user explicitly
   agreed to remember the fact.
3. Never edit `wiki/` pages in-session: consolidation is the ingest agent's job.
