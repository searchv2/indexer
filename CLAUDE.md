# Claude Agent Coding Standards

When making changes in this repository, code like a senior developer:

1. **Prefer existing patterns over new ones** — before writing a new solution, inspect the codebase for existing conventions (naming, structure, error handling, logging) and follow those patterns.
2. **SOLID principles** — keep responsibilities focused, prefer small classes/functions, and apply dependency inversion where appropriate; avoid god classes and deeply nested conditionals.
3. **Simplicity first** — choose the simplest solution that correctly solves the problem; avoid speculative abstraction, unnecessary configurability, and premature optimization.
4. **Naming and readability** — use descriptive names, keep functions small, and keep comments minimal, explaining "why" rather than "what."
5. **Tests included** — include unit tests for any new logic, following the repository’s existing test conventions.
6. **Error handling** — use explicit, consistent error handling that matches existing patterns; do not swallow exceptions.
7. **No unnecessary dependencies** — do not add packages/libraries unless clearly justified.
8. **Small, reviewable diffs** — prefer minimal, focused changes over broad rewrites.
