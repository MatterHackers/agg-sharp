# Claude Code Guidelines

## Philosophy

**YAGNI** - Don't build features until needed. Write the simplest code that works today.

**Circumstances alter cases** - Use judgment. There are no rigid rules—context determines the right approach.

**Quality through iterations** - Start fast and simple, then improve to meet actual needs. Code that doesn't matter can be quick and dirty. But code that matters *really* matters—treat it with respect and improve it meticulously.

## Test-First Bug Fixing (Critical Practice)

**This is the single most important practice for agent performance and reliability.**

When a bug is reported, always follow this workflow:

1. **Write a reproducing test first** - Create a test that fails, demonstrating the bug
2. **Fix the bug** - Make the minimal change needed to address the issue
3. **Verify via passing test** - The previously failing test should now pass

This approach works because:
- The failing test proves you understand the bug
- The fix is verifiable, not just "looks right"
- You can't accidentally break it again (regression protection)
- It aligns with the principle that coding is high-leverage because it's **partially verifiable**

**Do not skip the reproducing test.** Even if the fix seems obvious, the test validates your understanding and prevents regressions.

## Testing

- Tests MUST test actual production code, not copies - Never duplicate production logic in tests. Import and call the real code. Tests that verify copied code prove nothing about the actual system.
- Tests should run as fast as possible—fast tests get run more often
- Write tests for regressions and complex logic
- Avoid redundant tests that verify the same behavior
- All tests must pass before merging
- When test failures occur, use the fix-test-failures agent (`.claude/agents/fix-test-failures.md`) — it treats all failures as real bugs and resolves them through instrumentation and root cause analysis, never by weakening tests

## Project Context

- **Language:** C# (.NET 8.0)
- **Test Framework:** TUnit (v0.57.24)
- **Build:** `dotnet build`
- **Test:** `dotnet test` or run the test executable directly
- **Solution:** `agg-sharp.sln`
- **Test Project:** `Tests/Agg.Tests/Agg.Tests.csproj`
- **What is agg-sharp:** Core graphics/UI framework library used as a submodule by MatterCAD. Includes 2D graphics (agg), GUI widgets (Gui), polygon mesh, vector math, image processing, CSG, ray tracing, and GUI automation.

## Code Quality

**Names** - Choose carefully. Good names make code self-documenting.

**Comments** - Explain *why*, not *what*. The code shows what it does; comments should reveal intent, tradeoffs, and non-obvious reasoning.

**Refactoring** - Improve code when it serves a purpose, not for aesthetics. Refactor to fix bugs, add features, or improve clarity when you're already working in that area.

**Copyright** - When updating files with copyright notices, update the year to 2026 if not already current. Include Lars Brubaker in the copyright notice.

**Async/Await** - Never use `.GetAwaiter().GetResult()` or `.Result`. Always propagate async properly with `await`.
