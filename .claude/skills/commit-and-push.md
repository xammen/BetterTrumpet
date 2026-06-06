---
name: commit-and-push
description: Intelligent commit and push with auto-generated message
tags: [git, commit, workflow]
model: sonnet
---

# Commit and Push

Intelligently analyze changes, generate a commit message, and push to GitHub.

## Usage

```
/commit-and-push [options]
```

## Options

- No options: Stage all changes, generate commit message, push to current branch
- `--new-branch <name>`: Create and push to a new branch
- `--amend`: Amend the last commit (unpushed only)
- `--message "<msg>"`: Use custom commit message instead of auto-generated

## What this skill does

1. **Analyze changes:**
   - Runs `git status` and `git diff`
   - Identifies modified, added, and deleted files
   - Understands what was changed (new feature, bug fix, refactor, etc.)

2. **Generate commit message:**
   - Creates a descriptive commit message following conventions
   - Format: `<type>: <description>`
   - Types: feat, fix, refactor, docs, style, test, chore
   - Includes affected components

3. **Show preview:**
   - Displays the generated commit message
   - Lists files to be committed
   - Asks for confirmation

4. **Commit:**
   - Stages the files
   - Commits with the message
   - Adds co-authored-by Claude

5. **Push:**
   - Pushes to origin (creates branch if needed)
   - Shows the commit hash and remote URL

## Examples

```bash
# Analyze changes and commit with auto-generated message
/commit-and-push

# Commit to a new branch
/commit-and-push --new-branch fix-backdrop-issue

# Use custom message
/commit-and-push --message "fix: resolve backdrop rendering on startup"

# Amend last commit
/commit-and-push --amend
```

## Commit Message Generation

### Format
```
<type>(<scope>): <description>

<body (optional)>

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

### Types
- **feat**: New feature
- **fix**: Bug fix
- **refactor**: Code restructuring without functional changes
- **docs**: Documentation changes
- **style**: Code style/formatting changes
- **test**: Adding or updating tests
- **chore**: Build, dependencies, or maintenance

### Scope Examples
- `FlyoutWindow`: Changes to FlyoutWindow.xaml/cs
- `DeviceViewModel`: Changes to device view model
- `CLI`: Command-line interface changes
- `themes`: Theme/styling changes
- `build`: Build configuration changes

### Example Messages

```
feat(FlyoutWindow): add context menu to device header

- Add "Set as default device" option
- Implement SetAsDefaultDevice_Click handler
- Add MenuItem using statement

Fixes #15
```

```
fix(backdrop): resolve inconsistent rendering on startup

- Force theme refresh after initialization
- Add Dispatcher.BeginInvoke for acrylic application
- Ensure backdrop applies correctly on first launch

Fixes #13
```

```
refactor(FlyoutWindow): simplify device header layout

- Replace overlay buttons with Grid columns
- Remove Z-Index positioning complexity
- Improve maintainability
```

## Implementation

### 1. Analyze Changes
```bash
# Get status
git status --short

# Get detailed diff
git diff --stat
git diff
```

Analyze the diff to determine:
- What files changed
- What type of changes (new feature, bug fix, etc.)
- Which components are affected

### 2. Generate Message

Rules:
- If fixing a GitHub issue → `fix: <description>` + `Fixes #<number>`
- If adding new functionality → `feat: <description>`
- If refactoring → `refactor: <description>`
- Multiple files in same component → use component as scope
- Keep subject line under 70 characters

### 3. Confirm
```
📝 Proposed commit message:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
fix(backdrop): resolve inconsistent rendering on startup

- Force theme refresh after initialization
- Add Dispatcher.BeginInvoke for acrylic application

Fixes #13

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📁 Files to commit:
  M EarTrumpet/UI/Views/FlyoutWindow.xaml.cs
  M EarTrumpet/UI/Views/FlyoutWindow.xaml

Proceed? (y/n)
```

### 4. Commit and Push
```bash
# Stage files
git add <files>

# Commit
git commit -m "<message>"

# Push (create branch if --new-branch)
if [ -n "$NEW_BRANCH" ]; then
    git checkout -b "$NEW_BRANCH"
fi

git push -u origin $(git branch --show-current)
```

### 5. Show Result
```
✅ Committed and pushed!

📌 Commit: a1b2c3d
🌿 Branch: fix-backdrop-issue
🔗 https://github.com/xammen/BetterTrumpet/commit/a1b2c3d
```

## Safety Checks

Before committing, verify:
- ❌ No unresolved merge conflicts
- ❌ No uncommitted files that look like secrets (.env, credentials, etc.)
- ❌ No huge files (>10MB) being added
- ✅ All tests pass (if applicable)

## Notes

- Always reviews changes before committing
- Follows conventional commits format
- Creates branches when working on features/fixes
- Never force pushes (uses `git push` not `git push -f`)
- Adds co-authored-by for attribution
