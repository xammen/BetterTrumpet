---
name: fix-issue
description: Fix a GitHub issue with structured workflow
tags: [github, bug, workflow]
model: sonnet
---

# Fix GitHub Issue

Structured workflow to fix a GitHub issue from start to finish.

## Usage

```
/fix-issue <issue-number>
```

## What this skill does

1. **Fetch issue details:**
   - Uses `gh issue view <number>` to get the issue
   - Reads title, description, and comments
   - Understands the problem

2. **Code investigation:**
   - Searches for relevant files using Grep/Glob
   - Reads the code to understand current implementation
   - Identifies the root cause

3. **Plan the fix:**
   - Proposes a fix strategy
   - Lists files to modify
   - Asks for user confirmation before proceeding

4. **Implement the fix:**
   - Makes the necessary code changes
   - Follows project conventions and patterns
   - Adds comments referencing the issue number

5. **Test the fix:**
   - Builds the project
   - Launches the app
   - Provides test instructions
   - Waits for user feedback

6. **Document:**
   - Comments on the GitHub issue with the fix details
   - Suggests closing the issue if verified

## Examples

```bash
# Fix issue #13 (backdrop rendering bug)
/fix-issue 13

# Fix issue #42
/fix-issue 42
```

## Implementation Steps

### 1. Fetch Issue
```bash
gh issue view <number> --repo xammen/BetterTrumpet
```

Parse the output to extract:
- Title
- Description
- Current state (open/closed)
- Labels

### 2. Investigate
Based on the issue description:
- Search for relevant keywords in the codebase
- Identify affected components
- Read related files

### 3. Plan
Present a structured plan:
```
## Fix Plan for Issue #<number>

**Problem:** <brief summary>

**Root Cause:** <what's causing it>

**Solution:** <proposed fix>

**Files to Modify:**
- File1.cs: <what changes>
- File2.xaml: <what changes>

**Testing:** <how to verify>

Proceed with implementation? (y/n)
```

### 4. Implement
- Make the code changes
- Add comments like `// Fix for GitHub issue #<number>`
- Follow existing code style

### 5. Test
```bash
# Build
/build

# Provide test instructions
echo "Test instructions:"
echo "1. ..."
echo "2. ..."
echo "3. ..."
echo ""
echo "Does the fix work? (y/n)"
```

### 6. Document
```bash
# Comment on issue
gh issue comment <number> --body "Fixed in commit <hash>

**Changes:**
- ...

**Testing:**
- ...

Please verify and close if resolved."
```

## Options

- `--no-test`: Skip the build and test phase
- `--draft`: Create a draft plan only, don't implement

## Notes

- Always get user confirmation before implementing major changes
- Reference the issue number in code comments
- Test thoroughly before claiming the fix is complete
- If the fix doesn't work, iterate with user feedback
