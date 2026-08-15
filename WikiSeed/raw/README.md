# raw/ — immutable observations

Append-only inputs for the ingest agent. One file per writer per day:

- `aiweather-YYYY-MM-DD.md` — daily weather digest written automatically by the
  AI Weather plugin (condition changes, coverage, safety transitions).
- `assistant-YYYY-MM-DD.md` — notes the user explicitly asked the AI Assistant
  to remember during chat.

Rules: writers only append, never edit past lines. Humans may drop additional
files here (session notes, log extracts). The ingest agent consolidates these
into `wiki/` pages and cites them by file name.
