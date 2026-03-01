---
name: fix-test-failures
description: "This skill should be used after running tests when failures occur. It provides a systematic approach to diagnosing test failures through instrumentation and root cause analysis."
---

# Fix Test Failures

This skill provides a systematic approach to diagnosing and fixing test failures.

## The Goal

When a test fails, the goal is to **understand what went wrong before changing anything.** A test failure is valuable information -- it's telling you something about the system that you didn't expect. The worst thing you can do is silence that signal without understanding it.

Most of the time, a failing test reveals a real bug in the production code. Occasionally, the test itself has a problem -- an incorrect assumption, a setup issue, or a requirement that genuinely changed. Either way, the path forward is the same: investigate until you understand, then make the right fix.

## When to Use This Skill

- Tests fail and the cause isn't immediately obvious
- A test is flaky or intermittently failing
- You need to understand why a test is failing before fixing it
- You've made changes and tests are now failing

## The Process

### Step 1: Run Tests and Capture Failures

Run the failing test(s) to see the current error:

```bash
# Run all tests
dotnet test Tests/Agg.Tests/Agg.Tests.csproj

# Run with detailed output
dotnet test Tests/Agg.Tests/Agg.Tests.csproj --verbosity normal

# Run a specific test class
dotnet test Tests/Agg.Tests/Agg.Tests.csproj --filter "FullyQualifiedName~ClassName"

# Run a specific test method
dotnet test Tests/Agg.Tests/Agg.Tests.csproj --filter "FullyQualifiedName~MethodName"
```

Record the exact error message and stack trace. This is your starting point.

### Step 2: Understand What the Test Expects

Before adding instrumentation, read the test carefully:

1. What behavior is this test validating?
2. What assertion is failing?
3. What values were expected vs. received?
4. Form a hypothesis about what might be wrong

### Step 3: Add Strategic Instrumentation

Add `Console.WriteLine` statements to expose state at key points. The goal is to see what's actually happening inside the code, not just what the test reports.

**For state-related failures:**
```csharp
Console.WriteLine($"State before operation: {state}");
// ... operation ...
Console.WriteLine($"State after operation: {state}");
```

**For object inspection:**
```csharp
Console.WriteLine($"Children count: {obj.Children.Count}");
Console.WriteLine($"Mesh vertices: {obj.Mesh?.Vertices.Count ?? 0}");
```

**For execution flow:**
```csharp
Console.WriteLine($"Entering {nameof(MethodName)} with: {param}");
// ... method body ...
Console.WriteLine($"Returning: {result}");
```

### Step 4: Run Instrumented Tests

Run the test again with verbose output:

```bash
dotnet test Tests/Agg.Tests/Agg.Tests.csproj --filter "FullyQualifiedName~TestMethod" --verbosity normal
```

Look at the output to understand:
- What values are actually present
- Where execution diverges from expectations
- What state is incorrect and when it became incorrect

### Step 5: Identify the Root Cause

Based on instrumentation output, determine what's actually wrong:

- **Bug in production code** (most common) -- the code doesn't do what it should
- **Test assumption is incorrect** (rare) -- the test expected something that was never the right behavior
- **Requirements changed** -- the code intentionally changed and the test needs to reflect the new expected behavior
- **Threading or timing issue** -- async code, race conditions, or test isolation problems
- **Environment issue** -- file paths, missing resources, platform differences

### Step 6: Make the Right Fix

What you fix depends on what you found:

- **Production bug**: Fix the code so it produces the correct behavior. This is the most common case.
- **Incorrect test**: If the test itself was wrong (wrong expected value, flawed setup), fix the test. But be confident -- if you're not sure whether the test or the code is wrong, assume the code is wrong and investigate further.
- **Changed requirements**: Update the test to reflect the new correct behavior. This is different from weakening a test -- you're updating it because the definition of "correct" changed.

Common production code fixes:
- **Logic errors**: Fix the algorithm or condition
- **State issues**: Ensure proper initialization or cleanup
- **Null references**: Fix initialization order
- **Threading issues**: Add proper synchronization or use async correctly
- **File path issues**: Use `Path.Combine` and platform-appropriate paths

### Step 7: Verify and Clean Up

1. Run the test again to confirm it passes
2. Run the full test suite to ensure no regressions: `dotnet test Tests/Agg.Tests/Agg.Tests.csproj`
3. **Remove all instrumentation** -- the debug output was for diagnosis only
4. Commit the fix

## Common Pitfalls

These approaches might feel like they solve the problem, but they hide it instead:

- **Weakening an assertion** to make it pass means the test no longer validates what it was designed to check. The bug is still there, just undetected.
- **Swallowing exceptions** with try/catch means errors happen silently. Users will hit them even if tests don't.
- **Mocking away the behavior being tested** turns the test into a tautology -- it only proves the mock works, not the real code.
- **Using `[Skip]` permanently** means the test exists but protects nothing. If it's not worth fixing, it's not worth keeping.

These aren't forbidden -- they're just counterproductive. If you find yourself reaching for one, it usually means you haven't found the root cause yet.

## Iterative Debugging

If the first round of instrumentation doesn't reveal the issue:

1. Add more instrumentation at earlier points in execution
2. Log intermediate values, not just final state
3. Check for side effects from other code
4. Verify test setup is correct (`[Before(Class)]`)
5. Check if the issue is environment-specific (file paths, platform)

Keep iterating until the root cause is clear. The goal is understanding, then fixing.

## Project Test Structure

```
Tests/
  Agg.Tests/
    Agg.Tests.csproj              # TUnit test project (net8.0-windows)
    Agg/                          # Core graphics tests
    Agg.UI/                       # GUI widget tests
    Agg Automation Tests/         # GUI automation tests
    Agg.PolygonMesh/              # Mesh tests
    Agg.RayTracer/                # Ray tracer tests
    Agg.VectorMath/               # Vector math tests
    Agg.Csg/                      # CSG tests
    Other/                        # Misc tests (affine, clipper, etc.)
    TestingInfrastructure/        # Test setup helpers
```

**Key patterns:**
- Unit tests: Regular classes with `[Test]` methods
- Automation tests: Use `[NotInParallel]` with `AutomationRunner.ShowWindowAndExecuteTests`
- Class setup: `[Before(Class)]` for one-time initialization
- Assertions: `await Assert.That(value).IsEqualTo(expected)`
