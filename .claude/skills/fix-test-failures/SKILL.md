---
name: fix-test-failures
description: "This skill should be used after running tests when failures occur. It ensures test failures are properly diagnosed through instrumentation and logging until the root cause is found and fixed. The skill treats all test failures as real bugs that must be resolved, never skipped."
---

# Fix Test Failures

This skill provides a systematic approach to diagnosing and fixing test failures. The core philosophy is that **test failures are real bugs** - they must be understood and fixed, never ignored or worked around.

## NO CHEATING - Critical Tests

**These tests protect code quality and user experience. There are no workarounds.**

Every test exists because it validates behavior that users depend on. Bypassing tests means shipping broken software.

**Forbidden actions (no exceptions):**
- Weakening assertions to make tests pass
- Changing expected values to match broken behavior
- Wrapping failing code in try/catch to swallow errors
- Adding conditional logic to skip checks in test environments
- Commenting out assertions or test methods
- Using `[Skip]` as a permanent solution
- Relaxing timeouts to mask performance regressions
- Mocking away the actual behavior being tested

**The only acceptable outcome is fixing the actual bug in the production code.**

## When to Use This Skill

Use this skill when:
- Tests fail and the cause isn't immediately obvious
- A test is flaky or intermittently failing
- You need to understand why a test is failing before fixing it
- You've made changes and tests are now failing

## Core Principles

1. **Test failures are real bugs** - Never skip, disable, or delete failing tests without understanding and fixing the underlying issue
2. **No cheating** - Never weaken tests, change expected values, or work around failures
3. **Instrument to understand** - Add debug output to expose internal state and execution flow
4. **Fix the root cause** - Don't patch symptoms; find and fix the actual bug
5. **Clean up after** - Remove instrumentation once the fix is verified

## Test Failure Resolution Process

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

### Step 2: Analyze the Failure

Before adding instrumentation, understand what the test is checking:

1. Read the test code carefully
2. Identify what assertion is failing
3. Note what values were expected vs. received
4. Form a hypothesis about what might be wrong

### Step 3: Add Strategic Instrumentation

Add `Console.WriteLine` or `System.Diagnostics.Debug.WriteLine` statements to expose the state at key points. Target areas include:

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

**For function execution flow:**
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

Analyze the output to understand:
- What values are actually present
- Where the execution diverges from expectations
- What state is incorrect and when it became incorrect

### Step 5: Identify Root Cause

Based on instrumentation output, determine:
- Is the test wrong (rare - only if test assumptions were incorrect)?
- Is the code under test wrong (common)?
- Is there a threading or timing issue (for async tests)?
- Is there a state pollution issue from other tests?

### Step 6: Fix the Bug

Fix the actual bug in the production code, not by modifying the test to accept wrong behavior.

Common fixes:
- **Logic errors**: Fix the algorithm or condition
- **State issues**: Ensure proper initialization or cleanup
- **Null references**: Add null checks or fix initialization order
- **Threading issues**: Add proper synchronization or use async correctly
- **File path issues**: Use `Path.Combine` and platform-appropriate paths

### Step 7: Verify and Clean Up

1. Run the test again to confirm it passes
2. Run the full test suite to ensure no regressions: `dotnet test Tests/Agg.Tests/Agg.Tests.csproj`
3. **Remove all instrumentation output statements** - they were for debugging only
4. Commit the fix

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

## Iterative Debugging

If the first round of instrumentation doesn't reveal the issue:

1. Add more instrumentation at earlier points in execution
2. Log intermediate values, not just final state
3. Check for side effects from other code
4. Verify test setup is correct (`[Before(Class)]`)
5. Check if the issue is environment-specific (file paths, platform)

Keep iterating until the root cause is clear. The goal is understanding, then fixing.
