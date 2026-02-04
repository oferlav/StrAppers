# Mentor API Response – Markdown Rendering

## Backend behavior

The mentor chat API returns a `response` field that is **markdown**, not plain text. The backend:

- Sends fenced code blocks for Git/shell commands, e.g.:
  ```text
  ```bash
  git checkout main
  ```
  ```
- Normalizes line endings to `\n` and trims code blocks so they parse reliably.
- Uses inline code only for short identifiers (e.g. `git`, `main` in prose is intentionally not wrapped in backticks).

So the **API payload is correct markdown**. If the UI shows raw backticks or unformatted commands, the problem is on the **client**: the `response` string must be rendered as markdown, not displayed as plain text.

## Frontend requirement

The app that displays the mentor reply must:

1. **Render the `response` string as markdown**, not as plain text.
2. **Use a markdown library that supports fenced code blocks** (triple backticks with optional language, e.g. ` ```bash ` … ` ``` `).

### Examples

- **React:** Use something like `react-markdown` (with `remark-gfm` if you want GitHub Flavored Markdown). Ensure the component that shows the mentor message passes `response` into the markdown renderer, not into a plain `<div>` or `<span>`.
- **Vue / other:** Use a markdown-to-HTML library (e.g. `marked`, `markdown-it`) that supports fenced code blocks, then render the result (e.g. with `v-html` or a dedicated markdown component).

### What to check

- The component that displays the mentor message receives the **full** `response` string from the API (no stripping of `\n` or ` ``` `).
- That string is passed to a **markdown renderer** (e.g. `<ReactMarkdown>{response}</ReactMarkdown>` or equivalent).
- The renderer is configured so that:
  - Fenced blocks (```` ```language ```` and ` ``` `) become `<pre><code>` (or equivalent).
  - Inline code remains styled as code.

Once the frontend renders the API `response` as markdown with fenced code block support, Git commands and other code blocks will appear correctly formatted.
