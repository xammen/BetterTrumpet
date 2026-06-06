# BetterTrumpet Skills - Summary

## ✅ Skills Created (Phase 1 Complete)

| Skill | Model | Description | Status |
|-------|-------|-------------|--------|
| `/build` | Sonnet | Build and launch BetterTrumpet with options | ✅ Created |
| `/restart` | Haiku | Quickly restart the app | ✅ Created |
| `/fix-issue` | Sonnet | Structured workflow to fix GitHub issues | ✅ Created |
| `/xaml-debug` | Sonnet | Analyze and debug XAML layout issues | ✅ Created |
| `/commit-and-push` | Sonnet | Intelligent commit with auto-generated messages | ✅ Created |
| `/test-feature` | Haiku | Quick feature testing with instructions | ✅ Created |

## 🎯 Quick Reference

### Development Workflow
```bash
# Make changes to code
/build              # Build and launch
/restart            # Quick restart after minor changes
/test-feature <name> # Test a specific feature
```

### Git Workflow
```bash
/commit-and-push                    # Auto-generate commit message
/commit-and-push --new-branch fix-X # Create new branch
```

### Debugging
```bash
/xaml-debug FlyoutWindow.xaml       # Debug XAML layout
/fix-issue 13                       # Fix a GitHub issue
```

## 📊 Model Usage Strategy

**Sonnet (claude-sonnet-4-6):** Complex analysis & generation
- `/build` - Needs to handle build errors intelligently
- `/fix-issue` - Requires code analysis and planning
- `/xaml-debug` - Complex XAML structure analysis
- `/commit-and-push` - Generate meaningful commit messages

**Haiku (claude-haiku-4-5):** Simple, fast, repetitive tasks
- `/restart` - Simple kill + launch
- `/test-feature` - Provide instructions and collect feedback

**Opus (default):** Reserved for complex problem-solving not covered by skills

## 💰 Cost Savings Estimate

Based on today's session (15+ rebuilds, 10+ restarts):

| Task | Before (Opus) | After (Sonnet/Haiku) | Savings |
|------|---------------|---------------------|---------|
| 15x Build | ~$0.50 | ~$0.15 | 70% |
| 10x Restart | ~$0.20 | ~$0.02 | 90% |
| 3x Debug | ~$0.30 | ~$0.09 | 70% |
| **Total/day** | **~$1.00** | **~$0.26** | **74%** |

## 🚀 Next Skills to Create (Phase 2)

Priority order:
1. `/context-menu-add` - Pattern for adding context menus (Medium complexity, Sonnet)
2. `/theme-debug` - Debug theme/styling issues (Medium complexity, Sonnet)
3. `/docs-update` - Auto-update documentation (Low complexity, Haiku)
4. `/grep-usage` - Find symbol usage (Low complexity, Haiku)

## 📝 Usage Tips

### When to use each skill:

**Use `/build`:**
- After making code changes
- Need to test changes immediately
- Want to see build output

**Use `/restart`:**
- Just rebuilt and want to reload
- Testing UI changes quickly
- No code changes, just config/resource updates

**Use `/fix-issue`:**
- Starting work on a GitHub issue
- Want structured approach
- Need to understand problem first

**Use `/xaml-debug`:**
- Layout doesn't look right
- Elements overlapping unexpectedly
- Z-Index confusion

**Use `/commit-and-push`:**
- Ready to commit changes
- Want good commit message
- Following conventional commits

**Use `/test-feature`:**
- Before closing an issue
- After implementing a fix
- Want clear test steps

## 🔧 Customization

Each skill can be edited in `.claude/skills/<name>.md`:
- Adjust build parameters
- Add more test scenarios
- Customize commit message templates
- Add project-specific patterns

## 📚 Documentation

Each skill includes:
- ✅ Clear usage examples
- ✅ Step-by-step what it does
- ✅ Common issues and solutions
- ✅ Implementation details
- ✅ Notes and tips

## 🎉 Impact

**Before skills:**
- Manual rebuild every time: `msbuild ...` (long command)
- Kill app manually: `taskkill ...`
- Launch manually: `cd Build/Debug && ./BetterTrumpet.exe &`
- Write commit messages from scratch
- No structured workflow for issues

**After skills:**
- `/build` - One command
- `/restart` - One command
- Auto-generated commit messages
- Structured issue workflow
- XAML debugging assistance

**Developer experience:** 🚀 Massively improved!
