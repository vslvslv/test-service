---
name: create-pr
description: Creates a pull request from current git changes. Inspects staged and unstaged diffs, writes a commit message, commits and pushes, then opens a PR with gh. Use when the user asks to create a pull request, open a PR, or submit current changes as a PR.
---

# Create Pull Request from Current Changes

Follow this workflow to turn local changes into a pull request.

## Workflow

### 1. Inspect changes

Run from the repository root:

```bash
git status
git diff --staged
git diff
```

- Use `git diff --staged` for what will be committed.
- Use `git diff` for unstaged changes. If there are unstaged changes, either stage them (`git add ...`) or commit in two steps (staged first, then unstaged).
- Note which areas changed: Api, Tests, Web, Infrastructure (see [AGENTS.md](AGENTS.md) Commit & PR guidelines).

### 2. Stage all intended changes

If the user wants everything in one commit:

```bash
git add -A
```

Otherwise stage only the files that belong in this PR.

### 3. Write the commit message

- **Style**: Short, imperative subject (e.g. "Add entity bulk import/export", "Fix CORS for login").
- **Length**: Subject line under ~72 characters when possible.
- **Body** (optional): One or two lines explaining purpose or scope if the subject is not enough.

If the repo has [AGENTS.md](AGENTS.md) or similar, follow its commit guidelines.

### 4. Commit and push

```bash
git commit -m "Subject line" [-m "Optional body line"]
git push -u origin $(git branch --show-current)
```

Use the current branch name; do not switch branches unless the user asks. If push fails (e.g. no upstream), use the suggested `git push --set-upstream` command.

### 5. Open the pull request

```bash
gh pr create --title "Title for PR" --body "Description"
```

- **Title**: Same as or very close to the commit subject; clear and under ~72 characters.
- **Body**: Include:
  - **Purpose**: What this PR does and why.
  - **Impacted areas**: List (e.g. Api, Tests, Web, Infrastructure).
  - **Testing**: How you verified (e.g. `dotnet test`, `npm run build`, manual steps).
  - Link to an issue or task if applicable.

Use `gh pr create` without `--web` so the URL is printed in the terminal. If the CLI prompts interactively, provide title and body via the flags above to avoid prompts.

### 6. Return the PR URL

After `gh pr create` completes, copy the PR URL from the command output and return it to the user in your reply (e.g. `https://github.com/owner/repo/pull/123`).

## Requirements

- Repository must be a git repo with a remote (e.g. `origin`).
- GitHub CLI (`gh`) must be installed and authenticated (`gh auth status`).
- Changes must be committed before opening the PR.

## Example

**After reviewing diff:**

```
Commit message: Add entity export/import and Postman mock import

PR title: Add entity export/import and Postman mock import
PR body:
- Purpose: Bulk entity CSV/JSON export and import; Postman collection import and duplicate expectation to env for Mocks.
- Areas: Api, Tests, Web, documents.
- Testing: dotnet build, dotnet test; manual UI check for export/import and mocks.
```

Then: commit with that message, push, `gh pr create` with that title and body, and reply with the PR URL.
